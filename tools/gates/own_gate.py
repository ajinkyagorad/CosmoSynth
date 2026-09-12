#!/usr/bin/env python3
"""G0h - lane ownership. CONTRACT sections 9.3 and 10.7.

  tools/gates/own_gate.py <lane>      every path a lane changed is a path it owns
  tools/gates/own_gate.py --audit     no two lanes claim a path at equal specificity
  tools/gates/own_gate.py             --audit (what gate-all.sh runs in main)
  tools/gates/own_gate.py --self-test a throwaway repo proves it can fail

Ownership, from section 9.3:

  1. Every tracked path has exactly one owner. Globs may overlap; the MOST SPECIFIC match
     wins - a literal path beats a ** glob, and a longer literal prefix beats a shorter one.
  2. A non-owner may edit a file only strictly between its own `lane:<name> BEGIN` and
     `lane:<name> END` marker lines. It never edits a marker line itself.
  3. Any *.meta change fails: the integrator's editor generates those at merge time.
  4. A `1b:` glob is owned only while tools/phase.txt is 1b or later, and while phase.txt is
     0 or 1a a lane whose phases include 1a is additionally held to the 1a path set.
"""
import os
import re
import subprocess
import sys
import tempfile

ROOT = os.environ.get("GATE_ROOT") or os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))))
LANES = "tools/lanes.txt"
PHASE = "tools/phase.txt"

# While phase.txt is 0 or 1a, a lane whose phases include 1a may touch only these.
PHASE1A = [
    "tools/cosmodata/**",
    "unity/Assets/StreamingAssets/**",
    "unity/Assets/CosmoSynth/Core/**",
    "tests/**",
    "docs/accuracy.md",
    "docs/data-sources.md",
]


def git(*args, cwd=None):
    p = subprocess.run(["git", "-C", cwd or ROOT] + list(args), capture_output=True, text=True)
    return p.returncode, p.stdout


def read_phase(root):
    path = os.path.join(root, PHASE)
    if not os.path.exists(path):
        return "0"
    for line in open(path, encoding="utf-8"):
        line = line.split("#", 1)[0].strip()
        if line:
            return line
    return "0"


def read_lanes(root):
    """{lane: {"phases": [..], "globs": [(glob, only_1b)]}} from tools/lanes.txt."""
    path = os.path.join(root, LANES)
    if not os.path.exists(path):
        return None
    lanes = {}
    for line in open(path, encoding="utf-8"):
        line = line.split("#", 1)[0].strip()
        if not line:
            continue
        parts = line.split()
        if len(parts) < 2:
            continue
        name, phases = parts[0], parts[1].split(",")
        globs = []
        for g in parts[2:]:
            globs.append((g[3:], True) if g.startswith("1b:") else (g, False))
        lanes[name] = {"phases": phases, "globs": globs}
    return lanes


def specificity(glob):
    """(no_wildcard, literal prefix length). Bigger is more specific; section 9.3 rule 1."""
    star = glob.find("*")
    if star < 0:
        return (1, len(glob))
    return (0, star)


def matches(glob, path):
    if "**" in glob:
        prefix = glob.split("**", 1)[0]
        return path.startswith(prefix)
    if "*" in glob:
        return re.fullmatch(re.escape(glob).replace(r"\*", "[^/]*"), path) is not None
    return path == glob


def owner_of(lanes, path, phase):
    """(lane, specificity) of the most specific claim, or (None, None). Ties are reported."""
    best, bestspec, tied = None, None, []
    for name, info in lanes.items():
        for glob, only_1b in info["globs"]:
            if only_1b and phase not in ("1b", "2", "3"):
                continue
            if not matches(glob, path):
                continue
            spec = specificity(glob)
            if bestspec is None or spec > bestspec:
                best, bestspec, tied = name, spec, [name]
            elif spec == bestspec and name != best:
                tied.append(name)
    return best, bestspec, sorted(set(tied)) if len(set(tied)) > 1 else []


def slot_hunks_ok(root, base, branch, path, lane):
    """True when every changed line lies strictly inside this lane's BEGIN/END markers, and
    no marker line itself moved."""
    rc, base_text = git("show", "%s:%s" % (base, path), cwd=root)
    if rc != 0:
        return False, "not present at the merge base, so there is no slot to edit"

    begin = re.compile(r"lane:%s BEGIN" % re.escape(lane))
    end = re.compile(r"lane:%s END" % re.escape(lane))
    spans, open_at = [], None
    for i, line in enumerate(base_text.splitlines(), 1):
        if begin.search(line):
            open_at = i
        elif end.search(line) and open_at is not None:
            spans.append((open_at, i))
            open_at = None
    if not spans:
        return False, "no 'lane:%s BEGIN/END' slot in it" % lane

    rc, diff = git("diff", "-U0", base, branch, "--", path, cwd=root)
    for line in diff.splitlines():
        m = re.match(r"^@@ -(\d+)(?:,(\d+))? \+", line)
        if not m:
            continue
        start = int(m.group(1))
        count = int(m.group(2) or 1)
        # A pure insertion (count 0) sits after `start`; a change covers [start, start+count).
        lo = start if count else start + 1
        hi = start + count - 1 if count else start
        if not any(b < lo and hi < e for b, e in spans):
            return False, "hunk at line %d-%d is outside every lane:%s slot" % (lo, hi, lane)
    return True, ""


def check_lane(root, lane, base=None, branch=None):
    fails = []
    lanes = read_lanes(root)
    if lanes is None:
        return ["%s does not exist: ownership is not being checked" % LANES], []
    if lane not in lanes:
        return ["'%s' is not a lane in %s" % (lane, LANES)], []

    phase = read_phase(root)
    branch = branch or "lane/%s" % lane
    if base is None:
        rc, out = git("merge-base", "master", branch, cwd=root)
        if rc != 0:
            return ["no merge base between master and %s" % branch], []
        base = out.strip()

    rc, out = git("diff", "--name-only", base, branch, cwd=root)
    if rc != 0:
        return ["cannot diff %s..%s" % (base, branch)], []
    changed = [p for p in out.splitlines() if p.strip()]

    restricted = "1a" in lanes[lane]["phases"] and phase in ("0", "1a")
    notes = []
    for path in changed:
        if path.endswith(".meta"):
            fails.append("%s: .meta files are the integrator's, generated at merge time" % path)
            continue
        if path == "docs/reports/%s.md" % lane:
            continue                                   # every lane owns its own report
        owner, _, tied = owner_of(lanes, path, phase)
        if tied:
            fails.append("%s: claimed at equal specificity by %s" % (path, ", ".join(tied)))
            continue
        if owner == lane:
            if restricted and not any(matches(g, path) for g in PHASE1A):
                fails.append("%s: owned, but phase is %s and it is outside the 1a path set"
                             % (path, phase))
            continue
        ok, why = slot_hunks_ok(root, base, branch, path, lane)
        if ok:
            notes.append("%s: edited inside the lane:%s slot" % (path, lane))
        else:
            fails.append("%s: owned by %s, and %s" % (path, owner or "the integrator", why))
    return fails, notes


def audit(root):
    """Equal-specificity collisions: the same normalised claim under two lanes, and any
    tracked file two lanes claim equally."""
    fails = []
    lanes = read_lanes(root)
    if lanes is None:
        return ["%s does not exist" % LANES], 0, 0
    phase = read_phase(root)

    claims = {}
    for name, info in lanes.items():
        for glob, _ in info["globs"]:
            claims.setdefault(glob, set()).add(name)
    for glob, names in sorted(claims.items()):
        if len(names) > 1:
            fails.append("glob '%s' is claimed by %s" % (glob, ", ".join(sorted(names))))

    rc, out = git("ls-files", cwd=root)
    tracked = [p for p in out.splitlines() if p.strip()] if rc == 0 else []
    for path in tracked:
        _, _, tied = owner_of(lanes, path, phase)
        if tied:
            fails.append("%s is claimed at equal specificity by %s" % (path, ", ".join(tied)))
    return fails, len(lanes), len(tracked)


# --------------------------------------------------------------------------- self-test

FIXTURE_TABLE = """# lane        phases  owned globs
alpha         1a,1b   src/alpha/** docs/alpha.md 1b:src/late.cs
beta          1b      src/beta/**
"""

SLOTTED = """line one
// lane:alpha BEGIN
// lane:alpha END
// lane:beta BEGIN
// lane:beta END
last line
"""


def self_test():
    ok = True
    d = tempfile.mkdtemp(prefix="cosmosynth-own-st.")

    def run(*a):
        subprocess.run(["git", "-C", d] + list(a), capture_output=True, text=True, check=False)

    def write(rel, body):
        p = os.path.join(d, rel)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8") as fh:
            fh.write(body)

    run("init", "-q", "-b", "master")
    run("config", "user.email", "gate@example.invalid")
    run("config", "user.name", "gate")
    write("tools/lanes.txt", FIXTURE_TABLE)
    write("tools/phase.txt", "1b\n")
    write("src/alpha/a.cs", "alpha\n")
    write("src/beta/b.cs", "beta\n")
    write("src/shared/Registry.cs", SLOTTED)
    write("docs/alpha.md", "alpha doc\n")
    run("add", "-A")
    run("commit", "-qm", "base")
    rc, base = git("rev-parse", "HEAD", cwd=d)
    base = base.strip()

    cases = []

    def branch(name, mutate):
        run("checkout", "-q", "-B", name, base)
        mutate()
        run("add", "-A")
        run("commit", "-qm", name)
        run("checkout", "-q", "master")

    branch("lane/alpha-owned", lambda: write("src/alpha/a.cs", "alpha changed\n"))
    cases.append(("a lane edits a file it owns", "lane/alpha-owned", True))

    branch("lane/alpha-slot", lambda: write("src/shared/Registry.cs", SLOTTED.replace(
        "// lane:alpha END", "AddLayer<AlphaLayer>();\n// lane:alpha END")))
    cases.append(("a lane edits inside its own slot", "lane/alpha-slot", True))

    branch("lane/alpha-other-slot", lambda: write("src/shared/Registry.cs", SLOTTED.replace(
        "// lane:beta END", "AddLayer<Sneak>();\n// lane:beta END")))
    cases.append(("a lane edits another lane's slot", "lane/alpha-other-slot", False))

    branch("lane/alpha-marker", lambda: write("src/shared/Registry.cs", SLOTTED.replace(
        "// lane:alpha BEGIN", "// lane:alpha BEGIN (moved)")))
    cases.append(("a lane edits a marker line", "lane/alpha-marker", False))

    branch("lane/alpha-meta", lambda: write("src/alpha/a.cs.meta", "guid: 1\n"))
    cases.append(("a lane adds a .meta", "lane/alpha-meta", False))

    branch("lane/alpha-unowned", lambda: write("src/beta/b.cs", "alpha wrote this\n"))
    cases.append(("a lane edits a file it does not own", "lane/alpha-unowned", False))

    branch("lane/alpha-outside", lambda: write("src/shared/New.cs", "new\n"))
    cases.append(("a lane adds a file nobody owns", "lane/alpha-outside", False))

    for name, br, want_pass in cases:
        fails, _ = check_lane(d, "alpha", base=base, branch=br)
        got_pass = not fails
        if got_pass == want_pass:
            print("  ok    self-test: %-36s %s" % (name, "passes" if got_pass else "fails"))
        else:
            print("  SELF-TEST FAIL: %s %s" % (name, "passed" if got_pass else "failed"))
            for f in fails:
                print("        %s" % f)
            ok = False

    # The 1a restriction: with phase 0 the same owned edit outside the 1a set must fail.
    write("tools/phase.txt", "0\n")
    run("add", "-A")
    run("commit", "-qm", "phase 0")
    rc, base0 = git("rev-parse", "HEAD", cwd=d)
    base0 = base0.strip()
    run("checkout", "-q", "-B", "lane/alpha-phase", base0)
    write("src/alpha/a.cs", "alpha in phase 0\n")
    run("add", "-A")
    run("commit", "-qm", "phase")
    run("checkout", "-q", "master")
    fails, _ = check_lane(d, "alpha", base=base0, branch="lane/alpha-phase")
    if fails:
        print("  ok    self-test: %-36s fails" % "an owned edit outside the 1a set at phase 0")
    else:
        print("  SELF-TEST FAIL: the phase-0 restriction let an owned edit through")
        ok = False

    # --audit both ways.
    fails, _, _ = audit(d)
    if fails:
        print("  SELF-TEST FAIL: audit found a collision in a clean table: %s" % fails)
        ok = False
    else:
        print("  ok    self-test: %-36s passes" % "audit of a collision-free table")
    write("tools/lanes.txt", FIXTURE_TABLE + "gamma         1b      src/beta/**\n")
    fails, _, _ = audit(d)
    if fails:
        print("  ok    self-test: %-36s fails" % "audit of two lanes claiming src/beta/**")
    else:
        print("  SELF-TEST FAIL: audit passed a duplicated glob")
        ok = False

    subprocess.run(["rm", "-rf", d])
    print("SELF-TEST %s" % ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()

    args = [a for a in argv[1:] if not a.startswith("-")]
    if args:
        lane = args[0]
        fails, notes = check_lane(ROOT, lane)
        print("G0h ownership gate on lane/%s (phase %s)" % (lane, read_phase(ROOT)))
        for n in notes:
            print("  ok   %s" % n)
        for f in fails:
            print("  FAIL %s" % f)
        print("G0h %s%s" % ("FAIL (%d)" % len(fails) if fails else "PASS",
                            "" if fails else " - every changed path is owned or in a slot"))
        return 1 if fails else 0

    fails, nlanes, ntracked = audit(ROOT)
    print("G0h ownership audit of %s (phase %s)" % (LANES, read_phase(ROOT)))
    for f in fails:
        print("  FAIL %s" % f)
    if fails:
        print("G0h FAIL (%d)" % len(fails))
        return 1
    print("  ok   %d lanes, %d tracked paths, no equal-specificity collision" % (nlanes, ntracked))
    print("G0h PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
