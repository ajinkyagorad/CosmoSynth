#!/usr/bin/env python3
"""Build the shipped star field from the HYG catalogue — the real sky, in real places.

Phase 1 of docs/roadmap-universe.md. Replaces 3200 invented background stars with 119 613
real ones at measured positions, so flying away from the Sun arrives somewhere true and the
constellations come out of the data rather than being drawn in.

    python3 tools/cosmodata/build_starfield.py

Out: unity/Assets/StreamingAssets/CosmoSynth/sky/stars.csstars   (binary, ~2.9 MB)
     unity/Assets/StreamingAssets/CosmoSynth/sky/stars_named.json (the 548 with proper names)

Standard library only, network-bound, about a minute. The app never touches the network.

LICENCE — READ THIS BEFORE SHIPPING COMMERCIALLY
    HYG is **CC BY-SA 4.0**: attribution *and* share-alike. That is a stronger claim than
    anything else in this project, all of which is public-domain government work. Two
    consequences, both recorded in docs/data-sources.md rather than discovered later:

      * The attribution below must travel with the build, not just live in a source file.
      * The derived catalogue this script writes is an adaptation of a ShareAlike work, so
        that file carries the same licence. It does not follow that the whole application
        does — the app is not an adaptation of a star catalogue — but that is a question for
        a lawyer and not for a build script, and it is exactly the kind of thing this
        project's data-sources document exists to flag before it becomes expensive.

    HYG is itself a compilation of Hipparcos, the Yale Bright Star Catalogue and the Gliese
    catalogue of nearby stars. The underlying astrometry is ESA and Yale work.

WHAT IS MEASURED AND WHAT IS NOT
    Position and magnitude are measured. Temperature is **inferred** from the B-V colour
    index through Ballesteros' formula, and where B-V is missing (1890 stars) it is inferred
    more weakly still, from the spectral class alone. The distinction is carried into the
    file so the runtime can be honest about it.

    10 224 stars carry HYG's "parallax unknown" sentinel of 100 000 parsecs. Their direction
    is measured and their distance is not known at all. They are kept and flagged rather than
    dropped, because the application already has the right treatment for that case — it pins
    anything past the horizon to a sky sphere, direction exact and distance given up, which
    is precisely what these stars are. Silently placing them at 100 000 pc would invent a
    distance; silently dropping them would delete a tenth of the sky.
"""

from __future__ import annotations

import argparse
import csv
import gzip
import io
import json
import math
import os
import struct
import sys
import urllib.request
from datetime import datetime, timezone
from typing import Dict, List, Optional, Tuple

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
STREAMING = os.path.join(REPO_ROOT, "unity", "Assets", "StreamingAssets", "CosmoSynth")
DEFAULT_OUT = os.path.join(STREAMING, "sky")

SOURCE_URL = ("https://codeberg.org/astronexus/hyg/media/branch/main/"
              "data/hyg/CURRENT/hyg_v44.csv.gz")
SOURCE_HOME = "https://codeberg.org/astronexus/hyg"
LICENCE = "CC BY-SA 4.0"
ATTRIBUTION = ("HYG star database v4.4 (astronexus), CC BY-SA 4.0 — compiled from the "
               "Hipparcos, Yale Bright Star and Gliese catalogues")

MAGIC = b"CSS1"
RECORD_FORMAT = "<3f f f I"          # position pc, apparent magnitude, temperature K, flags
RECORD_BYTES = struct.calcsize(RECORD_FORMAT)
HEADER_FORMAT = "<4sIII"
HEADER_BYTES = struct.calcsize(HEADER_FORMAT)

#: HYG writes this distance when the parallax is unusable. It is a sentinel, not a measurement.
UNKNOWN_DISTANCE_PC = 100000.0

FLAG_DISTANCE_UNKNOWN = 1 << 0
FLAG_TEMPERATURE_FROM_SPECTRUM = 1 << 1   # weaker than B-V: no colour index was published
FLAG_HAS_PROPER_NAME = 1 << 2

#: Representative effective temperatures by spectral class, for the 1890 stars with no B-V.
#: Main-sequence values; a giant of the same class is cooler, which is why this is a fallback
#: and is flagged in the record rather than treated as equivalent to a colour measurement.
SPECTRAL_TEMPERATURES: Dict[str, float] = {
    "O": 40000.0, "B": 20000.0, "A": 8750.0, "F": 6750.0,
    "G": 5600.0, "K": 4400.0, "M": 3200.0,
    "L": 1900.0, "T": 1200.0, "C": 3000.0, "S": 3000.0, "W": 50000.0,
}
DEFAULT_TEMPERATURE = 5600.0


def temperature_from_colour_index(b_minus_v: float) -> float:
    """Effective temperature from B-V, by Ballesteros' formula.

    Ballesteros, F. J. (2012), "New insights into black bodies", EPL 97 34008.
    Good to a few per cent across the main sequence, which is far better than this
    application can show, and it is a published closed form rather than a fitted table.
    """
    denominator_a = 0.92 * b_minus_v + 1.7
    denominator_b = 0.92 * b_minus_v + 0.62
    if abs(denominator_a) < 1e-6 or abs(denominator_b) < 1e-6:
        return DEFAULT_TEMPERATURE
    return 4600.0 * (1.0 / denominator_a + 1.0 / denominator_b)


def temperature_from_spectrum(spectral_type: str) -> Optional[float]:
    for character in spectral_type.strip():
        if character.upper() in SPECTRAL_TEMPERATURES:
            return SPECTRAL_TEMPERATURES[character.upper()]
    return None


def download(url: str) -> bytes:
    """Fetch the catalogue, refusing anything that is not actually gzip.

    A wrong path on a git host answers with a perfectly cheerful HTML page, and a CSV reader
    handed HTML produces zero stars and no error. Magic bytes, not status codes — the same
    rule the model importer learned.
    """
    request = urllib.request.Request(url, headers={"User-Agent": "CosmoSynth/1.0 (importer)"})
    with urllib.request.urlopen(request, timeout=600) as response:
        payload = response.read()

    if payload[:2] != b"\x1f\x8b":
        raise SystemExit(
            f"error: {url} returned {len(payload)} bytes beginning {payload[:16]!r}, "
            "which is not gzip. A 200 response is not evidence of a catalogue.")
    return payload


def build(payload: bytes, magnitude_limit: float, verbose: bool
          ) -> Tuple[List[Tuple], List[Dict], Dict[str, int]]:
    stars: List[Tuple] = []
    named: List[Dict] = []
    counts = {"read": 0, "kept": 0, "sol": 0, "unparsable": 0,
              "distance_unknown": 0, "temperature_from_spectrum": 0, "too_faint": 0}

    with gzip.open(io.BytesIO(payload), "rt", encoding="utf-8", errors="replace") as handle:
        for row in csv.DictReader(handle):
            counts["read"] += 1

            # Row 0 is the Sun. It is already in solar_system.json with a measured GM, radius
            # and luminosity; adding it again as a catalogue star would put a second Sun at the
            # origin and give the merge an id collision to resolve.
            if row.get("id") == "0" or row.get("proper", "").strip() == "Sol":
                counts["sol"] += 1
                continue

            try:
                x = float(row["x"]); y = float(row["y"]); z = float(row["z"])
                magnitude = float(row["mag"])
            except (TypeError, ValueError):
                counts["unparsable"] += 1
                continue

            if magnitude > magnitude_limit:
                counts["too_faint"] += 1
                continue

            flags = 0

            try:
                distance = float(row["dist"])
            except (TypeError, ValueError):
                distance = UNKNOWN_DISTANCE_PC
            if distance >= UNKNOWN_DISTANCE_PC:
                flags |= FLAG_DISTANCE_UNKNOWN
                counts["distance_unknown"] += 1

            colour_index = row.get("ci", "").strip()
            if colour_index:
                temperature = temperature_from_colour_index(float(colour_index))
            else:
                fallback = temperature_from_spectrum(row.get("spect", ""))
                temperature = fallback if fallback is not None else DEFAULT_TEMPERATURE
                flags |= FLAG_TEMPERATURE_FROM_SPECTRUM
                counts["temperature_from_spectrum"] += 1

            # Keep temperature inside the range the black-body colour function is valid over.
            temperature = min(max(temperature, 1000.0), 60000.0)

            proper = row.get("proper", "").strip()
            if proper:
                flags |= FLAG_HAS_PROPER_NAME

            stars.append((x, y, z, magnitude, temperature, flags, proper,
                          row.get("con", "").strip(), row.get("bf", "").strip()))
            counts["kept"] += 1

    # Brightest first, so the runtime can take a prefix and get the sky everyone recognises
    # without reading or sorting 119 000 records at load.
    stars.sort(key=lambda star: star[3])

    for index, star in enumerate(stars):
        if star[6]:
            named.append({"index": index, "name": star[6],
                          "constellation": star[7], "designation": star[8]})

    if verbose:
        for label, value in counts.items():
            print(f"    {label:26s} {value}")

    return stars, named, counts


def write_binary(stars: List[Tuple], path: str) -> int:
    payload = bytearray(struct.pack(HEADER_FORMAT, MAGIC, 1, len(stars), 0))
    for x, y, z, magnitude, temperature, flags, _proper, _con, _bf in stars:
        payload += struct.pack(RECORD_FORMAT, x, y, z, magnitude, temperature, flags)

    with open(path, "wb") as handle:
        handle.write(payload)
    return len(payload)


def verify(path: str, expected: int) -> None:
    """Read back what was written. A catalogue that cannot be re-read is not shipped."""
    with open(path, "rb") as handle:
        data = handle.read()

    magic, version, count, _flags = struct.unpack_from(HEADER_FORMAT, data, 0)
    if magic != MAGIC:
        raise SystemExit(f"error: wrote a file that does not begin with {MAGIC!r}")
    if count != expected:
        raise SystemExit(f"error: header says {count} stars, wrote {expected}")
    if len(data) != HEADER_BYTES + count * RECORD_BYTES:
        raise SystemExit(
            f"error: {len(data)} bytes on disk, {HEADER_BYTES + count * RECORD_BYTES} expected")

    brightest = struct.unpack_from(RECORD_FORMAT, data, HEADER_BYTES)
    if not math.isfinite(brightest[0]) or not math.isfinite(brightest[3]):
        raise SystemExit("error: first record is not finite")

    previous = -99.0
    for i in range(0, count, max(1, count // 500)):
        magnitude = struct.unpack_from(RECORD_FORMAT, data, HEADER_BYTES + i * RECORD_BYTES)[3]
        if magnitude < previous - 1e-4:
            raise SystemExit(f"error: records are not sorted by magnitude at index {i}")
        previous = magnitude


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--output", default=DEFAULT_OUT)
    parser.add_argument("--magnitude-limit", type=float, default=99.0,
                        help="drop stars fainter than this (default: keep all)")
    parser.add_argument("--cache", help="use a local hyg csv.gz instead of downloading")
    parser.add_argument("-v", "--verbose", action="store_true")
    args = parser.parse_args()

    if args.cache:
        print(f"reading {args.cache}", flush=True)
        payload = open(args.cache, "rb").read()
    else:
        print(f"downloading {SOURCE_URL}", flush=True)
        payload = download(SOURCE_URL)
    print(f"  {len(payload) / 1e6:.1f} MB compressed", flush=True)

    print("parsing", flush=True)
    stars, named, counts = build(payload, args.magnitude_limit, args.verbose)
    if not stars:
        raise SystemExit("error: no stars survived parsing")

    os.makedirs(args.output, exist_ok=True)
    binary_path = os.path.join(args.output, "stars.csstars")
    size = write_binary(stars, binary_path)
    verify(binary_path, len(stars))

    named_path = os.path.join(args.output, "stars_named.json")
    with open(named_path, "w", encoding="utf-8") as handle:
        json.dump({
            "source": ATTRIBUTION,
            "sourceUrl": SOURCE_HOME,
            "licence": LICENCE,
            "generated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
            "note": ("Indices refer to stars.csstars, which is sorted brightest first. "
                     "Positions are ICRF equatorial cartesian, parsecs."),
            "stars": named,
        }, handle, indent=1)
        handle.write("\n")

    naked_eye = sum(1 for star in stars if star[3] <= 6.0)
    print(f"\nWrote {binary_path}  ({size / 1e6:.2f} MB, {len(stars)} stars, "
          f"{RECORD_BYTES} bytes each)")
    print(f"Wrote {named_path}  ({len(named)} named)")
    print(f"  naked-eye (mag <= 6): {naked_eye}")
    print(f"  distance unknown    : {counts['distance_unknown']} "
          f"(direction measured, distance is not — flagged, not invented)")
    print(f"  temperature inferred from spectral class only: "
          f"{counts['temperature_from_spectrum']}")
    print(f"  brightest: mag {stars[0][3]:.2f} '{stars[0][6] or '(unnamed)'}'")
    print(f"\n  LICENCE: {LICENCE} — {ATTRIBUTION}")
    print( "  This is ShareAlike. See the licence note in this file's docstring and in")
    print( "  docs/data-sources.md before shipping commercially.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
