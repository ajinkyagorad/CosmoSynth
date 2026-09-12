#!/usr/bin/env python3
"""Build lss/*: large-scale structure, from 13 Mpc to 1.6 Gpc.

    python3 tools/cosmodata/build_lss.py

Out: unity/Assets/StreamingAssets/CosmoSynth/lss/sdss.csrs       oriented galaxies, 13-420 Mpc
     unity/Assets/StreamingAssets/CosmoSynth/lss/desi.cspt       the cosmic web, 430-1600 Mpc
     unity/Assets/StreamingAssets/CosmoSynth/lss/voids.cspt      805 interior voids
     unity/Assets/StreamingAssets/CosmoSynth/lss/structures.json named clusters and basins

Standard library only, including the .npy reader - numpy is not imported. The contract allows
numpy for FITS cubes and HEALPix; nothing here is either, and a structured .npy is a header
and a flat record array.

ONE COSMOLOGY, NAMED IN EVERY FILE HEADER
    flat LCDM, H0 = 67.66 km/s/Mpc (h = 0.6766), Omega_m = 0.3150
    Planck 2018 TT,TE,EE+lowE+lensing+BAO

    Not a preference: the staged VAST void catalogue is the Planck2018 variant and its own
    coordinates invert to this cosmology, and the CMB layer is Planck-based. Design B section
    1.7 specified H0 = 74.6, which was Cosmicflows-4's zero point; CF4 is blocked by
    docs/DATA-LICENCE-AMENDMENT.md section 3, so its zero point goes with it. Mixing the two
    puts the same cluster in two places 10% apart.

THE TWO LAYERS TILE THE VOLUME, THEY DO NOT OVERLAP
    SDSS and DESI observed many of the same galaxies. Stacking both point clouds doubles the
    density in the overlap and leaves a visible seam at the DESI footprint edge. So the cut
    is made in redshift and the two are disjoint by construction:

        sdss.csrs   0.003 <= z < 0.10   the near field, with measured shapes
        desi.cspt   0.10  <= z <= 0.40  the volume-limited structure sample

    No object can be in both files. The price is that the boundary at 430 Mpc is a change of
    catalogue, and the app should say so rather than pretending one continuous survey.

WHAT THESE POSITIONS ARE NOT
    They are redshift-space positions from heliocentric redshifts. Clusters are radially
    smeared into fingers of God up to ~10 Mpc long - a real feature of the measurement, not a
    bug, and the reason `redshiftFrame` and `space` are in every meta block. No CMB-frame
    correction is applied: the dipole is 0.1% at z = 0.1, but it is 40% of the redshift at the
    0.003 floor, so rows below z = 0.01 carry a NearbyPeculiarVelocity bit instead of a
    correction this lane cannot source without the galactic rotation it is not allowed to use.
"""

from __future__ import annotations

import argparse
import ast
import csv
import json
import math
import os
import struct
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import csdata                                                    # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT_DIR = os.path.join(REPO, "unity", "Assets", "StreamingAssets", "CosmoSynth", "lss")

# --- the one cosmology ------------------------------------------------------------------
H0 = 67.66              # km/s/Mpc
OMEGA_M = 0.3150
H_LITTLE = 0.6766
C_KM_S = 299792.458
COSMOLOGY = {"model": "flat LCDM", "H0": H0, "h": H_LITTLE, "OmegaM": OMEGA_M,
             "OmegaLambda": round(1.0 - OMEGA_M, 4),
             "reference": "Planck 2018 TT,TE,EE+lowE+lensing+BAO",
             "comovingDistance": "Simpson, 512 intervals over [0, z], matching "
                                 "Core/Scale/Cosmology.ComovingDistanceMpc"}

SIMPSON_N = 512         # must match Core/Scale/Cosmology.cs exactly, or nothing round-trips

NEARBY_PV = 1 << 0
SHAPE_UNKNOWN = 1 << 1


def comoving_distance_mpc(z):
    """Simpson over [0, z] of dz/E(z). The same algorithm as Core, step for step."""
    if z <= 0.0:
        return 0.0
    n = SIMPSON_N
    h = z / n
    om, ol = OMEGA_M, 1.0 - OMEGA_M
    total = 1.0                                    # E(0) = 1, so 1/E(0) = 1
    for i in range(1, n):
        zi = i * h
        e = math.sqrt(om * (1.0 + zi) ** 3 + ol)
        total += (4.0 if i & 1 else 2.0) / e
    total += 1.0 / math.sqrt(om * (1.0 + z) ** 3 + ol)
    return (C_KM_S / H0) * total * h / 3.0


def icrf(ra_deg, dec_deg, distance):
    ra, dec = math.radians(ra_deg), math.radians(dec_deg)
    return (distance * math.cos(dec) * math.cos(ra),
            distance * math.cos(dec) * math.sin(ra),
            distance * math.sin(dec))


def fnv1a64(text):
    h = 0xCBF29CE484222325
    for byte in text.encode("utf-8"):
        h = ((h ^ byte) * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF
    return h


M64 = 0xFFFFFFFFFFFFFFFF


def fmix64(h):
    """Murmur3's 64-bit finalizer.

    Design B specifies the draw as FNV-1a-64(key) / 2^64. Measured on this data that is not
    uniform enough: over sequential row keys it kept 51,021 rows where 50,000 were asked for,
    a 2% excess and about 5 sigma on a binomial whose sigma is 210. FNV's avalanche is weak
    in the high bits, which is exactly the end of the word this draw reads. The finalizer
    costs three lines, keeps the draw deterministic and reproducible, and brings the count
    back inside noise. Recorded as a deviation from B in the lane report.
    """
    h = (h ^ (h >> 33)) & M64
    h = (h * 0xFF51AFD7ED558CCD) & M64
    h = (h ^ (h >> 33)) & M64
    h = (h * 0xC4CEB9FE1A85EC53) & M64
    return (h ^ (h >> 33)) & M64


def hash01(text):
    """A deterministic uniform draw in [0, 1) from a row key, so a rerun reproduces exactly."""
    return fmix64(fnv1a64(text)) / 2.0 ** 64


def read_npy(path):
    """Structured .npy -> (field names, list of tuples). Stdlib; numpy is not imported."""
    with open(path, "rb") as handle:
        magic = handle.read(6)
        if magic != b"\x93NUMPY":
            raise SystemExit(f"{path}: not a .npy file")
        major, _minor = handle.read(2)
        header_len = struct.unpack("<H" if major == 1 else "<I",
                                   handle.read(2 if major == 1 else 4))[0]
        header = ast.literal_eval(handle.read(header_len).decode("latin1"))
        if header.get("fortran_order"):
            raise SystemExit(f"{path}: fortran order is not supported")
        descr = header["descr"]
        if not isinstance(descr, list):
            raise SystemExit(f"{path}: expected a structured dtype, got {descr!r}")
        codes, names = "<", []
        for name, kind in descr:
            if kind not in ("<f4", "<f8", "<i4", "<i8"):
                raise SystemExit(f"{path}: unsupported field type {kind!r}")
            codes += {"<f4": "f", "<f8": "d", "<i4": "i", "<i8": "q"}[kind]
            names.append(name)
        record = struct.calcsize(codes)
        rows = header["shape"][0]
        blob = handle.read()
    if len(blob) != rows * record:
        raise SystemExit(f"{path}: {len(blob)} payload bytes, expected {rows * record}")
    return names, list(struct.iter_unpack(codes, blob))


def check_factory(assertions):
    def check(name, ok, expected, got, note=""):
        assertions.append({"name": name, "expected": expected, "got": got,
                           "ok": bool(ok), "note": note})
        if not ok:
            raise SystemExit(f"ASSERTION FAILED {name}: expected {expected}, got {got} {note}")
    return check


def staging_dir(repo):
    local = os.path.join(repo, "data-staging")
    if os.path.isdir(local):
        return local
    try:
        common = subprocess.check_output(
            ["git", "-C", repo, "rev-parse", "--path-format=absolute", "--git-common-dir"],
            stderr=subprocess.DEVNULL).decode().strip()
        shared = os.path.join(os.path.dirname(common), "data-staging")
        if os.path.isdir(shared):
            return shared
    except Exception:
        pass
    raise SystemExit("data-staging/ not found; it lives in the main checkout, not a worktree")


STAGING = staging_dir(REPO)


def base_meta(kind, generated, sources, extra):
    meta = {
        "kind": kind, "schema": 1,
        "generator": "tools/cosmodata/build_lss.py",
        "generatorCommit": csdata.generator_commit(REPO),
        "generatedUtc": generated,
        "licenceProfile": "commercial",
        "frame": "ICRF", "epoch": "J2000.0",
        "cosmology": COSMOLOGY,
        "sources": sources,
    }
    meta.update(extra)
    return meta


# ----------------------------------------------------------------------------------------
# 1. DESI: the volume-limited structure sample
# ----------------------------------------------------------------------------------------

def build_desi(generated, keep):
    assertions = []
    check = check_factory(assertions)

    files = [("ngc", "bgs_bright_m21p5_ngc"), ("sgc", "bgs_bright_m21p5_sgc")]
    records, total = [], 0
    metas = []
    for cap, stem in files:
        path = os.path.join(STAGING, "desi-dr1-lss", stem + ".npy")
        side = json.load(open(os.path.join(STAGING, "desi-dr1-lss", stem + ".meta.json")))
        metas.append((cap, stem, side))
        names, rows = read_npy(path)
        check(f"desi_{cap}_rows", len(rows) == side["rows"], side["rows"], len(rows),
              "against the staged .meta.json")
        idx = {n: i for i, n in enumerate(names)}
        for i, row in enumerate(rows):
            records.append((cap, i, row[idx["RA"]], row[idx["DEC"]], row[idx["Z"]],
                            row[idx["flux_r_dered"]], row[idx["flux_g_dered"]],
                            row[idx["WEIGHT"]]))
        total += len(rows)

    check("desi_total_rows", total == 300043, 300043, total,
          "BGS_BRIGHT-21.5 NGC + SGC, DR1 v1.5")

    zs = [r[4] for r in records]
    check("desi_z_range", 0.0999 <= min(zs) and max(zs) <= 0.4001,
          "0.1 <= z <= 0.4", f"{min(zs):.4f} .. {max(zs):.4f}",
          "the volume-limited sample's own selection, asserted on values not on a header")

    mean_w = sum(r[7] for r in records) / len(records)
    rate = keep / float(len(records))

    kept = []
    for cap, i, ra, dec, z, fr, fg, w in records:
        # Weighted thinning: the LSS WEIGHT undoes the survey's own incompleteness, so a
        # uniform cut would re-imprint exactly the systematic the weight exists to remove.
        p = min(1.0, rate * (w / mean_w))
        if hash01(f"desi:{cap}:{i}") >= p:
            continue
        d = comoving_distance_mpc(z)
        rmag = 22.5 - 2.5 * math.log10(fr) if fr > 0 else float("nan")
        gr = ((22.5 - 2.5 * math.log10(fg)) - rmag) if (fg > 0 and fr > 0) else float("nan")
        kept.append((ra, dec, z, d, rmag, gr))

    check("desi_kept_at_or_just_below_target",
          keep * 0.97 <= len(kept) <= keep, f"{int(keep * 0.97)} .. {keep}", len(kept),
          "clamping p at 1 for high-weight rows is concave, so the count lands at or a "
          "little below the target and never above it")

    # Known answer through the whole path, independent of the rows: the design's own probe
    # value recomputed at this cosmology.
    d_test = comoving_distance_mpc(0.1)
    check("desi_distance_function", abs(d_test - 432.442818) < 0.001,
          "D_C(0.1) = 432.442818 Mpc", round(d_test, 6),
          "independently reproduced by a 400,000-step midpoint rule to 1.7e-11 Mpc")
    d_low = comoving_distance_mpc(0.001)
    check("desi_low_z_limit", abs(d_low / (C_KM_S * 0.001 / H0) - 0.99976) < 1e-5,
          "D_C(0.001) / (cz/H0) = 0.99976", round(d_low / (C_KM_S * 0.001 / H0), 6),
          "the Hubble law is the z -> 0 limit of the integral; this is the units check")

    pos, zcol, rcol, gcol = [], [], [], []
    for ra, dec, z, d, rmag, gr in kept:
        pos.extend(icrf(ra, dec, d))
        zcol.append(z)
        rcol.append(rmag)
        gcol.append(gr)

    dists = [math.sqrt(pos[i] ** 2 + pos[i + 1] ** 2 + pos[i + 2] ** 2)
             for i in range(0, len(pos), 3)]
    check("desi_distance_range", 400.0 < min(dists) and max(dists) < 1700.0,
          "400 .. 1700 Mpc", f"{min(dists):.1f} .. {max(dists):.1f}")

    columns = [
        csdata.Column("pos", "f32", 3, csdata.pack("f32", pos), unit="Mpc"),
        csdata.Column("z", "f32", 1, csdata.pack("f32", zcol)),
        csdata.Column("rmag", "f32", 1, csdata.pack("f32", rcol), unit="mag"),
        csdata.Column("gr", "f32", 1, csdata.pack("f32", gcol), unit="mag"),
    ]
    meta = base_meta("desi-bgs", generated, [
        {"id": "desi-dr1-lss", "name": "DESI DR1 LSS clustering catalogues, BGS_BRIGHT-21.5",
         "url": metas[0][2]["url"], "rows": total,
         "licence": "CC BY 4.0", "licenceTag": "by",
         "attribution": "DESI Collaboration, DESI DR1, CC BY 4.0. Subsampled and converted to "
                        "comoving positions by CosmoSynth (a change indicated per CC BY 3(b))",
         "files": [{"npy": m[1] + ".npy", "sha256": m[2]["npy_sha256"], "rows": m[2]["rows"]}
                   for m in metas]}], {
        "space": "redshift-space", "redshiftFrame": "heliocentric",
        "selection": "BGS_BRIGHT-21.5 volume-limited, M_r < -21.5, 0.1 <= z <= 0.4, "
                     "NGC + SGC, weighted deterministic subsample",
        "subsample": {"method": "murmur3 fmix64(FNV-1a-64('desi:<cap>:<row>')) / 2^64 < keep/N * WEIGHT/meanWEIGHT",
                      "keep": keep, "fromRows": total, "meanWeight": round(mean_w, 5)},
        "footprint": "two disconnected galactic caps, ~14,000 deg^2. The sample's edge is "
                     "where DESI stopped looking, not a wall.",
        "provenance": {
            "pos": {"class": "Measured", "source": "desi-dr1-lss",
                    "note": "direction Measured; distance Inferred, being a comoving distance "
                            "from a heliocentric redshift at the stated cosmology, in "
                            "redshift space"},
            "z": {"class": "Measured", "source": "desi-dr1-lss"},
            "rmag": {"class": "Measured", "source": "desi-dr1-lss",
                     "note": "22.5 - 2.5 log10(flux_r_dered), de-reddened Legacy Surveys flux"},
            "gr": {"class": "Measured", "source": "desi-dr1-lss"},
        },
        "counts": {"rows": len(kept), "fromRows": total,
                   "magnitudeUnknown": sum(1 for v in rcol if v != v)},
        "assertions": assertions,
    })
    out = os.path.join(OUT_DIR, "desi.cspt")
    size = csdata.write(out, "CSPT", meta, columns)
    csdata.write_unity_meta(out)
    return out, size, len(kept), total


# ----------------------------------------------------------------------------------------
# 2. SDSS: the near field, with measured shapes
# ----------------------------------------------------------------------------------------

def build_sdss(generated, keep, z_max):
    assertions = []
    check = check_factory(assertions)
    path = os.path.join(STAGING, "sdss-dr18", "sdss_dr18_galaxies.csv")

    rows = []
    total = 0
    with open(path, newline="") as handle:
        for i, row in enumerate(csv.DictReader(handle)):
            total += 1
            try:
                z = float(row["z"])
                ra, dec = float(row["ra"]), float(row["dec"])
            except ValueError:
                continue
            if not (0.003 <= z < z_max):
                continue
            rows.append((i, ra, dec, z, row["petroMag_r"], row["expAB_r"], row["expPhi_r"],
                         row["modelMag_g"], row["modelMag_r"]))

    check("sdss_total_rows", total == 492189, 492189, total, "the staged SkyServer pull")
    check("sdss_in_shell", len(rows) > 300000, "> 300000", len(rows),
          f"0.003 <= z < {z_max}")

    rate = keep / float(len(rows))
    kept = []
    n_shape_unknown = n_nearby = 0
    for i, ra, dec, z, rmag_s, ba_s, pa_s, g_s, r_s in rows:
        if hash01(f"sdss:{i}") >= rate:
            continue
        flags = 0
        try:
            ba = float(ba_s)
            pa = float(pa_s) % 180.0
        except ValueError:
            ba, pa, flags = float("nan"), float("nan"), flags | SHAPE_UNKNOWN
        else:
            # 1.000 out of an exponential fit is the fit giving up, not a round galaxy -
            # the same failure as 2MASS's sup_ba = 1.000 on NGC 3109.
            if not (0.05 <= ba < 0.999):
                ba, pa, flags = float("nan"), float("nan"), flags | SHAPE_UNKNOWN
        if flags & SHAPE_UNKNOWN:
            n_shape_unknown += 1
        if z < 0.01:
            flags |= NEARBY_PV
            n_nearby += 1
        try:
            rmag = float(rmag_s)
        except ValueError:
            rmag = float("nan")
        try:
            gr = float(g_s) - float(r_s)
        except ValueError:
            gr = float("nan")
        kept.append((ra, dec, z, comoving_distance_mpc(z), rmag, gr, ba, pa, flags))

    check("sdss_kept_within_2pc", abs(len(kept) - keep) <= max(50, keep * 0.02),
          f"{keep} +/- 2%", len(kept))
    check("sdss_shape_coverage", n_shape_unknown < 0.10 * len(kept),
          "< 10% shape-unknown", f"{n_shape_unknown} of {len(kept)}",
          "SDSS fits an exponential to nearly everything; if this ever rises, look at why")

    # The footprint check that catches a frame or units error: the galactic plane crosses
    # SDSS's footprint between RA 60 and 120, and the catalogue is nearly empty there.
    in_gap = sum(1 for r in kept if 75.0 <= r[0] < 105.0)
    check("sdss_galactic_plane_gap", in_gap < 0.002 * len(kept), "< 0.2% of rows",
          f"{in_gap} of {len(kept)} in RA 75-105")

    pos, zc, rc, gc, bac, pac, fl = [], [], [], [], [], [], []
    for ra, dec, z, d, rmag, gr, ba, pa, flags in kept:
        pos.extend(icrf(ra, dec, d))
        zc.append(z); rc.append(rmag); gc.append(gr); bac.append(ba); pac.append(pa)
        fl.append(flags)

    dists = [math.sqrt(pos[i] ** 2 + pos[i + 1] ** 2 + pos[i + 2] ** 2)
             for i in range(0, len(pos), 3)]
    check("sdss_distance_range", 10.0 < min(dists) and max(dists) < 440.0,
          "13 .. 430 Mpc", f"{min(dists):.1f} .. {max(dists):.1f}")

    columns = [
        csdata.Column("pos", "f32", 3, csdata.pack("f32", pos), unit="Mpc"),
        csdata.Column("z", "f32", 1, csdata.pack("f32", zc)),
        csdata.Column("rmag", "f32", 1, csdata.pack("f32", rc), unit="mag"),
        csdata.Column("gr", "f32", 1, csdata.pack("f32", gc), unit="mag"),
        csdata.Column("ba", "f32", 1, csdata.pack("f32", bac)),
        csdata.Column("pa", "f32", 1, csdata.pack("f32", pac), unit="deg"),
        csdata.Column("flags", "u16", 1, csdata.pack("u16", fl)),
    ]
    meta = base_meta("sdss-galaxies", generated, [
        {"id": "sdss-dr18", "name": "SDSS DR18 SpecObj x PhotoObj, galaxies",
         "url": "https://skyserver.sdss.org/dr18/SkyServerWS/SearchTools/SqlSearch",
         "sha256": csdata.sha256(path), "rows": total,
         "licence": "public domain", "licenceTag": "pd",
         "attribution": "Sloan Digital Sky Survey DR18"}], {
        "space": "redshift-space", "redshiftFrame": "heliocentric",
        "selection": f"class GALAXY, zWarning 0, sciencePrimary 1, 0.003 <= z < {z_max}",
        "subsample": {"method": "murmur3 fmix64(FNV-1a-64('sdss:<row>')) / 2^64 < keep/N", "keep": keep,
                      "fromRows": len(rows)},
        "footprint": "northern galactic cap plus three southern stripes, ~9,300 deg^2. "
                     "Nearly empty between RA 60 and 120: that is the galactic plane.",
        "provenance": {
            "pos": {"class": "Measured", "source": "sdss-dr18",
                    "note": "direction Measured; distance Inferred from a heliocentric "
                            "redshift at the stated cosmology, in redshift space"},
            "z": {"class": "Measured", "source": "sdss-dr18"},
            "rmag": {"class": "Measured", "source": "sdss-dr18", "note": "petroMag_r, not "
                     "extinction corrected"},
            "gr": {"class": "Measured", "source": "sdss-dr18", "note": "modelMag_g - modelMag_r"},
            "ba": {"class": "Measured", "source": "sdss-dr18",
                   "note": "expAB_r, the projected axis ratio of an exponential fit in r"},
            "pa": {"class": "Measured", "source": "sdss-dr18",
                   "note": "expPhi_r, degrees East of North, folded to [0, 180)"},
        },
        "provenanceBits": [
            {"bit": 0, "field": "pos", "class": "Inferred",
             "note": "z < 0.01: peculiar velocity is a large fraction of the redshift, so "
                     "this distance is weak. No CMB-frame correction has been applied"},
            {"bit": 1, "field": "ba", "class": "Inferred",
             "note": "no usable exponential fit; ba and pa are NaN. Draw round, do not orient"},
        ],
        "counts": {"rows": len(kept), "fromRows": len(rows), "ofCatalogue": total,
                   "shapeUnknown": n_shape_unknown, "nearbyPeculiarVelocity": n_nearby},
        "assertions": assertions,
    })
    out = os.path.join(OUT_DIR, "sdss.csrs")
    size = csdata.write(out, "CSRS", meta, columns)
    csdata.write_unity_meta(out)
    return out, size, len(kept), total


# ----------------------------------------------------------------------------------------
# 3. Voids
# ----------------------------------------------------------------------------------------

def build_voids(generated):
    assertions = []
    check = check_factory(assertions)
    path = os.path.join(STAGING, "vast-voids-sdss-dr7",
                        "VoidFinder-nsa_v1_0_1_Planck2018_comoving_maximal.txt")

    rows = []
    with open(path) as handle:
        for line in handle:
            if line.startswith("#") or not line.strip():
                continue
            f = [float(v) for v in line.split()]
            rows.append(f)
    check("void_rows", len(rows) == 1163, 1163, len(rows), "VAST VoidFinder, SDSS DR7")

    # The catalogue's own internal consistency, checked rather than trusted: r must be the
    # norm of (x, y, z), which is what proves the cartesian columns and the radial column
    # share a unit before anything is divided by h.
    worst = max(abs(math.sqrt(f[0] ** 2 + f[1] ** 2 + f[2] ** 2) - f[6]) for f in rows)
    check("void_radius_consistency", worst < 1e-6, "< 1e-6 Mpc/h", f"{worst:.2e}")

    interior = [f for f in rows if int(f[5]) == 0]
    check("void_interior", len(interior) == 805, 805, len(interior),
          "edge = 0. A boundary-touching void's size is set by where SDSS stopped looking")

    pos, reff, rmax = [], [], []
    for f in interior:
        pos.extend((f[0] / H_LITTLE, f[1] / H_LITTLE, f[2] / H_LITTLE))
        reff.append(f[9] / H_LITTLE)
        rmax.append(f[3] / H_LITTLE)
    median_reff = sorted(reff)[len(reff) // 2]
    check("void_median_reff", 20.0 < median_reff < 32.0, "20 .. 32 Mpc",
          round(median_reff, 2),
          "the Zenodo description's 15-19 Mpc/h, divided by h = 0.6766")

    columns = [
        csdata.Column("pos", "f32", 3, csdata.pack("f32", pos), unit="Mpc"),
        csdata.Column("rEff", "f32", 1, csdata.pack("f32", reff), unit="Mpc"),
        csdata.Column("rMax", "f32", 1, csdata.pack("f32", rmax), unit="Mpc"),
    ]
    meta = base_meta("voids", generated, [
        {"id": "vast-voids", "name": "VAST VoidFinder void catalogue, SDSS DR7, Planck 2018",
         "url": "https://zenodo.org/records/VAST", "sha256": csdata.sha256(path),
         "rows": len(rows), "licence": "CC BY 4.0", "licenceTag": "by",
         "attribution": "VAST void catalogue (VoidFinder, SDSS DR7, Planck2018 cosmology), "
                        "CC BY 4.0. Converted from comoving Mpc/h to comoving Mpc by "
                        "CosmoSynth (a change indicated per CC BY 3(b))"}], {
        "space": "comoving", "redshiftFrame": "n/a",
        "selection": "edge = 0 only: the 333 boundary-touching and 25 exterior voids are not "
                     "shipped, because their size is set by the survey edge and drawing them "
                     "produces a shell of fake voids around the footprint",
        "provenance": {
            "pos": {"class": "Inferred", "source": "vast-voids",
                    "note": "a void centre is a property of a watershed of the galaxy field, "
                            "not an observed object"},
            "rEff": {"class": "Inferred", "source": "vast-voids",
                     "note": "radius of a sphere of the same volume as the identified void"},
            "rMax": {"class": "Inferred", "source": "vast-voids",
                     "note": "radius of the largest sphere inscribed in the void"},
        },
        "counts": {"rows": len(interior), "fromRows": len(rows),
                   "edgeTouching": sum(1 for f in rows if int(f[5]) == 1),
                   "beyondEdge": sum(1 for f in rows if int(f[5]) == 2)},
        "assertions": assertions,
    })
    out = os.path.join(OUT_DIR, "voids.cspt")
    size = csdata.write(out, "CSPT", meta, columns)
    csdata.write_unity_meta(out)
    return out, size, len(interior)


# ----------------------------------------------------------------------------------------
# 4. Named structures
# ----------------------------------------------------------------------------------------

DC23 = "Dupuy & Courtois 2023, Cosmicflows-4 basins of attraction, Table A.1; values " \
       "transcribed from docs/research/2026-09-11-data-beyond-and-cmb.md section 4.3, " \
       "which read them from the paper PDF"
SIMBAD = "position resolved through CDS SIMBAD, 2026-09-12; redshift is SIMBAD's"

# name, ra, dec, V_cmb km/s, volume in 1e6 (Mpc/h)^3, repeller?
BASINS = [
    ("Laniakea", 263.0, -39.0, 7412.0, 1.9, False),
    ("Apus", 199.0, -75.0, 13096.0, 9.5, False),
    ("Hercules basin", 237.0, 9.0, 11213.0, 3.1, False),
    ("Lepus", 99.0, -49.0, 13189.0, 8.1, False),
    ("Perseus-Pisces", 56.0, 21.0, 6102.0, 4.8, False),
    ("Shapley", 199.0, -40.0, 15468.0, 7.9, False),
    ("SDSS-1a", 133.0, 24.0, 34117.0, 184.8, False),
    ("SDSS-2a", 209.0, 12.0, 32979.0, 169.8, False),
    ("SDSS-2b", 218.0, 36.0, 33007.0, 130.2, False),
    ("Sculptor Void", 18.0, -16.0, 11294.0, 1.3, True),
    ("near Bootes Void", 194.0, 13.0, 12908.0, 7.8, True),
    ("Dipole Repeller", 0.0, 22.0, 19795.0, 163.0, True),
]

# name, simbad id, ra, dec, z, abell number or None
CLUSTERS = [
    ("Virgo Cluster", "NAME Virgo Cluster", 187.699, 12.385, 0.0038, None),
    ("Coma Cluster", "ACO 1656", 194.935, 27.912, 0.0234, 1656),
    ("Norma Cluster", "ACO 3627", 243.594, -60.869, 0.01628, 3627),
    ("Perseus Cluster", "ACO 426", 49.948, 41.515, 0.01836, 426),
    ("Leo Cluster", "ACO 1367", 176.186, 19.700, 0.0225, 1367),
    ("Hercules Cluster", "ACO 2151", 241.313, 17.749, 0.0352, 2151),
    ("Centaurus Cluster", "ACO 3526", 192.205, -41.305, 0.0114, 3526),
    ("Fornax Cluster", "ACO S 373", 54.623, -35.452, 0.0050, None),
    ("Hydra I Cluster", "ACO 1060", 159.150, -27.518, 0.0129, 1060),
    ("Shapley core", "ACO 3558", 202.014, -31.527, 0.0474, 3558),
    ("Abell 2199", "ACO 2199", 247.144, 39.541, 0.0309, 2199),
]


def build_structures(generated):
    peaks_path = os.path.join(STAGING, "named-structures", "structures.csv")
    with open(peaks_path, newline="") as handle:
        peaks = list(csv.DictReader(line for line in handle if not line.startswith("#")))

    def abell_number(text):
        digits = ""
        for token in (text or "").replace("(", " ").split():
            if token.lower().startswith("abell"):
                continue
            if token.isdigit():
                digits = token
                break
        return int(digits) if digits else None

    labelled = {}
    for row in peaks:
        n = abell_number(row.get("name", ""))
        if n is not None:
            labelled[n] = row

    places = []
    for name, ra, dec, vcmb, volume, repeller in BASINS:
        z = vcmb / C_KM_S
        entry = {
            "id": name.lower().replace(" ", "-"),
            "name": name,
            "category": "repeller" if repeller else "basin",
            "ra": ra % 360.0, "dec": dec, "z": round(z, 5),
            "distanceMpc": round(comoving_distance_mpc(z), 1),
            "reference": DC23,
            "provenance": {"position": "Inferred", "distance": "Inferred",
                           "volume": "Inferred"},
            "volume1e6Mpch3": volume,
            "description": "The attractor (or repeller) of a gravitational basin in the "
                           "Cosmicflows-4 velocity reconstruction. It is a property of a "
                           "reconstructed field, not an object that can be observed at this "
                           "position.",
        }
        if name == "Laniakea":
            entry["description"] += (
                " CosmoSynth ships this point and this published volume, and does NOT ship the "
                "basin boundary: the watershed grid that defines it is Cosmicflows-4 data, "
                "which carries no commercial licence (docs/DATA-LICENCE-AMENDMENT.md section "
                "3). A boundary drawn without that grid would be invented, so there is none.")
        places.append(entry)

    for name, simbad_id, ra, dec, z, abell in CLUSTERS:
        entry = {
            "id": name.lower().replace(" ", "-"),
            "name": name, "category": "cluster",
            "ra": ra, "dec": dec, "z": z,
            "distanceMpc": round(comoving_distance_mpc(z), 1),
            "reference": f"{simbad_id}; {SIMBAD}",
            "provenance": {"position": "Measured", "distance": "Inferred"},
        }
        peak = labelled.get(abell) if abell else None
        if peak:
            # An independent confirmation from our own data, not a second entry for the same
            # cluster: the same object, found twice, with the disagreement written down.
            entry["sdssPeak"] = {
                "ra": float(peak["ra_deg"]), "dec": float(peak["dec_deg"]),
                "z": float(peak["z"]),
                "distanceMpc": round(comoving_distance_mpc(float(peak["z"])), 1),
                "evidence": peak["match_evidence"],
                "provenance": "Measured - overdensity peak in data-staging/sdss-dr18, "
                              "4 Mpc grid smoothed at 8 Mpc",
            }
        places.append(entry)

    # Peaks we identified that are not in the SIMBAD list are entries in their own right.
    for n, peak in sorted(labelled.items()):
        if any(c[5] == n for c in CLUSTERS):
            continue
        places.append({
            "id": f"abell-{n}", "name": f"Abell {n}", "category": "cluster",
            "ra": float(peak["ra_deg"]), "dec": float(peak["dec_deg"]),
            "z": float(peak["z"]),
            "distanceMpc": round(comoving_distance_mpc(float(peak["z"])), 1),
            "reference": "measured as an overdensity peak in SDSS DR18 (public domain) by "
                         "data-staging/_tools/derive_structures.py; label attached by "
                         "positional match to a CDS Sesame coordinate within one 8 Mpc "
                         "smoothing length: " + peak["match_evidence"],
            "provenance": {"position": "Measured", "distance": "Inferred"},
        })

    places.append({
        "id": "bootes-void", "name": "Bootes Void", "category": "void",
        "ra": 222.946, "dec": 45.796, "z": 0.05,
        "distanceMpc": round(comoving_distance_mpc(0.05), 1),
        "reference": "NAME Bootes Void; " + SIMBAD + ". The redshift is INFERRED",
        "provenance": {"position": "Measured", "distance": "Inferred"},
        "description": "No sphere in lss/voids.cspt is labelled with this name. VoidFinder "
                       "partitions this region into several smaller voids, none of which is "
                       "the ~60 Mpc object the name usually denotes, so naming a catalogue "
                       "row after it would be a wrong claim dressed as data.",
    })
    places.append({
        "id": "local-void", "name": "Local Void", "category": "void",
        "ra": 279.5, "dec": 18.0, "z": 0.002,
        "distanceMpc": round(comoving_distance_mpc(0.002), 1),
        "reference": "NAME Local Void; " + SIMBAD + ". Both the centre and the redshift are "
                     "coarse and INFERRED",
        "provenance": {"position": "Inferred", "distance": "Inferred"},
    })

    for entry in places:
        if not entry.get("reference"):
            raise SystemExit(f"{entry['id']}: every place needs a reference")
        if not (0.0 <= entry["ra"] < 360.0 and -90.0 <= entry["dec"] <= 90.0):
            raise SystemExit(f"{entry['id']}: position out of range")

    doc = {
        "schema": "cosmosynth.places/1",
        "generator": "tools/cosmodata/build_lss.py",
        "generatorCommit": csdata.generator_commit(REPO),
        "generatedUtc": generated,
        "frame": "ICRF", "epoch": "J2000.0",
        "cosmology": COSMOLOGY,
        "note": "Every distance here is comoving, from the redshift, at the cosmology above. "
                "Basin attractors are reconstruction products; cluster positions are "
                "measured and their distances are not.",
        "places": places,
    }
    out = os.path.join(OUT_DIR, "structures.json")
    tmp = out + ".tmp"
    try:
        with open(tmp, "w", newline="\n") as handle:
            json.dump(doc, handle, indent=1)
            handle.write("\n")
        json.load(open(tmp))
    except BaseException:
        for victim in (tmp, out):
            try:
                os.unlink(victim)
            except OSError:
                pass
        raise
    os.replace(tmp, out)
    csdata.write_unity_meta(out)
    return out, os.path.getsize(out), len(places)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--keep-desi", type=int, default=100000)
    ap.add_argument("--keep-sdss", type=int, default=50000)
    ap.add_argument("--sdss-zmax", type=float, default=0.10,
                    help="must equal DESI's z floor, or the two layers overlap")
    ap.add_argument("--only", choices=["desi", "sdss", "voids", "structures"], action="append")
    args = ap.parse_args()

    generated = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    os.makedirs(OUT_DIR, exist_ok=True)
    csdata.write_unity_meta(OUT_DIR, folder=True)

    # --only merges, it never truncates: build_spacecraft.py's --only replaced 24 craft with 1
    # and reported success.
    wanted = set(args.only or ["desi", "sdss", "voids", "structures"])
    total_bytes = 0

    if "sdss" in wanted:
        out, size, kept, total = build_sdss(generated, args.keep_sdss, args.sdss_zmax)
        total_bytes += size
        print("sdss.csrs        %6d rows of %d, %9d bytes  (13 - 420 Mpc, oriented)"
              % (kept, total, size))
    if "desi" in wanted:
        out, size, kept, total = build_desi(generated, args.keep_desi)
        total_bytes += size
        print("desi.cspt        %6d rows of %d, %9d bytes  (430 - 1600 Mpc)"
              % (kept, total, size))
    if "voids" in wanted:
        out, size, kept = build_voids(generated)
        total_bytes += size
        print("voids.cspt       %6d rows of 1163, %9d bytes  (interior only)" % (kept, size))
    if "structures" in wanted:
        out, size, kept = build_structures(generated)
        total_bytes += size
        print("structures.json  %6d places,      %9d bytes" % (kept, size))

    print("total %d bytes (%.2f MB) at H0 = %s, Omega_m = %s"
          % (total_bytes, total_bytes / 1e6, H0, OMEGA_M))


if __name__ == "__main__":
    main()
