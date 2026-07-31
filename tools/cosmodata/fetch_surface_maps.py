#!/usr/bin/env python3
"""Download real NASA surface imagery for catalogue bodies.

Every map here is measured imagery from a NASA mission, not generated. Bodies that get one are
re-labelled from Procedural to Measured in the catalogue; bodies that do not keep the
procedural surface and keep saying so.

**Every download is validated, not trusted.** NASA's asset hosts answer a wrong path with a
200 and an HTML error page, so a naive fetcher happily writes a 338 kB soft-404 to disk and
ships it as a planet. This checks the magic bytes, decodes the real pixel dimensions from the
file header, and rejects anything that is not an image with roughly a 2:1 equirectangular
aspect. A candidate that fails is reported and skipped — never silently substituted.

Run:  python3 tools/cosmodata/fetch_surface_maps.py
Out:  unity/Assets/StreamingAssets/CosmoSynth/surfaces/<body>.jpg  (+ surfaces.json)
"""

from __future__ import annotations

import argparse
import json
import os
import struct
import sys
import urllib.request
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Tuple

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUTPUT_DIR = os.path.join(
    REPO_ROOT, "unity", "Assets", "StreamingAssets", "CosmoSynth", "surfaces")

#: Candidate maps per body, tried in order until one validates.
#:
#: Only URLs confirmed to serve a real equirectangular image are listed. Several plausible-
#: looking NASA paths (nasa3d.arc.nasa.gov/shared_assets/images/<id>/<id>-1024.jpg among them)
#: return HTTP 200 with an HTML error page and are deliberately absent.
SOURCES: Dict[str, List[Dict[str, str]]] = {
    "earth": [{
        "url": "https://eoimages.gsfc.nasa.gov/images/imagerecords/57000/57752/"
               "land_shallow_topo_2048.jpg",
        "source": "NASA Visible Earth — Blue Marble: Land Surface, Shallow Water and Shaded Topography",
        "reference": "https://visibleearth.nasa.gov/images/57752/blue-marble-land-surface-shallow-water-and-shaded-topography",
        "instrument": "Terra/MODIS composite",
    }],
    "moon": [{
        "url": "https://svs.gsfc.nasa.gov/vis/a000000/a004700/a004720/lroc_color_poles_1k.jpg",
        "source": "NASA SVS / LRO — LROC colour global mosaic",
        "reference": "https://svs.gsfc.nasa.gov/4720",
        "instrument": "Lunar Reconnaissance Orbiter / LROC WAC",
    }],
}

#: An equirectangular map is 2:1. Allow a little slack for maps trimmed at the poles.
MIN_ASPECT, MAX_ASPECT = 1.7, 2.3
MIN_WIDTH = 512


def image_dimensions(data: bytes) -> Optional[Tuple[str, int, int]]:
    """Decode (kind, width, height) from a JPEG or PNG header, or None if it is neither.

    Parsing the header rather than calling an image library keeps this dependency-free, and it
    is the check that actually matters: an HTML error page has neither magic number, so it can
    never pass.
    """
    if data[:8] == b"\x89PNG\r\n\x1a\n":
        if len(data) < 24:
            return None
        width, height = struct.unpack(">II", data[16:24])
        return "png", width, height

    if data[:2] == b"\xff\xd8":
        offset = 2
        while offset + 9 < len(data):
            if data[offset] != 0xFF:
                return None
            marker = data[offset + 1]
            # SOF0/1/2 carry the frame dimensions.
            if marker in (0xC0, 0xC1, 0xC2):
                height, width = struct.unpack(">HH", data[offset + 5:offset + 9])
                return "jpeg", width, height
            segment = struct.unpack(">H", data[offset + 2:offset + 4])[0]
            offset += 2 + segment
        return None

    return None


def fetch(candidate: Dict[str, str], timeout: float = 120.0) -> Optional[Tuple[bytes, str, int, int]]:
    """Download and validate one candidate. Returns None with a reason printed on failure."""
    url = candidate["url"]
    try:
        request = urllib.request.Request(url, headers={"User-Agent": "CosmoSynth/1.0 (+data import)"})
        with urllib.request.urlopen(request, timeout=timeout) as response:
            data = response.read()
    except Exception as exc:  # noqa: BLE001
        print(f"      network error: {exc}")
        return None

    dimensions = image_dimensions(data)
    if dimensions is None:
        # This is the soft-404 case: 200 OK, HTML body, would have been written as a planet.
        head = data[:80].decode("utf-8", errors="replace").replace("\n", " ")
        print(f"      REJECTED: not an image ({len(data)} bytes, starts {head!r})")
        return None

    kind, width, height = dimensions
    aspect = width / height if height else 0.0

    if width < MIN_WIDTH:
        print(f"      REJECTED: {width}x{height} is below the {MIN_WIDTH} px minimum")
        return None
    if not (MIN_ASPECT <= aspect <= MAX_ASPECT):
        print(f"      REJECTED: {width}x{height} aspect {aspect:.2f} is not equirectangular")
        return None

    return data, kind, width, height


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default=OUTPUT_DIR)
    args = parser.parse_args()

    os.makedirs(args.output, exist_ok=True)
    manifest: Dict[str, Any] = {
        "_comment": (
            "Real measured surface imagery, downloaded by tools/cosmodata/fetch_surface_maps.py. "
            "Bodies listed here render with measured imagery and are marked Measured in the "
            "provenance panel. Every other body keeps its procedural surface."
        ),
        "generated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "maps": {},
    }

    imported, skipped = 0, []

    for body_id, candidates in SOURCES.items():
        print(f"[{body_id}]")
        chosen = None

        for candidate in candidates:
            print(f"    trying {candidate['url']}")
            result = fetch(candidate)
            if result is None:
                continue

            data, kind, width, height = result
            extension = "png" if kind == "png" else "jpg"
            filename = f"{body_id}.{extension}"
            with open(os.path.join(args.output, filename), "wb") as handle:
                handle.write(data)

            print(f"      OK {width}x{height} {kind}, {len(data) // 1024} kB -> {filename}")
            chosen = {
                "file": filename,
                "width": width,
                "height": height,
                "source": candidate["source"],
                "reference": candidate["reference"],
                "instrument": candidate["instrument"],
                "url": candidate["url"],
                "retrieved": datetime.now(timezone.utc).strftime("%Y-%m-%d"),
            }
            break

        if chosen is None:
            skipped.append(body_id)
            print("      no candidate validated; this body keeps its procedural surface")
        else:
            manifest["maps"][body_id] = chosen
            imported += 1

    with open(os.path.join(args.output, "surfaces.json"), "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2)
        handle.write("\n")

    print(f"\n{imported} measured surface map(s) imported to {args.output}")
    if skipped:
        print(f"  still procedural: {', '.join(skipped)}")
    print("  every other catalogue body remains procedural and is labelled so in the app.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
