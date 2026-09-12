#!/usr/bin/env python3
"""Build lv/localvolume.cslv: the Local Group and the Local Volume, out to 12.8 Mpc.

    python3 tools/cosmodata/build_localvolume.py

Out: unity/Assets/StreamingAssets/CosmoSynth/lv/localvolume.cslv   (CSLV, ~90 KB)

Standard library only. Reads nothing from the network: every input is already staged under
data-staging/ with its SOURCE.txt, LICENCE.txt and NOTES.md.

SOURCES, AND WHY THESE ONES
    lvdb/dwarf_all.csv            911 dwarfs, LVDB v1.1.1, CC0
    heroes-derived/heroes.json    31 giants, derived on triton from LVDB + 2MASS LGA, CC0
    2mass-xsc-irsa/xsc_rext20.csv 198,647 extended sources, US Government Works
    authored/overrides.json       2 kinematic hero overrides, ours, one citation each

    docs/DATA-LICENCE-AMENDMENT.md section 2 row 13 replaces design B's UNGC + VizieR XSC
    with the LGA and IRSA. The cost is real and is carried in the file: **every giant's
    distance is Inferred**, being the median heliocentric distance of the LVDB dwarfs that
    name it as their host. There is no commercially clean catalogue of measured
    redshift-independent distances to nearby giants; UNGC, HyperLEDA, NED-D and EDD are all
    blocked. See data-staging/heroes-derived/NOTES.md for the derivation and its accuracy.

THE DEFECT THIS FILE EXISTS TO NOT REPEAT
    The WebXR atlas drew 542 default ellipticities and 689 default position angles as if they
    were measured. LVDB does not ship defaults - it ships blanks, 536 and 682 of them - and
    the bug was downstream. Here a blank stays blank: `ell` and `pa` are NaN and the row's
    EllUnknown / PaUnknown bits are set, and `provenanceBits` in the meta block makes a
    reader that asks "was this measured?" get the right answer without reading this comment.

    The same trap in the other direction: 2MASS's ellipse fit latches onto bright knots in
    clumpy, low-surface-brightness galaxies and returns a confident wrong shape. NGC 3109,
    an edge-on Magellanic irregular, comes back at axis ratio 1.000 - face on. Shapes are
    accepted only when two independent fits agree (the `shapeReliable` guard); the six
    galaxies that fail it ship as shape-unknown, not as circles.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import os
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import csdata                                                    # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))


def staging_dir(repo):
    """data-staging/ is gitignored, so it exists only in the main checkout, not in a lane
    worktree. Resolve it through git's common dir rather than hard-coding a path."""
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
    raise SystemExit("data-staging/ not found; it lives in the main checkout, not in a worktree")


STAGING = staging_dir(REPO)
OUT_DIR = os.path.join(REPO, "unity", "Assets", "StreamingAssets", "CosmoSynth", "lv")
OUT = os.path.join(OUT_DIR, "localvolume.cslv")

NAN = float("nan")

# Flag bits. Kept in step with Core/Deep/GalaxySet.cs GalaxyFlags and with the
# provenanceBits block written into the meta below; change one, change all three.
ELL_UNKNOWN = 1 << 0
PA_UNKNOWN = 1 << 1
SIZE_UNKNOWN = 1 << 2
MV_DERIVED = 1 << 3
IS_HOME = 1 << 4
LANDMARK_LG = 1 << 5
SHAPE_FROM_XSC = 1 << 6
SHAPE_UNRELIABLE = 1 << 7
DISTANCE_FROM_SATELLITES = 1 << 8
INCLINATION_OVERRIDE = 1 << 9

SOURCE_LVDB, SOURCE_LGA, SOURCE_AUTHORED = 0, 1, 2
BAND_V, BAND_K = 0, 1

# The 13 Local Group landmarks the WebXR atlas labels, plus M31 and M33 (MWA-18b).
LANDMARKS = ["LMC", "SMC", "Sagittarius", "Fornax", "Sculptor", "Leo I", "NGC 6822",
             "M31", "M33", "IC 10", "WLM", "Draco", "Sextans"]


def norm(name):
    """'NGC 6822' / 'ngc_6822' / 'NGC6822' all collapse to the same key."""
    return "".join(ch for ch in (name or "").upper() if ch.isalnum())


def num(text):
    if text is None:
        return None
    text = text.strip()
    if not text:
        return None
    try:
        value = float(text)
    except ValueError:
        return None
    return None if value != value else value


def icrf(ra_deg, dec_deg, distance):
    """ICRF equatorial cartesian, in whatever unit `distance` is in."""
    ra, dec = math.radians(ra_deg), math.radians(dec_deg)
    return (distance * math.cos(dec) * math.cos(ra),
            distance * math.cos(dec) * math.sin(ra),
            distance * math.sin(dec))


def angsep_deg(ra1, dec1, ra2, dec2):
    """Exact angular separation (Vincenty), not the small-angle approximation."""
    p1, p2 = math.radians(dec1), math.radians(dec2)
    dl = math.radians(ra2 - ra1)
    num_ = math.hypot(math.cos(p2) * math.sin(dl),
                      math.cos(p1) * math.sin(p2) - math.sin(p1) * math.cos(p2) * math.cos(dl))
    den = math.sin(p1) * math.sin(p2) + math.cos(p1) * math.cos(p2) * math.cos(dl)
    return math.degrees(math.atan2(num_, den))


def fold_pa(pa):
    """LVDB position angles run -81 to 335 degrees. Major axes have no head or tail."""
    return pa % 180.0


def inclination_deg(q, q0=0.2):
    """Hubble 1926: cos^2 i = (q^2 - q0^2) / (1 - q0^2). Edge on where q <= q0."""
    if q is None or q != q or q <= q0:
        return 90.0
    c2 = (q * q - q0 * q0) / (1.0 - q0 * q0)
    return math.degrees(math.acos(math.sqrt(max(0.0, min(1.0, c2)))))


# ----------------------------------------------------------------------------------------
# Inputs
# ----------------------------------------------------------------------------------------

def load_xsc(path):
    """A declination-banded index over the 2MASS XSC subset, for a 30-arcsec crossmatch."""
    bands = {}
    with open(path, newline="") as handle:
        for row in csv.DictReader(handle):
            ra, dec = num(row["ra"]), num(row["dec"])
            if ra is None or dec is None:
                continue
            bands.setdefault(int(math.floor(dec)), []).append((
                ra, dec, num(row["sup_ba"]), num(row["sup_phi"]), num(row["k_ba"]),
                num(row["k_phi"]), num(row["r_ext"]), num(row["r_k20fe"])))
    return bands


def xsc_match(bands, ra, dec, radius_arcsec=30.0):
    best, best_sep = None, radius_arcsec / 3600.0
    for band in (int(math.floor(dec)) - 1, int(math.floor(dec)), int(math.floor(dec)) + 1):
        for entry in bands.get(band, ()):
            sep = angsep_deg(ra, dec, entry[0], entry[1])
            if sep < best_sep:
                best, best_sep = entry, sep
    return best, best_sep * 3600.0 if best else None


def xsc_shape_reliable(sup_ba, sup_phi, k_ba, k_phi, r_k20fe):
    """The 2MASS ellipse-fit guard, from data-staging/2mass-lga-irsa/NOTES.md.

    2MASS sees the old stellar population; on a clumpy or low-surface-brightness object the
    fit latches onto knots. Two independent fits agreeing is the cheap test that separates a
    measured shape from a confident wrong one.
    """
    if sup_ba is None or k_ba is None or r_k20fe is None:
        return False
    if abs(sup_ba - k_ba) > 0.15 or sup_ba >= 0.995 or r_k20fe < 30.0:
        return False
    if sup_ba <= 0.9:
        if sup_phi is None or k_phi is None:
            return False
        d = abs(fold_pa(sup_phi) - fold_pa(k_phi)) % 180.0
        if min(d, 180.0 - d) > 20.0:
            return False
    return True


# ----------------------------------------------------------------------------------------
# Build
# ----------------------------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--no-xsc", action="store_true",
                    help="skip the 2MASS XSC shape fill (18 MB read, ~10 s)")
    args = ap.parse_args()

    lvdb_path = os.path.join(STAGING, "lvdb", "dwarf_all.csv")
    heroes_path = os.path.join(STAGING, "heroes-derived", "heroes.json")
    xsc_path = os.path.join(STAGING, "2mass-xsc-irsa", "xsc_rext20.csv")
    overrides_path = os.path.join(HERE, "authored", "overrides.json")

    dwarfs = list(csv.DictReader(open(lvdb_path, newline="")))
    heroes = json.load(open(heroes_path))
    overrides = json.load(open(overrides_path))["overrides"]
    for entry in overrides:
        if not entry.get("reference"):
            raise SystemExit("overrides.json: every entry needs a reference")
    override_by_name = {norm(o["match"]): o for o in overrides}

    assertions = []

    def check(name, ok, expected, got, note=""):
        assertions.append({"name": name, "expected": expected, "got": got,
                           "ok": bool(ok), "note": note})
        if not ok:
            raise SystemExit(f"ASSERTION FAILED {name}: expected {expected}, got {got} {note}")

    check("lvdb_rows", len(dwarfs) == 911, 911, len(dwarfs), "LVDB v1.1.1 dwarf_all.csv")
    check("heroes_rows", len(heroes["galaxies"]) == 31, 31, len(heroes["galaxies"]))

    rows = []          # dicts, assembled then packed
    names, refs = [], []
    ref_index = {}

    def ref_id(text):
        text = (text or "").strip() or "none"
        if text not in ref_index:
            ref_index[text] = len(refs)
            refs.append(text)
        return ref_index[text]

    hosts, host_index = [], {}

    def host_id(text):
        text = (text or "").strip() or "none"
        if text not in host_index:
            if len(hosts) >= 255:
                raise SystemExit("more than 255 distinct hosts; widen the host column")
            host_index[text] = len(hosts)
            hosts.append(text)
        return host_index[text]

    host_id("none")

    # --- 1. every LVDB dwarf ------------------------------------------------------------
    worst_dm = 0.0
    n_mv_derived = 0
    for row in dwarfs:
        ra, dec = num(row["ra"]), num(row["dec"])
        distance = num(row["distance"])
        dm = num(row["distance_modulus"])
        if ra is None or dec is None or distance is None:
            raise SystemExit(f"LVDB row {row['key']} has no position")
        if dm is not None:
            predicted = 10 ** (dm / 5.0 + 1.0) / 1000.0
            worst_dm = max(worst_dm, abs(predicted - distance) / distance)

        flags = 0
        mv = num(row["M_V"])
        if mv is None:
            apparent = num(row["apparent_magnitude_v"])
            if apparent is not None and dm is not None:
                mv, flags = apparent - dm, flags | MV_DERIVED
                n_mv_derived += 1
            else:
                mv = NAN

        ell, pa = num(row["ellipticity"]), num(row["position_angle"])
        if ell is None:
            ell, flags = NAN, flags | ELL_UNKNOWN
        if pa is None:
            pa, flags = NAN, flags | PA_UNKNOWN
        else:
            pa = fold_pa(pa)

        size_pc = num(row["rhalf_physical"])
        if size_pc is None:
            size, flags = NAN, flags | SIZE_UNKNOWN
        else:
            size = size_pc / 1000.0

        err = 0.0
        lo, hi = num(row["distance_em"]), num(row["distance_ep"])
        if lo is not None and hi is not None:
            err = 0.5 * (abs(lo) + abs(hi))

        rows.append(dict(
            key=row["key"], name=row["name"].strip() or row["key"],
            ra=ra, dec=dec, distance=distance, dist_err=err,
            absmag=mv, band=BAND_V, size=size, ell=ell, pa=pa, incl=NAN,
            ttype=99, host=host_id(row.get("host", "")), source=SOURCE_LVDB,
            flags=flags, ref=ref_id(row.get("ref_distance", "")),
            rhalf_arcmin=num(row["rhalf"])))

    check("lvdb_dm_vs_distance", worst_dm < 0.005, "< 0.005", round(worst_dm, 6),
          "distance = 10^(DM/5+1)/1000 kpc")

    lvdb_by_name = {norm(r["name"]): r for r in rows}
    lvdb_by_key = {norm(r["key"]): r for r in rows}

    # --- 2. the 2MASS XSC shape fill for dwarfs -----------------------------------------
    n_counterparts = n_pa_filled = n_ell_filled = 0
    n_rejected_fit = n_rejected_size = 0
    if not args.no_xsc:
        bands = load_xsc(xsc_path)
        total = sum(len(v) for v in bands.values())
        check("xsc_rows", total == 198647, 198647, total)
        for row in rows:
            match, _sep = xsc_match(bands, row["ra"], row["dec"])
            if match is None:
                continue
            n_counterparts += 1
            ra_x, dec_x, sup_ba, sup_phi, k_ba, k_phi, r_ext, r_k20fe = match
            if not xsc_shape_reliable(sup_ba, sup_phi, k_ba, k_phi, r_k20fe):
                n_rejected_fit += 1
                continue
            # The angular sizes must agree too, or the nearest source inside 30 arcsec is a
            # different object on the same line of sight. LVDB rhalf is arcmin, XSC r_ext is
            # arcsec: compare like with like.
            rhalf_arcmin = row["rhalf_arcmin"]
            if rhalf_arcmin is None or r_ext is None:
                continue
            ratio = (rhalf_arcmin * 60.0) / r_ext
            if not (1.0 / 3.0 <= ratio <= 3.0):
                n_rejected_size += 1
                continue
            if row["flags"] & PA_UNKNOWN and sup_phi is not None:
                row["pa"] = fold_pa(sup_phi)
                row["flags"] = (row["flags"] & ~PA_UNKNOWN) | SHAPE_FROM_XSC
                n_pa_filled += 1
            if row["flags"] & ELL_UNKNOWN and sup_ba is not None:
                row["ell"] = 1.0 - sup_ba
                row["flags"] = (row["flags"] & ~ELL_UNKNOWN) | SHAPE_FROM_XSC
                n_ell_filled += 1
        check("xsc_counterparts", n_counterparts == 92, 92, n_counterparts,
              "30-arcsec nearest neighbour, data-staging/lvdb/NOTES.md")

    # --- 3. the giants ------------------------------------------------------------------
    n_no_distance = 0
    duplicates = []
    for hero in heroes["galaxies"]:
        key = norm(hero["name"])
        if key in lvdb_by_name or key in lvdb_by_key:
            duplicates.append(hero["name"])   # already an LVDB row, with a measured distance
            continue
        distance = hero.get("distance_kpc")
        if distance is None:
            n_no_distance += 1            # NGC 4945, Maffei 1, Maffei 2, Circinus
            continue

        flags = DISTANCE_FROM_SATELLITES
        reliable = bool(hero.get("shapeReliable"))
        ell = hero.get("ellipticity")
        pa = hero.get("pa_deg_EofN")
        if reliable and ell is not None and pa is not None:
            pa = fold_pa(pa)
        else:
            ell, pa = NAN, NAN
            flags |= ELL_UNKNOWN | PA_UNKNOWN | SHAPE_UNRELIABLE

        size = hero.get("rHalfLight_kpc")
        if size is None:
            size, flags = NAN, flags | SIZE_UNKNOWN

        absmag = hero.get("absMag_K")
        band = BAND_K
        if absmag is None:
            absmag, band = hero.get("absMag_V", NAN), BAND_V

        mad = hero.get("distance_kpc_mad")
        rows.append(dict(
            key=hero["name"], name=hero.get("commonName") or hero["name"],
            ra=hero["ra_deg"], dec=hero["dec_deg"], distance=distance,
            dist_err=(mad if mad is not None else 0.0),
            absmag=(absmag if absmag is not None else NAN), band=band,
            size=size, ell=ell, pa=pa, incl=NAN, ttype=99,
            host=host_id("none"), source=SOURCE_LGA, flags=flags,
            ref=ref_id("Inferred distance: median of %s LVDB v1.1.1 satellites, MAD %s kpc; "
                       "shape and photometry 2MASS LGA v2 (IRSA)"
                       % (hero.get("distance_n_satellites"), mad)),
            rhalf_arcmin=None, hero_name=hero["name"]))

    check("heroes_without_distance", n_no_distance == 4, 4, n_no_distance,
          "NGC 4945, Maffei 1, Maffei 2, Circinus have no LVDB host group")

    # --- 4. the Milky Way, as the galactic centre, never drawn as a point ---------------
    rows.append(dict(
        key="milky-way", name="Milky Way",
        ra=266.41684, dec=-29.00781, distance=8.15, dist_err=0.0,
        absmag=NAN, band=BAND_V, size=NAN, ell=NAN, pa=NAN, incl=NAN, ttype=99,
        host=host_id("none"), source=SOURCE_AUTHORED,
        flags=IS_HOME | ELL_UNKNOWN | PA_UNKNOWN | SIZE_UNKNOWN,
        ref=ref_id("Sgr A* position Reid & Brunthaler 2004; R0 = 8.15 kpc Reid et al. 2019 "
                   "ApJ 885, 131. The row is the galactic centre, not the Sun."),
        rhalf_arcmin=None))

    # --- 5. authored overrides win ------------------------------------------------------
    n_overrides = 0
    for row in rows:
        entry = override_by_name.get(norm(row.get("hero_name", row["key"]))) \
            or override_by_name.get(norm(row["name"]))
        if not entry:
            continue
        row["incl"] = float(entry["inclinationDeg"])
        row["pa"] = fold_pa(float(entry["positionAngleDeg"]))
        row["flags"] = (row["flags"] & ~PA_UNKNOWN) | INCLINATION_OVERRIDE
        row["ref"] = ref_id(entry["reference"])
        n_overrides += 1
    check("overrides_applied", n_overrides == len(overrides), len(overrides), n_overrides)

    # --- 6. Local Group landmark flag ---------------------------------------------------
    wanted = {norm(n) for n in LANDMARKS}
    found = set()
    for row in rows:
        for candidate in (norm(row["name"]), norm(row["key"])):
            if candidate in wanted:
                row["flags"] |= LANDMARK_LG
                found.add(candidate)
                break
    missing = sorted(wanted - found)
    check("lg_landmarks", not missing, sorted(wanted), sorted(found),
          "missing: %s" % missing)

    # ------------------------------------------------------------------------------------
    # Range assertions on values, never on headers.
    # ------------------------------------------------------------------------------------
    for row in rows:
        if not (0.0 <= row["ra"] < 360.0 and -90.0 <= row["dec"] <= 90.0):
            raise SystemExit(f"{row['key']}: position out of range")
        if not (5.0 <= row["distance"] <= 30000.0):
            raise SystemExit(f"{row['key']}: distance {row['distance']} kpc out of 5 - 30000 kpc")
        e = row["ell"]
        if e == e and not (0.0 <= e < 1.0):
            raise SystemExit(f"{row['key']}: ellipticity {e} out of [0, 1)")
        p = row["pa"]
        if p == p and not (0.0 <= p < 180.0):
            raise SystemExit(f"{row['key']}: pa {p} out of [0, 180)")
        m = row["absmag"]
        if m == m and not (-26.0 <= m <= 2.0):
            raise SystemExit(f"{row['key']}: absMag {m} out of [-26, 2]")
        s = row["size"]
        if s == s and not (0.000001 <= s <= 60.0):
            raise SystemExit(f"{row['key']}: size {s} kpc out of [1 pc, 60 kpc]")

    rows.sort(key=lambda r: r["distance"])

    within_1mpc = sum(1 for r in rows if r["distance"] <= 1000.0)
    external_1mpc = sum(1 for r in rows if r["distance"] <= 1000.0 and not r["flags"] & IS_HOME)
    measured_ell = sum(1 for r in rows if not r["flags"] & ELL_UNKNOWN)
    measured_pa = sum(1 for r in rows if not r["flags"] & PA_UNKNOWN)

    m31 = next(r for r in rows if norm(r["key"]) == "M31")
    check("m31_distance_vs_literature", abs(m31["distance"] - 770.0) <= m31["dist_err"],
          "|d - 770| <= MAD", round(abs(m31["distance"] - 770.0), 1),
          "MAD %.1f kpc over %s satellites" % (m31["dist_err"], 39))
    check("m31_inclination_override", abs(m31["incl"] - 77.0) < 1e-9, 77.0, m31["incl"])
    check("m31_pa_override", abs(m31["pa"] - 38.0) < 1e-9, 38.0, m31["pa"])
    check("m31_photometric_inclination", abs(inclination_deg(0.473) - 64.06) < 0.01,
          64.06, round(inclination_deg(0.473), 2),
          "what the 2MASS axis ratio alone would have given")

    lmc = next(r for r in rows if norm(r["key"]) == "LMC")
    check("lmc_pa_folded", abs(lmc["pa"] - 47.24) < 1e-6, 47.24, lmc["pa"],
          "LVDB ships 227.24; a major axis has no head or tail")
    check("lmc_distance", abs(lmc["distance"] - 49.59) < 0.01, 49.59, lmc["distance"])

    smc = next(r for r in rows if norm(r["key"]) == "SMC")
    check("smc_shape_unknown", (smc["flags"] & (ELL_UNKNOWN | PA_UNKNOWN))
          == (ELL_UNKNOWN | PA_UNKNOWN), "both unknown",
          "ell %s pa %s" % (smc["ell"], smc["pa"]),
          "LVDB leaves both blank; the atlas drew a default")

    antlia = next(r for r in rows if norm(r["key"]) == "ANTLIA2")
    check("antlia2", abs(antlia["distance"] - 124.17) < 0.01 and abs(antlia["ell"] - 0.6) < 1e-6
          and abs(antlia["pa"] - 156.0) < 1e-6, "124.17 kpc, ell 0.6, pa 156",
          "%.2f kpc, ell %.3f, pa %.1f" % (antlia["distance"], antlia["ell"], antlia["pa"]))

    check("no_blank_became_a_number",
          all((r["ell"] != r["ell"]) for r in rows if r["flags"] & ELL_UNKNOWN)
          and all((r["pa"] != r["pa"]) for r in rows if r["flags"] & PA_UNKNOWN),
          "every flagged blank is NaN", "yes")

    # ------------------------------------------------------------------------------------
    # Pack
    # ------------------------------------------------------------------------------------
    pos = []
    for row in rows:
        pos.extend(icrf(row["ra"], row["dec"], row["distance"]))
        names.append(row["name"])

    columns = [
        csdata.Column("pos", "f32", 3, csdata.pack("f32", pos), unit="kpc"),
        csdata.Column("distErr", "f32", 1,
                      csdata.pack("f32", [r["dist_err"] for r in rows]), unit="kpc"),
        csdata.Column("absMag", "f32", 1,
                      csdata.pack("f32", [r["absmag"] for r in rows]), unit="mag"),
        csdata.Column("band", "u8", 1, csdata.pack("u8", [r["band"] for r in rows])),
        csdata.Column("size", "f32", 1,
                      csdata.pack("f32", [r["size"] for r in rows]), unit="kpc"),
        csdata.Column("ell", "f32", 1, csdata.pack("f32", [r["ell"] for r in rows])),
        csdata.Column("pa", "f32", 1,
                      csdata.pack("f32", [r["pa"] for r in rows]), unit="deg"),
        csdata.Column("incl", "f32", 1,
                      csdata.pack("f32", [r["incl"] for r in rows]), unit="deg"),
        csdata.Column("ttype", "i8", 1, csdata.pack("i8", [r["ttype"] for r in rows])),
        csdata.Column("host", "u8", 1, csdata.pack("u8", [r["host"] for r in rows])),
        csdata.Column("source", "u8", 1, csdata.pack("u8", [r["source"] for r in rows])),
        csdata.Column("refIndex", "u16", 1, csdata.pack("u16", [r["ref"] for r in rows])),
        csdata.Column("flags", "u32", 1, csdata.pack("u32", [r["flags"] for r in rows])),
    ]

    meta = {
        "kind": "localvolume", "schema": 1,
        "generator": "tools/cosmodata/build_localvolume.py",
        "generatorCommit": csdata.generator_commit(REPO),
        "generatedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "licenceProfile": "commercial",
        "frame": "ICRF", "epoch": "J2000.0",
        "sizeKind": "half-light radius",
        "sources": [
            {"id": "lvdb", "name": "Local Volume Database v1.1.1 dwarf_all.csv",
             "url": "https://raw.githubusercontent.com/apace7/local_volume_database/v1.1.1/data/dwarf_all.csv",
             "sha256": csdata.sha256(lvdb_path), "rows": len(dwarfs),
             "licence": "CC0 1.0", "licenceTag": "cc0",
             "attribution": "Local Volume Database (Pace 2024), v1.1.1, CC0 1.0"},
            {"id": "heroes", "name": "CosmoSynth hero galaxies, derived from LVDB + 2MASS LGA v2",
             "url": "data-staging/heroes-derived/heroes.json",
             "sha256": csdata.sha256(heroes_path), "rows": len(heroes["galaxies"]),
             "licence": "CC0 1.0", "licenceTag": "cc0",
             "attribution": "2MASS Large Galaxy Atlas (Jarrett et al. 2003, AJ 125, 525) via "
                            "NASA/IPAC IRSA; distances derived from LVDB v1.1.1"},
            {"id": "xsc", "name": "2MASS Extended Source Catalog subset (r_ext > 20 arcsec), IRSA",
             "url": "https://irsa.ipac.caltech.edu/TAP/sync",
             "sha256": (csdata.sha256(xsc_path) if not args.no_xsc else "not read"),
             "licence": "US Government Works", "licenceTag": "pd",
             "attribution": "Two Micron All Sky Survey, a joint project of the University of "
                            "Massachusetts and IPAC/Caltech, funded by NASA and the NSF"},
            {"id": "overrides", "name": "authored hero overrides",
             "url": "tools/cosmodata/authored/overrides.json",
             "licence": "CC0 1.0", "licenceTag": "own",
             "attribution": "CosmoSynth, one citation per value"},
        ],
        "names": names,
        "refs": refs,
        "hostCodes": hosts,
        "bandCodes": ["V", "K"],
        "sourceCodes": ["lvdb", "lga-hero", "authored"],
        "provenance": {
            "pos": {"class": "Measured", "source": "lvdb",
                    "note": "direction Measured; distance Measured for LVDB dwarfs "
                            "(distance modulus, ref per row), Inferred for giants"},
            "absMag": {"class": "Measured", "source": "lvdb"},
            "size": {"class": "Measured", "source": "lvdb",
                     "note": "half-light radius; Measured angular size times a distance"},
            "ell": {"class": "Measured", "source": "lvdb"},
            "pa": {"class": "Measured", "source": "lvdb"},
            "incl": {"class": "Inferred", "source": "overrides",
                     "note": "NaN unless an authored kinematic override exists; Core derives "
                             "the rest from the axis ratio with q0 = 0.2"},
            "ttype": {"class": "Inferred", "source": "heroes",
                      "note": "99 everywhere: the 2MASS morphology is a Hubble type string, "
                              "and mapping it to a de Vaucouleurs number was not done"},
        },
        "provenanceBits": [
            {"bit": 0, "field": "ell", "class": "Inferred",
             "note": "not measured; the source leaves it blank. Draw the galaxy round and faint"},
            {"bit": 1, "field": "pa", "class": "Inferred",
             "note": "not measured; the source leaves it blank. Do not orient this galaxy"},
            {"bit": 2, "field": "size", "class": "Inferred", "note": "no half-light radius published"},
            {"bit": 3, "field": "absMag", "class": "Inferred",
             "note": "M_V derived as m - DM, not published directly"},
            {"bit": 4, "field": "pos", "class": "Inferred",
             "note": "the Milky Way row: the galactic centre, never drawn as a point"},
            {"bit": 5, "field": "name", "class": "Measured", "note": "a Local Group landmark"},
            {"bit": 6, "field": "ell", "class": "Measured",
             "note": "shape filled from the 2MASS XSC counterpart within 30 arcsec, not from LVDB"},
            {"bit": 7, "field": "ell", "class": "Inferred",
             "note": "2MASS ellipse fit rejected: the two band fits disagree, so the shape is "
                     "not trustworthy. NGC 3109 is the worked example"},
            {"bit": 8, "field": "pos", "class": "Inferred",
             "note": "distance is the median of the LVDB satellites that name this galaxy as "
                     "host, not a measurement of the galaxy itself"},
            {"bit": 9, "field": "incl", "class": "Inferred",
             "note": "authored kinematic override; see refs[] for the citation and its chain"},
        ],
        "uncertainty": {"pos": "distErr"},
        "counts": {
            "rows": len(rows), "lvdbDwarfs": len(dwarfs),
            "giants": sum(1 for r in rows if r["source"] == SOURCE_LGA),
            "heroesWithoutDistance": n_no_distance,
            "heroesAlreadyInLvdb": len(duplicates),
            "ellUnknown": len(rows) - measured_ell, "paUnknown": len(rows) - measured_pa,
            "ellMeasured": measured_ell, "paMeasured": measured_pa,
            "mvDerived": n_mv_derived,
            "xscCounterparts": n_counterparts, "xscPaFilled": n_pa_filled,
            "xscEllFilled": n_ell_filled, "xscRejectedByFitGuard": n_rejected_fit,
            "xscRejectedBySizeGuard": n_rejected_size,
            "shapeUnreliable": sum(1 for r in rows if r["flags"] & SHAPE_UNRELIABLE),
            "within1Mpc": within_1mpc, "within1MpcExternal": external_1mpc,
            "landmarksLg": sum(1 for r in rows if r["flags"] & LANDMARK_LG),
        },
        "assertions": assertions,
    }

    os.makedirs(OUT_DIR, exist_ok=True)
    size = csdata.write(OUT, "CSLV", meta, columns)
    csdata.write_unity_meta(OUT)
    csdata.write_unity_meta(OUT_DIR, folder=True)

    print("localvolume.cslv  %d rows, %d bytes" % (len(rows), size))
    print("  %d LVDB dwarfs + %d giants + 1 Milky Way" % (
        len(dwarfs), sum(1 for r in rows if r["source"] == SOURCE_LGA)))
    print("  %d heroes already in LVDB with a measured distance: %s"
          % (len(duplicates), ", ".join(duplicates)))
    print("  shapes: %d ellipticities and %d position angles measured; "
          "%d and %d unknown and flagged" % (measured_ell, measured_pa,
                                             len(rows) - measured_ell, len(rows) - measured_pa))
    print("  2MASS XSC: %d counterparts, %d PAs and %d ellipticities filled; "
          "%d rejected by the two-band fit guard, %d by the size guard"
          % (n_counterparts, n_pa_filled, n_ell_filled, n_rejected_fit, n_rejected_size))
    print("  within 1 Mpc: %d (%d external to the Milky Way)" % (within_1mpc, external_1mpc))
    print("  %d assertions, all green" % len(assertions))


if __name__ == "__main__":
    main()
