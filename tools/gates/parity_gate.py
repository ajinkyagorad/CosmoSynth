#!/usr/bin/env python3
"""G0i - the Atlas parity gate.

docs/ATLAS-PARITY.md is the checklist that says what "entire functionality plus more"
means for the WebXR pages this project is replacing. A checklist nobody can fail is a
wish list, so this reads it and refuses rows that are not accounted for:

  - a duplicate or malformed id
  - an empty "what it does", "home" or "evidence" cell
  - a state outside absent / partial / present / dropped
  - a lane outside the contract's lane list
  - `none` as the lane on a row that is not dropped
  - a dropped row whose evidence does not start with DROP:
  - a DROP: reason on a row that is not dropped
  - a `present` or `partial` claim whose evidence names no file

The last one is the point. Every other check stops the table going vague; that one stops
it going optimistic. A row is present because a file does it, and the row says which file.

`absent` is an accounted-for state, so the gate is green while most of the work is still
to do and prints the absent count. `--require-parity` also fails while any row is absent:
that is the form gate-all.sh switches to when the phase that clears them closes, and it is
the machine-readable meaning of "entire functionality".

  tools/gates/parity_gate.py                  check docs/ATLAS-PARITY.md
  tools/gates/parity_gate.py --require-parity  ... and fail while any row is absent
  tools/gates/parity_gate.py --self-test      run the deliberately-broken fixture
  tools/gates/parity_gate.py <path>           check another file

Exit 0 green, 10 red.
"""
import os
import re
import sys
import tempfile

DOC = "docs/ATLAS-PARITY.md"

# The contract's lane table (docs/CONTRACT-SUMMARY.md "Lanes and what each owns"), plus
# `integrator` for everything the lane table does not assign, `parity` for this file and
# this gate, and `none` which is legal only on a dropped row.
LANES = {
    "p0-core", "p0-shaders", "stars", "milkyway", "galaxies", "cmb", "solar",
    "exoplanets", "fiction", "nav", "ui", "lines", "audio", "integrator", "parity",
    "none",
}
STATES = {"absent", "partial", "present", "dropped"}

ID_RE = re.compile(r"^[A-Z][A-Z0-9]{1,4}-\d{2}[a-z]?$")
# A file that does the thing: a path with a source/data extension, or anything under SA/.
FILE_RE = re.compile(r"(?:SA/[\w./*-]+|[\w][\w./*-]*\.(?:cs|py|sh|json|md|txt|shader))")


def rows(text):
    """Every six-column markdown row whose first cell is an id, with its line number."""
    out = []
    for n, line in enumerate(text.splitlines(), 1):
        s = line.strip()
        if not s.startswith("|"):
            continue
        cells = [c.strip() for c in s.strip("|").split("|")]
        if len(cells) != 6 or not ID_RE.match(cells[0]):
            continue
        out.append((n, cells))
    return out


def check(text):
    """Return (problems, counts). A problem is (line, id, message)."""
    problems = []
    counts = {s: 0 for s in STATES}
    seen = {}

    found = rows(text)
    if not found:
        problems.append((0, "-", "no parity rows found: the table was renamed or reformatted"))
        return problems, counts

    for line, (rid, what, home, state, lane, evidence) in found:
        if rid in seen:
            problems.append((line, rid, "duplicate id, first seen on line %d" % seen[rid]))
        seen[rid] = line

        state = state.lower()
        lane = lane.lower()
        dropped = state == "dropped"

        if len(what) < 8:
            problems.append((line, rid, "empty or stub 'what it does' cell"))
        # A dropped row has no home by definition; every other row must name one.
        if not dropped and len(home) < 2:
            problems.append((line, rid, "no CosmoSynth home named"))
        # `-` is allowed only where the row claims nothing: an absent row's note is
        # optional, because the home column already says what is missing. Every other
        # state has to say something.
        if len(evidence) < 2 and state != "absent":
            problems.append((line, rid, "no evidence or note"))

        if state not in STATES:
            problems.append((line, rid, "state %r is not one of %s"
                             % (state, "/".join(sorted(STATES)))))
            continue
        counts[state] += 1

        if lane not in LANES:
            problems.append((line, rid, "lane %r is not a lane in the contract" % lane))
        elif lane == "none" and not dropped:
            problems.append((line, rid, "lane 'none' is only legal on a dropped row"))
        elif dropped and lane != "none":
            problems.append((line, rid, "a dropped row is owned by no lane; use 'none'"))

        starts_drop = evidence.upper().startswith("DROP:")
        if dropped and not starts_drop:
            problems.append((line, rid, "dropped without a reason: evidence must start 'DROP:'"))
        if starts_drop and not dropped:
            problems.append((line, rid, "a DROP: reason on a row whose state is %r" % state))

        if state in ("present", "partial") and not FILE_RE.search(evidence):
            problems.append((line, rid,
                             "claims %s but names no file - evidence must point at the code "
                             "that does it" % state))

    return problems, counts


def report(text, require_parity, label):
    problems, counts = check(text)
    total = sum(counts.values())

    for line, rid, msg in problems:
        print("  %s:%d  %s: %s" % (label, line, rid, msg))

    tail = "(absent %d, partial %d, present %d, dropped %d)" % (
        counts["absent"], counts["partial"], counts["present"], counts["dropped"])

    if problems:
        print("parity_gate: FAIL - %d row(s) unaccounted for in %d %s"
              % (len(problems), total, tail))
        return 10
    if require_parity and counts["absent"]:
        print("parity_gate: FAIL - %d row(s) still absent of %d %s"
              % (counts["absent"], total, tail))
        return 10
    print("parity_gate: PASS - %d rows, 0 unaccounted %s" % (total, tail))
    if counts["absent"]:
        print("  %d rows are absent: accounted for, not built. "
              "--require-parity fails on these." % counts["absent"])
    return 0


GOOD = """
| Id | What | Home | State | Lane | Evidence |
|---|---|---|---|---|---|
| AAA-01 | draws a thing that matters | `stars` layer | present | stars | `U/Runtime/Rendering/StarSprite.cs` |
| AAA-02 | draws half a thing | `dust` layer | partial | milkyway | `U/Core/Sky/StarCatalog.cs` holds it |
| AAA-03 | draws a thing nobody built | `cmb` layer | absent | cmb | nothing exists |
| AAA-04 | a page stylesheet | - | dropped | none | DROP: styling, no function |
"""

# One row per violation class. Each comment is the message the checker must produce.
BROKEN = """
| Id | What | Home | State | Lane | Evidence |
|---|---|---|---|---|---|
| BBB-01 | draws a thing that matters | `stars` layer | present | stars | it is basically done |
| BBB-02 | draws a thing that matters | `dust` layer | partial | milkyway | mostly there |
| BBB-03 | draws a thing that matters | `cmb` layer | planned | cmb | `U/x.cs` |
| BBB-04 | draws a thing that matters | `lv` layer | absent | someday | nothing |
| BBB-05 | draws a thing that matters | `lss` layer | absent | none | nothing |
| BBB-06 | draws a thing that matters | - | dropped | none | it was rubbish |
| BBB-07 | draws a thing that matters | `arms` layer | absent | galaxies | DROP: not really |
| BBB-08 | draws a thing that matters | `orbits` layer | dropped | lines | DROP: superseded |
| BBB-09 | x | `belts` layer | absent | solar | nothing |
| BBB-10 | draws a thing that matters |  | absent | solar | nothing |
| BBB-11 | draws a thing that matters | `sites` layer | present | solar |  |
| BBB-01 | draws a thing that matters | `winds` layer | absent | solar | nothing |
"""

EXPECTED = [
    ("BBB-01", "names no file"),
    ("BBB-02", "names no file"),
    ("BBB-03", "is not one of"),
    ("BBB-04", "is not a lane"),
    ("BBB-05", "only legal on a dropped row"),
    ("BBB-06", "must start 'DROP:'"),
    ("BBB-07", "DROP: reason on a row"),
    ("BBB-08", "owned by no lane"),
    ("BBB-09", "stub 'what it does'"),
    ("BBB-10", "no CosmoSynth home"),
    ("BBB-11", "no evidence"),
    ("BBB-11", "names no file"),
    ("BBB-01", "duplicate id"),
]


def self_test():
    """A gate that has never failed is not a gate. Fail it on purpose, here, every run."""
    ok = True

    problems, counts = check(BROKEN)
    got = [(rid, msg) for _, rid, msg in problems]
    for rid, fragment in EXPECTED:
        if not any(r == rid and fragment in m for r, m in got):
            print("  self-test: %s should have been reported for %r" % (rid, fragment))
            ok = False
    if len(problems) != len(EXPECTED):
        print("  self-test: expected %d problems, got %d:" % (len(EXPECTED), len(problems)))
        for rid, msg in got:
            print("      %s: %s" % (rid, msg))
        ok = False

    problems, counts = check(GOOD)
    if problems:
        print("  self-test: the clean table was rejected:")
        for _, rid, msg in problems:
            print("      %s: %s" % (rid, msg))
        ok = False
    if counts != {"present": 1, "partial": 1, "absent": 1, "dropped": 1}:
        print("  self-test: miscounted the clean table: %r" % counts)
        ok = False

    # An empty document must be red, so "all green" can never mean "nothing was read".
    problems, _ = check("# nothing here\n")
    if not problems:
        print("  self-test: an empty document passed")
        ok = False

    # End to end through report(), on real files, so the exit codes are exercised too.
    with tempfile.TemporaryDirectory() as d:
        good = os.path.join(d, "good.md")
        bad = os.path.join(d, "bad.md")
        open(good, "w").write(GOOD)
        open(bad, "w").write(BROKEN)
        for path, args, want in ((good, False, 0), (good, True, 10), (bad, False, 10)):
            rc = report(open(path).read(), args, os.path.basename(path))
            if rc != want:
                print("  self-test: %s require_parity=%s gave %d, wanted %d"
                      % (os.path.basename(path), args, rc, want))
                ok = False

    print("SELF-TEST %s" % ("PASS" if ok else "FAIL"))
    return 0 if ok else 10


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("--")]
    flags = {a for a in argv[1:] if a.startswith("--")}

    if "--self-test" in flags:
        return self_test()

    # tools/gates/parity_gate.py -> tools/gates -> tools -> the repo root
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    path = args[0] if args else os.path.join(root, DOC)
    if not os.path.exists(path):
        print("parity_gate: FAIL - %s does not exist" % path)
        return 10
    return report(open(path, encoding="utf-8").read(), "--require-parity" in flags,
                  os.path.relpath(path, root) if not args else path)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
