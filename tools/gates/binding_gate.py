#!/usr/bin/env python3
"""G0g - every physical control is bound exactly once.

CONTRACT sections 5.7.4, 9.4 and 10.6. The defect this exists to stop is recorded in CLAUDE.md:
four components each polled the devices, so the trigger meant select AND grab AND boost,
stick-click meant pause AND confirm, and A meant recentre AND confirm - none of it visible in
any one file.

Two row forms are accepted, and the file may use only one of them:

  4-tuple (CONTRACT 5.7.4, from P0.7, written by the `nav` lane):
      new Binding("<control>", BindDevice.<device>, BindContext.<context>, "<action>"),
      uniqueness key = (control, device, context)

  2-tuple (what XrInput.cs carries today):
      ("<control>", "<action>"),
      uniqueness key = (control,)

A row inside the array that matches neither form is a failure: an unparsable row is how a
binding hides from the gate that exists to see it.

CONTRACT 10.6 lists this gate as known-red until P0.7 on the grounds that "the gate cannot
pass on" the 2-tuple form. It can: the invariant is the same one, with fewer columns, and the
2-tuple parser reads it. A gate that is red on purpose is a gate build.sh refuses to build
past, and a permanently excused gate teaches people to ignore the excuse list.

  tools/gates/binding_gate.py [FILE]     exit 0 = every control bound once
  tools/gates/binding_gate.py --self-test
"""
import os
import re
import sys
import tempfile

ROOT = os.environ.get("GATE_ROOT") or os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))))
DEFAULT = "unity/Assets/CosmoSynth/Runtime/XR/XrInput.cs"

ARRAY = re.compile(r"\bBindings\s*=\s*$|\bBindings\s*=\s*\{|\bBindings\s*=\s*new\b")

ROW4 = re.compile(r"""^\s*new\s+Binding\(\s*"([^"]*)"\s*,\s*
                       BindDevice\.(\w+)\s*,\s*
                       BindContext\.(\w+)\s*,\s*
                       "([^"]*)"\s*\)\s*,?\s*$""", re.X)
ROW2 = re.compile(r'^\s*\(\s*"([^"]*)"\s*,\s*"([^"]*)"\s*\)\s*,?\s*$')


def rows(text):
    """The lines of the Bindings initialiser, from its opening brace to the matching close.

    Brace counting, not a regex over the whole file: the array holds string literals with
    braces in none of them today, but a comment or an action name with one would silently
    truncate a line-based scan and the gate would pass by seeing fewer bindings.
    """
    lines = text.splitlines()
    start = None
    for i, line in enumerate(lines):
        if ARRAY.search(line):
            start = i
            break
    if start is None:
        return None, []

    depth = 0
    opened = False
    out = []
    for line in lines[start:]:
        stripped = line.split("//", 1)[0]
        if opened and depth == 1 and stripped.strip():
            out.append(line.split("//", 1)[0].rstrip())
        for ch in stripped:
            if ch == "{":
                depth += 1
                opened = True
            elif ch == "}":
                depth -= 1
                if opened and depth == 0:
                    # Punctuation-only lines are the array's own braces, not rows: the
                    # closing '};' is inside the collected range because depth is still 1
                    # when the line begins.
                    return start + 1, [r for r in out if r.strip().strip('{};,')]
    return start + 1, None      # unterminated


def check(path):
    fails = []
    if not os.path.exists(path):
        return ["%s does not exist" % path], 0, "none"

    with open(path, encoding="utf-8") as fh:
        text = fh.read()

    line0, body = rows(text)
    if line0 is None:
        return ["no Bindings initialiser in %s" % path], 0, "none"
    if body is None:
        return ["the Bindings initialiser in %s is not closed" % path], 0, "none"

    parsed = []
    form = None
    for row in body:
        if not row.strip():
            continue
        m4 = ROW4.match(row)
        m2 = ROW2.match(row)
        if m4:
            here = "4-tuple"
            key = (m4.group(1), m4.group(2), m4.group(3))
            action = m4.group(4)
        elif m2:
            here = "2-tuple"
            key = (m2.group(1),)
            action = m2.group(2)
        else:
            fails.append("unparsable binding row: %s" % row.strip())
            continue
        if form is None:
            form = here
        elif form != here:
            fails.append("mixed row forms (%s and %s): %s" % (form, here, row.strip()))
            continue
        parsed.append((key, action))

    seen = {}
    for key, action in parsed:
        label = "/".join(key)
        if label in seen:
            fails.append("control '%s' is bound twice: '%s' and '%s'" % (label, seen[label], action))
        else:
            seen[label] = action

    return fails, len(parsed), form or "none"


def self_test():
    ok = True
    d = tempfile.mkdtemp(prefix="cosmosynth-binding-st.")

    def write(name, body):
        p = os.path.join(d, name)
        with open(p, "w", encoding="utf-8") as fh:
            fh.write(body)
        return p

    good2 = write("Good2.cs", """
class XrInput {
    public static readonly (string Control, string Action)[] Bindings =
    {
        ("trigger", "select"),
        ("grip", "grab"),
        ("a", "recentre"),
    };
}
""")
    good4 = write("Good4.cs", """
class XrInput {
    public static readonly Binding[] Bindings = new Binding[]
    {
        new Binding("trigger", BindDevice.Right, BindContext.World, "select"),
        new Binding("trigger", BindDevice.Left, BindContext.World, "measure"),
        new Binding("trigger", BindDevice.Right, BindContext.Panel, "press"),
    };
}
""")
    dup2 = write("Dup2.cs", """
class XrInput {
    public static readonly (string Control, string Action)[] Bindings =
    {
        ("trigger", "select"),
        ("trigger", "boost"),
    };
}
""")
    dup4 = write("Dup4.cs", """
class XrInput {
    public static readonly Binding[] Bindings = new Binding[]
    {
        new Binding("stick-click", BindDevice.Left, BindContext.World, "pause"),
        new Binding("stick-click", BindDevice.Left, BindContext.World, "confirm"),
    };
}
""")
    junk = write("Junk.cs", """
class XrInput {
    public static readonly Binding[] Bindings = new Binding[]
    {
        new Binding("trigger", BindDevice.Right, BindContext.World, "select"),
        SomethingElse("grip", "grab"),
    };
}
""")
    mixed = write("Mixed.cs", """
class XrInput {
    public static readonly Binding[] Bindings = new Binding[]
    {
        new Binding("trigger", BindDevice.Right, BindContext.World, "select"),
        ("grip", "grab"),
    };
}
""")
    missing = write("Missing.cs", "class XrInput { int x; }\n")

    cases = [
        ("good 2-tuple", good2, True), ("good 4-tuple", good4, True),
        ("duplicate control (2-tuple)", dup2, False),
        ("duplicate triple (4-tuple)", dup4, False),
        ("unparsable row", junk, False),
        ("mixed row forms", mixed, False),
        ("no Bindings array", missing, False),
    ]
    for name, path, want_pass in cases:
        fails, n, form = check(path)
        got_pass = not fails
        if got_pass == want_pass:
            print("  ok    self-test: %-28s %s (%d rows, %s)"
                  % (name, "passes" if got_pass else "fails", n, form))
        else:
            print("  SELF-TEST FAIL: %s %s" % (name, "passed" if got_pass else "failed"))
            for f in fails:
                print("        %s" % f)
            ok = False

    import shutil
    shutil.rmtree(d, ignore_errors=True)
    print("SELF-TEST %s" % ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    path = argv[1] if len(argv) > 1 else os.path.join(ROOT, DEFAULT)
    fails, n, form = check(path)
    print("G0g binding gate on %s" % os.path.relpath(path, ROOT))
    if fails:
        for f in fails:
            print("  FAIL %s" % f)
        print("G0g FAIL (%d)" % len(fails))
        return 1
    print("  ok   %d bindings, %s rows, every control bound once" % (n, form))
    print("G0g PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
