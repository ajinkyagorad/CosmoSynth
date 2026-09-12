#!/usr/bin/env python3
"""Build the shipped star layer: a TRUE 3-D catalogue, its names, and the 88 figures.

    python3 tools/cosmodata/build_starfield.py                     # development default
    COSMODATA_LICENCE=commercial python3 tools/cosmodata/build_starfield.py

Out: unity/Assets/StreamingAssets/CosmoSynth/sky/stars.csstars     (container CSS2)
     unity/Assets/StreamingAssets/CosmoSynth/sky/stars_named.json
     unity/Assets/StreamingAssets/CosmoSynth/sky/constellations.json

Standard library only.

TWO SOURCE PATHS, ONE IMPORTER (design B section 1.5; docs/licence/stars.md)
    development (`sideload-nc`, `public-nc`)
        HYG v4.4 photometry over AT-HYG v4.0 astrometry: ~119,600 stars with measured
        parallaxes, which is the only thing on offer that makes the sky genuinely 3-D.
        `--from-css1` rebuilds the container from the CSS1 file already in the tree when the
        upstream CSVs are not available, which is a format migration and not a re-derivation:
        it cannot invent the columns CSS1 never carried, and it says so in `counts`.

    commercial
        Yale BSC5p (NASA HEASARC) + IAU-CSN names + Stellarium figures, all staged, all
        cleared for commercial distribution. HYG and AT-HYG are refused: astronexus grants
        CC BY-SA 4.0 but cannot grant commercial rights in the ESA Hipparcos/Gaia astrometry
        underneath, which is CC BY-NC 3.0 IGO. What this path LOSES is the point of the
        report in docs/reports/data-stars.md: 9,096 stars instead of 119,613, ground-based
        parallaxes for a third of them instead of Gaia for nearly all, and no HIP key.

WHAT IS MEASURED AND WHAT IS NOT
    Position, magnitude, colour index and proper motion are measured. Temperature is
    inferred from B-V (Ballesteros 2012), or more weakly from the spectral class alone.
    Extinction A_V is shipped as a column of zeros flagged ExtinctionUnknown: the dust layer
    that fills it does not exist yet, and a zero that claims to be a measurement is exactly
    the failure this project keeps meeting. The column exists now so that when dust arrives
    the star is de-reddened ONCE, in the data, and the volume never dims it a second time.
"""

from __future__ import annotations

import argparse
import csv
import gzip
import io
import json
import math
import os
import re
import struct
import subprocess
import sys
from datetime import datetime, timezone

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import csdata                                          # noqa: E402
import licence                                         # noqa: E402

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
STREAMING = os.path.join(REPO_ROOT, "unity", "Assets", "StreamingAssets", "CosmoSynth")
DEFAULT_OUT = os.path.join(STREAMING, "sky")
def _staging():
    """data-staging/ is gitignored, so it exists only in the main checkout, not in a lane
    worktree. Look where it can be, and say where it looked when it is not there."""
    for candidate in (os.environ.get("COSMODATA_STAGING"),
                      os.path.join(REPO_ROOT, "data-staging"),
                      os.path.join(os.path.dirname(REPO_ROOT), "CosmoSynth", "data-staging"),
                      os.path.expanduser("~/projects/CosmoSynth/data-staging")):
        if candidate and os.path.isdir(candidate):
            return candidate
    return os.path.join(REPO_ROOT, "data-staging")


STAGING = _staging()
CACHE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cache")

MAGIC = "CSS2"

#: HYG writes this distance when the parallax is unusable. A sentinel, not a measurement.
UNKNOWN_DISTANCE_PC = 100000.0
#: 1 pc/yr expressed in km/s, for AT-HYG's velocity columns.
PC_PER_YEAR_IN_KM_S = 977792.2
#: Tangential speed in km/s from proper motion in arcsec/yr and distance in parsecs.
TANGENTIAL_K = 4.74047

#: BSC5's parallaxes are pre-Hipparcos ground-based values, and below this they are not
#: distances. MEASURED 2026-09-12 against 2,670 of the same stars' modern parallaxes: the
#: median relative error is 2.1% above 0.200", 7.2% in 0.100-0.200", 10.5% in 0.060-0.100",
#: then 22.1% in 0.040-0.060" and 32-62% below that. Canopus's 0.028" puts it at 35.7 pc
#: against a true 94.8 pc, which is the failure this cut exists to refuse. 0.060" keeps 204
#: of 9,110 rows; the rest get a measured direction and an honest DistanceUnknown, because
#: "we do not know" is a true statement and 35.7 pc is not.
BSC5_MINIMUM_PARALLAX = 0.060

F_DISTANCE_UNKNOWN          = 1 << 0
F_TEMPERATURE_FROM_SPECTRUM = 1 << 1
F_HAS_PROPER_NAME           = 1 << 2
F_PHOTOMETRY_FROM_TYCHO     = 1 << 3
F_RADIAL_VELOCITY_MISSING   = 1 << 4
F_FROM_NEARBY_CENSUS        = 1 << 5
F_NO_VISUAL_MAGNITUDE       = 1 << 6
F_VELOCITY_SUSPECT          = 1 << 7
# bits 8-10: distance-source code, 0 unknown 1 Gaia DR3 2 Gaia DR2 3 Hipparcos 4 Gliese
#            5 other 6 census 7 ground-based trigonometric (BSC5)
F_DISTANCE_SOURCE_SHIFT     = 8
F_EXTINCTION_UNKNOWN        = 1 << 11
F_VELOCITY_UNKNOWN          = 1 << 12
F_FROM_CSS1_MIGRATION       = 1 << 13

DSRC_UNKNOWN, DSRC_GAIA_DR3, DSRC_GAIA_DR2, DSRC_HIPPARCOS = 0, 1, 2, 3
DSRC_GLIESE, DSRC_OTHER, DSRC_CENSUS, DSRC_GROUND_TRIG = 4, 5, 6, 7

SPECTRAL_CLASSES = "OBAFGKMLTYCSW"
SPECTRAL_TEMPERATURES = {
    "O": 40000.0, "B": 20000.0, "A": 8750.0, "F": 6750.0, "G": 5600.0,
    "K": 4400.0, "M": 3200.0, "L": 1900.0, "T": 1200.0, "Y": 600.0,
    "C": 3000.0, "S": 3000.0, "W": 50000.0,
}
DEFAULT_TEMPERATURE = 5600.0
SPEC_UNKNOWN = 255

#: B-V is quantised into a byte. The range covers every real star (a carbon star reaches
#: about +5 but nothing in these catalogues does); 0.0157 mag of resolution moves Ballesteros'
#: temperature by well under 1%, and the column is a cross-check on `teff`, not its source.
BV_LO, BV_HI = -0.5, 3.5
#: A_V in units of 1/4096 mag: 0 to 16 magnitudes of extinction at 0.00024 mag.
AV_SCALE = 1.0 / 4096.0


# --------------------------------------------------------------------------------------
# Physics
# --------------------------------------------------------------------------------------

def temperature_from_colour_index(bv: float) -> float:
    """Ballesteros, F. J. (2012), "New insights into black bodies", EPL 97 34008."""
    a = 0.92 * bv + 1.7
    b = 0.92 * bv + 0.62
    if abs(a) < 1e-6 or abs(b) < 1e-6:
        return DEFAULT_TEMPERATURE
    return 4600.0 * (1.0 / a + 1.0 / b)


def spectral_code(spect: str) -> int:
    """Class and subclass packed into a byte: class*10 + subclass, 255 when unreadable."""
    s = (spect or "").strip().upper()
    for i, ch in enumerate(s):
        if ch in SPECTRAL_CLASSES:
            cls = SPECTRAL_CLASSES.index(ch)
            sub = 0
            if i + 1 < len(s) and s[i + 1].isdigit():
                sub = int(s[i + 1])
            return cls * 10 + sub
    return SPEC_UNKNOWN


def temperature_from_spectrum(spect: str):
    for ch in (spect or "").strip().upper():
        if ch in SPECTRAL_TEMPERATURES:
            return SPECTRAL_TEMPERATURES[ch]
    return None


def cartesian(ra_deg: float, dec_deg: float, distance_pc: float):
    ra, dec = math.radians(ra_deg), math.radians(dec_deg)
    c = math.cos(dec)
    return (distance_pc * c * math.cos(ra),
            distance_pc * c * math.sin(ra),
            distance_pc * math.sin(dec))


def space_velocity(ra_deg, dec_deg, pmra_arcsec_cosdec, pmdec_arcsec, rv_km_s, distance_pc):
    """ICRF cartesian space velocity, km/s, from proper motion + radial velocity.

    `pmra_arcsec_cosdec` is mu_alpha * cos(delta): the great-circle rate, not the coordinate
    rate. Mixing the two is invisible everywhere except near the poles, which is why the
    importer asserts it on Sigma Octantis rather than trusting the column description.
    """
    ra, dec = math.radians(ra_deg), math.radians(dec_deg)
    sa, ca, sd, cd = math.sin(ra), math.cos(ra), math.sin(dec), math.cos(dec)
    v_ra = TANGENTIAL_K * pmra_arcsec_cosdec * distance_pc
    v_dec = TANGENTIAL_K * pmdec_arcsec * distance_pc
    # unit vectors: r (radial), e_ra (east), e_dec (north)
    return (rv_km_s * cd * ca - v_ra * sa - v_dec * sd * ca,
            rv_km_s * cd * sa + v_ra * ca - v_dec * sd * sa,
            rv_km_s * sd + v_dec * cd)


# --------------------------------------------------------------------------------------
# Records
# --------------------------------------------------------------------------------------

class Star:
    __slots__ = ("x", "y", "z", "vx", "vy", "vz", "vmag", "teff", "bv", "av",
                 "spec", "hip", "hd", "flags", "name", "designation", "constellation")

    def __init__(self):
        self.x = self.y = self.z = 0.0
        self.vx = self.vy = self.vz = 0.0
        self.vmag = float("nan")
        self.teff = DEFAULT_TEMPERATURE
        self.bv = None
        self.av = 0.0
        self.spec = SPEC_UNKNOWN
        self.hip = 0
        self.hd = 0
        self.flags = 0
        self.name = None
        self.designation = None
        self.constellation = None

    @property
    def speed(self):
        return math.sqrt(self.vx * self.vx + self.vy * self.vy + self.vz * self.vz)


# --------------------------------------------------------------------------------------
# Source: the CSS1 file already in the tree (development, offline)
# --------------------------------------------------------------------------------------

CSS1_HEADER = "<4sIII"
CSS1_RECORD = "<3f f f I"


def load_from_css1(path: str, names_path: str):
    """Migrate the shipped CSS1 catalogue into CSS2 records.

    This is a container migration, not a rebuild: CSS1 carried position, V, temperature and
    three flag bits and nothing else, so velocity, B-V, spectral class, HIP and HD come out
    unknown and flagged. Everything CSS1 did carry is byte-exact, including the sort order,
    which is why the existing stars_named.json indices survive unchanged.
    """
    with open(path, "rb") as handle:
        blob = handle.read()
    magic, version, count, flags = struct.unpack(CSS1_HEADER, blob[:16])
    if magic != b"CSS1":
        raise SystemExit(f"{path}: not a CSS1 file (magic {magic!r})")
    record_bytes = struct.calcsize(CSS1_RECORD)
    if len(blob) != 16 + count * record_bytes:
        raise SystemExit(f"{path}: declares {count} stars but is {len(blob)} bytes")

    stars = []
    for i in range(count):
        at = 16 + i * record_bytes
        x, y, z, vmag, teff, f = struct.unpack_from(CSS1_RECORD, blob, at)
        s = Star()
        s.x, s.y, s.z, s.vmag, s.teff = x, y, z, vmag, teff
        s.flags = (f & 0b111) | F_EXTINCTION_UNKNOWN | F_VELOCITY_UNKNOWN | F_FROM_CSS1_MIGRATION
        if not (f & F_DISTANCE_UNKNOWN):
            s.flags |= DSRC_OTHER << F_DISTANCE_SOURCE_SHIFT
        stars.append(s)

    if os.path.exists(names_path):
        named = json.load(open(names_path, encoding="utf-8"))
        for entry in named.get("stars", []):
            i = entry.get("index", -1)
            if 0 <= i < len(stars):
                stars[i].name = entry.get("name")
                stars[i].designation = entry.get("designation")
                stars[i].constellation = entry.get("constellation")
                stars[i].flags |= F_HAS_PROPER_NAME
    return stars, count


# --------------------------------------------------------------------------------------
# Source: HYG v4.4 + AT-HYG v4.0 (development, needs the CSVs)
# --------------------------------------------------------------------------------------

def _open_csv(path: str):
    opener = gzip.open if path.endswith(".gz") else open
    return csv.DictReader(io.TextIOWrapper(opener(path, "rb"), encoding="utf-8"))


def load_from_hyg(hyg_path: str, athyg_path=None):
    """The full design-B build: AT-HYG astrometry joined to HYG v4.4 photometry on the HYG id."""
    photometry = {}
    for r in _open_csv(hyg_path):
        try:
            photometry[int(r["id"])] = r
        except (KeyError, ValueError):
            continue
    if not photometry:
        raise SystemExit(f"{hyg_path}: no rows with an integer `id` column")

    stars = []
    rows = _open_csv(athyg_path) if athyg_path else _open_csv(hyg_path)
    for r in rows:
        try:
            row_id = int(r.get("hyg") or r.get("id") or -1)
        except ValueError:
            row_id = -1
        if row_id == 0:
            continue                                    # Sol: drawn as a body, not a row
        p = photometry.get(row_id, r)

        s = Star()
        try:
            s.x, s.y, s.z = float(r["x"]), float(r["y"]), float(r["z"])
        except (KeyError, ValueError):
            continue
        try:
            s.vmag = float(p["mag"])
        except (KeyError, ValueError):
            try:
                s.vmag = float(r["mag"])
                s.flags |= F_PHOTOMETRY_FROM_TYCHO
            except (KeyError, ValueError):
                continue
        if athyg_path and row_id not in photometry:
            s.flags |= F_PHOTOMETRY_FROM_TYCHO

        try:
            distance = float(r["dist"])
        except (KeyError, ValueError):
            distance = UNKNOWN_DISTANCE_PC
        if distance >= UNKNOWN_DISTANCE_PC or distance <= 0.0:
            s.flags |= F_DISTANCE_UNKNOWN
        else:
            s.flags |= _hyg_distance_source(r.get("dist_src", "")) << F_DISTANCE_SOURCE_SHIFT

        try:
            s.bv = float(p["ci"])
            s.teff = temperature_from_colour_index(s.bv)
        except (KeyError, ValueError):
            t = temperature_from_spectrum(p.get("spect", ""))
            s.teff = t if t is not None else DEFAULT_TEMPERATURE
            s.flags |= F_TEMPERATURE_FROM_SPECTRUM
        s.teff = min(60000.0, max(1000.0, s.teff))
        s.spec = spectral_code(p.get("spect", ""))

        try:
            s.hip = int(float(r["hip"]))
        except (KeyError, ValueError, TypeError):
            s.hip = 0

        if all(r.get(k) for k in ("vx", "vy", "vz")):
            s.vx = float(r["vx"]) * PC_PER_YEAR_IN_KM_S
            s.vy = float(r["vy"]) * PC_PER_YEAR_IN_KM_S
            s.vz = float(r["vz"]) * PC_PER_YEAR_IN_KM_S
            if not r.get("rv"):
                s.flags |= F_RADIAL_VELOCITY_MISSING
            if s.speed > 1000.0:
                s.vx = s.vy = s.vz = 0.0
                s.flags |= F_VELOCITY_SUSPECT
        else:
            s.flags |= F_VELOCITY_UNKNOWN

        s.flags |= F_EXTINCTION_UNKNOWN
        proper = (p.get("proper") or r.get("proper") or "").strip()
        if proper:
            s.name = proper
            s.flags |= F_HAS_PROPER_NAME
        s.designation = (r.get("bf") or "").strip() or None
        s.constellation = (r.get("con") or "").strip() or None
        stars.append(s)
    return stars, len(stars)


def _hyg_distance_source(src: str) -> int:
    s = (src or "").strip().upper()
    if s.startswith("G_R3") or s == "GAIA3":
        return DSRC_GAIA_DR3
    if s.startswith("G_R2"):
        return DSRC_GAIA_DR2
    if s.startswith("HIP"):
        return DSRC_HIPPARCOS
    if s.startswith("GL") or s.startswith("GJ"):
        return DSRC_GLIESE
    return DSRC_OTHER if s else DSRC_UNKNOWN


# --------------------------------------------------------------------------------------
# Source: Yale BSC5p via NASA HEASARC (commercial-clean, staged)
# --------------------------------------------------------------------------------------

def load_bsc5(path: str):
    fields, rows = [], []
    with gzip.open(path, "rt", encoding="latin-1") as handle:
        for line in handle:
            m = re.match(r"^field\[(\w+)\]", line)
            if m:
                fields.append(m.group(1))
            elif line.startswith("<DATA>"):
                break
        for line in handle:
            if line.startswith("<END>"):
                break
            rows.append(line.rstrip("\n").split("|"))
    if not fields or not rows:
        raise SystemExit(f"{path}: no TDAT field list or no data rows")
    ix = {n: i for i, n in enumerate(fields)}

    def num(row, key):
        v = row[ix[key]].strip() if ix[key] < len(row) else ""
        try:
            return float(v)
        except ValueError:
            return None

    # The pole test: near the celestial poles a raw mu_alpha is 1/cos(dec) times the
    # great-circle rate, so a wrong convention shows up there and nowhere else.
    polar = [abs(num(r, "pmra") or 0.0) for r in rows
             if (num(r, "dec") is not None and abs(num(r, "dec")) > 85.0)]
    if polar and max(polar) > 1.0:
        raise SystemExit(
            f"BSC5 pmra reaches {max(polar):.3f} arcsec/yr within 5 deg of a pole, which "
            "means the column is mu_alpha and not mu_alpha*cos(dec). Fix space_velocity().")

    stars, dropped = [], 0
    for r in rows:
        ra, dec, v = num(r, "ra"), num(r, "dec"), num(r, "vmag")
        if ra is None or dec is None or v is None:
            dropped += 1
            continue
        s = Star()
        s.vmag = v
        plx = num(r, "parallax")
        code = (r[ix["par_code"]].strip() if ix["par_code"] < len(r) else "")
        if plx and plx >= BSC5_MINIMUM_PARALLAX and code.upper() != "D":
            distance = 1.0 / plx
            s.flags |= DSRC_GROUND_TRIG << F_DISTANCE_SOURCE_SHIFT
        else:
            distance = UNKNOWN_DISTANCE_PC
            s.flags |= F_DISTANCE_UNKNOWN
        s.x, s.y, s.z = cartesian(ra, dec, distance)

        bv = num(r, "bv_color")
        if bv is not None:
            s.bv = bv
            s.teff = temperature_from_colour_index(bv)
        else:
            t = temperature_from_spectrum(r[ix["spect_type"]])
            s.teff = t if t is not None else DEFAULT_TEMPERATURE
            s.flags |= F_TEMPERATURE_FROM_SPECTRUM
        s.teff = min(60000.0, max(1000.0, s.teff))
        s.spec = spectral_code(r[ix["spect_type"]] if ix["spect_type"] < len(r) else "")

        hd = r[ix["hd"]].strip()
        s.hd = int(hd) if hd.isdigit() else 0

        pmra, pmdec, rv = num(r, "pmra"), num(r, "pmdec"), num(r, "radvel")
        if pmra is not None and pmdec is not None and not (s.flags & F_DISTANCE_UNKNOWN):
            if rv is None:
                rv = 0.0
                s.flags |= F_RADIAL_VELOCITY_MISSING
            s.vx, s.vy, s.vz = space_velocity(ra, dec, pmra, pmdec, rv, distance)
            if s.speed > 1000.0:
                s.vx = s.vy = s.vz = 0.0
                s.flags |= F_VELOCITY_SUSPECT
        else:
            s.flags |= F_VELOCITY_UNKNOWN

        s.flags |= F_EXTINCTION_UNKNOWN
        s.designation = (r[ix["alt_name"]].strip() if ix["alt_name"] < len(r) else "") or None
        stars.append(s)
    return stars, dropped


# --------------------------------------------------------------------------------------
# Names and the HIP bridge
# --------------------------------------------------------------------------------------

def load_iau_csn(path: str):
    """IAU-CSN: the only cleared file carrying HIP and HD and a J2000 position per row."""
    lines = open(path, encoding="utf-8").read().splitlines()
    head = next(i for i, l in enumerate(lines) if l.startswith("#Name/ASCII"))
    mag_at = lines[head].index("mag")
    name_to = lines[head].index("Name/Diacritics")
    desig_a, desig_b = lines[head].index("Designation"), lines[head].index("ID    ID")

    out = []
    for line in lines[head + 1:]:
        if not line.strip() or line[0] in "#$":
            continue
        tail = line[mag_at - 2:].split()
        if len(tail) < 6:
            continue
        mag, band, hip, hd, ra, dec = tail[:6]
        try:
            ra, dec = float(ra), float(dec)
        except ValueError:
            continue
        out.append({
            "name": line[:name_to].strip(),
            "designation": line[desig_a:desig_b].strip(),
            "hip": int(hip) if hip.isdigit() else 0,
            "hd": int(hd) if hd.isdigit() else 0,
            "band": band, "mag": mag, "ra": ra, "dec": dec,
        })
    return out


def build_hip_bridge(stars, csn, sky, profile):
    """HIP number -> catalogue row, through every route the profile allows.

    Returns (mapping, report, crosscheck). Each stage declares a licence tag exactly like a
    data source, so switching the profile narrows the bridge instead of silently shipping a
    broken figure set.

    None of these stages resolves a HIP to a POSITION out of the Hipparcos catalogue: that
    table is the thing that is not ours to redistribute. They resolve a HIP to another
    catalogue's identifier or to a position published by somebody else under their own
    licence, which is a fact about naming and not an extract of ESA astrometry.

    Two routes are independent of each other -- a name table and a position table with
    different publishers -- so where both fire they are compared, and a disagreement is an
    assertion failure rather than a note. On the shipped development catalogue they agree on
    240 stars and disagree on none.
    """
    by_hip = {s.hip: i for i, s in enumerate(stars) if s.hip}
    by_hd = {}
    by_name = {}
    for i, s in enumerate(stars):
        if s.hd:
            by_hd.setdefault(s.hd, i)
        if s.name:
            by_name.setdefault(s.name.strip().lower(), i)

    mapping, report = {}, []
    per_stage = {}

    def stage(name, tag, produce):
        if not licence.allows(tag, current=profile):
            report.append({"stage": name, "licenceTag": tag, "used": False, "resolved": 0,
                           "reason": "tag %s not allowed by profile %s" % (tag, profile)})
            return
        produced = produce() or {}
        per_stage[name] = produced
        added = 0
        for hip, row in produced.items():
            if hip not in mapping:
                mapping[hip] = row
                added += 1
        report.append({"stage": name, "licenceTag": tag, "used": True,
                       "resolved": added, "candidates": len(produced)})

    # 1. The catalogue's own HIP column. Present on a full HYG build, absent after a CSS1
    #    migration and absent from BSC5, which carries no HIP at all.
    stage("catalog-hip", "by-sa", lambda: dict(by_hip))

    # 2. IAU-CSN: HIP -> HD -> row. CC BY, and the only cleared file with both identifiers.
    def csn_hd():
        out = {}
        for row in csn:
            if row["hip"] and row["hd"]:
                i = by_hd.get(row["hd"])
                if i is not None:
                    out[row["hip"]] = i
        return out
    stage("iau-csn-hd", "by", csn_hd)

    # 3. IAU-CSN: HIP -> the IAU's own published J2000 position -> nearest catalogue star.
    #    Rejected unless the nearest star is within 30 arcsec AND five times closer than the
    #    runner-up, so a close pair refuses rather than guesses.
    def csn_position():
        out = {}
        directions = []
        for i, star in enumerate(stars):
            r = math.sqrt(star.x * star.x + star.y * star.y + star.z * star.z)
            if r > 0.0:
                directions.append((i, star.x / r, star.y / r, star.z / r))
        if not directions:
            return out
        cos_tolerance = math.cos(math.radians(30.0 / 3600.0))
        for row in csn:
            if not row["hip"]:
                continue
            ra, dec = math.radians(row["ra"]), math.radians(row["dec"])
            ux = math.cos(dec) * math.cos(ra)
            uy = math.cos(dec) * math.sin(ra)
            uz = math.sin(dec)
            best, best_dot, second_dot = None, -2.0, -2.0
            for (i, x, y, z) in directions:
                d = ux * x + uy * y + uz * z
                if d > best_dot:
                    second_dot, best_dot, best = best_dot, d, i
                elif d > second_dot:
                    second_dot = d
            if best is None or best_dot < cos_tolerance:
                continue
            sep = math.acos(min(1.0, best_dot))
            runner = math.acos(min(1.0, max(-1.0, second_dot)))
            if runner > 5.0 * max(sep, math.radians(1.0 / 3600.0)):
                out[row["hip"]] = best
        return out
    stage("iau-csn-position", "by", csn_position)

    # 4. Stellarium's own common_names table is keyed "HIP nnn", so a traditional name links
    #    a HIP to whichever catalogue row already carries that proper name.
    def stellarium_name():
        # 46 of Stellarium's traditional names are attached to more than one HIP -- Algenib
        # is both Gamma Pegasi and Alpha Persei, Marfak names four stars. A name shared by
        # two stars identifies neither, so those names are refused rather than resolved to
        # whichever row was seen first.
        by_label = {}
        for key, entries in (sky.get("common_names") or {}).items():
            if not key.startswith("HIP "):
                continue
            try:
                hip = int(key[4:])
            except ValueError:
                continue
            for entry in entries:
                for candidate in (entry.get("english"), entry.get("native")):
                    if candidate:
                        by_label.setdefault(candidate.strip().lower(), set()).add(hip)
        # The ambiguity runs both ways. A HIP can carry several traditional aliases, and
        # they need not agree: Stellarium lists Graffias as an alias of Beta-1 Scorpii
        # (HIP 78820) while the catalogue attaches that name to Xi Scorpii, a different
        # star. So a name identifies a star only when it is unique in BOTH directions --
        # one HIP per name, and one row per HIP -- and anything else is refused.
        candidates = {}
        for label, owners in by_label.items():
            if len(owners) != 1:
                continue
            i = by_name.get(label)
            if i is not None:
                candidates.setdefault(next(iter(owners)), set()).add(i)
        return {hip: next(iter(rows)) for hip, rows in candidates.items() if len(rows) == 1}
    stage("stellarium-name", "by-sa", stellarium_name)

    # 5. Wikidata HIP<->HD, CC0. Fetched offline by tools/cosmodata/fetch_hip_bridge.py and
    #    cached; importers do not reach the network for an identifier table.
    def wikidata():
        out = {}
        path = os.path.join(CACHE, "hip_hd_wikidata.json")
        if not os.path.exists(path):
            return out
        for hip, hd in json.load(open(path, encoding="utf-8")).items():
            i = by_hd.get(int(hd))
            if i is not None:
                out[int(hip)] = i
        return out
    stage("wikidata-hip-hd", "cc0", wikidata)

    # The cross-check: two independently published tables, compared where both fire.
    a = per_stage.get("stellarium-name", {})
    b = per_stage.get("iau-csn-position", {})
    shared = set(a) & set(b)

    def separation_arcsec(i, j):
        p, q = stars[i], stars[j]
        rp = math.sqrt(p.x * p.x + p.y * p.y + p.z * p.z)
        rq = math.sqrt(q.x * q.x + q.y * q.y + q.z * q.z)
        if rp <= 0.0 or rq <= 0.0:
            return 1e9
        dot = (p.x * q.x + p.y * q.y + p.z * q.z) / (rp * rq)
        return math.degrees(math.acos(max(-1.0, min(1.0, dot)))) * 3600.0

    # Two routes landing on the two components of one double star agree about WHICH STAR;
    # two routes landing in different parts of the sky do not. Only the second is a fault.
    same_system, conflict = [], []
    for hip in sorted(shared):
        if a[hip] == b[hip]:
            continue
        (same_system if separation_arcsec(a[hip], b[hip]) < 10.0 else conflict).append(hip)
    crosscheck = {"stages": ["stellarium-name", "iau-csn-position"],
                  "compared": len(shared),
                  "agree": len(shared) - len(same_system) - len(conflict),
                  "sameSystem": len(same_system), "sameSystemHip": same_system[:20],
                  "conflict": len(conflict), "conflictHip": conflict[:20]}
    return mapping, report, crosscheck


def build_constellations(sky, mapping, stars):
    """The 88 figures as pairs of catalogue row indices.

    A figure is kept only where BOTH endpoints resolve. An unresolved segment is dropped and
    counted, never drawn to a guessed position: a line to a star that is not in the catalogue
    is a line to nowhere, and the test that gates this file would rather fail than draw it.
    """
    figures, total, kept, unresolved = [], 0, 0, set()
    for c in sky.get("constellations", []):
        abbr = c["id"].split()[-1]
        names = c.get("common_name") or {}
        segments, hips = [], set()
        for stroke in c.get("lines", []):
            for a, b in zip(stroke, stroke[1:]):
                total += 1
                ia, ib = mapping.get(int(a)), mapping.get(int(b))
                if ia is None:
                    unresolved.add(int(a))
                if ib is None:
                    unresolved.add(int(b))
                if ia is not None and ib is not None and ia != ib:
                    segments.append([ia, ib])
                    hips.add(int(a))
                    hips.add(int(b))
                    kept += 1
        if not segments:
            continue
        # The label goes on the figure's BRIGHTEST star, not the centroid of its endpoints:
        # the centroid is not a star, and in 3-D it is usually nowhere near the figure.
        brightest = min((s for pair in segments for s in pair),
                        key=lambda i: stars[i].vmag if stars[i].vmag == stars[i].vmag else 99)
        figures.append({
            "abbr": abbr,
            "name": names.get("native") or names.get("english") or abbr,
            "english": names.get("english") or abbr,
            "segments": segments,
            "labelIndex": brightest,
        })
    return figures, total, kept, sorted(unresolved)


# --------------------------------------------------------------------------------------
# Assertions: physical ranges and known answers, on values and never on headers
# --------------------------------------------------------------------------------------

#: name, V, +-V, distance pc, +-distance, must_have_distance
#:
#: The magnitude tolerances are 0.05: HYG prints to 0.01 and the published V for Sirius
#: ranges -1.44 to -1.46 across catalogues. The distance tolerances are wider on the clean
#: path because a ground-based parallax is not a Gaia one, and each is justified in the row.
#:
#: `must_have_distance` is true only inside 15 pc, which is inside the competence of every
#: parallax any of these sources carries (the BSC5 cut above is 0.060", i.e. 17 pc). Beyond
#: that a catalogue may honestly say it does not know -- but if it claims a distance, the
#: claim is checked. That is not a weaker test: it is the difference between "no measurement"
#: and "a wrong measurement", and only the second is a defect.
KNOWN_ANSWERS = [
    ("Sirius",     -1.44,  0.05,     2.637,  0.05,  True),
    ("Vega",        0.03,  0.05,     7.680,  0.60,  True),
    ("Arcturus",   -0.05,  0.05,    11.260,  0.60,  True),
    ("Canopus",    -0.62,  0.15,    94.800, 40.00,  False),
]
KNOWN_HD = {"Sirius": 48915, "Vega": 172167, "Arcturus": 124897, "Canopus": 45348}


def run_assertions(stars, path_name, names_by_index):
    """Every check the importer ran, with the value it got, for the meta block."""
    out = []

    def check(name, expected, got, tolerance=None, ok=None):
        if ok is None:
            ok = got is not None and abs(got - expected) <= tolerance
        out.append({"name": name, "expected": expected, "got": got,
                    "tolerance": tolerance, "ok": bool(ok)})
        return ok

    finite = all(math.isfinite(s.x) and math.isfinite(s.y) and math.isfinite(s.z)
                 for s in stars)
    check("all_positions_finite", True, finite, ok=finite)

    bad_t = [s for s in stars if not (500.0 < s.teff < 100000.0)]
    check("temperatures_in_stellar_range", 0, len(bad_t), tolerance=0)

    fast = [s for s in stars if s.speed > 1000.0]
    check("no_speed_over_1000_km_s", 0, len(fast), tolerance=0)

    suspect = sum(1 for s in stars if s.flags & F_VELOCITY_SUSPECT)
    check("velocity_suspect_under_half_percent", 0, suspect,
          tolerance=max(1, int(0.005 * len(stars))))

    far = max((abs(s.x), abs(s.y), abs(s.z)) for s in stars) if stars else (0,)
    check("positions_within_350kpc", 0.0, max(far), tolerance=350000.0)

    sorted_ok = all(
        (stars[i].vmag != stars[i].vmag) or (stars[i + 1].vmag != stars[i + 1].vmag) or
        stars[i].vmag <= stars[i + 1].vmag + 1e-4 for i in range(len(stars) - 1))
    check("sorted_by_magnitude", True, sorted_ok, ok=sorted_ok)

    mags = [s.vmag for s in stars if s.vmag == s.vmag]
    n60 = sum(1 for m in mags if m <= 6.0)
    # The naked-eye sky is about 5,000 stars; a catalogue that says 500 or 50,000 is not the
    # sky. 10% allows for the real difference between HYG's and Yale's photometry.
    check("stars_brighter_than_V6", 5000, n60, tolerance=500)
    check("brightest_is_sirius_class", -1.46, min(mags) if mags else None, tolerance=0.10)

    by_name = {n.lower(): i for i, n in names_by_index.items() if n}
    by_hd = {s.hd: i for i, s in enumerate(stars) if s.hd}
    for name, vmag, vtol, dist, dtol, must in KNOWN_ANSWERS:
        i = by_name.get(name.lower())
        if i is None:
            i = by_hd.get(KNOWN_HD[name])
        if i is None:
            check(f"{name.lower()}_present", 1, 0, tolerance=0)
            continue
        s = stars[i]
        check(f"{name.lower()}_vmag", vmag, round(s.vmag, 3), tolerance=vtol)
        if s.flags & F_DISTANCE_UNKNOWN:
            check(f"{name.lower()}_distance_pc", dist, None, tolerance=dtol, ok=not must)
        else:
            d = math.sqrt(s.x * s.x + s.y * s.y + s.z * s.z)
            check(f"{name.lower()}_distance_pc", dist, round(d, 3), tolerance=dtol)

    # The guard that stops "I do not know" from becoming a way to pass everything: a sky
    # with no distances at all is a dome, and the file must say which one it is.
    with_distance = sum(1 for s in stars if not (s.flags & F_DISTANCE_UNKNOWN))
    check("some_stars_have_distances", 1, with_distance, tolerance=None,
          ok=with_distance >= 200)
    return out


# --------------------------------------------------------------------------------------
# Writing
# --------------------------------------------------------------------------------------

def generator_commit():
    try:
        sha = subprocess.check_output(["git", "-C", REPO_ROOT, "rev-parse", "--short", "HEAD"],
                                      stderr=subprocess.DEVNULL).decode().strip()
        dirty = subprocess.call(["git", "-C", REPO_ROOT, "diff", "--quiet"],
                                stderr=subprocess.DEVNULL) != 0
        return sha + ("+dirty" if dirty else "")
    except Exception:
        return "unknown"


def quantise_bv(bv):
    if bv is None:
        return 0
    t = (bv - BV_LO) / (BV_HI - BV_LO)
    return max(1, min(255, int(round(t * 254.0)) + 1))      # 0 is reserved for "missing"


def write_outputs(stars, out_dir, magic, meta_extra, figures, figure_stats, bridge_report,
                  sources, profile, dry_run=False):
    n = len(stars)
    pos, vel = [], []
    vmag, teff, av, bv, spec, hip, hd, flags = [], [], [], [], [], [], [], []
    for s in stars:
        pos.extend((s.x, s.y, s.z))
        vel.extend((s.vx, s.vy, s.vz))
        vmag.append(s.vmag)
        teff.append(s.teff)
        av.append(max(0, min(65535, int(round(s.av / AV_SCALE)))))
        bv.append(quantise_bv(s.bv))
        spec.append(s.spec)
        hip.append(s.hip)
        hd.append(s.hd)
        flags.append(s.flags)

    columns = [
        csdata.Column("pos", "f32", 3, csdata.pack("f32", pos), unit="pc"),
        csdata.Column("vel", "f32", 3, csdata.pack("f32", vel), unit="km/s"),
        csdata.Column("vmag", "f32", 1, csdata.pack("f32", vmag), unit="mag"),
        csdata.Column("teff", "f32", 1, csdata.pack("f32", teff), unit="K"),
        csdata.Column("av", "u16", 1, csdata.pack("u16", av), unit="mag",
                      extra={"encoding": {"kind": "linear", "scale": AV_SCALE, "offset": 0.0}}),
        csdata.Column("bv", "u8", 1, csdata.pack("u8", bv), unit="mag",
                      extra={"encoding": {"kind": "linear",
                                          "scale": (BV_HI - BV_LO) / 254.0,
                                          "offset": BV_LO - (BV_HI - BV_LO) / 254.0},
                             "missing": 0}),
        csdata.Column("spec", "u8", 1, csdata.pack("u8", spec),
                      extra={"classes": SPECTRAL_CLASSES, "encoding":
                             {"kind": "code", "note": "class*10 + subclass; 255 unknown"}}),
        csdata.Column("hip", "u32", 1, csdata.pack("u32", hip), extra={"missing": 0}),
        csdata.Column("hd", "u32", 1, csdata.pack("u32", hd), extra={"missing": 0}),
        csdata.Column("flags", "u32", 1, csdata.pack("u32", flags)),
    ]

    names = [{"index": i, "name": s.name, "designation": s.designation,
              "constellation": s.constellation, "hip": s.hip, "hd": s.hd,
              "vmag": None if s.vmag != s.vmag else round(s.vmag, 3)}
             for i, s in enumerate(stars) if s.name]

    counts = {
        "rows": n,
        "distanceUnknown": sum(1 for s in stars if s.flags & F_DISTANCE_UNKNOWN),
        "distanceKnown": sum(1 for s in stars if not (s.flags & F_DISTANCE_UNKNOWN)),
        "temperatureFromSpectrum": sum(1 for s in stars if s.flags & F_TEMPERATURE_FROM_SPECTRUM),
        "velocityUnknown": sum(1 for s in stars if s.flags & F_VELOCITY_UNKNOWN),
        "velocitySuspect": sum(1 for s in stars if s.flags & F_VELOCITY_SUSPECT),
        "extinctionUnknown": sum(1 for s in stars if s.flags & F_EXTINCTION_UNKNOWN),
        "colourIndexMissing": sum(1 for s in stars if s.bv is None),
        "spectralClassMissing": sum(1 for s in stars if s.spec == SPEC_UNKNOWN),
        "withHip": sum(1 for s in stars if s.hip),
        "withHd": sum(1 for s in stars if s.hd),
        "named": len(names),
        "brighterThanV6": sum(1 for s in stars if s.vmag == s.vmag and s.vmag <= 6.0),
        "brighterThanV65": sum(1 for s in stars if s.vmag == s.vmag and s.vmag <= 6.5),
    }
    counts.update(meta_extra.pop("counts", {}))

    names_by_index = {i: s.name for i, s in enumerate(stars) if s.name}
    assertions = run_assertions(stars, magic, names_by_index)
    failed = [a for a in assertions if not a["ok"]]

    #: The faintest star the catalogue holds, so the observed-sky limit slider can state its
    #: own ceiling instead of pretending magnitude 14 reveals something.
    finite_mags = [s.vmag for s in stars if s.vmag == s.vmag]
    meta = {
        "kind": "stars", "schema": 1,
        "generator": "tools/cosmodata/build_starfield.py",
        "generatorCommit": generator_commit(),
        "generatedUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "licenceProfile": profile,
        "frame": "ICRF", "epoch": "J2000.0",
        "sources": sources,
        "provenance": {
            "pos": {"class": "Measured", "source": sources[0]["id"],
                    "reference": "catalogue position, epoch J2000.0"},
            "vmag": {"class": "Measured", "source": sources[0]["id"],
                     "reference": "Johnson V"},
            "bv": {"class": "Measured", "source": sources[0]["id"], "reference": "Johnson B-V"},
            "teff": {"class": "Inferred", "source": "Ballesteros 2012",
                     "reference": "EPL 97 34008, from B-V"},
            "vel": {"class": "Inferred", "source": sources[0]["id"],
                    "reference": "proper motion + distance + radial velocity"},
            "spec": {"class": "Measured", "source": sources[0]["id"],
                     "reference": "published spectral type, truncated to class and subclass"},
            "av": {"class": "Inferred", "source": "none",
                   "reference": "no 3-D dust map is shipped yet; every row is zero and "
                                "flagged ExtinctionUnknown. A zero here is the absence of a "
                                "measurement, not a measurement of zero extinction."},
            "hip": {"class": "Measured", "source": "identifier", "reference": "Hipparcos number"},
            "hd": {"class": "Measured", "source": "identifier", "reference": "Henry Draper number"},
        },
        "provenanceBits": [
            {"bit": 0, "field": "pos", "class": "Inferred",
             "note": "parallax unusable: direction measured, distance unknown"},
            {"bit": 1, "field": "teff", "class": "Inferred",
             "note": "spectral class only; no B-V published"},
            {"bit": 3, "field": "vmag", "class": "Measured",
             "note": "Tycho-2 VT converted to V, not Johnson V"},
            {"bit": 4, "field": "vel", "class": "Inferred",
             "note": "radial velocity unknown; tangential component only"},
            {"bit": 7, "field": "vel", "class": "Unknown",
             "note": "computed speed exceeded 1000 km/s: a bad parallax, not a star. Zeroed."},
            {"bit": 11, "field": "av", "class": "Unknown", "note": "no extinction model"},
            {"bit": 12, "field": "vel", "class": "Unknown", "note": "no proper motion published"},
            {"bit": 13, "field": "vel", "class": "Unknown",
             "note": "migrated from the CSS1 container, which carried no velocity, colour "
                     "index, spectral class or cross-identifier"},
        ],
        "provenanceCodes": [{"bits": [8, 10], "field": "pos", "values": {
            "0": {"class": "Unknown", "note": "no distance"},
            "1": {"class": "Measured", "note": "Gaia DR3 parallax"},
            "2": {"class": "Measured", "note": "Gaia DR2 parallax"},
            "3": {"class": "Measured", "note": "Hipparcos parallax"},
            "4": {"class": "Measured", "note": "Gliese"},
            "5": {"class": "Measured", "note": "other published parallax"},
            "6": {"class": "Measured", "note": "Reyle+2021 nearby census"},
            "7": {"class": "Inferred", "note": "ground-based trigonometric parallax (BSC5), "
                                               "errors of tens of mas at the faint end"}}}],
        "limits": {
            "faintestVmag": round(max(finite_mags), 3) if finite_mags else None,
            "brightestVmag": round(min(finite_mags), 3) if finite_mags else None,
            "note": "the observed-sky limit slider cannot reveal anything past faintestVmag",
        },
        "counts": counts,
        "assertions": assertions,
    }
    meta.update(meta_extra)

    if failed:
        for a in failed:
            print(f"  ASSERTION FAILED {a['name']}: expected {a['expected']} "
                  f"+-{a['tolerance']}, got {a['got']}", file=sys.stderr)
        for victim in ("stars.csstars", "stars_named.json", "constellations.json"):
            p = os.path.join(out_dir, victim)
            if os.path.exists(p):
                os.unlink(p)
        raise SystemExit(f"{len(failed)} assertion(s) failed; wrote nothing and deleted the "
                         "stale shipped files. An artefact on disk is not evidence of a run.")

    if dry_run:
        print(json.dumps({"counts": counts, "assertions": assertions,
                          "figures": figure_stats, "bridge": bridge_report}, indent=1))
        return {}

    os.makedirs(out_dir, exist_ok=True)
    written = {}
    star_path = os.path.join(out_dir, "stars.csstars")
    written["stars.csstars"] = csdata.write(star_path, magic, meta, columns, rows=n)

    names_doc = {
        "source": sources[0]["name"], "licence": sources[0]["licence"],
        "licenceProfile": profile, "generated": meta["generatedUtc"],
        "note": "Indices refer to stars.csstars, sorted brightest first. Written in the same "
                "run as the catalogue, so the indices cannot disagree with it.",
        "stars": names,
    }
    p = os.path.join(out_dir, "stars_named.json")
    with open(p + ".tmp", "w", encoding="utf-8") as h:
        json.dump(names_doc, h, indent=1, ensure_ascii=False)
    os.replace(p + ".tmp", p)
    written["stars_named.json"] = os.path.getsize(p)

    figures_doc = {
        "source": "Stellarium sky culture \"modern\" (Stellarium's team), CC BY-SA 4.0 "
                  "https://creativecommons.org/licenses/by-sa/4.0/ ; IAU constellation "
                  "boundary data (c) 2012-2022 Pierre Barbier, CC BY-SA 4.0",
        "licence": "CC BY-SA 4.0", "licenceTag": "by-sa", "licenceProfile": profile,
        "generated": meta["generatedUtc"],
        "note": "Segments are PAIRS OF ROW INDICES into stars.csstars, written in the same "
                "run. A segment appears only when both of its stars are in the catalogue.",
        "bridge": bridge_report,
        "counts": figure_stats,
        "figures": figures,
    }
    p = os.path.join(out_dir, "constellations.json")
    with open(p + ".tmp", "w", encoding="utf-8") as h:
        json.dump(figures_doc, h, indent=1, ensure_ascii=False)
    os.replace(p + ".tmp", p)
    written["constellations.json"] = os.path.getsize(p)
    return written


# --------------------------------------------------------------------------------------

def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument("--profile", choices=licence.PROFILES,
                        help="override COSMODATA_LICENCE for this run")
    parser.add_argument("--hyg", help="path to hyg_v44.csv(.gz)")
    parser.add_argument("--athyg", help="path to hyglike_from_athyg_v40.csv(.gz)")
    parser.add_argument("--from-css1", metavar="PATH", nargs="?", const=True,
                        help="development fallback: migrate the CSS1 file already in the tree")
    parser.add_argument("--require-figure-fraction", type=float, default=0.0,
                        help="fail unless at least this fraction of the 695 figure segments "
                             "resolves to catalogue rows (0 = report and ship what resolved)")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args(argv)

    profile = args.profile or licence.profile()
    print(licence.describe(profile))

    csn_path = os.path.join(STAGING, "iau-wgsn-star-names", "IAU-CSN.txt")
    sky_path = os.path.join(STAGING, "stellarium-modern-skyculture", "index.json")
    bsc_path = os.path.join(STAGING, "yale-bsc5-heasarc", "heasarc_bsc5p.tdat.gz")

    meta_extra, sources = {}, []
    commercial = not licence.allows("by-sa", "hyg44", current=profile)

    if commercial:
        if not os.path.exists(bsc_path):
            raise SystemExit(f"commercial profile needs {bsc_path}")
        print("source: Yale BSC5p (NASA HEASARC) -- commercial-clean")
        stars, dropped = load_bsc5(bsc_path)
        sources = [{
            "id": "bsc5p", "name": "Yale Bright Star Catalogue 5th Revised Ed. (Hoffleit & "
                                   "Warren 1991), NASA HEASARC copy",
            "url": "https://heasarc.gsfc.nasa.gov/W3Browse/star-catalog/bsc5p.html",
            "licence": "NASA HEASARC free use", "licenceTag": "pd",
            "attribution": "Bright-star data: Yale Bright Star Catalogue, 5th Revised "
                           "Edition (Hoffleit & Warren 1991), via NASA HEASARC."}]
        meta_extra["counts"] = {"droppedNoPositionOrMagnitude": dropped}
    elif args.hyg:
        print(f"source: HYG {args.hyg}" + (f" + AT-HYG {args.athyg}" if args.athyg else ""))
        stars, _ = load_from_hyg(args.hyg, args.athyg)
        sources = [{
            "id": "hyg44", "name": "HYG star database v4.4 (astronexus)",
            "url": "https://codeberg.org/astronexus/hyg",
            "licence": "CC BY-SA 4.0", "licenceTag": "by-sa",
            "attribution": "HYG star database v4.4 (astronexus), CC BY-SA 4.0 -- compiled "
                           "from the Hipparcos, Yale Bright Star and Gliese catalogues",
            "upstreamLicence": "ESA CC BY-NC 3.0 IGO (Hipparcos/Gaia astrometry)"}]
    else:
        path = args.from_css1 if isinstance(args.from_css1, str) else \
            os.path.join(args.out, "stars.csstars")
        print(f"source: CSS1 migration from {path}")
        if not os.path.exists(path):
            raise SystemExit(
                f"{path} not found. Pass --hyg PATH for a full development rebuild, or run "
                "with COSMODATA_LICENCE=commercial for the clean build.")
        stars, _ = load_from_css1(path, os.path.join(args.out, "stars_named.json"))
        sources = [{
            "id": "hyg44", "name": "HYG star database v4.4 (astronexus), via the CSS1 "
                                   "container built 2026-08-01",
            "url": "https://codeberg.org/astronexus/hyg",
            "licence": "CC BY-SA 4.0", "licenceTag": "by-sa",
            "attribution": "HYG star database v4.4 (astronexus), CC BY-SA 4.0 -- compiled "
                           "from the Hipparcos, Yale Bright Star and Gliese catalogues",
            "upstreamLicence": "ESA CC BY-NC 3.0 IGO (Hipparcos/Gaia astrometry)",
            "note": "container migration: velocity, colour index, spectral class and "
                    "cross-identifiers were not present in CSS1 and are flagged unknown"}]

    # NaN magnitudes sort last, so a prefix read is always the brightest N.
    stars.sort(key=lambda s: (s.vmag != s.vmag, s.vmag if s.vmag == s.vmag else 0.0))

    csn = load_iau_csn(csn_path) if os.path.exists(csn_path) else []
    sky = json.load(open(sky_path, encoding="utf-8")) if os.path.exists(sky_path) else {}

    # Names. On the clean path they come from IAU-CSN, joined on HD.
    if commercial and csn:
        by_hd = {}
        for i, s in enumerate(stars):
            if s.hd:
                by_hd.setdefault(s.hd, i)
        named = 0
        for row in csn:
            i = by_hd.get(row["hd"]) if row["hd"] else None
            if i is not None and not stars[i].name:
                stars[i].name = row["name"]
                stars[i].hip = stars[i].hip or row["hip"]
                stars[i].flags |= F_HAS_PROPER_NAME
                named += 1
        print(f"names: {named} from IAU-CSN joined on HD")
        sources.append({
            "id": "iau-csn", "name": "IAU Catalog of Star Names (IAU WGSN)",
            "url": "https://www.pas.rochester.edu/~emamajek/WGSN/IAU-CSN.txt",
            "licence": "CC BY", "licenceTag": "by",
            "attribution": "Star names: IAU Catalog of Star Names (IAU-CSN), IAU Division C "
                           "Working Group on Star Names (WGSN). CC BY."})

    mapping, bridge_report, crosscheck = build_hip_bridge(stars, csn, sky, profile)
    figures, total_seg, kept_seg, unresolved = build_constellations(sky, mapping, stars)
    distinct = len({int(h) for c in sky.get("constellations", [])
                    for stroke in c.get("lines", []) for h in stroke})
    figure_stats = {
        "figuresInSource": len(sky.get("constellations", [])),
        "figuresShipped": len(figures),
        "segmentsInSource": total_seg,
        "segmentsShipped": kept_seg,
        "segmentFraction": round(kept_seg / total_seg, 4) if total_seg else 0.0,
        "distinctHipInSource": distinct,
        "hipResolved": len({h for h in mapping if h in
                            {int(x) for c in sky.get("constellations", [])
                             for s in c.get("lines", []) for x in s}}),
        "hipUnresolved": len(unresolved),
    }
    figure_stats["hipFraction"] = (round(figure_stats["hipResolved"] / distinct, 4)
                                   if distinct else 0.0)
    figure_stats["bridgeCrossCheck"] = crosscheck
    if crosscheck["conflict"]:
        raise SystemExit(
            "the name bridge and the position bridge point at different parts of the sky "
            "for %d HIP ids (%s...). Two independent tables cannot both be right; "
            "wrote nothing." % (crosscheck["conflict"], crosscheck["conflictHip"][:5]))
    if args.require_figure_fraction and \
            figure_stats["segmentFraction"] < args.require_figure_fraction:
        raise SystemExit(
            "constellation segments resolved %.1f%%, below the required %.1f%%. The missing "
            "half of the bridge is a CC0 Wikidata HIP<->HD table: run "
            "tools/cosmodata/fetch_hip_bridge.py once."
            % (100 * figure_stats["segmentFraction"],
               100 * args.require_figure_fraction))
    if sky:
        sources.append({
            "id": "stellarium-modern",
            "name": "Stellarium sky culture \"modern\" (88 IAU figures + IAU boundaries)",
            "url": "https://github.com/Stellarium/stellarium/tree/master/skycultures/modern",
            "licence": "CC BY-SA 4.0", "licenceTag": "by-sa",
            "attribution": "Constellation figures and boundaries: Stellarium sky culture "
                           "\"modern\" (Stellarium's team), CC BY-SA 4.0 - "
                           "https://creativecommons.org/licenses/by-sa/4.0/ ; Constellation "
                           "boundary data (c) 2012-2022 Pierre Barbier, CC BY-SA 4.0"})

    print(f"stars: {len(stars)}")
    for row in bridge_report:
        print(f"  bridge {row['stage']:22s} [{row['licenceTag']:7s}] "
              f"{'+' + str(row['resolved']) if row['used'] else row.get('reason', 'skipped')}")
    print(f"figures: {figure_stats['figuresShipped']}/{figure_stats['figuresInSource']}, "
          f"segments {kept_seg}/{total_seg} "
          f"({100 * figure_stats['segmentFraction']:.1f}%), "
          f"HIP resolved {figure_stats['hipResolved']}/{distinct} "
          f"({100 * figure_stats['hipFraction']:.1f}%)")

    written = write_outputs(stars, args.out, MAGIC, meta_extra, figures, figure_stats,
                            bridge_report, sources, profile, dry_run=args.dry_run)
    for name, size in written.items():
        print(f"wrote {name}: {size} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
