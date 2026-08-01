#!/usr/bin/env python3
"""Build the spacecraft catalogue: real trajectories from JPL, real models from NASA.

Two archives are joined here. JPL Horizons says where each spacecraft is; NASA 3D Resources
says what it looks like. Both are fetched once, offline, and written into StreamingAssets as a
catalogue fragment plus one compact mesh per mission — the application itself never touches
the network, exactly as with the planets.

    python3 tools/cosmodata/build_spacecraft.py
    python3 tools/cosmodata/build_spacecraft.py --only voyager-1,juno --skip-models

Out: unity/Assets/StreamingAssets/CosmoSynth/catalog/spacecraft.json
     unity/Assets/StreamingAssets/CosmoSynth/models/*.csmesh

**Dependencies, and why this one importer has them.** Every other tool in this directory is
standard library only. This one shells out to ``npx @gltf-transform/cli`` because essentially
every NASA model is Draco-compressed, and Draco is an entropy-coded connectivity format whose
decoder is thousands of lines — not something to reimplement here, and not something to get
subtly wrong. The dependency is confined to this step: the .csmesh files it produces are
committed, so building the app, running the tests and using the data need nothing but Python.

**A trap worth stating plainly.** A well-known Python mesh library, handed one of these
models without a Draco decoder available, returns a scene with the right number of meshes and
*exactly* the right triangle count, every vertex at the origin. It reports no error. Triangle
counts therefore prove nothing about whether a model decoded, which is why
``gltf.validate_decoded`` checks that the geometry occupies space and this script refuses to
write a mesh that does not.
"""

from __future__ import annotations

import argparse
import math
import os
import struct
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import gltf  # noqa: E402
import horizons  # noqa: E402
import meshpack  # noqa: E402
import spacecraft as mission_table  # noqa: E402

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
STREAMING = os.path.join(REPO_ROOT, "unity", "Assets", "StreamingAssets", "CosmoSynth")
DEFAULT_CATALOG = os.path.join(STREAMING, "catalog", "spacecraft.json")
DEFAULT_MODEL_DIR = os.path.join(STREAMING, "models")

#: Path recorded in the catalogue, relative to StreamingAssets.
MODEL_STREAMING_PREFIX = "CosmoSynth/models"

J2000_JD = 2451545.0
SECONDS_PER_DAY = 86400.0

HORIZONS_URL = "https://ssd.jpl.nasa.gov/horizons/"
RETRIEVED = datetime.now(timezone.utc).strftime("%Y-%m-%d")

#: Spacecraft are dark; sunlit metal and thermal blanket read as a warm off-white at distance.
DEFAULT_COLOR = [0.72, 0.70, 0.66]


def measured(source: str, reference: str = HORIZONS_URL) -> Dict[str, Any]:
    return {"class": "Measured", "source": source, "reference": reference, "retrieved": RETRIEVED}


def inferred(source: str, reference: str = "") -> Dict[str, Any]:
    entry = {"class": "Inferred", "source": source, "retrieved": RETRIEVED}
    if reference:
        entry["reference"] = reference
    return entry


def seconds_past_j2000(julian_date_tdb: float) -> float:
    return (julian_date_tdb - J2000_JD) * SECONDS_PER_DAY


# --------------------------------------------------------------------------------------
# Models
# --------------------------------------------------------------------------------------


def download(url: str, destination: str) -> bytes:
    """Fetch a URL and return its bytes, refusing anything that is not a glTF binary.

    NASA's asset hosts answer a wrong path with HTTP 200 and an HTML error page, so success
    here is established by the file's magic number, never by the status code.
    """
    request = urllib.request.Request(url, headers={"User-Agent": "CosmoSynth/1.0 (importer)"})
    with urllib.request.urlopen(request, timeout=180) as response:
        data = response.read()

    if data[:4] != b"glTF":
        raise gltf.GltfError(
            f"{url} returned {len(data)} bytes beginning {data[:16]!r}, which is not a .glb. "
            "A 200 response from these hosts is not evidence that the asset exists.")

    with open(destination, "wb") as handle:
        handle.write(data)
    return data


def decompress_draco(source_path: str, destination_path: str) -> None:
    """Expand Draco-compressed geometry into plain glTF buffers, via gltf-transform."""
    command = ["npx", "--yes", "@gltf-transform/cli@latest", "copy", source_path, destination_path]

    try:
        result = subprocess.run(command, capture_output=True, text=True, timeout=900)
    except FileNotFoundError:
        raise gltf.GltfError(
            "npx was not found, and this model is Draco-compressed. Install Node.js, or pass "
            "--skip-models to rebuild the trajectories without touching the meshes.")
    except subprocess.TimeoutExpired:
        raise gltf.GltfError("gltf-transform timed out decompressing the model.")

    if result.returncode != 0 or not os.path.exists(destination_path):
        raise gltf.GltfError(
            f"gltf-transform failed (exit {result.returncode}): "
            f"{(result.stderr or result.stdout or '').strip()[:400]}")


def build_model(mission: mission_table.Mission, model_dir: str, verbose: bool) -> Optional[str]:
    """Download, decode, decimate and write one spacecraft mesh. Returns its file name.

    **A failed build must leave no mesh behind.** This used to write straight to the output
    path, so a mission whose model failed kept whatever was there from the run before — and
    since ``--skip-models`` adopts any ``.csmesh`` it finds by name, the next partial rebuild
    would put that stale file back into the catalogue as though it had just been made. That is
    exactly how the inside-out SOHO nearly shipped after the importer had correctly refused it:
    the refusal was printed, the old file survived, and a later run picked it up.

    It is the same shape as the Draco trap the reader already guards against — a plausible
    artefact that is not what it claims to be — and the same answer applies: prove it, do not
    assume it. The mesh is written to a temporary name alongside the target and moved into
    place only once it is complete, and any failure removes the target outright. Afterwards
    there is either a mesh built by this run or no mesh at all, and never a mesh from a
    previous one.
    """
    os.makedirs(model_dir, exist_ok=True)
    output_name = f"{mission.id}.csmesh"
    output_path = os.path.join(model_dir, output_name)

    try:
        return _build_model_into(mission, output_path, output_name, verbose)
    except BaseException:
        _discard(output_path, mission.id)
        raise


def _discard(output_path: str, mission_id: str) -> None:
    """Remove a mesh that is no longer known to be good, and say so."""
    if not os.path.exists(output_path):
        return
    try:
        os.remove(output_path)
        print(f"\n      ! discarded the previous {os.path.basename(output_path)}: this build "
              "failed, and a mesh nobody has verified is worse than none", flush=True)
    except OSError as exc:
        # Loud, because the whole point of this path is that the file must not survive.
        print(f"\n      !! could not remove the stale {output_path} ({exc}). It will be picked "
              "up by --skip-models and shipped. Delete it by hand.", file=sys.stderr, flush=True)


def _build_model_into(mission: mission_table.Mission, output_path: str, output_name: str,
                      verbose: bool) -> str:
    with tempfile.TemporaryDirectory(prefix="cosmosynth-model-") as scratch:
        raw_path = os.path.join(scratch, "source.glb")
        download(mission.model_url, raw_path)

        # Peek at the required extensions before opening the file properly: the reader refuses
        # Draco by design, and this is the one place that needs to notice it rather than fail.
        if "KHR_draco_mesh_compression" in gltf.required_extensions(raw_path):
            plain_path = os.path.join(scratch, "plain.glb")
            decompress_draco(raw_path, plain_path)
            raw_path = plain_path

        primitives = gltf.GlbFile.from_file(raw_path).primitives()

        # Some published models are wound inside out, which importing correctly cannot fix.
        # Repaired only where the normals and the enclosed volume independently agree that they
        # are; validate_decoded below still refuses anything that comes out inconsistent.
        primitives, repair = gltf.orient_outward(primitives)

        # The check that matters: geometry that parsed but occupies no space never decoded.
        # It also now refuses geometry that decoded facing the wrong way, which counts and
        # measures exactly the same as geometry that did not.
        gltf.validate_decoded(primitives, mission.id)

        mesh = meshpack.build(primitives,
                              triangle_budget=mission.triangle_budget,
                              target_length_metres=mission.length_metres)

        if mesh.triangle_count == 0:
            raise gltf.GltfError(f"{mission.id}: decimation removed every triangle.")

        # Written beside the target rather than in the scratch directory, because os.replace is
        # only atomic within one filesystem and the scratch directory is usually on another.
        staging = output_path + ".partial"
        size = meshpack.write(mesh, staging)
        os.replace(staging, output_path)

    if verbose:
        source_triangles = sum(p.triangle_count for p in primitives)
        print(f"      model {source_triangles} -> {mesh.triangle_count} triangles, "
              f"{len(mesh.submeshes)} submeshes, {size / 1024:.0f} kB"
              + (f"  [{repair}: the published model is inside out]" if repair else ""), flush=True)
    elif repair:
        # Never silent. Changing a model's orientation is a change to published data, and the
        # one place it could hide is a non-verbose build.
        print(f"({repair} for {mission.id}: the published model is inside out) ", end="", flush=True)

    return output_name


# --------------------------------------------------------------------------------------
# Trajectories
# --------------------------------------------------------------------------------------


def build_trajectory(mission: mission_table.Mission, delay: float, verbose: bool) -> Dict[str, Any]:
    """Fetch a mission's state vectors and pack them as a uniformly sampled table."""
    samples = horizons.fetch_vectors(
        body=str(mission.naif_id),
        center=mission.center,
        start_time=mission.start,
        stop_time=mission.stop,
        step_size=mission.step,
        ref_plane=horizons.REF_PLANE_ICRF,
    )
    horizons.polite_sleep(delay)

    if len(samples) < 2:
        raise horizons.HorizonsError(
            f"{mission.id}: Horizons returned {len(samples)} samples; a trajectory needs two.")

    # A table this long means the cadence is fighting the motion — which happens when a
    # sampled placement has been chosen for an orbit that should be using elements. Shipping
    # it would be expensive and, near periapsis, wrong. See spacecraft.py.
    if len(samples) > mission_table.MAX_SAMPLES:
        raise horizons.HorizonsError(
            f"{mission.id}: {len(samples)} samples exceeds the {mission_table.MAX_SAMPLES} cap. "
            "Widen the step, narrow the span, or place this mission with elements instead.")

    epoch = seconds_past_j2000(samples[0].julian_date_tdb)
    end = seconds_past_j2000(samples[-1].julian_date_tdb)
    step = (end - epoch) / (len(samples) - 1)

    # The runtime reconstructs sample times from the epoch and the step rather than storing
    # them, which is only valid if Horizons really did step uniformly. It does, but a calendar
    # step ("1 mo") does not, and silently mis-timing every sample would show up as a
    # trajectory that is subtly the wrong shape rather than as an error.
    for index, sample in enumerate(samples):
        expected = epoch + step * index
        actual = seconds_past_j2000(sample.julian_date_tdb)
        if abs(actual - expected) > max(1.0, abs(step) * 1e-6):
            raise horizons.HorizonsError(
                f"{mission.id}: sample {index} is at {actual:.3f} s but uniform spacing predicts "
                f"{expected:.3f} s. Step '{mission.step}' is not uniform; use hours or days.")

    # Positions to the metre and velocities to the millimetre per second. Horizons prints
    # sixteen digits, most of which describe a spacecraft's position far more precisely than
    # anyone knows it, and every one of them costs a byte in the APK.
    flat: List[float] = []
    for sample in samples:
        flat.extend(round(value, 3) for value in sample.position_km)
        flat.extend(round(value, 6) for value in sample.velocity_km_s)

    if verbose:
        span_years = (end - epoch) / (SECONDS_PER_DAY * 365.25)
        print(f"      trajectory {len(samples)} samples every {step / 3600.0:.3g} h "
              f"({span_years:.1f} yr)", flush=True)

    return {"epochSecondsPastJ2000": epoch, "stepSeconds": step, "samples": flat}


def build_orbit(mission: mission_table.Mission, parent_gm: float, delay: float,
                verbose: bool) -> Dict[str, Any]:
    """Fetch osculating elements at the mission's epoch, as the planets use."""
    # Horizons rejects a zero-length interval, so ask for a day and keep the first record.
    stop = _next_day(mission.epoch)

    elements = horizons.fetch_elements(
        body=str(mission.naif_id),
        center=mission.center,
        start_time=mission.epoch,
        stop_time=stop,
        step_size="1d",
        ref_plane=horizons.REF_PLANE_ICRF,
    )
    horizons.polite_sleep(delay)

    first = elements[0]
    values = first.values

    # Horizons prints its element block as prose, so a field can simply be absent — and a bare
    # KeyError here is not a HorizonsError, so it would escape the per-mission handler in
    # main() and take the whole 24-mission run down with it.
    missing = [key for key in ("A", "EC", "IN", "OM", "W", "MA") if key not in values]
    if missing:
        raise horizons.HorizonsError(
            f"{mission.id}: Horizons returned no {', '.join(missing)} in its element block; "
            f"got {sorted(values)}.")

    semi_major_axis = values["A"]
    eccentricity = values["EC"]

    if eccentricity >= 1.0:
        raise horizons.HorizonsError(
            f"{mission.id}: eccentricity {eccentricity} is not an ellipse. An open conic has no "
            "period, so the validity window cannot be derived; place this mission with a "
            "sampled table instead.")

    if verbose:
        period_hours = values.get("PR", 0.0) / 3600.0
        print(f"      orbit a={semi_major_axis:.6g} km  e={eccentricity:.4f}  "
              f"i={values['IN']:.2f} deg  period={period_hours:.2f} h", flush=True)

    return {
        "semiLatusRectumKm": semi_major_axis * (1.0 - eccentricity * eccentricity),
        "eccentricity": eccentricity,
        "inclinationDeg": values["IN"],
        "longitudeOfAscendingNodeDeg": values["OM"],
        "argumentOfPeriapsisDeg": values["W"],
        "meanAnomalyAtEpochDeg": values["MA"],
        "epochSecondsPastJ2000": seconds_past_j2000(first.julian_date_tdb),
        # The relative two-body problem is governed by the sum of the masses. A spacecraft
        # contributes nothing measurable, so this is the parent's GM — and the catalogue's
        # own consistency check expects exactly that.
        "gravitationalParameter": parent_gm,
    }


def _next_day(iso_date: str) -> str:
    from datetime import date, timedelta
    year, month, day = (int(part) for part in iso_date.split("-"))
    return (date(year, month, day) + timedelta(days=1)).isoformat()


def parent_constants() -> Dict[str, Dict[str, float]]:
    """GM, equatorial radius and J2 for every body a spacecraft is placed against.

    The first two come from the Solar System catalogue, which is the authority for them. J2 is
    not in it — nothing else in the application needs an oblateness coefficient — so it lives
    in the mission table beside the missions that consume it.
    """
    import json
    path = os.path.join(STREAMING, "catalog", "solar_system.json")
    if not os.path.exists(path):
        raise SystemExit(
            f"error: {path} not found. Run build_solar_system.py first — a spacecraft's orbit "
            "is governed by its parent's mass, which is recorded there.")

    with open(path, encoding="utf-8") as handle:
        document = json.load(handle)

    return {
        body["id"]: {
            "gm": body["gravitationalParameter"],
            "radiusKm": body.get("equatorialRadiusKm") or 0.0,
            "j2": mission_table.J2.get(body["id"], 0.0),
        }
        for body in document["bodies"]
    }


# --------------------------------------------------------------------------------------
# Records
# --------------------------------------------------------------------------------------


def window_seconds(iso_date: str) -> float:
    """Seconds past J2000 for a calendar date at midnight.

    The 43200 is J2000 itself: the epoch is 2000-01-01 **noon** TT, so midnight that morning is
    half a day before it. Getting that term wrong displaces every window by twelve hours, and
    for a mission that began at 00:40 on its launch day that is the difference between a
    spacecraft appearing and not existing.

    What is genuinely approximate is the time *scale*: this treats the calendar date as TDB and
    ignores TDB-UTC, currently about 69 seconds, and leap seconds before it. That is deliberate
    — these are mission windows quoted to the day — and it is far below anything that matters
    here. It is also measurably small: for all eight sampled missions the value this returns
    agrees with the TDB epoch Horizons reports for the first sample to 0.000 s.
    """
    from datetime import date
    year, month, day = (int(part) for part in iso_date.split("-"))
    return (date(year, month, day) - date(2000, 1, 1)).days * SECONDS_PER_DAY - 43200.0


def secular_rates(orbit: Dict[str, Any], j2: float, equatorial_radius_km: float) -> Dict[str, float]:
    """First-order J2 precession of an osculating conic, in degrees per day.

    An osculating element set is a snapshot: it is the conic that matches the true state at one
    instant, and the true orbit leaves it immediately. The dominant reason for a close orbiter
    is the primary's oblateness, whose secular effect on the node and the apsides has a closed
    form (Vallado, *Fundamentals of Astrodynamics and Applications*, 4th ed., eq. 9-38/9-39):

        d(RAAN)/dt = -3/2 n J2 (Re/p)^2 cos i
        d(argp)/dt =  3/4 n J2 (Re/p)^2 (5 cos^2 i - 1)

    This is what decides how long a shipped element set means anything, and the answer is much
    shorter than a mission. The ISS's node regresses 4.95 deg/day, so over the 27 years its
    coverage window used to claim it had turned about 49 000 degrees — 136 complete
    revolutions. The drawn orbital plane was not approximately right, it was arbitrary.
    """
    p = orbit["semiLatusRectumKm"]
    e = orbit["eccentricity"]
    mu = orbit["gravitationalParameter"]

    if p <= 0.0 or mu <= 0.0 or e >= 1.0 or j2 <= 0.0 or equatorial_radius_km <= 0.0:
        return {"nodeDegPerDay": 0.0, "apsidalDegPerDay": 0.0}

    semi_major_axis = p / (1.0 - e * e)
    mean_motion = (mu / semi_major_axis ** 3) ** 0.5              # rad/s
    inclination = math.radians(orbit["inclinationDeg"])
    factor = mean_motion * j2 * (equatorial_radius_km / p) ** 2

    node = -1.5 * factor * math.cos(inclination)
    apsidal = 0.75 * factor * (5.0 * math.cos(inclination) ** 2 - 1.0)

    to_deg_per_day = math.degrees(1.0) * SECONDS_PER_DAY
    return {"nodeDegPerDay": node * to_deg_per_day,
            "apsidalDegPerDay": apsidal * to_deg_per_day}


def kepler_validity_seconds(rates: Dict[str, float]) -> float:
    """How long either side of its epoch a conic still says where a spacecraft is, in seconds.

    One full revolution of the faster of the two precessing angles. That bound is not "the
    error is small" — at 359 degrees of node regression the drawing is as wrong as at 361. It
    is the point past which the drawn orbit has swept every possible orientation and therefore
    carries no information at all about where the spacecraft is, which is the honest place to
    stop claiming to know. Inside it the error is bounded and stated; outside it there is
    nothing to state.

    Zero means the rates are negligible and the mission's own dates govern — which is the case
    for anything far from an oblate primary: Chandra, TESS, the Jupiter and Saturn orbiters and
    Parker are all limited by third bodies and by manoeuvres, not by J2.
    """
    fastest = max(abs(rates["nodeDegPerDay"]), abs(rates["apsidalDegPerDay"]))
    if fastest <= 0.0:
        return 0.0
    return 360.0 / fastest * SECONDS_PER_DAY


def build_record(mission: mission_table.Mission, model_name: Optional[str],
                 placement: Optional[Dict[str, Any]],
                 parent: Optional[Dict[str, float]] = None) -> Dict[str, Any]:
    record: Dict[str, Any] = {
        "id": mission.id,
        "name": mission.name,
        "type": "Spacecraft",
        "parent": mission.parent,
        # A spacecraft's own gravity is not zero, but it is around 1e-16 km^3/s^2 and nothing
        # in this application is sensitive to it. Recording zero says "this body does not
        # gravitate here", which is true, rather than inventing a figure nobody published.
        "gravitationalParameter": 0.0,
        "displayColor": DEFAULT_COLOR,
        "summary": mission.summary,
    }

    provenance: Dict[str, Any] = {}

    if mission.ended:
        record["epitaph"] = mission.ended

    # Every spacecraft states when its placement stops meaning anything. Elements and landing
    # sites evaluate at any epoch, so without this a dead mission keeps flying.
    #
    # The mission table's dates are the outer bound — when the spacecraft existed and was
    # operating. Each placement then narrows that to what its own data actually supports, which
    # for two of the three is much less.
    known_from = window_seconds(mission.known_from)
    known_until = window_seconds(mission.known_until)

    if mission.placement == mission_table.SAMPLED:
        record["trajectory"] = placement

        # The table's own ends, not the mission's dates.
        #
        # These used to be independent, and they disagreed on all eight sampled missions:
        # Horizons stops at the last whole step inside the requested span, so the declared
        # window ran past the data by between one and 148 days. CelestialBody.KnownAt happens
        # to take the intersection, so nothing was drawn wrongly — but the catalogue asserted
        # coverage it did not have, and a file that lies about its own extent will eventually
        # be believed by something that does not cross-check.
        epoch = placement["epochSecondsPastJ2000"]
        end = epoch + placement["stepSeconds"] * (len(placement["samples"]) // 6 - 1)
        known_from = max(known_from, epoch)
        known_until = min(known_until, end)

        provenance["trajectory"] = measured(
            f"JPL Horizons state vectors for NAIF {mission.naif_id}, ICRF/J2000, "
            f"centre {mission.center}, {mission.start} to {mission.stop} at {mission.step}, "
            "cubic Hermite interpolation between samples")
        # What the cadence can and cannot resolve, stated rather than implied. A uniform table
        # has one step, and a gravity assist lasts hours.
        provenance["trajectory.model"] = inferred(
            f"cubic Hermite between samples {mission.step} apart; smooth motion is reproduced "
            "to the interpolation error, but any encounter shorter than one step is cut across "
            "rather than followed")

    elif mission.placement == mission_table.KEPLER:
        record["orbit"] = placement
        provenance["orbit"] = measured(
            f"JPL Horizons osculating elements for NAIF {mission.naif_id} at {mission.epoch} "
            f"TDB, ICRF/J2000, centre {mission.center}")

        # An osculating conic is a snapshot, not the trajectory, and how long the snapshot
        # lasts is a number rather than a caveat. Where oblateness dominates it is computed;
        # where it does not, the mission table's hand-checked dates are all there is.
        rates = ({"nodeDegPerDay": 0.0, "apsidalDegPerDay": 0.0} if parent is None
                 else secular_rates(placement, parent["j2"], parent["radiusKm"]))
        validity = kepler_validity_seconds(rates)

        if validity > 0.0:
            epoch = placement["epochSecondsPastJ2000"]
            known_from = max(known_from, epoch - validity)
            known_until = min(known_until, epoch + validity)

        detail = (f"node {rates['nodeDegPerDay']:+.4g} deg/day, apsides "
                  f"{rates['apsidalDegPerDay']:+.4g} deg/day under {mission.parent}'s J2"
                  if validity > 0.0 else
                  "oblateness is negligible here; the limits are third bodies and manoeuvres, "
                  "which have no closed form and are bounded by the mission dates instead")
        provenance["orbit.model"] = inferred(
            "two-body propagation of elements osculating at one epoch. The real orbit "
            f"precesses: {detail}. The coverage window is one full revolution of the faster "
            "angle either side of the epoch, past which the drawn orbital plane has swept "
            "every orientation and says nothing about where the spacecraft is.")

    elif mission.placement == mission_table.SITE:
        record["site"] = {
            "latitudeDeg": mission.latitude_deg,
            "eastLongitudeDeg": mission.longitude_deg,
            "name": mission.site_name,
        }
        if mission.altitude_km:
            record["site"]["altitudeKm"] = mission.altitude_km
        provenance["site"] = measured(mission.site_reference, "")

    if known_until <= known_from:
        raise ValueError(
            f"{mission.id}: coverage window collapsed to nothing "
            f"({known_from:.0f} to {known_until:.0f} s past J2000). The element epoch is "
            "probably outside the mission dates, or the data is shorter than declared.")

    record["knownFromSecondsPastJ2000"] = known_from
    record["knownUntilSecondsPastJ2000"] = known_until

    if model_name:
        record["model"] = {
            "path": f"{MODEL_STREAMING_PREFIX}/{model_name}",
            "lengthMetres": mission.length_metres,
            "attribution": mission_table.MODEL_LICENCE,
            "sourceUrl": f"{mission_table.MODEL_REPO}/tree/master/3D%20Models",
        }
        # The dimension is published; that the model's longest axis *is* that dimension is an
        # assumption about an export whose units nobody recorded. Only the second part is a
        # judgement, so the pair is labelled Inferred and names which dimension was used.
        provenance["model"] = inferred(
            f"scaled so the model's longest axis is the published {mission.length_note} "
            f"of {mission.length_metres:g} m",
            mission_table.MODEL_LICENCE_URL)

    record["provenance"] = provenance
    return record


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--output", default=DEFAULT_CATALOG)
    parser.add_argument("--model-dir", default=DEFAULT_MODEL_DIR)
    parser.add_argument("--delay", type=float, default=0.4,
                        help="seconds between Horizons requests")
    parser.add_argument("--only", help="comma-separated mission ids; the rest are left as they are")
    parser.add_argument("--replace", action="store_true",
                        help="with --only, write a catalogue of just those missions instead of "
                             "merging them into the one already there")
    parser.add_argument("--skip-models", action="store_true",
                        help="rebuild trajectories only, leaving existing meshes alone")
    parser.add_argument("--skip-trajectories", action="store_true",
                        help="rebuild meshes only, reusing the trajectories already written")
    parser.add_argument("-v", "--verbose", action="store_true")
    args = parser.parse_args()

    missions = mission_table.ALL
    if args.only:
        wanted = {name.strip() for name in args.only.split(",")}
        missions = [mission for mission in missions if mission.id in wanted]
        unknown = wanted - {mission.id for mission in missions}
        if unknown:
            print(f"error: unknown mission ids {sorted(unknown)}", file=sys.stderr)
            return 2

    existing = _load_existing(args.output) if args.skip_trajectories else {}
    # Needed even when trajectories are reused: the coverage window of a Kepler placement is
    # derived from the parent's oblateness, so it has to be recomputed on every write.
    parents = parent_constants()

    records: List[Dict[str, Any]] = []
    failures: List[str] = []

    for index, mission in enumerate(missions):
        print(f"[{index + 1}/{len(missions)}] {mission.name} ... ", end="", flush=True)
        if args.verbose:
            print(flush=True)

        model_name: Optional[str] = None
        placement: Optional[Dict[str, Any]] = None
        problems: List[str] = []

        if not args.skip_models:
            try:
                model_name = build_model(mission, args.model_dir, args.verbose)
            except (gltf.GltfError, urllib.error.URLError, OSError) as exc:
                problems.append(f"model: {exc}")
        else:
            # --skip-models adopts whatever mesh is on disk, so it has to be checked rather than
            # trusted: nothing here knows which run wrote it or whether that run succeeded. The
            # header is self-describing, so a file that does not decode as a whole .csmesh is
            # refused and named instead of being written into the catalogue.
            candidate = os.path.join(args.model_dir, f"{mission.id}.csmesh")
            if os.path.exists(candidate):
                complaint = _mesh_complaint(candidate)
                if complaint:
                    problems.append(f"model: reusing {mission.id}.csmesh refused, {complaint}")
                else:
                    model_name = f"{mission.id}.csmesh"

        if mission.placement == mission_table.SITE:
            # A landing site is already in the table; nothing to fetch.
            placement = {}
        elif args.skip_trajectories and _reusable(existing, mission) is not None:
            placement = _reusable(existing, mission)
        else:
            try:
                if mission.placement == mission_table.SAMPLED:
                    placement = build_trajectory(mission, args.delay, args.verbose)
                else:
                    if mission.parent not in parents:
                        raise horizons.HorizonsError(
                            f"parent '{mission.parent}' is not in the Solar System catalogue")
                    placement = build_orbit(mission, parents[mission.parent]["gm"],
                                            args.delay, args.verbose)
            except horizons.HorizonsError as exc:
                problems.append(f"{mission.placement}: {exc}")

        # A spacecraft with no placement is not a spacecraft, it is a claim with no content.
        if placement is None:
            failures.append(f"{mission.id}: {'; '.join(problems)}")
            print("SKIPPED" if not args.verbose else f"  SKIPPED: {'; '.join(problems)}", flush=True)
            continue

        if problems:
            # A missing mesh is survivable — the runtime falls back to a marker — so the
            # mission still ships, with the reason printed rather than swallowed.
            print(f"({'; '.join(problems)}) ", end="", flush=True)

        record = build_record(mission, model_name, placement, parents.get(mission.parent))
        records.append(record)

        # The window each placement actually earned, next to the one the mission table asked
        # for. They differ for every sampled mission and for every close orbiter, and a build
        # that does not print both makes the narrowing invisible.
        span_days = ((record["knownUntilSecondsPastJ2000"]
                      - record["knownFromSecondsPastJ2000"]) / SECONDS_PER_DAY)
        print(f"ok  [{_iso(record['knownFromSecondsPastJ2000'])} .. "
              f"{_iso(record['knownUntilSecondsPastJ2000'])}, {span_days:.0f} d"
              + (f", asked {mission.known_from} .. {mission.known_until}"
                 if _iso(record["knownFromSecondsPastJ2000"]) != mission.known_from
                 or _iso(record["knownUntilSecondsPastJ2000"]) != mission.known_until
                 else "") + "]", flush=True)

    if not records:
        print("\nerror: nothing was imported.", file=sys.stderr)
        return 1

    # --only rebuilds some missions; it does not mean "throw the others away".
    #
    # It reads as a filter and used to behave as a truncation: the output was whatever this run
    # produced, so `--only soho` replaced a 24-spacecraft catalogue with a one-spacecraft one
    # and said "Wrote ... 1 spacecraft" as though that were the intent. Same silent-staleness
    # shape as the mesh trap — the file afterwards is not what anybody thinks it is — except
    # that here the data is destroyed rather than kept. Merging is what the flag means; --replace
    # is there for when it genuinely is not.
    if args.only and not args.replace:
        rebuilt = {record["id"] for record in records}
        merged = [record for record in _load_records(args.output).values()
                  if record["id"] not in rebuilt]
        if merged:
            print(f"  (merging {len(records)} rebuilt with {len(merged)} already in "
                  f"{os.path.basename(args.output)}; pass --replace to write only the rebuilt)")
            order = {mission.id: index for index, mission in enumerate(mission_table.ALL)}
            records = sorted(records + merged, key=lambda r: order.get(r["id"], len(order)))

    catalog = {
        "name": "Spacecraft",
        "source": (
            f"Trajectories: JPL Horizons ({HORIZONS_URL}), geometric state vectors in ICRF/J2000. "
            f"Models: NASA 3D Resources ({mission_table.MODEL_REPO}), {mission_table.MODEL_LICENCE}. "
            "Landing sites from the published mission coordinates, planetocentric. "
            "Generated by tools/cosmodata/build_spacecraft.py — do not hand-edit."
        ),
        "generated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "bodies": records,
    }

    os.makedirs(os.path.dirname(args.output), exist_ok=True)
    horizons.dump_json(args.output, catalog)

    size_kb = os.path.getsize(args.output) / 1024.0
    meshes = _mesh_total(args.model_dir)

    print(f"\nWrote {args.output}  ({size_kb:.1f} kB)")
    print(f"  {len(records)} spacecraft, {meshes[0]} meshes totalling {meshes[1] / 1024.0:.0f} kB")
    if failures:
        print(f"  {len(failures)} skipped:")
        for failure in failures:
            print(f"    {failure}")

    # A mission that ships without geometry is drawn as a grey sphere, which looks like a mesh
    # that has not finished streaming rather than one that was never built. The per-mission line
    # said so an hour of scrollback ago; this says so at the end, where it is read.
    modelless = [record["id"] for record in records if "model" not in record]
    if modelless:
        print(f"  {len(modelless)} with no geometry, drawn as plain spheres: "
              f"{', '.join(sorted(modelless))}")

    # Meshes nothing refers to. A renamed or retired mission leaves its file behind, where it
    # costs APK space forever and will be silently adopted by any future mission that happens
    # to take the same id.
    claimed = {os.path.basename(record["model"]["path"]) for record in records if "model" in record}
    if os.path.isdir(args.model_dir):
        orphans = sorted(name for name in os.listdir(args.model_dir)
                         if name.endswith(".csmesh") and name not in claimed)
        if orphans and not args.only:
            print(f"  {len(orphans)} mesh(es) no mission claims, safe to delete: {', '.join(orphans)}")

    return 0


def _mesh_complaint(path: str) -> str:
    """Why this file is not a whole .csmesh, or "" if it is one. Never raises.

    Used where a mesh is adopted rather than built. The format's header states the vertex,
    index and submesh counts, so the file's own length is a checksum on it: a truncated write,
    a half-finished copy or something that is not a mesh at all all fail it. That is the same
    argument as the magic number in the runtime reader — the whole reason this format has one
    is that a plausible-looking file proves nothing.
    """
    try:
        with open(path, "rb") as handle:
            header = handle.read(meshpack.HEADER_BYTES)
        actual = os.path.getsize(path)
    except OSError as exc:
        return f"it could not be read ({exc})"

    if len(header) < meshpack.HEADER_BYTES:
        return f"it is {len(header)} bytes, too short to hold a header"
    if header[:4] != meshpack.MAGIC:
        return f"it begins {header[:4]!r} rather than {meshpack.MAGIC!r}"

    flags, vertices, indices, submeshes = struct.unpack_from("<IIII", header, 4)
    expected = (meshpack.HEADER_BYTES + vertices * 24
                + indices * (4 if flags & 1 else 2) + submeshes * 32)
    if actual != expected:
        return (f"it declares {vertices} vertices, {indices} indices and {submeshes} submeshes, "
                f"needing {expected} bytes, but the file is {actual}")
    return ""


def _iso(seconds_past_j2000: float) -> str:
    """Calendar date of an instant, for the build log."""
    from datetime import date, timedelta
    return (date(2000, 1, 1) + timedelta(seconds=seconds_past_j2000 + 43200.0)).isoformat()


def _reusable(existing: Dict[str, Dict[str, Any]],
              mission: mission_table.Mission) -> Optional[Dict[str, Any]]:
    """The stored placement for this mission, but only if it is of the kind it now needs."""
    kind = {mission_table.SAMPLED: "trajectory", mission_table.KEPLER: "orbit"}.get(mission.placement)
    if kind is None:
        return None
    return existing.get(mission.id, {}).get(kind)


def _load_existing(path: str) -> Dict[str, Dict[str, Any]]:
    """Placements already in the output file, keyed by mission id then by kind.

    Keyed by kind rather than flattened with ``trajectory or orbit``, which is how this used to
    read it. That form loses the distinction: change a mission from a sampled table to elements
    in the mission table, re-run with --skip-trajectories, and the old *trajectory* payload is
    written into the new record's ``orbit`` field. The result parses, carries a full set of
    numbers, and describes nothing.
    """
    import json
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as handle:
        document = json.load(handle)

    return {body["id"]: {kind: body[kind] for kind in ("trajectory", "orbit") if kind in body}
            for body in document.get("bodies", [])}


def _load_records(path: str) -> Dict[str, Dict[str, Any]]:
    """Whole records already in the output file, keyed by mission id."""
    import json
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as handle:
        document = json.load(handle)
    return {body["id"]: body for body in document.get("bodies", [])}


def _mesh_total(model_dir: str):
    if not os.path.isdir(model_dir):
        return 0, 0
    names = [name for name in os.listdir(model_dir) if name.endswith(".csmesh")]
    return len(names), sum(os.path.getsize(os.path.join(model_dir, name)) for name in names)


if __name__ == "__main__":
    raise SystemExit(main())
