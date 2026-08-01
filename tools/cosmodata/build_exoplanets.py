#!/usr/bin/env python3
"""Build the exoplanet catalogue from the NASA Exoplanet Archive.

Queries the `pscomppars` table — one row per confirmed planet, holding the best available
value for each parameter drawn from the peer-reviewed literature — and turns it into a
CosmoSynth catalogue with host stars at their real positions.

THE POINT OF THIS FILE IS WHAT IT REFUSES TO INVENT.

For most exoplanets we know a handful of quantities and nothing else. A transit gives a radius
and a period; a radial-velocity detection gives a minimum mass. Almost nothing gives a
composition, an atmosphere, a rotation rate, a colour, or a moon. So every field is tagged:

    Measured   the archive has a published value, with the citation
    Inferred   derived from measured values through a stated physical relation
    Procedural synthesised because nothing constrains it, deterministic from a seed
    (absent)   reported as Unknown, never quietly defaulted

That distinction is the whole reason this project exists, and an exoplanet is where it bites
hardest — a rendered world looks equally convincing whether its radius was measured to four
digits or its continents were invented wholesale.

Run:  python3 tools/cosmodata/build_exoplanets.py
Out:  unity/Assets/StreamingAssets/CosmoSynth/catalog/exoplanets.json
"""

from __future__ import annotations

import argparse
import csv
import io
import json
import math
import os
import re
import sys
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_OUTPUT = os.path.join(
    REPO_ROOT, "unity", "Assets", "StreamingAssets", "CosmoSynth", "catalog", "exoplanets.json")

TAP_URL = "https://exoplanetarchive.ipac.caltech.edu/TAP/sync"
ARCHIVE_REFERENCE = "https://exoplanetarchive.ipac.caltech.edu/"
ARCHIVE_SOURCE = "NASA Exoplanet Archive (pscomppars)"

# Physical constants, matching CosmoSynth.Core.Constants exactly.
GM_SUN = 1.32712440041279419e11      # km^3/s^2
GM_EARTH = 3.98600435507e5
AU_KM = 149597870.700
PARSEC_KM = 3.0856775814913673e13
SOLAR_RADIUS_KM = 695700.0
EARTH_RADIUS_KM = 6378.1366
SOLAR_LUMINOSITY_W = 3.828e26
SECONDS_PER_DAY = 86400.0

COLUMNS = [
    "pl_name", "hostname", "ra", "dec", "sy_dist",
    "pl_orbsmax", "pl_orbper", "pl_orbeccen", "pl_orbincl",
    "pl_rade", "pl_bmasse", "pl_eqt",
    "st_teff", "st_rad", "st_mass", "st_lum", "st_spectype",
    "discoverymethod", "disc_year", "sy_pnum",
]

RETRIEVED = datetime.now(timezone.utc).strftime("%Y-%m-%d")


def measured(detail: str = "") -> Dict[str, Any]:
    entry = {"class": "Measured", "source": ARCHIVE_SOURCE,
             "reference": ARCHIVE_REFERENCE, "retrieved": RETRIEVED}
    if detail:
        entry["source"] = f"{ARCHIVE_SOURCE} — {detail}"
    return entry


def inferred(relation: str) -> Dict[str, Any]:
    return {"class": "Inferred", "source": relation, "retrieved": RETRIEVED}


def procedural(relation: str, seed: int) -> Dict[str, Any]:
    return {"class": "Procedural", "source": relation, "reference": f"seed:{seed}"}


def query(where: str, limit: int) -> List[Dict[str, str]]:
    adql = f"select top {limit} {','.join(COLUMNS)} from pscomppars where {where}"
    url = f"{TAP_URL}?{urllib.parse.urlencode({'query': adql, 'format': 'csv'})}"

    with urllib.request.urlopen(url, timeout=180) as response:
        text = response.read().decode("utf-8", errors="replace")

    if not text.lstrip().startswith("pl_name"):
        raise SystemExit(f"Archive returned something unexpected:\n{text[:600]}")

    return list(csv.DictReader(io.StringIO(text)))


def number(row: Dict[str, str], key: str) -> Optional[float]:
    """Parse a column, returning None for the archive's many blanks."""
    raw = (row.get(key) or "").strip()
    if not raw:
        return None
    try:
        value = float(raw)
    except ValueError:
        return None
    return None if math.isnan(value) else value


def slug(text: str) -> str:
    """Machine id from a name.

    Lower-cased, which loses a real astronomical distinction: by IAU convention an uppercase
    trailing letter is a *stellar* component and a lowercase one is a *planet*, so "55 Cnc B"
    (a red dwarf companion) and "55 Cnc b" (a planet) are different objects that slug
    identically. Preserving case would not help either — the catalogue's lookup is
    case-insensitive by design, since users type body names by hand.

    So the case is encoded explicitly instead: a trailing uppercase component gets a "-comp"
    suffix, which survives case-insensitive comparison and reads as what it is.
    """
    base = re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")

    parts = text.strip().split()
    if len(parts) > 1 and len(parts[-1]) == 1 and parts[-1].isupper():
        return f"{base}-comp"
    return base


def stable_seed(text: str) -> int:
    """FNV-1a, matching CosmoSynth.Core.Hash.HashString's stability guarantee."""
    value = 0xCBF29CE484222325
    for ch in text:
        value ^= ord(ch)
        value = (value * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF
    return value & 0x7FFFFFFF


def build_star(hostname: str, row: Dict[str, str]) -> Optional[Dict[str, Any]]:
    """One host star, positioned from its measured right ascension, declination and distance."""
    ra, dec, distance_pc = number(row, "ra"), number(row, "dec"), number(row, "sy_dist")
    if ra is None or dec is None or distance_pc is None or distance_pc <= 0:
        return None   # cannot place it; refusing rather than guessing a position

    distance_km = distance_pc * PARSEC_KM
    ra_rad, dec_rad = math.radians(ra), math.radians(dec)
    cos_dec = math.cos(dec_rad)

    # ICRF Cartesian, the frame the whole engine works in.
    position = [
        cos_dec * math.cos(ra_rad) * distance_km,
        cos_dec * math.sin(ra_rad) * distance_km,
        math.sin(dec_rad) * distance_km,
    ]

    star_id = f"exo-{slug(hostname)}"
    seed = stable_seed(star_id)
    provenance: Dict[str, Any] = {"positionKm": measured("ra, dec and sy_dist")}

    mass_solar = number(row, "st_mass")
    radius_solar = number(row, "st_rad")
    teff = number(row, "st_teff")
    log_luminosity = number(row, "st_lum")

    star: Dict[str, Any] = {
        "id": star_id,
        "name": hostname,
        "type": "Star",
        "designations": [hostname],
        "positionKm": position,
    }

    if mass_solar and mass_solar > 0:
        star["gravitationalParameter"] = GM_SUN * mass_solar
        provenance["gravitationalParameter"] = measured("st_mass")
    else:
        # No published mass. A main-sequence star's mass follows from its temperature closely
        # enough to propagate an orbit; saying so is the point.
        mass_solar = 1.0 if not teff else max(0.08, min(3.0, (teff / 5772.0) ** 1.6))
        star["gravitationalParameter"] = GM_SUN * mass_solar
        provenance["gravitationalParameter"] = inferred(
            "main-sequence mass-temperature relation; st_mass not published")

    if radius_solar and radius_solar > 0:
        star["equatorialRadiusKm"] = SOLAR_RADIUS_KM * radius_solar
        star["polarRadiusKm"] = SOLAR_RADIUS_KM * radius_solar
        provenance["equatorialRadiusKm"] = measured("st_rad")
    else:
        radius_solar = mass_solar ** 0.8 if mass_solar < 1.0 else mass_solar ** 0.57
        star["equatorialRadiusKm"] = SOLAR_RADIUS_KM * radius_solar
        star["polarRadiusKm"] = SOLAR_RADIUS_KM * radius_solar
        provenance["equatorialRadiusKm"] = inferred(
            "main-sequence mass-radius relation; st_rad not published")

    if log_luminosity is not None:
        # st_lum is log10(L / L_sun).
        star["luminosityWatts"] = SOLAR_LUMINOSITY_W * (10.0 ** log_luminosity)
        provenance["luminosityWatts"] = measured("st_lum")
    elif teff and radius_solar:
        # Stefan-Boltzmann from the measured radius and temperature.
        star["luminosityWatts"] = SOLAR_LUMINOSITY_W * (radius_solar ** 2) * ((teff / 5772.0) ** 4)
        provenance["luminosityWatts"] = inferred("Stefan-Boltzmann from st_rad and st_teff")
    else:
        star["luminosityWatts"] = SOLAR_LUMINOSITY_W * (mass_solar ** 3.5)
        provenance["luminosityWatts"] = inferred("mass-luminosity relation; st_lum not published")

    if teff:
        star["effectiveTemperatureK"] = teff
        provenance["effectiveTemperatureK"] = measured("st_teff")

    spectral = (row.get("st_spectype") or "").strip()
    if spectral:
        star["spectralType"] = spectral
        provenance["spectralType"] = measured("st_spectype")

    # Colour follows from temperature by Planck's law — physics, not invention.
    if teff:
        star["displayColor"] = black_body_rgb(teff)
        provenance["displayColor"] = inferred("Planck black-body colour at st_teff")

    star["proceduralSeed"] = seed
    star["provenance"] = provenance
    star["_distancePc"] = distance_pc
    return star


def black_body_rgb(temperature_k: float) -> List[float]:
    """Normalised linear RGB from Planck's law, matching the C# generator."""
    def radiance(wavelength_m: float) -> float:
        h, c, k = 6.62607015e-34, 299792458.0, 1.380649e-23
        exponent = h * c / (wavelength_m * k * temperature_k)
        if exponent > 700:
            return 0.0
        denominator = (wavelength_m ** 5) * (math.exp(exponent) - 1.0)
        return (2 * h * c * c) / denominator if denominator > 0 else 0.0

    r, g, b = radiance(600e-9), radiance(550e-9), radiance(450e-9)
    peak = max(r, g, b)
    return [1.0, 1.0, 1.0] if peak <= 0 else [r / peak, g / peak, b / peak]


def build_planet(row: Dict[str, str], star: Dict[str, Any]) -> Optional[Dict[str, Any]]:
    """One planet, with every unmeasured property labelled as such."""
    name = (row.get("pl_name") or "").strip()
    if not name:
        return None

    planet_id = f"exo-{slug(name)}"
    seed = stable_seed(planet_id)
    provenance: Dict[str, Any] = {}

    mass_earth = number(row, "pl_bmasse")
    radius_earth = number(row, "pl_rade")
    semi_major_au = number(row, "pl_orbsmax")
    period_days = number(row, "pl_orbper")
    eccentricity = number(row, "pl_orbeccen")
    inclination = number(row, "pl_orbincl")
    equilibrium_k = number(row, "pl_eqt")

    star_gm = star["gravitationalParameter"]

    # --- Orbit: need a semi-major axis, from measurement or from the period ---
    if semi_major_au and semi_major_au > 0:
        semi_major_km = semi_major_au * AU_KM
        orbit_provenance = measured("pl_orbsmax")
    elif period_days and period_days > 0:
        # Kepler's third law. This is a derivation from two measured values, not a guess.
        period_s = period_days * SECONDS_PER_DAY
        semi_major_km = (star_gm * period_s ** 2 / (4 * math.pi ** 2)) ** (1.0 / 3.0)
        orbit_provenance = inferred("Kepler's third law from pl_orbper and the stellar mass")
    else:
        return None   # nothing to place it on; skip rather than invent an orbit

    # --- Mass, cross-checked against the radius for physical possibility ---
    #
    # pscomppars is a COMPOSITE table: the best radius may come from a transit paper and the
    # best mass from an unrelated radial-velocity paper, and the two are not guaranteed to be
    # consistent. LP 890-9 c is listed at 1.367 Earth radii with 25.3 Earth masses, which is a
    # mean density of 54 000 kg/m^3 — denser than osmium, and impossible. Usually the mass is
    # really an upper limit that was never intended to be paired with that radius.
    #
    # Where the pair is impossible the radius is kept and the mass re-derived from it, because
    # a transit radius is far better constrained than an RV mass for a small planet. The
    # override is recorded in the provenance rather than applied silently.
    mass_rejected = False
    if mass_earth and radius_earth and mass_earth > 0 and radius_earth > 0:
        volume_m3 = (4.0 / 3.0) * math.pi * (radius_earth * EARTH_RADIUS_KM * 1000.0) ** 3
        density = (mass_earth * 5.9722e24) / volume_m3
        if density > 30000.0:
            mass_rejected = True
            rejected_mass, rejected_density = mass_earth, density
            mass_earth = None

    if mass_earth and mass_earth > 0:
        planet_gm = GM_EARTH * mass_earth
        mass_provenance = measured("pl_bmasse")
    elif radius_earth and radius_earth > 0:
        # Invert the mass-radius relation. Real scatter here is large — a factor of a few for
        # anything above about two Earth radii — so this is inferred, not measured.
        mass_earth = radius_earth ** 3.7 if radius_earth < 1.5 else radius_earth ** 2.06
        planet_gm = GM_EARTH * mass_earth
        mass_provenance = inferred(
            f"mass-radius relation; published pl_bmasse of {rejected_mass:.1f} Earth masses "
            f"rejected as physically impossible with pl_rade ({rejected_density:,.0f} kg/m^3)"
            if mass_rejected else "mass-radius relation; pl_bmasse not published")
    else:
        return None   # no mass and no radius: not enough to be a body

    planet: Dict[str, Any] = {
        "id": planet_id,
        "name": name,
        "type": "Planet",
        "parent": star["id"],
        "designations": [name],
        "gravitationalParameter": planet_gm,
        "proceduralSeed": seed,
    }
    provenance["gravitationalParameter"] = mass_provenance

    if radius_earth and radius_earth > 0:
        radius_km = radius_earth * EARTH_RADIUS_KM
        provenance["equatorialRadiusKm"] = measured("pl_rade")
    else:
        radius_earth = mass_earth ** 0.27 if mass_earth < 2 else mass_earth ** 0.5
        radius_km = radius_earth * EARTH_RADIUS_KM
        provenance["equatorialRadiusKm"] = inferred("mass-radius relation; pl_rade not published")

    planet["equatorialRadiusKm"] = radius_km
    # No exoplanet has a measured oblateness. Treating it as a sphere is an assumption.
    planet["polarRadiusKm"] = radius_km
    provenance["polarRadiusKm"] = inferred("assumed spherical; no oblateness is measured for any exoplanet")

    # Eccentricity and inclination are frequently unpublished; a circular, edge-on orbit is the
    # convention, and it is stated rather than implied.
    if eccentricity is not None and 0.0 <= eccentricity < 1.0:
        provenance["orbit"] = orbit_provenance
    else:
        eccentricity = 0.0
        provenance["orbit"] = inferred(
            f"{orbit_provenance['source']}; eccentricity not published, assumed circular")

    inclination_deg = inclination if inclination is not None else 90.0

    planet["orbit"] = {
        "semiLatusRectumKm": semi_major_km * (1.0 - eccentricity ** 2),
        "eccentricity": eccentricity,
        "inclinationDeg": inclination_deg,
        # The orientation of the orbit on the sky is not measured for the overwhelming majority
        # of planets. Deriving it from the seed keeps the system reproducible and is invention.
        "longitudeOfAscendingNodeDeg": (seed % 360000) / 1000.0,
        "argumentOfPeriapsisDeg": ((seed // 7) % 360000) / 1000.0,
        "meanAnomalyAtEpochDeg": ((seed // 13) % 360000) / 1000.0,
        "epochSecondsPastJ2000": 0.0,
        "gravitationalParameter": star_gm + planet_gm,
    }
    provenance["orbit.orientation"] = procedural(
        "node, periapsis and phase are unmeasured for almost every exoplanet", seed)

    if equilibrium_k:
        provenance["equilibriumTemperatureK"] = measured("pl_eqt")
        planet["_equilibriumTemperatureK"] = equilibrium_k

    method = (row.get("discoverymethod") or "").strip()
    year = (row.get("disc_year") or "").strip()
    if method:
        planet["designations"].append(f"{method} {year}".strip())

    # Everything about the surface is invented. Say so once, plainly.
    provenance["surface"] = procedural(
        "no exoplanet has a resolved surface; terrain, colour and ocean are generated", seed)
    provenance["atmosphere"] = procedural(
        "atmospheric composition is measured for only a handful of planets, none of them here", seed)

    planet["provenance"] = provenance
    return planet


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default=DEFAULT_OUTPUT)
    parser.add_argument("--max-distance-pc", type=float, default=60.0,
                        help="only import systems within this distance (parsecs)")
    parser.add_argument("--limit", type=int, default=4000)
    parser.add_argument("--max-systems", type=int, default=60,
                        help="keep only the nearest N host systems")
    args = parser.parse_args()

    where = (f"sy_dist is not null and sy_dist < {args.max_distance_pc} "
             f"and ra is not null and dec is not null")
    print(f"querying the NASA Exoplanet Archive: systems within {args.max_distance_pc} pc ...")
    rows = query(where, args.limit)
    print(f"  {len(rows)} planet rows returned")

    stars: Dict[str, Dict[str, Any]] = {}
    planets: List[Dict[str, Any]] = []
    seen_ids: set = set()
    skipped = 0

    for row in rows:
        hostname = (row.get("hostname") or "").strip()
        if not hostname:
            skipped += 1
            continue

        star = stars.get(hostname)
        if star is None:
            star = build_star(hostname, row)
            if star is None:
                skipped += 1
                continue
            stars[hostname] = star

        planet = build_planet(row, star)
        if planet is None:
            skipped += 1
            continue

        # A duplicate id would make the catalogue unloadable, so it is caught here rather than
        # discovered by the parser. Any collision that survives the slug rule is a naming case
        # worth knowing about, so it is reported rather than silently renamed.
        if planet["id"] in seen_ids:
            print(f"    ! duplicate id {planet['id']!r} from {planet['name']!r}; skipping")
            skipped += 1
            continue
        seen_ids.add(planet["id"])
        planets.append(planet)

    # Drop stars that ended up with no usable planet.
    # Keep only the nearest N systems.
    #
    # Not a data limitation — a rendering one, and worth being plain about. Every body currently
    # gets its own GameObject, mesh and material, so 470 host stars plus 753 planets is over
    # twelve hundred of them; loading that killed the process outright. Distant systems should
    # be points in the star field until you travel to one, and that path is not built yet. Sixty
    # systems is the solar neighbourhood out to roughly 15 parsecs, which is what you would
    # actually visit, and it runs.
    ranked = sorted(stars.values(), key=lambda s: s.get("_distancePc") or 1e9)
    keep = {s["id"] for s in ranked[:args.max_systems]}
    planets = [p for p in planets if p["parent"] in keep]

    used = {p["parent"] for p in planets}

    # Summarise before stripping the helper field, not after.
    nearest = sorted(
        ((s["name"], s.get("_distancePc") or 1e9) for s in stars.values() if s["id"] in used),
        key=lambda pair: pair[1])[:8]

    bodies = [s for s in stars.values() if s["id"] in used] + planets
    for body in bodies:
        body.pop("_distancePc", None)

    catalog = {
        "name": "Exoplanets",
        "source": (
            f"{ARCHIVE_SOURCE} — confirmed planets within {args.max_distance_pc} pc, queried "
            f"{RETRIEVED}. Host stars are placed at their measured right ascension, declination "
            "and distance in ICRF. Generated by tools/cosmodata/build_exoplanets.py."),
        "generated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "bodies": bodies,
    }

    os.makedirs(os.path.dirname(args.output), exist_ok=True)
    with open(args.output, "w", encoding="utf-8") as handle:
        # Compact, not pretty-printed. This file ships inside the APK and is downloaded to a
        # headset; indentation cost more than a megabyte for no reader's benefit.
        json.dump(catalog, handle, separators=(",", ":"))
        handle.write("\n")

    size_kb = os.path.getsize(args.output) / 1024.0
    print(f"\nWrote {args.output}  ({size_kb:.0f} kB)")
    print(f"  {len(used)} host stars, {len(planets)} planets, {skipped} rows skipped for missing data")
    print("  nearest systems:")
    for name, distance in nearest:
        print(f"    {name:22} {distance:6.2f} pc  ({(distance or 0) * 3.26156:6.2f} ly)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
