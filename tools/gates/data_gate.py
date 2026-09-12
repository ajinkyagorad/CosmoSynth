#!/usr/bin/env python3
"""G0f - shipped data is documented, inside budget, and droppable. CONTRACT sections 6.2.2,
6.7, 10.6 and Appendix D.

  tools/gates/data_gate.py            the fast checks
  tools/gates/data_gate.py --full     adds the drop test (about a minute, about 100 MB)
  tools/gates/data_gate.py --self-test

WHAT THIS GATE CANNOT DO YET, AND SAYS SO OUT LOUD.

The contract's G0f is `ledger.py --check` over `SA/manifest.json`: every SA file in the
manifest, every manifest file present, every licenceTag allowed by the profile, and a drop
test that deletes every `"droppable": true` entry and re-runs the Core suite. `manifest.json`
and `ledger.py` are Section B / Phase 1a and do not exist on this tree. So those checks are
printed as NOT CHECKED, with the phase at which they become mandatory, and the summary line
carries the skipped count. From `tools/phase.txt` = 1a onward a missing manifest is a FAILURE,
not a skip: this gate must not be allowed to stay lenient by inertia.

The documentation check is also weaker than the contract's. `docs/data-sources.md` documents
SOURCES, not output filenames: the 24 spacecraft meshes are one NASA 3D Resources entry, and
naming each would be prose nobody maintains. So the rule is per DIRECTORY - every directory
under StreamingAssets must have at least one file named in the document, or its own path
named. A new data family with no entry fails; a 25th mesh in a documented folder passes. The
per-file rule arrives with the manifest, which is the machine-readable source (R35).
"""
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.environ.get("GATE_ROOT") or os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))))
SA = "unity/Assets/StreamingAssets"
DOC = "docs/data-sources.md"
BUDGET = "tools/gates/data-budget.txt"
PHASE = "tools/phase.txt"
MANIFEST = "unity/Assets/StreamingAssets/CosmoSynth/manifest.json"

FAILS = []
SKIPS = []
OKS = []


def check(name, ok, detail):
    (OKS if ok else FAILS).append((name, detail))


def skip(name, detail):
    SKIPS.append((name, detail))


def read_phase(root):
    path = os.path.join(root, PHASE)
    if not os.path.exists(path):
        return "0"
    for line in open(path, encoding="utf-8"):
        line = line.split("#", 1)[0].strip()
        if line:
            return line
    return "0"


def read_budget(root):
    path = os.path.join(root, BUDGET)
    if not os.path.exists(path):
        return None
    for line in open(path, encoding="utf-8"):
        m = re.match(r"^\s*(\d+)", line)
        if m:
            return int(m.group(1))
    return None


def sa_files(root):
    base = os.path.join(root, SA)
    out = []
    for dirpath, _, names in os.walk(base):
        for n in names:
            if n.endswith(".meta"):
                continue
            full = os.path.join(dirpath, n)
            out.append((os.path.relpath(full, root).replace(os.sep, "/"), os.path.getsize(full)))
    return sorted(out)


def run(root, full=False):
    del FAILS[:], SKIPS[:], OKS[:]
    phase = read_phase(root)

    files = sa_files(root)
    if not files:
        check("present", False, "%s holds no data files" % SA)
        return phase
    total = sum(size for _, size in files)

    # ------------------------------------------------------------------ budget
    budget = read_budget(root)
    if budget is None:
        check("budget", False, "%s is missing" % BUDGET)
    else:
        check("budget", total <= budget * 1024 * 1024,
              "%.1f MB of %d MB in %d files" % (total / 1048576.0, budget, len(files)))

    # -------------------------------------------------------------- not empty
    empty = [p for p, size in files if size == 0]
    check("non-empty", not empty,
          "every shipped file has bytes" if not empty else "empty: %s" % ", ".join(empty[:5]))

    # --------------------------------------------------------------- tracked
    p = subprocess.run(["git", "-C", root, "ls-files", SA], capture_output=True, text=True)
    if p.returncode != 0:
        skip("tracked", "not a git tree")
    else:
        tracked = set(p.stdout.split())
        untracked = [f for f, _ in files if f not in tracked]
        check("tracked", not untracked,
              "all %d files are committed" % len(files) if not untracked
              else "untracked, so a lane worktree would not have them: %s"
                   % ", ".join(untracked[:5]))

    # ------------------------------------------------------------ documented
    doc_path = os.path.join(root, DOC)
    if not os.path.exists(doc_path):
        check("documented", False, "%s is missing" % DOC)
    else:
        doc = open(doc_path, encoding="utf-8", errors="replace").read()
        dirs = {}
        for f, _ in files:
            dirs.setdefault(os.path.dirname(f), []).append(os.path.basename(f))
        undocumented = []
        for d, names in sorted(dirs.items()):
            short = d[len(SA) + 1:] if d.startswith(SA + "/") else d
            if any(n in doc for n in names) or short in doc or d in doc:
                continue
            undocumented.append("%s (%d files)" % (short, len(names)))
        check("documented", not undocumented,
              "%d data directories, each with an entry" % len(dirs) if not undocumented
              else "no entry in %s for: %s" % (DOC, "; ".join(undocumented)))

    # -------------------------------------------------------------- manifest
    manifest = os.path.join(root, MANIFEST)
    if os.path.exists(manifest):
        check("manifest", True, "%s present" % MANIFEST)
        skip("manifest-crosscheck",
             "ledger.py --check is not written: the manifest is present but not cross-checked")
    elif phase in ("0",):
        skip("manifest", "%s absent; Section B writes it in 1a. REQUIRED from phase 1a "
                         "(tools/phase.txt is '%s')" % (MANIFEST, phase))
    else:
        check("manifest", False,
              "%s is absent and tools/phase.txt is '%s': it is required from 1a" % (MANIFEST, phase))

    # ------------------------------------------------------------- drop test
    if not full:
        skip("drop", "--full only (it costs about a minute and about 100 MB)")
    elif not os.path.exists(manifest):
        skip("drop", "no manifest, so nothing declares itself droppable; "
                     "the drop test starts when Section B lands it")
    else:
        skip("drop", "the manifest exists but the drop runner is not written; "
                     "CONTRACT 10.6 drop_test.sh")

    return phase


def report(phase, full):
    print("G0f data gate on %s (phase %s%s)"
          % (os.path.relpath(ROOT), phase, ", --full" if full else ""))
    for name, detail in OKS:
        print("  ok   %-18s %s" % (name, detail))
    for name, detail in SKIPS:
        print("  SKIP %-18s %s" % (name, detail))
    for name, detail in FAILS:
        print("  FAIL %-18s %s" % (name, detail))
    if FAILS:
        print("G0f FAIL (%d)" % len(FAILS))
        return 1
    print("G0f PASS (%d checked, %d NOT CHECKED - see the SKIP lines)" % (len(OKS), len(SKIPS)))
    return 0


# --------------------------------------------------------------------------- self-test

def self_test():
    ok = True

    def build(root, size=1024, doc=True, empty=False, phase="0"):
        os.makedirs(os.path.join(root, SA, "CosmoSynth", "sky"))
        os.makedirs(os.path.join(root, "docs"))
        os.makedirs(os.path.join(root, "tools", "gates"))
        with open(os.path.join(root, SA, "CosmoSynth", "sky", "stars.csstars"), "wb") as fh:
            fh.write(b"\0" * (0 if empty else size))
        with open(os.path.join(root, DOC), "w", encoding="utf-8") as fh:
            fh.write("stars.csstars from AT-HYG, CC BY-SA 4.0\n" if doc else "nothing here\n")
        with open(os.path.join(root, BUDGET), "w", encoding="utf-8") as fh:
            fh.write("50\n")
        with open(os.path.join(root, PHASE), "w", encoding="utf-8") as fh:
            fh.write(phase + "\n")

    cases = [
        ("a documented file inside budget", dict(), True),
        ("a file no entry in the document names", dict(doc=False), False),
        ("a zero-byte shipped file", dict(empty=True), False),
        ("more bytes than data-budget.txt allows", dict(size=51 * 1024 * 1024), False),
        ("phase 1a with no manifest.json", dict(phase="1a"), False),
    ]
    for name, kw, want_pass in cases:
        d = tempfile.mkdtemp(prefix="cosmosynth-data-st.")
        build(d, **kw)
        run(d)
        got_pass = not FAILS
        if got_pass == want_pass:
            print("  ok    self-test: %-38s %s" % (name, "passes" if got_pass else "fails"))
        else:
            print("  SELF-TEST FAIL: %s %s (%s)"
                  % (name, "passed" if got_pass else "failed", FAILS))
            ok = False
        shutil.rmtree(d, ignore_errors=True)

    print("SELF-TEST %s" % ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    full = "--full" in argv
    phase = run(ROOT, full=full)
    return report(phase, full)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
