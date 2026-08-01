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


#: Past this flattening the oblate-spheroid model is not describing the body, and the numbers
#: that fed it were not measuring one either. Mars is 0.0059 and Saturn, the flattest thing
#: here, is 0.098; the small irregulars come out at 0.2 to 0.7 because Horizons writes their
#: shape as "Radius (km) = 13.1 x 11.1 x 9.3" and the scraper reads the longest semi-axis.
#: Those get left spherical, which is at least a stated assumption rather than a derivation
#: from a quantity that does not mean what the formula needs it to mean.
MAX_DERIVED_FLATTENING = 0.15


def _polar_radius(equatorial_radius: float, mean_radius: Optional[float],
                  published_polar: Optional[float] = None,
                  published_flattening: Optional[float] = None):
    """Polar radius and its provenance, from what Horizons actually publishes.

    **Where Horizons publishes one, use it.** It does for the four giant planets, and this used
    to ignore it completely: the scraper collected ``polarRadiusKm`` and the caller never read
    the value, choosing between the mean and the equatorial radius instead. Combined with the
    equatorial pattern not matching "Equat. radius", that made Jupiter 69911 x 69911 km — a
    perfect sphere, when its real flattening is 0.065 and is one of the most visible things
    about it. The published pair is 71492 x 66854.

    **Horizons does not publish a polar radius for most bodies, including Mars.** It publishes
    an equatorial radius and a *volumetric mean* radius, and this used to take the mean as the
    polar one whenever it was the smaller of the two — and label it Measured. Mars shipped with
    a polar radius of 3389.92 km, which is its volumetric mean; the IAU value is 3376.2, so the
    figure was 13.7 km out and claimed a source that did not say it. Earth shipped at 6371.01
    against a true 6356.75, out by 14.3 km.

    The mean radius does carry the information, though, because it is defined as the radius of
    a sphere of equal volume: R_vol = (a^2 c)^(1/3), so c = R_vol^3 / a^2 exactly. That gives
    6356.78 for Earth — 28 metres from the WGS 84 value — and 3377.4 for Mars, 1.2 km from the
    IAU figure, the residual being that Mars is triaxial and not quite the spheroid this
    assumes. Both are an order of magnitude better than what they replace, and both are
    *derived*, so they are labelled Inferred and say what they were derived from.

    Note that this leaves every volumetric quantity untouched: the mean radius reconstructed
    from the new pair is the one it came from, by construction. Only the flattening and the
    surface-point radius change, which are precisely the two things that were wrong.
    """
    if published_polar and 0.0 < published_polar <= equatorial_radius:
        return published_polar, measured("JPL Horizons body constants")

    # Horizons publishes a flattening for Earth and Mars even though it publishes no polar
    # radius for either, and c = a(1 - f) is that same measurement rearranged rather than a
    # model of anything. It is also better than the volumetric derivation below: Earth comes
    # out at 6356.752 km, the WGS 84 value exactly, against 6356.780 derived, and Mars at
    # 3376.19 against the IAU 3376.20 — where the derivation gives 3377.4, because Mars is
    # triaxial and the derivation assumes it is not.
    if published_flattening and 0.0 < published_flattening < MAX_DERIVED_FLATTENING:
        return equatorial_radius * (1.0 - published_flattening), measured("JPL Horizons body constants")

    if not mean_radius or mean_radius >= equatorial_radius:
        return equatorial_radius, inferred("assumed spherical: no separate polar radius published")

    derived = mean_radius ** 3 / equatorial_radius ** 2
    flattening = (equatorial_radius - derived) / equatorial_radius

    if flattening > MAX_DERIVED_FLATTENING:
        return equatorial_radius, inferred(
            f"assumed spherical: the published equatorial and volumetric mean radii imply a "
            f"flattening of {flattening:.2f}, which means the body is irregular rather than an "
            "oblate spheroid, so no polar radius can be derived from them")

    return derived, inferred(
        "derived from the published equatorial and volumetric mean radii assuming an oblate "
        "spheroid, c = Rvol^3 / a^2; JPL Horizons publishes no polar radius for this body")


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
    published_equatorial = constants.get("equatorialRadiusKm")
    equatorial_radius = published_equatorial or mean_radius

    # A body whose radii came from the override table did not get them from Horizons, and its
    # provenance must not say it did — those four are overridden precisely because Horizons'
    # prose for them cannot be parsed safely.
    radius_source = (measured(override["_source"], "") if override and "equatorialRadiusKm" in override
                     else measured("JPL Horizons body constants"))

    if equatorial_radius:
        record["equatorialRadiusKm"] = equatorial_radius
        # Falling back to the mean radius is a decision, not a measurement, and saying
        # "JPL Horizons body constants" for it claims a source that published something else.
        provenance["equatorialRadiusKm"] = (
            radius_source if published_equatorial
            else inferred("no equatorial radius published; the volumetric mean radius is used "
                          "and the body is treated as a sphere"))

        polar_radius, polar_provenance = _polar_radius(
            equatorial_radius, mean_radius, constants.get("polarRadiusKm"),
            constants.get("flattening"))
        if polar_provenance["class"] == "Measured":
            polar_provenance = radius_source
        record["polarRadiusKm"] = polar_radius
        provenance["polarRadiusKm"] = polar_provenance

        # Where Horizons states the flattening as well, it is an independent check on the pair
        # above — a different field, printed by the same source, that must agree with them.
        # This is the assertion that would have caught the giants being perfect spheres.
        published_flattening = constants.get("flattening")
        if published_flattening and equatorial_radius > 0.0:
            derived = (equatorial_radius - polar_radius) / equatorial_radius
            if abs(derived - published_flattening) > 1e-4:
                print(f"    ! {identifier}: radii imply a flattening of {derived:.5f} but Horizons "
                      f"publishes {published_flattening:.5f}", flush=True)

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
