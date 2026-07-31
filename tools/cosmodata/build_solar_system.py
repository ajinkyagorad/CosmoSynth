#!/usr/bin/env python3
"""Build the shipped Solar System catalogue from JPL Horizons.

This is the offline half of the data architecture. Horizons is queried once, here, and the
result is written as a compact catalogue that the Unity application loads with no network
access at all — which is what makes the app work on a headset, in a classroom, and on a
plane, and what keeps JPL's service from being hit once per frame.

Every value written carries provenance:

  Measured   - taken directly from a JPL/NASA product, with the citation attached
  Inferred   - derived from measurements through a stated model
  Procedural - synthesised by us to fill a gap
  Fictional  - invented

The orbits written here are osculating Keplerian elements, which are an approximation to the
real perturbed motion, not the truth. How large that approximation is, per body, is measured
in tests/CosmoSynth.Core.Tests/HorizonsValidationTests.cs and documented in docs/accuracy.md.

Run:  python3 tools/cosmodata/build_solar_system.py
Out:  unity/Assets/StreamingAssets/CosmoSynth/catalog/solar_system.json
"""

from __future__ import annotations

import argparse
import os
import sys
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bodies as body_table  # noqa: E402
import horizons  # noqa: E402

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_OUTPUT = os.path.join(
    REPO_ROOT, "unity", "Assets", "StreamingAssets", "CosmoSynth", "catalog", "solar_system.json"
)

EPOCH = "2026-01-01"

#: Horizons rejects a zero-length interval, so a single-epoch query asks for one day and
#: keeps only the first record.
EPOCH_STOP = "2026-01-02"


J2000_JD = 2451545.0
SECONDS_PER_DAY = 86400.0

HORIZONS_URL = "https://ssd.jpl.nasa.gov/horizons/"
RETRIEVED = datetime.now(timezone.utc).strftime("%Y-%m-%d")


def measured(source: str, reference: str = HORIZONS_URL, uncertainty: Optional[float] = None) -> Dict[str, Any]:
    entry = {"class": "Measured", "source": source, "reference": reference, "retrieved": RETRIEVED}
    if uncertainty is not None:
        entry["uncertainty"] = uncertainty
    return entry


def inferred(source: str, reference: str = "") -> Dict[str, Any]:
    entry = {"class": "Inferred", "source": source, "retrieved": RETRIEVED}
    if reference:
        entry["reference"] = reference
    return entry


def seconds_past_j2000(julian_date_tdb: float) -> float:
    return (julian_date_tdb - J2000_JD) * SECONDS_PER_DAY


def build_body(entry: Dict[str, Any], delay: float) -> Optional[Dict[str, Any]]:
    """Fetch one body and assemble its catalogue record."""
    identifier = entry["id"]
    command = entry["command"]

    constants = horizons.fetch_body_constants(command)
    horizons.polite_sleep(delay)

    # Published values take precedence where Horizons' prose cannot be parsed unambiguously.
    override = body_table.OVERRIDES.get(identifier)
    if override:
        for key, value in override.items():
            if not key.startswith("_"):
                constants[key] = value

    record: Dict[str, Any] = {
        "id": identifier,
        "name": entry["name"],
        "type": entry["type"],
        "designations": entry.get("designations", []),
    }
    provenance: Dict[str, Any] = {}

    if entry.get("parent"):
        record["parent"] = entry["parent"]

    # --- Gravitational parameter -------------------------------------------------
    gm = constants.get("gmKm3S2")
    if gm is None:
        # Horizons prints the constants block as prose and does not always include GM for
        # small bodies. Refusing to guess is the point: a fabricated mass would propagate
        # silently into every orbit downstream.
        print(f"    ! no GM published for {identifier}; skipping", flush=True)
        return None

    record["gravitationalParameter"] = gm
    if override:
        provenance["gravitationalParameter"] = (
            inferred(override["_source"]) if override.get("_inferred") else measured(override["_source"], "")
        )
    else:
        provenance["gravitationalParameter"] = measured("JPL Horizons body constants (DE440)")

    # --- Radii --------------------------------------------------------------------
    mean_radius = constants.get("meanRadiusKm")
    equatorial_radius = constants.get("equatorialRadiusKm") or mean_radius

    if equatorial_radius:
        record["equatorialRadiusKm"] = equatorial_radius
        # Where only a mean radius is published, treating the body as a sphere is an
        # assumption, not a measurement, so it is labelled as inferred.
        record["polarRadiusKm"] = mean_radius if mean_radius and mean_radius < equatorial_radius else equatorial_radius
        provenance["equatorialRadiusKm"] = measured("JPL Horizons body constants")
        provenance["polarRadiusKm"] = (
            measured("JPL Horizons body constants")
            if mean_radius and mean_radius < equatorial_radius
            else inferred("assumed spherical: no separate polar radius published")
        )

    # --- Albedo -------------------------------------------------------------------
    if "bondAlbedo" in entry:
        record["bondAlbedo"] = entry["bondAlbedo"]
        provenance["bondAlbedo"] = measured("NASA planetary fact sheets",
                                            "https://nssdc.gsfc.nasa.gov/planetary/factsheet/")
    if constants.get("geometricAlbedo") is not None:
        record["geometricAlbedo"] = constants["geometricAlbedo"]
        provenance["geometricAlbedo"] = measured("JPL Horizons body constants")

    # --- Star properties ------------------------------------------------------------
    for field in ("luminosityWatts", "effectiveTemperatureK", "spectralType"):
        if field in entry:
            record[field] = entry[field]
            provenance[field] = measured("IAU 2015 Resolution B3 nominal solar values")

    # --- Orbit ----------------------------------------------------------------------
    if entry.get("parent"):
        elements = horizons.fetch_elements(
            body=command,
            center=entry["center"],
            start_time=EPOCH,
            stop_time=EPOCH_STOP,
            step_size="1d",
            ref_plane=horizons.REF_PLANE_ICRF,   # ICRF: the frame the whole engine works in
        )
        horizons.polite_sleep(delay)

        first = elements[0]
        values = first.values
        semi_major_axis = values["A"]
        eccentricity = values["EC"]

        record["orbit"] = {
            # Store the semi-latus rectum, which stays finite for every conic.
            "semiLatusRectumKm": semi_major_axis * (1.0 - eccentricity * eccentricity),
            "eccentricity": eccentricity,
            "inclinationDeg": values["IN"],
            "longitudeOfAscendingNodeDeg": values["OM"],
            "argumentOfPeriapsisDeg": values["W"],
            "meanAnomalyAtEpochDeg": values["MA"],
            "epochSecondsPastJ2000": seconds_past_j2000(first.julian_date_tdb),
            # The relative two-body problem is governed by the sum of the two masses.
            "gravitationalParameter": None,   # filled in once the parent's GM is known
        }
        record["_orbitCenterGmNeeded"] = entry["parent"]
        record["_selfGm"] = gm

        provenance["orbit"] = measured(
            f"JPL Horizons osculating elements at {EPOCH} TDB, ICRF/J2000, centre {entry['center']}")

    # --- Rotation -------------------------------------------------------------------
    if entry.get("orientation"):
        record["_orientationPreset"] = entry["orientation"]
        provenance["orientation"] = measured(
            "IAU WGCCRE 2015 report (Archinal et al. 2018)",
            "https://doi.org/10.1007/s10569-017-9805-5")

    # --- Atmosphere -----------------------------------------------------------------
    atmosphere = body_table.ATMOSPHERES.get(identifier)
    if atmosphere:
        cleaned = {key: value for key, value in atmosphere.items() if not key.startswith("_")}
        record["atmosphere"] = cleaned
        provenance["atmosphere"] = measured(atmosphere["_source"])
        # The scattering coefficients are tuned to look right, not retrieved from a radiative
        # transfer code, and must not claim to be measurements.
        provenance["atmosphere.scattering"] = inferred(
            "Rayleigh/Mie coefficients chosen to reproduce the observed sky colour")

    # --- Display --------------------------------------------------------------------
    colour = body_table.DISPLAY_COLORS.get(identifier)
    if colour:
        record["displayColor"] = colour
        provenance["displayColor"] = inferred("approximate integrated disc colour from spacecraft imagery")

    # How far these Keplerian elements drift from the real, perturbed ephemeris is measured
    # properly in tests/CosmoSynth.Core.Tests/HorizonsValidationTests.cs, which propagates
    # against 260 archived state vectors and prints the residual per body. It is not
    # re-measured here, because doing it well needs the propagator, and doing it badly here
    # would put a number in the catalogue that does not mean what its name suggests.

    record["provenance"] = provenance
    return record


def resolve_orbit_gravitational_parameters(records: List[Dict[str, Any]]) -> None:
    """Fill in each orbit's GM as the parent's plus the body's own."""
    by_id = {record["id"]: record for record in records}

    for record in records:
        parent_id = record.pop("_orbitCenterGmNeeded", None)
        self_gm = record.pop("_selfGm", None)
        if parent_id is None or "orbit" not in record:
            continue

        parent = by_id.get(parent_id)
        if parent is None:
            raise SystemExit(f"body '{record['id']}' names a parent '{parent_id}' that was not imported")

        record["orbit"]["gravitationalParameter"] = parent["gravitationalParameter"] + (self_gm or 0.0)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default=DEFAULT_OUTPUT)
    parser.add_argument("--delay", type=float, default=0.4, help="seconds between Horizons requests")
    parser.add_argument("--only", help="comma-separated body ids, for a quick partial run")
    args = parser.parse_args()

    selected = body_table.BODIES
    if args.only:
        wanted = {name.strip() for name in args.only.split(",")}
        selected = [entry for entry in selected if entry["id"] in wanted]

    records: List[Dict[str, Any]] = []
    skipped: List[str] = []

    for index, entry in enumerate(selected):
        print(f"[{index + 1}/{len(selected)}] {entry['name']} ... ", end="", flush=True)
        try:
            record = build_body(entry, args.delay)
        except horizons.HorizonsError as exc:
            print(f"FAILED\n    {exc}", flush=True)
            skipped.append(entry["id"])
            continue

        if record is None:
            skipped.append(entry["id"])
            continue

        records.append(record)
        radius = record.get("equatorialRadiusKm")
        print(f"GM={record['gravitationalParameter']:.6g}"
              + (f", R={radius:.1f} km" if radius else ", radius unknown"), flush=True)

    resolve_orbit_gravitational_parameters(records)

    catalog = {
        "name": "Solar System",
        "source": (
            f"JPL Horizons ({HORIZONS_URL}), osculating elements at {EPOCH} TDB in ICRF/J2000. "
            "Rotational elements from the IAU WGCCRE 2015 report. Atmospheric parameters from "
            "NASA planetary fact sheets and mission results. Generated by "
            "tools/cosmodata/build_solar_system.py — do not hand-edit."
        ),
        "generated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "bodies": records,
    }

    os.makedirs(os.path.dirname(args.output), exist_ok=True)
    horizons.dump_json(args.output, catalog)

    size_kb = os.path.getsize(args.output) / 1024.0
    print(f"\nWrote {args.output}  ({size_kb:.1f} kB)")
    print(f"  {len(records)} bodies imported")
    if skipped:
        print(f"  {len(skipped)} skipped: {', '.join(skipped)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
