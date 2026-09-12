#!/usr/bin/env python3
"""Fetch the CC0 HIP <-> HD identifier table that finishes the constellation bridge.

    python3 tools/cosmodata/fetch_hip_bridge.py           # only the ids the figures need
    python3 tools/cosmodata/fetch_hip_bridge.py --all     # every pair Wikidata has

Writes tools/cosmodata/cache/hip_hd_wikidata.json, which build_starfield.py reads if it is
there and ignores if it is not. The cache is NOT shipped and NOT committed; the ledger
ignores cache/.

WHY THIS IS A SEPARATE SCRIPT
    Every figure vertex in the Stellarium sky culture is a Hipparcos number. Hipparcos itself
    is ESA CC BY-NC 3.0 IGO, so the HIP -> position table cannot be redistributed, and the
    shipped catalogue has to be reached through somebody else's identifier table instead.

    MEASURED 2026-09-12, on the staged data: IAU-CSN resolves 244 of the 710 figure HIP ids
    (34.4%), and Stellarium's own traditional-name table adds 8 more. That is 138 of 695
    segments, so 80% of the figure geometry is missing and no amount of care with the files
    already on disk recovers it. Wikidata is the remaining route and it is CC0:

        "All data in Wikidata has a CC0 license."
        -- https://www.wikidata.org/wiki/Wikidata:Licensing, read 2026-09-12

    It is a separate script because importers do not reach the network for an identifier
    table: this runs once, by hand, and its result is a file. It also could not be run when
    this lane was written, because triton had 5.2 GB free against a 6 GB floor.

WHAT IT IS NOT
    It is not a laundering route for ESA astrometry. What comes back is pairs of catalogue
    numbers -- facts about naming -- and the positions still come from Yale or from HYG under
    their own licences. If this script ever starts asking Wikidata for coordinates, that is
    the line, and it is on the wrong side of it.

Standard library only.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
CACHE = os.path.join(HERE, "cache")
OUT = os.path.join(CACHE, "hip_hd_wikidata.json")

ENDPOINT = "https://query.wikidata.org/sparql"
USER_AGENT = "CosmoSynth/2.0 (importer; https://github.com/ajinkyagorad) python-urllib"

#: P528 is "catalog code", qualified by P972 "catalog". Q537199 is the Hipparcos catalogue
#: and Q111130 the Henry Draper catalogue. A plain scan of this times out (VERIFIED
#: 2026-09-12, 120 s), so it is always run over an explicit VALUES block in small batches.
QUERY = """SELECT ?hip ?hd WHERE {
  VALUES ?hip { %s }
  ?s p:P528 ?a . ?a ps:P528 ?hip ; pq:P972 wd:Q537199 .
  ?s p:P528 ?b . ?b ps:P528 ?hd  ; pq:P972 wd:Q111130 .
}"""

BATCH = 50
MINIMUM_FREE_GIB = 6.0


def figure_hips():
    """The HIP ids the 88 figures actually use: 710 of them, not the whole catalogue."""
    for candidate in (os.environ.get("COSMODATA_STAGING"),
                      os.path.join(REPO_ROOT, "data-staging"),
                      os.path.expanduser("~/projects/CosmoSynth/data-staging")):
        if candidate and os.path.isdir(candidate):
            path = os.path.join(candidate, "stellarium-modern-skyculture", "index.json")
            if os.path.exists(path):
                sky = json.load(open(path, encoding="utf-8"))
                return sorted({int(h) for c in sky.get("constellations", [])
                               for stroke in c.get("lines", []) for h in stroke})
    raise SystemExit("stellarium-modern-skyculture/index.json not found in data-staging/")


def ask(hips):
    values = " ".join('"%d"' % h for h in hips)
    url = ENDPOINT + "?" + urllib.parse.urlencode({"query": QUERY % values, "format": "json"})
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT,
                                                   "Accept": "application/sparql-results+json"})
    # WDQS answers 429 with a Retry-After during an outage and has been seen at one request
    # per minute (observed 2026-09-12: "Aggressively rate-limiting to 1 req / min - this rule
    # was created during active wdqs outage"). Honour it rather than hammering: a 429 that is
    # retried immediately is how a client gets blocked outright.
    for attempt in range(6):
        try:
            with urllib.request.urlopen(request, timeout=180) as response:
                body = response.read()
            if not body.lstrip().startswith(b"{"):
                raise ValueError("the endpoint answered with something that is not JSON")
            return json.loads(body)["results"]["bindings"]
        except urllib.error.HTTPError as error:
            if error.code not in (429, 500, 502, 503, 504) or attempt == 5:
                raise
            wait = 65.0
            header = error.headers.get("Retry-After") if error.headers else None
            if header:
                try:
                    wait = max(5.0, float(header))
                except ValueError:
                    pass
            print(f"  HTTP {error.code}, waiting {wait:.0f}s", file=sys.stderr)
            time.sleep(wait)
        except Exception as error:                       # noqa: BLE001
            if attempt == 5:
                raise
            print(f"  retry {attempt + 1}: {error}", file=sys.stderr)
            time.sleep(10.0 * (attempt + 1))
    return []


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--all", action="store_true",
                        help="ask for every HIP 1..120404 rather than only the figure stars")
    parser.add_argument("--force", action="store_true",
                        help="run even when the disk is below the free-space floor")
    args = parser.parse_args(argv)

    statvfs = os.statvfs("/")
    free_gib = statvfs.f_bavail * statvfs.f_frsize / (1024 ** 3)
    if free_gib < MINIMUM_FREE_GIB and not args.force:
        raise SystemExit(
            f"{free_gib:.1f} GiB free on /, below the {MINIMUM_FREE_GIB:.0f} GiB floor. "
            "This fetch is about 40 KB, but the floor is the floor: pass --force if you "
            "have decided otherwise.")

    hips = list(range(1, 120405)) if args.all else figure_hips()
    print(f"asking Wikidata for {len(hips)} HIP ids in batches of {BATCH}")

    pairs = {}
    for start in range(0, len(hips), BATCH):
        batch = hips[start:start + BATCH]
        for row in ask(batch):
            try:
                hip = int(row["hip"]["value"])
                hd = int(row["hd"]["value"].strip().lstrip("HD").strip())
            except (KeyError, ValueError):
                continue
            pairs[hip] = hd
        print(f"  {start + len(batch)}/{len(hips)}: {len(pairs)} pairs so far")
        time.sleep(62.0)

    if not pairs:
        raise SystemExit("no pairs came back; writing nothing rather than an empty cache")

    os.makedirs(CACHE, exist_ok=True)
    with open(OUT + ".tmp", "w", encoding="utf-8") as handle:
        json.dump({str(k): v for k, v in sorted(pairs.items())}, handle, indent=0)
    os.replace(OUT + ".tmp", OUT)
    print(f"wrote {OUT}: {len(pairs)} HIP->HD pairs "
          f"({100.0 * len(pairs) / len(hips):.1f}% of those asked for)")
    print("now re-run: python3 tools/cosmodata/build_starfield.py")
    return 0


if __name__ == "__main__":
    sys.exit(main())
