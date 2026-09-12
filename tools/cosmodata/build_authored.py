#!/usr/bin/env python3
"""Validate the hand-authored places file and copy it into StreamingAssets.

Design B section 2.10. The file itself is `tools/cosmodata/authored/landmarks.json` and is
committed; this script is the gate that stops an entry without a citation, with an unknown
category or with a position that is neither (l, b) nor (ra, dec) from ever reaching the app.

    python3 tools/cosmodata/build_authored.py [--out DIR]
"""

import argparse
import json
import math
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))

CATEGORIES = {"cloud", "nebula", "cluster", "snr", "pulsar", "blackhole", "mw", "lg"}
CLOSEUPS = {"orion", "crab", "localbubble"}
PROVENANCE_CLASSES = {"Measured", "Inferred", "Procedural", "Fictional"}


def validate(document, path, catalogue_names=None):
    """Returns the place list, or raises. `catalogue_names` maps a source id to its name set."""
    problems = []
    if document.get("schema") != "cosmosynth.places/1":
        problems.append("schema is %r, expected 'cosmosynth.places/1'" % document.get("schema"))
    places = document.get("places")
    if not isinstance(places, list) or not places:
        raise SystemExit("%s: no places" % path)

    seen = set()
    for i, place in enumerate(places):
        where = "%s[%d] %s" % (path, i, place.get("id", "<no id>"))
        pid = place.get("id")
        if not pid or not isinstance(pid, str):
            problems.append("%s: missing id" % where)
        elif pid in seen:
            problems.append("%s: duplicate id" % where)
        else:
            seen.add(pid)
        if not place.get("name"):
            problems.append("%s: missing name" % where)
        # The rule this file exists to enforce.
        if not place.get("reference"):
            problems.append("%s: no reference. Every entry needs exactly one." % where)
        category = place.get("category")
        if category not in CATEGORIES:
            problems.append("%s: category %r is not one of %s"
                            % (where, category, sorted(CATEGORIES)))

        source = place.get("source")
        if source:
            # Position comes from a shipped catalogue, so there are no coordinates to be wrong.
            if not place.get("sourceName"):
                problems.append("%s: source %r without a sourceName to look up" % (where, source))
            elif catalogue_names is not None:
                names = catalogue_names.get(source)
                if names is None:
                    problems.append("%s: source %r is not a shipped catalogue" % (where, source))
                elif place["sourceName"] not in names:
                    problems.append("%s: %r is not a row of %s"
                                    % (where, place["sourceName"], source))
            for key in ("l", "b", "ra", "dec", "distancePc"):
                if key in place:
                    problems.append("%s: has both a source and a %s; pick one" % (where, key))
        else:
            has_gal = "l" in place and "b" in place
            has_eq = "ra" in place and "dec" in place
            if has_gal == has_eq:
                problems.append("%s: needs exactly one of (l, b) or (ra, dec)" % where)
            if "distancePc" not in place:
                problems.append("%s: no distancePc" % where)
            for key in ("l", "b", "ra", "dec", "distancePc", "distanceErrPc", "radiusPc"):
                if key in place:
                    value = place[key]
                    if not isinstance(value, (int, float)) or not math.isfinite(value):
                        problems.append("%s: %s is %r, not a finite number" % (where, key, value))
            if has_eq and not (0.0 <= place.get("ra", 0.0) < 360.0 and
                               -90.0 <= place.get("dec", 0.0) <= 90.0):
                problems.append("%s: ra/dec out of range" % where)
            if has_gal and not (-360.0 <= place.get("l", 0.0) <= 360.0 and
                                -90.0 <= place.get("b", 0.0) <= 90.0):
                problems.append("%s: l/b out of range" % where)
            if place.get("distancePc", 0.0) < 0.0:
                problems.append("%s: negative distance" % where)

        closeup = place.get("closeup")
        if closeup is not None and closeup not in CLOSEUPS:
            problems.append("%s: closeup %r is not one of %s" % (where, closeup, sorted(CLOSEUPS)))
        if "pulsarHz" in place and not (0.0 < place["pulsarHz"] < 1000.0):
            problems.append("%s: pulsarHz %r is not a plausible spin frequency"
                            % (where, place["pulsarHz"]))
        prov = place.get("provenance") or {}
        for field, cls in prov.items():
            if cls not in PROVENANCE_CLASSES:
                problems.append("%s: provenance %s is %r, not one of %s"
                                % (where, field, cls, sorted(PROVENANCE_CLASSES)))

    if problems:
        for p in problems:
            print("  FAIL  " + p)
        raise SystemExit("%s: %d problem(s); nothing written" % (path, len(problems)))
    return places


def catalogue_name_sets(out_dir):
    """Names available for `source` lookups, read from the files already in StreamingAssets."""
    sys.path.insert(0, HERE)
    import csdata
    out = {}
    for source, filename in (("globulars", "globulars.csgp"),
                             ("radcliffeClusters", "radcliffe_clusters.csgp")):
        path = os.path.join(out_dir, filename)
        if os.path.exists(path):
            out[source] = set(csdata.read(path, "CSGP").meta.get("names", []))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(REPO, "unity", "Assets", "StreamingAssets",
                                                  "CosmoSynth", "galaxy"))
    args = ap.parse_args()
    import csdata                                        # for write_unity_meta
    src = os.path.join(HERE, "authored", "landmarks.json")
    target = os.path.join(args.out, "landmarks.json")

    with open(src) as handle:
        document = json.load(handle)
    names = catalogue_name_sets(args.out)
    places = validate(document, src, names)
    print("  ok  %d places, every one with a reference" % len(places))
    for source, name_set in sorted(names.items()):
        used = [p["sourceName"] for p in places if p.get("source") == source]
        print("  ok  %d position(s) resolved against %s (%d names)"
              % (len(used), source, len(name_set)))

    os.makedirs(args.out, exist_ok=True)
    tmp = target + ".tmp"
    try:
        shutil.copyfile(src, tmp)
        validate(json.load(open(tmp)), tmp, names)
        os.replace(tmp, target)
        csdata.write_unity_meta(target)
    except Exception:
        for stale in (tmp, target):
            if os.path.exists(stale):
                os.remove(stale)
        raise
    print("wrote %s  %d bytes" % (target, os.path.getsize(target)))


if __name__ == "__main__":
    main()
