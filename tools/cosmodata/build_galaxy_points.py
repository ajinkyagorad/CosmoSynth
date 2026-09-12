#!/usr/bin/env python3
"""Bake the Milky Way point populations: galaxy/*.csgp and galaxy/namedclouds.json.

Section B section 2.8 named Skowron+2019 Cepheids, Harris/Baumgardt globulars and the VizieR
copy of Reid+2019 table 1.  The licence amendment of 2026-09-12 blocked three of those four
(`docs/DATA-LICENCE-AMENDMENT.md` section 2 row 11): **no commercial-clean Galactic Cepheid
catalogue exists**, and the maser table may not be redistributed although the arm model fitted
from it may be implemented.  What ships instead, all CC0:

  globulars.csgp          213 Milky Way globular clusters, Local Volume Database v1.1.1
  clouds.csgp           1,064 CO molecular clouds, Rice et al. 2016 -- the disc tracer that
                              replaces the Cepheids
  radcliffe.csgp        1,500 points of the Radcliffe Wave spine, Konietzka et al. 2024
  radcliffe_clusters.csgp 125 stellar clusters strung along it
  namedclouds.json         94 named clouds with measured distances, Zucker et al. 2020,
                              in the same `cosmosynth.places/1` schema as landmarks.json so
                              PlaceCatalog reads both and no second parser exists

Standard library only.  The Rice FITS bintable is read by hand: it is 56 big-endian columns of
fixed width, which `struct` does exactly, and importing a FITS library for it would be the
whole dependency for one table.

    python3 tools/cosmodata/build_galaxy_points.py [--out DIR]
"""

import argparse
import csv
import json
import math
import os
import struct
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import csdata

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
STAGING = os.environ.get("COSMODATA_STAGING",
                         "/home/triton/projects/CosmoSynth/data-staging")

# The Hipparcos ICRS->galactic rotation, from design B section 1.6.  It lives here, in the
# importer, because the importer is what turns galactic catalogue coordinates into the ICRF
# positions every shipped file stores.  Core has its own copy and a test that the two agree.
EQ_TO_GAL = (
    (-0.0548755604, -0.8734370902, -0.4838350155),
    (+0.4941094279, -0.4448296300, +0.7469822445),
    (-0.8676661490, -0.1980763734, +0.4559837762),
)


def gal_to_icrf(x, y, z):
    """Heliocentric galactic cartesian -> ICRF cartesian, same units in and out."""
    m = EQ_TO_GAL          # rows are galactic axes in ICRF components, so transpose to go back
    return (m[0][0] * x + m[1][0] * y + m[2][0] * z,
            m[0][1] * x + m[1][1] * y + m[2][1] * z,
            m[0][2] * x + m[1][2] * y + m[2][2] * z)


def icrf_to_gal(x, y, z):
    m = EQ_TO_GAL
    return tuple(sum(m[r][c] * v for c, v in enumerate((x, y, z))) for r in range(3))


def lbd_to_gal(l_deg, b_deg, d):
    lr, br = math.radians(l_deg), math.radians(b_deg)
    return (d * math.cos(br) * math.cos(lr), d * math.cos(br) * math.sin(lr), d * math.sin(br))


def radec_to_icrf(ra_deg, dec_deg, d):
    ar, dr = math.radians(ra_deg), math.radians(dec_deg)
    return (d * math.cos(dr) * math.cos(ar), d * math.cos(dr) * math.sin(ar), d * math.sin(dr))


# Flag bits, shared with Core/Deep/PointSet.cs (PointFlags).
OUTLIER = 1 << 0
NO_ABS_MAG = 1 << 1
LOW_SN_PARALLAX = 1 << 2
NOT_MILKY_WAY_HOST = 1 << 3
AMBIGUOUS_DISTANCE = 1 << 4
NOT_A_MEMBER = 1 << 5


sha256 = csdata.sha256


def commit():
    return csdata.generator_commit(REPO)


class Refusal(Exception):
    pass


class Checks(object):
    """Assertions that refuse rather than warn, and end up recorded in the file's own meta."""

    def __init__(self, target):
        self.rows = []
        self.target = target

    def __call__(self, name, ok, expected=None, got=None, note=None):
        row = {"name": name, "ok": bool(ok)}
        if expected is not None:
            row["expected"] = expected
        if got is not None:
            row["got"] = got
        if note:
            row["note"] = note
        self.rows.append(row)
        if not ok:
            if os.path.exists(self.target):
                os.remove(self.target)
            raise Refusal("REFUSED %s: '%s' expected %r, got %r%s"
                          % (os.path.basename(self.target), name, expected, got,
                             " - " + note if note else ""))
        print("  ok  %-36s %s" % (name, "" if got is None else got))


def base_meta(kind, generator, sources, provenance, extra=None):
    meta = {
        "kind": kind, "schema": 1,
        "generator": generator, "generatorCommit": commit(),
        "generatedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "licenceProfile": os.environ.get("COSMODATA_LICENCE", "commercial"),
        "frame": "ICRF", "epoch": "J2000.0",
        "sources": sources,
        "provenance": provenance,
        "provenanceBits": [
            {"bit": 0, "field": "pos", "class": "Inferred", "note": "distance flagged suspect"},
            {"bit": 1, "field": "absV", "class": "Inferred", "note": "no published M_V"},
            {"bit": 2, "field": "pos", "class": "Inferred", "note": "parallax S/N below 5"},
            {"bit": 3, "field": "pos", "class": "Measured",
             "note": "object is hosted by a satellite galaxy, not by the Milky Way"},
            {"bit": 4, "field": "pos", "class": "Inferred",
             "note": "kinematic near/far distance ambiguity unresolved: not a measurement"},
            {"bit": 5, "field": "pos", "class": "Measured",
             "note": "not a member of the structure this file describes"},
        ],
        "uncertainty": {"pos": "distErr"},
    }
    if extra:
        meta.update(extra)
    return meta


# ---------------------------------------------------------------------------------------
# Globular clusters: Local Volume Database v1.1.1, CC0
# ---------------------------------------------------------------------------------------

LVDB_ATTRIBUTION = (
    "Globular clusters: Local Volume Database (Pace 2025, OJAp 8, 32), v1.1.1, CC0 1.0. "
    "Per-object distance and magnitude references are carried in this file's meta.refs.")


def build_globulars(out_dir):
    target = os.path.join(out_dir, "globulars.csgp")
    check = Checks(target)
    src = os.path.join(STAGING, "lvdb-globular-clusters")
    print("globulars.csgp")

    rows = []
    for name in ("gc_harris.csv", "gc_mw_new.csv"):
        with open(os.path.join(src, name), newline="") as handle:
            for row in csv.DictReader(handle):
                row["_file"] = name
                rows.append(row)
    check("row count", len(rows) == 213, 213, len(rows),
          "155 Harris-set + 58 newer clusters; the dwarf-hosted file is deliberately not read")

    def num(row, key):
        value = (row.get(key) or "").strip()
        if value == "":
            return None                      # empty means "not measured", never zero
        try:
            return float(value)
        except ValueError:
            return None

    pos, dist_err, flags, abs_v, rhalf_pc = [], [], [], [], []
    names, refs = [], []
    no_mv = 0
    for row in rows:
        dm = num(row, "distance_modulus")
        if dm is None:
            raise Refusal("globular %s has no distance modulus" % row.get("key"))
        d_pc = 10.0 ** (dm / 5.0 + 1.0)
        # The modulus errors are asymmetric; take the larger arm, which never understates.
        dm_err = max(num(row, "distance_modulus_em") or 0.0,
                     num(row, "distance_modulus_ep") or 0.0)
        err_pc = d_pc * (10.0 ** (dm_err / 5.0) - 1.0) if dm_err else 0.0

        ra, dec = num(row, "ra"), num(row, "dec")
        if ra is None or dec is None:
            raise Refusal("globular %s has no position" % row.get("key"))
        pos.extend(radec_to_icrf(ra, dec, d_pc))
        dist_err.append(err_pc)

        bits = 0
        mv = num(row, "M_V")
        if mv is None:
            bits |= NO_ABS_MAG
            no_mv += 1
            mv = float("nan")
        if (row.get("host") or "").strip() not in ("mw", ""):
            bits |= NOT_MILKY_WAY_HOST
        flags.append(bits)
        abs_v.append(mv)

        # rhalf is ANGULAR (arcmin). rhalf_physical is the catalogue's own pc value where it
        # has one; prefer it, and otherwise derive from the angular value and this distance.
        rp = num(row, "rhalf_physical")
        if rp is None:
            ang = num(row, "rhalf")
            rp = math.radians((ang or 0.0) / 60.0) * d_pc if ang else float("nan")
        rhalf_pc.append(rp)

        names.append((row.get("name") or row.get("key") or "").strip())
        refs.append({"distance": (row.get("ref_distance") or "").strip(),
                     "absV": (row.get("ref_m_v") or "").strip()})

    dists = [math.sqrt(pos[3 * i] ** 2 + pos[3 * i + 1] ** 2 + pos[3 * i + 2] ** 2)
             for i in range(len(rows))]
    check("distance range", 1000.0 <= min(dists) and max(dists) <= 200000.0,
          "1 .. 200 kpc", "%.0f .. %.0f pc" % (min(dists), max(dists)))
    finite_mv = [m for m in abs_v if m == m]
    check("M_V range", -11.0 <= min(finite_mv) and max(finite_mv) <= 3.0, "-11 .. +3",
          "%.2f .. %.2f" % (min(finite_mv), max(finite_mv)))
    check("clusters without M_V", no_mv == 7, 7, no_mv, "flagged, not dropped and not defaulted")

    index = {n: i for i, n in enumerate(names)}
    check("47 Tuc (NGC 104) distance", abs(dists[index["NGC 104"]] - 4520.6) < 5.0,
          "4520.6 +/- 5 pc", round(dists[index["NGC 104"]], 1),
          "LVDB distance modulus 13.28, ref Baumgardt2021MNRAS.505.5957B")
    nearest = min(range(len(names)), key=lambda i: dists[i])
    check("nearest globular is M4", names[nearest] == "NGC 6121" and dists[nearest] < 2000.0,
          "NGC 6121 < 2000 pc", "%s %.0f pc" % (names[nearest], dists[nearest]))
    brightest = min(range(len(names)), key=lambda i: abs_v[i] if abs_v[i] == abs_v[i] else 99)
    check("brightest globular is omega Cen",
          names[brightest] == "NGC 5139" and abs(abs_v[brightest] + 10.54) < 0.02,
          "NGC 5139, M_V -10.54", "%s %.2f" % (names[brightest], abs_v[brightest]),
          "Harris 2010 gives -10.26 for the same cluster; LVDB follows a newer reference, "
          "and the 0.28 mag difference is between catalogues, not in this pipeline")

    meta = base_meta(
        "globulars", "tools/cosmodata/build_galaxy_points.py",
        [{"id": "lvdb", "name": "Local Volume Database v1.1.1, gc_harris + gc_mw_new",
          "url": "https://github.com/apace7/local_volume_database/tree/v1.1.1",
          "retrievedUtc": "2026-09-12T14:25:00Z", "licence": "CC0 1.0", "licenceTag": "cc0",
          "attribution": LVDB_ATTRIBUTION,
          "sha256": {"gc_harris.csv": sha256(os.path.join(src, "gc_harris.csv")),
                     "gc_mw_new.csv": sha256(os.path.join(src, "gc_mw_new.csv"))}}],
        {"pos": {"class": "Measured", "source": "lvdb",
                 "reference": "ICRS ra/dec are measured; the RADIUS is Inferred from a "
                              "published distance modulus, per-object ref in meta.refs"},
         "distErr": {"class": "Measured", "source": "lvdb",
                     "reference": "larger arm of the asymmetric distance-modulus error"},
         "absV": {"class": "Measured", "source": "lvdb",
                  "reference": "LVDB M_V, already extinction-corrected; NOT recomputed from "
                               "m_V minus mu, which would ignore extinction"},
         "rhalfPc": {"class": "Measured", "source": "lvdb",
                     "reference": "rhalf_physical where published, else the angular rhalf "
                                  "times this distance"}},
        {"names": names, "refs": refs,
         "counts": {"rows": len(rows), "noAbsV": no_mv,
                    "satelliteHosted": sum(1 for f in flags if f & NOT_MILKY_WAY_HOST)}})
    meta["assertions"] = check.rows
    columns = [
        csdata.Column("pos", "f32", 3, csdata.pack("f32", pos), extra={"unit": "pc", "frame": "ICRF"}),
        csdata.Column("distErr", "f32", 1, csdata.pack("f32", dist_err), extra={"unit": "pc"}),
        csdata.Column("flags", "u16", 1, csdata.pack("u16", flags)),
        csdata.Column("absV", "f32", 1, csdata.pack("f32", abs_v), extra={"unit": "mag"}),
        csdata.Column("rhalfPc", "f32", 1, csdata.pack("f32", rhalf_pc), extra={"unit": "pc"}),
    ]
    size = csdata.write(target, "CSGP", meta, columns, rows=len(rows))
    csdata.write_unity_meta(target)
    print("  wrote %s  %d bytes  %d rows" % (target, size, len(rows)))
    return size


# ---------------------------------------------------------------------------------------
# Molecular clouds: Rice et al. 2016, CC0 (Harvard Dataverse doi:10.7910/DVN/EWY90X)
# ---------------------------------------------------------------------------------------

RICE_ATTRIBUTION = (
    "Molecular clouds: Rice, Goodman, Bergin, Beaumont, Dame and Borkin (2016), ApJ 822, 52. "
    "Catalogue via Harvard Dataverse doi:10.7910/DVN/EWY90X, CC0 1.0.")


def read_fits_bintable(path):
    """The one bintable in the staged Rice FITS, as {column name: list of values}."""
    with open(path, "rb") as handle:
        raw = handle.read()
    if raw[:6] != b"SIMPLE":
        raise Refusal("%s is not a FITS file" % path)

    def cards(off):
        out = {}
        while True:
            block = raw[off:off + 2880]
            off += 2880
            if not block:
                raise Refusal("%s: no END card" % path)
            for i in range(36):
                card = block[i * 80:(i + 1) * 80].decode("ascii", "replace")
                key = card[:8].strip()
                if key == "END":
                    return out, off
                if card[8:10] == "= ":
                    out[key] = card[10:].split("/")[0].strip().strip("'").strip()

    _, off = cards(0)
    hdr, data_off = cards(off)
    if hdr.get("XTENSION") != "BINTABLE":
        raise Refusal("%s: expected a BINTABLE, found %r" % (path, hdr.get("XTENSION")))
    n, width, fields = int(hdr["NAXIS2"]), int(hdr["NAXIS1"]), int(hdr["TFIELDS"])
    layout, pos = [], 0
    for i in range(1, fields + 1):
        form = hdr["TFORM%d" % i]
        rep = int(form[:-1] or 1)
        code = form[-1]
        size = {"D": 8, "E": 4, "K": 8, "J": 4, "A": 1}[code] * rep
        layout.append((hdr["TTYPE%d" % i], code, pos, size))
        pos += size
    if pos != width:
        raise Refusal("%s: columns total %d bytes, NAXIS1 says %d" % (path, pos, width))

    out = {}
    for name, code, off_, size in layout:
        if code == "A":
            out[name] = [raw[data_off + r * width + off_:
                             data_off + r * width + off_ + size].decode("ascii", "replace")
                         for r in range(n)]
        else:
            fmt = {"D": ">d", "E": ">f", "K": ">q", "J": ">i"}[code]
            out[name] = [struct.unpack(fmt, raw[data_off + r * width + off_:
                                                data_off + r * width + off_ + size])[0]
                         for r in range(n)]
    return out, n


def build_clouds(out_dir):
    target = os.path.join(out_dir, "clouds.csgp")
    check = Checks(target)
    src = os.path.join(STAGING, "rice2016-molecular-clouds", "cloud_catalog.fits")
    print("clouds.csgp")
    table, n = read_fits_bintable(src)
    check("catalogue rows", n == 1064, 1064, n)
    # Two TTYPE cards in this file are truncated. Match the literal strings; do not "fix" them.
    for literal in ("l (deg)", "b (deg)", "v (km"):
        check("malformed column %r present" % literal, literal in table, True, literal in table)

    keep = [i for i in range(n)
            if table["distance"][i] == table["distance"][i] and table["distance"][i] > 0.0]
    dropped = n - len(keep)
    check("rows with a distance", 1000 <= len(keep) <= 1064, "1000 .. 1064", len(keep),
          "%d rows carry NaN in `distance` and are dropped, counted here" % dropped)

    pos, dist_err, flags, size_pc, mass, vlsr = [], [], [], [], [], []
    unresolved = 0
    for i in keep:
        # x_sol/y_sol/z_sol are kpc, heliocentric galactic. Rice's own columns; verified against
        # l, b and distance to the last printed digit, so no recomputation is needed.
        gx = table["x_sol"][i] * 1000.0
        gy = table["y_sol"][i] * 1000.0
        gz = table["z_sol"][i] * 1000.0
        pos.extend(gal_to_icrf(gx, gy, gz))
        err = max(table["error_distance_plus"][i], table["error_distance_minus"][i]) * 1000.0
        dist_err.append(err if err == err else 0.0)
        bits = 0
        kda = table["KDA_resolution"][i].strip()
        if kda == "U":
            bits |= AMBIGUOUS_DISTANCE
            unresolved += 1
        if table["distance"][i] > 20.0 or abs(table["z_gal"][i]) > 2.0:
            bits |= OUTLIER
        flags.append(bits)
        size_pc.append(table["size"][i])
        mass.append(table["mass"][i])
        vlsr.append(table["v (km"][i])

    dists = [math.hypot(math.hypot(pos[3 * k], pos[3 * k + 1]), pos[3 * k + 2])
             for k in range(len(keep))]
    check("distance range", 200.0 <= min(dists) and max(dists) <= 20000.0, "0.2 .. 20 kpc",
          "%.0f .. %.0f pc" % (min(dists), max(dists)))
    check("KDA unresolved count", unresolved == 663, 663, unresolved,
          "63% of the catalogue: their `distance` is a choice, not a measurement, and the "
          "renderer must show them differently")

    # The thin molecular disc, and its flaring. Both are published facts about the Galaxy, and
    # both are properties of the SHIPPED positions, so a rotation or unit error breaks them.
    def median_abs_z(lo, hi):
        vals = sorted(abs(table["z_gal"][i]) * 1000.0 for i in keep
                      if lo <= table["R_gal"][i] < hi)
        return vals[len(vals) // 2] if vals else float("nan")

    inner, outer = median_abs_z(4.0, 10.0), median_abs_z(10.0, 16.0)
    check("thin molecular disc", inner < 60.0, "median |z| < 60 pc for R 4-10 kpc",
          round(inner, 1))
    check("outer disc flares", outer / inner >= 1.8, ">= 1.8x thicker at R 10-16 kpc",
          round(outer / inner, 2), "median |z| %.1f -> %.1f pc" % (inner, outer))

    meta = base_meta(
        "clouds", "tools/cosmodata/build_galaxy_points.py",
        [{"id": "rice2016", "name": "Rice et al. 2016 Galactic molecular cloud catalogue",
          "url": "https://doi.org/10.7910/DVN/EWY90X",
          "retrievedUtc": "2026-09-12T14:28:00Z", "licence": "CC0 1.0", "licenceTag": "cc0",
          "attribution": RICE_ATTRIBUTION, "sha256": sha256(src)}],
        {"pos": {"class": "Inferred", "source": "rice2016",
                 "reference": "direction is Measured (CO centroid); the distance is KINEMATIC, "
                              "from a rotation curve, and 663 of 1039 have an unresolved "
                              "near/far ambiguity (flag bit 4)"},
         "distErr": {"class": "Measured", "source": "rice2016",
                     "reference": "larger arm of the published asymmetric distance error"},
         "sizePc": {"class": "Inferred", "source": "rice2016",
                    "reference": "angular radius times the kinematic distance"},
         "massMsun": {"class": "Inferred", "source": "rice2016",
                      "reference": "CO luminosity mass, X_CO-dependent"},
         "vlsr": {"class": "Measured", "source": "rice2016", "reference": "CO line centroid"}},
        {"counts": {"rows": len(keep), "droppedNoDistance": dropped,
                    "kdaUnresolved": unresolved},
         "notes": ["This layer exists because no commercial-clean Galactic Cepheid catalogue "
                   "exists (DATA-LICENCE-AMENDMENT section 2 row 11). It traces the same young "
                   "disc the Cepheids would have, without their per-star ages or their "
                   "few-per-cent distances."]})
    meta["assertions"] = check.rows
    columns = [
        csdata.Column("pos", "f32", 3, csdata.pack("f32", pos), extra={"unit": "pc", "frame": "ICRF"}),
        csdata.Column("distErr", "f32", 1, csdata.pack("f32", dist_err), extra={"unit": "pc"}),
        csdata.Column("flags", "u16", 1, csdata.pack("u16", flags)),
        csdata.Column("sizePc", "f32", 1, csdata.pack("f32", size_pc), extra={"unit": "pc"}),
        csdata.Column("massMsun", "f32", 1, csdata.pack("f32", mass), extra={"unit": "Msun"}),
        csdata.Column("vlsr", "f32", 1, csdata.pack("f32", vlsr), extra={"unit": "km/s"}),
    ]
    size = csdata.write(target, "CSGP", meta, columns, rows=len(keep))
    csdata.write_unity_meta(target)
    print("  wrote %s  %d bytes  %d rows" % (target, size, len(keep)))
    return size


# ---------------------------------------------------------------------------------------
# The Radcliffe Wave: Konietzka et al. 2024, CC0
# ---------------------------------------------------------------------------------------

RW_ATTRIBUTION = (
    "Radcliffe Wave: Konietzka, Goodman, Zucker et al. (2024), Nature 628, 62. Best-fit model "
    "and member clusters via Harvard Dataverse doi:10.7910/DVN/F98QHY and XSCB9N, CC0 1.0.")


def read_tsv(path):
    with open(path) as handle:
        head = handle.readline().rstrip("\n").split("\t")
        rows = [line.rstrip("\n").split("\t") for line in handle if line.strip()]
    return head, rows


def build_radcliffe(out_dir):
    written = 0
    src = os.path.join(STAGING, "radcliffe-wave-konietzka2024")

    # --- the fitted spine ---------------------------------------------------------------
    target = os.path.join(out_dir, "radcliffe.csgp")
    check = Checks(target)
    print("radcliffe.csgp")
    path = os.path.join(src, "Radcliffe_Wave_best_fit_model_Konietzka2024.tab")
    head, rows = read_tsv(path)
    ix = {k: i for i, k in enumerate(head)}
    check("spine points", len(rows) == 1500, 1500, len(rows))
    pos, vz = [], []
    for row in rows:
        gx, gy, gz = (float(row[ix["x"]]), float(row[ix["y"]]), float(row[ix["z"]]))
        pos.extend(gal_to_icrf(gx, gy, gz))
        vz.append(float(row[ix["vz"]]))
    span = math.dist((float(rows[0][ix["x"]]), float(rows[0][ix["y"]]), float(rows[0][ix["z"]])),
                     (float(rows[-1][ix["x"]]), float(rows[-1][ix["y"]]),
                      float(rows[-1][ix["z"]])))
    check("end-to-end span", 2000.0 <= span <= 3200.0, "2.0 .. 3.2 kpc", round(span, 1),
          "the paper's 2.7 kpc wave")
    amp = max(abs(float(r[ix["z"]])) for r in rows)
    check("vertical amplitude", 50.0 <= amp <= 400.0, "50 .. 400 pc", round(amp, 1))
    nearest = min(math.dist((0.0, 0.0, 0.0), (float(r[ix["x"]]), float(r[ix["y"]]),
                                              float(r[ix["z"]]))) for r in rows)
    check("closest approach to the Sun", 100.0 <= nearest <= 500.0, "100 .. 500 pc",
          round(nearest, 1), "the paper puts the Sun about 300 pc from the wave")

    meta = base_meta(
        "radcliffe", "tools/cosmodata/build_galaxy_points.py",
        [{"id": "konietzka2024", "name": "Radcliffe Wave best-fit model",
          "url": "https://doi.org/10.7910/DVN/F98QHY",
          "retrievedUtc": "2026-09-12T14:28:00Z", "licence": "CC0 1.0", "licenceTag": "cc0",
          "attribution": RW_ATTRIBUTION, "sha256": sha256(path)}],
        {"pos": {"class": "Inferred", "source": "konietzka2024",
                 "reference": "a fitted damped-sinusoid spine, not observed points"},
         "vz": {"class": "Inferred", "source": "konietzka2024",
                "reference": "the oscillation velocity that is the 2024 paper's result"}},
        {"polyline": True,
         "counts": {"rows": len(rows)},
         "notes": ["Consecutive rows form one open polyline; this is a curve, not a cloud of "
                   "points.",
                   "Animating z from vz is a legitimate extrapolation and must be labelled as "
                   "one; a static render throws away the finding."]})
    meta["assertions"] = check.rows
    written += csdata.write(target, "CSGP", meta, [
        csdata.Column("pos", "f32", 3, csdata.pack("f32", pos), extra={"unit": "pc", "frame": "ICRF"}),
        csdata.Column("distErr", "f32", 1, csdata.pack("f32", [0.0] * len(rows)), extra={"unit": "pc"}),
        csdata.Column("flags", "u16", 1, csdata.pack("u16", [0] * len(rows))),
        csdata.Column("vz", "f32", 1, csdata.pack("f32", vz), extra={"unit": "km/s"}),
    ], rows=len(rows))
    csdata.write_unity_meta(target)
    print("  wrote %s  %d rows" % (target, len(rows)))

    # --- the member clusters ------------------------------------------------------------
    target = os.path.join(out_dir, "radcliffe_clusters.csgp")
    check = Checks(target)
    print("radcliffe_clusters.csgp")
    path = os.path.join(src, "Radcliffe_Wave_stellar_clusters_Konietzka2024.tab")
    head, rows = read_tsv(path)
    ix = {k: i for i, k in enumerate(head)}
    check("cluster rows", len(rows) == 125, 125, len(rows))
    pos, err, flags, ages, names = [], [], [], [], []
    outliers = 0
    for row in rows:
        gx, gy, gz = (float(row[ix["x"]]), float(row[ix["y"]]), float(row[ix["z"]]))
        pos.extend(gal_to_icrf(gx, gy, gz))
        err.append(math.sqrt(float(row[ix["sigmax"]]) ** 2 + float(row[ix["sigmay"]]) ** 2 +
                             float(row[ix["sigmaz"]]) ** 2))
        bits = 0
        if int(float(row[ix["inlier"]])) == 0:
            bits |= NOT_A_MEMBER
            outliers += 1
        flags.append(bits)
        ages.append(float(row[ix["age"]]))
        names.append(row[ix["name"]].strip().strip('"'))     # bare numbers in quotes: strings
    check("non-members flagged", 0 < outliers < len(rows), "some but not all", outliers,
          "inlier = 0 rows are NOT wave members; drawing them as part of it misstates the result")
    check("names stay strings", all(isinstance(n, str) for n in names), True, True)
    check("ages", 0.0 <= min(ages) and max(ages) <= 100.0, "0 .. 100 Myr",
          "%.0f .. %.0f" % (min(ages), max(ages)))

    meta = base_meta(
        "radcliffeClusters", "tools/cosmodata/build_galaxy_points.py",
        [{"id": "konietzka2024c", "name": "Radcliffe Wave stellar clusters",
          "url": "https://doi.org/10.7910/DVN/XSCB9N",
          "retrievedUtc": "2026-09-12T14:28:00Z", "licence": "CC0 1.0", "licenceTag": "cc0",
          "attribution": RW_ATTRIBUTION, "sha256": sha256(path)}],
        {"pos": {"class": "Measured", "source": "konietzka2024c",
                 "reference": "Gaia-based cluster distances, per-cluster reference in "
                              "meta.clusterRefs"},
         "distErr": {"class": "Measured", "source": "konietzka2024c",
                     "reference": "quadrature sum of sigma_x, sigma_y and sigma_z"},
         "ageMyr": {"class": "Inferred", "source": "konietzka2024c",
                    "reference": "isochrone age"}},
        {"names": names,
         "clusterRefs": [row[ix["reference"]].strip().strip('"') for row in rows],
         "counts": {"rows": len(rows), "nonMembers": outliers}})
    meta["assertions"] = check.rows
    written += csdata.write(target, "CSGP", meta, [
        csdata.Column("pos", "f32", 3, csdata.pack("f32", pos), extra={"unit": "pc", "frame": "ICRF"}),
        csdata.Column("distErr", "f32", 1, csdata.pack("f32", err), extra={"unit": "pc"}),
        csdata.Column("flags", "u16", 1, csdata.pack("u16", flags)),
        csdata.Column("ageMyr", "f32", 1, csdata.pack("f32", ages), extra={"unit": "Myr"}),
    ], rows=len(rows))
    csdata.write_unity_meta(target)
    print("  wrote %s  %d rows" % (target, len(rows)))
    return written


# ---------------------------------------------------------------------------------------
# Named clouds: Zucker et al. 2020, CC0, written in the places schema
# ---------------------------------------------------------------------------------------

ZUCKER_ATTRIBUTION = (
    "Cloud distances: Zucker, Speagle, Schlafly, Green, Alves, Goodman et al. (2020), "
    "A&A 633, A51 / AJ 879, 125 handbook table, via Harvard Dataverse doi:10.7910/DVN/07L7YZ, "
    "CC0 1.0.")


def build_named_clouds(out_dir):
    target = os.path.join(out_dir, "namedclouds.json")
    check = Checks(target)
    print("namedclouds.json")
    path = os.path.join(STAGING, "zucker2020-cloud-distances",
                        "Handbook_Distances_Zucker2020.dat")
    with open(path) as handle:
        head = handle.readline().split()
        rows = [line.split() for line in handle if line.strip()]
    ix = {k: i for i, k in enumerate(head)}
    check("sightlines", len(rows) == 326, 326, len(rows))

    groups = {}
    for row in rows:
        groups.setdefault(row[ix["name"]], []).append(row)
    check("named clouds", len(groups) == 94, 94, len(groups),
          "one row is one SIGHTLINE, not one cloud; Orion alone has 17")

    def median(values):
        values = sorted(values)
        return values[len(values) // 2]

    places = []
    for name in sorted(groups):
        group = groups[name]
        group.sort(key=lambda r: float(r[ix["d50"]]))
        pick = group[len(group) // 2]                  # a real sightline at the median distance
        l = float(pick[ix["l"]])
        b = float(pick[ix["b"]])
        d = float(pick[ix["d50"]])
        err = max(float(pick[ix["d84"]]) - d, d - float(pick[ix["d16"]]))
        # The angular spread of the group's sightlines, at that distance, is the cloud's extent.
        spread = 0.0
        for row in group:
            li, bi = math.radians(float(row[ix["l"]])), math.radians(float(row[ix["b"]]))
            cosang = (math.sin(bi) * math.sin(math.radians(b)) +
                      math.cos(bi) * math.cos(math.radians(b)) * math.cos(li - math.radians(l)))
            spread = max(spread, math.acos(max(-1.0, min(1.0, cosang))))
        places.append({
            "id": name.lower().replace("_", "-"),
            "name": name.replace("_", " "),
            "category": "cloud",
            "l": l, "b": b, "distancePc": d, "distanceErrPc": round(err, 1),
            "radiusPc": round(max(spread * d, 1.0), 1),
            "description": "%d sightline%s, distance %.0f pc" % (len(group),
                                                                 "" if len(group) == 1 else "s",
                                                                 d),
            "reference": "Zucker et al. 2020, AJ 879, 125 (handbook of distances to "
                         "molecular clouds); per-sightline posterior median",
            "provenance": {"position": "Measured", "distance": "Inferred",
                           "radius": "Inferred"},
        })

    by_id = {p["id"]: p for p in places}
    check("Orion present", "orion" in by_id, True, "orion" in by_id)
    check("Orion distance", abs(by_id["orion"]["distancePc"] - 420.0) <= 25.0,
          "420 +/- 25 pc", by_id["orion"]["distancePc"])
    check("Taurus is the nearest of the big four",
          by_id["taurus"]["distancePc"] < by_id["perseus"]["distancePc"] <
          by_id["orion"]["distancePc"] < by_id["california"]["distancePc"],
          "Taurus < Perseus < Orion < California",
          "%.0f < %.0f < %.0f < %.0f" % (by_id["taurus"]["distancePc"],
                                         by_id["perseus"]["distancePc"],
                                         by_id["orion"]["distancePc"],
                                         by_id["california"]["distancePc"]))
    check("every place has a reference", all(p.get("reference") for p in places), True, True)

    document = {
        "schema": "cosmosynth.places/1",
        "generator": "tools/cosmodata/build_galaxy_points.py",
        "generatorCommit": commit(),
        "generatedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "attribution": ZUCKER_ATTRIBUTION,
        "licenceTag": "cc0",
        "sourceSha256": sha256(path),
        "assertions": check.rows,
        "places": places,
    }
    tmp = target + ".tmp"
    os.makedirs(out_dir, exist_ok=True)
    try:
        with open(tmp, "w") as handle:
            json.dump(document, handle, indent=1)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        json.load(open(tmp))
        os.replace(tmp, target)
        csdata.write_unity_meta(target)
    except Exception:
        for stale in (tmp, target):
            if os.path.exists(stale):
                os.remove(stale)
        raise
    size = os.path.getsize(target)
    print("  wrote %s  %d bytes  %d places" % (target, size, len(places)))
    return size


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(REPO, "unity", "Assets", "StreamingAssets",
                                                  "CosmoSynth", "galaxy"))
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    # The rotation every galactic->ICRF conversion below goes through, checked against a known
    # answer before it is used on anything: Sgr A* is at l = 359.944, b = -0.046.
    gx, gy, gz = icrf_to_gal(*radec_to_icrf(266.41684, -29.00781, 1.0))
    l = math.degrees(math.atan2(gy, gx)) % 360.0
    b = math.degrees(math.asin(max(-1.0, min(1.0, gz))))
    if abs(((l - 359.9442 + 180.0) % 360.0) - 180.0) > 0.01 or abs(b + 0.0462) > 0.01:
        raise Refusal("the ICRS->galactic rotation is wrong: Sgr A* came out at "
                      "l = %.4f, b = %.4f, not l = 359.944, b = -0.046" % (l, b))
    print("ICRS->galactic rotation ok: Sgr A* at l = %.4f, b = %.4f" % (l, b))

    total = 0
    total += build_globulars(args.out)
    total += build_clouds(args.out)
    total += build_radcliffe(args.out)
    total += build_named_clouds(args.out)
    print("total %d bytes" % total)


if __name__ == "__main__":
    try:
        main()
    except Refusal as exc:
        raise SystemExit(str(exc))
