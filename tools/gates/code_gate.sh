#!/usr/bin/env bash
# G0d - runtime code invariants. Every rule here is a bug that has already shipped to a
# headset somewhere on this box: a second camera, a CopyFrom that drags the editor's
# clear flags into play mode, a Depth-only clear that keeps passthrough out, a
# targetTexture that renders the world into a texture nobody draws, an immediate-mode
# Graphics/GL call that ignores single-pass instancing.
#
# Scope: unity/Assets/CosmoSynth/Runtime/**/*.cs
#
#   tools/gates/code_gate.sh              check the working tree
#   tools/gates/code_gate.sh --self-test  prove each rule can fail
#
# A hit is excused only by a same-line `// gate-justified: <reason>`, and is still counted.
# Exit 0 pass, 1 fail.
set -uo pipefail

ROOT="${GATE_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
SRC="$ROOT/unity/Assets/CosmoSynth/Runtime"
fail=0
pass() { printf '  ok    %s\n' "$1"; }
bad()  { printf '  FAIL  %s\n' "$1"; fail=1; }

# All matches, minus the justified ones. Never `grep -q`: it exits on the first match,
# the writer of the pipe dies with SIGPIPE, and pipefail then reports a correct tree as
# failed - racily, depending on where the match sits.
hits()  { grep -rnE --include='*.cs' -- "$1" "$SRC" 2>/dev/null | grep -v '// gate-justified:'; }
count() { printf '%s' "$1" | grep -c . | tr -dc '0-9'; }

BANNED='CopyFrom\(
CameraClearFlags\.Depth
CameraClearFlags\.Nothing
targetTexture[[:space:]]*=
Graphics\.DrawMesh
Graphics\.RenderMesh
Graphics\.Blit
CommandBuffer
OnRenderImage
\bGL\.
Camera\.current'

confined() {   # $1 = ERE, $2 = basename allowed, $3 = label
  local h n stray
  h=$(hits "$1")
  stray=$(printf '%s\n' "$h" | grep -v "/$2:" | grep .)
  n=$(count "$stray"); n=${n:-0}
  if [ "$n" -eq 0 ]; then pass "$3 only in $2"
  else bad "$3 outside $2 ($n)"; printf '%s\n' "$stray" | sed 's/^/        /' | head -5; fi
}

run_tree() {
  [ -d "$SRC" ] || { bad "no runtime source at $SRC"; return; }

  # ---- exactly one camera. Two cameras on a Quest is the 1.7.0 bug class itself.
  local CAMS NCAM
  CAMS=$(hits 'AddComponent<Camera>|AddComponent\(typeof\(Camera\)\)')
  NCAM=$(count "$CAMS"); NCAM=${NCAM:-0}
  if [ "$NCAM" -eq 1 ]; then
    pass "exactly 1 AddComponent<Camera>: $(printf '%s' "$CAMS" | head -1 | cut -c1-90)"
  else
    bad "$NCAM AddComponent<Camera> sites (exactly 1 required)"
    printf '%s\n' "$CAMS" | sed 's/^/        /' | head -10
  fi
  # G0c and G3 read this: a setting that is only safe with one camera must know how many
  # there are. Written on every run so it can never be stale-but-plausible.
  # Per TREE, the same md5-of-root convention run-tests.sh uses for its lock. One filename
  # for every worktree on this box is a race, not a handoff: six lane agents run gate-all
  # concurrently here, and G0c read another lane's count -- reporting "2 cameras" against a tree
  # whose only AddComponent<Camera> this gate had just counted as 1, on the phase whose entire
  # subject is the camera count.
  echo "$NCAM" > "${TMPDIR:-/tmp}/cosmosynth-g0d-$(printf %s "$ROOT" | md5sum | cut -c1-8).cameras"

  local pat h n
  while IFS= read -r pat; do
    [ -n "$pat" ] || continue
    h=$(hits "$pat"); n=$(count "$h"); n=${n:-0}
    if [ "$n" -eq 0 ]; then pass "no $pat"
    else bad "$n x $pat"; printf '%s\n' "$h" | sed 's/^/        /' | head -5; fi
  done <<EOF
$BANNED
EOF

  confined 'Shader\.Find\(' 'CosmoShaders.cs' 'Shader.Find('
  # An x,z,y reorder outside the one file that owns the coordinate convention is how a
  # scene ends up mirrored in a way nobody can see on a flat screen.
  confined '\.x[[:space:]]*,[^,;]*\.z[[:space:]]*,[^,;]*\.y' 'FloatingOrigin.cs' 'axis-swap shape'
  confined 'new Color\(|Color\.[a-z]' 'CosmoTheme.cs' 'literal colour'

  local SETC
  SETC=$(hits 'SetCatalog\(' | grep -v 'void SetCatalog(')
  n=$(count "$SETC"); n=${n:-0}
  if [ "$n" -le 1 ]; then pass "SetCatalog( called at most once outside its definition ($n)"
  else bad "SetCatalog( called $n times outside its definition"; printf '%s\n' "$SETC" | sed 's/^/        /' | head -5; fi

  # ---- a radius that was never drawn is a lie; whoever reads one must read the other
  #
  # Comments are STRIPPED before counting. They were not, and a `//` line mentioning the guard
  # satisfied the rule: BodyLabelLayer names IsGeometryDrawn only in prose at :189-190 and guards
  # with IsPointable at :193, so deleting the guard the comment describes left G0d green. That is
  # this project's own named verification-gap pattern -- the gate passed for a reason unrelated to
  # its name.
  #
  # IsPointable counts too. It is IsGeometryDrawn plus a pick test, so a file that asks it has
  # asked the question; the rule is "did you ask whether the body is drawn", not "did you type one
  # particular identifier".
  local BADR="" f a b
  while IFS= read -r f; do
    [ -n "$f" ] || continue
    a=$(sed 's://.*::' "$f" | grep -c 'RenderedRadius' | tr -dc '0-9')
    b=$(sed 's://.*::' "$f" | grep -cE 'IsGeometryDrawn|IsPointable' | tr -dc '0-9')
    [ "${a:-0}" -gt 0 ] && [ "${b:-0}" -eq 0 ] && BADR="$BADR ${f#"$ROOT"/}"
  done < <(grep -rlE --include='*.cs' 'RenderedRadius' "$SRC" 2>/dev/null)
  if [ -z "$BADR" ]; then pass "every RenderedRadius reader also asks whether the body is drawn"
  else bad "RenderedRadius without IsGeometryDrawn/IsPointable:$BADR"; fi

  # ---- room-space code reads RoomRadius, never RenderedRadius
  #
  # The rule above catches a STALE radius. It cannot catch one in the wrong SPACE, and that is
  # the other half of the same defect: RenderedRadius is the sphere transform's world radius,
  # and in Envelope mode the sphere is placed at TRUE scale on purpose and compressed by its own
  # shader. A caller measuring a room in metres then reads a planet in kilometres. It threw the
  # status card 6378 km to the left of the viewer with 76 cm type at true scale, and a pointer
  # ray would have hit the planet under your feet from anywhere (G1c `far`, 2026-09-12).
  #
  # Runtime/UI and Runtime/XR place things in the ROOM, so they read RoomRadius, which is zero
  # for a body that is not drawn AND for one the eye is inside. The raw radius has exactly three
  # legitimate readers and all are in Runtime/Rendering: RingSystem and AtmosphereShell are
  # children of the body and inherit its true scale, and ScaleRenderer measures rendered extents.
  local BADS
  # Whole-line comments are prose about the rule, not reads of the radius. (The first version
  # stripped them with a ':'-delimited sed whose pattern contained ':'; sed errored, the
  # output was empty, and the rule passed on its own mutant.)
  BADS=$(grep -rn --include='*.cs' 'RenderedRadius' "$SRC/UI" "$SRC/XR" 2>/dev/null \
         | grep -vE '^[^:]+:[0-9]+:[[:space:]]*//')
  if [ -z "$BADS" ]; then pass "room-space code (UI, XR) reads RoomRadius, not RenderedRadius"
  else bad "RenderedRadius in room-space code - use RoomRadius:"; printf '%s\n' "$BADS" | sed 's/^/        /' | head -8; fail=1; fi

  # ---- every runtime material comes from CosmoShaders
  #
  # Two rules, because the failure has two shapes and grep can only see one of them.
  #
  # (a) A CosmoShaders member that is not one of the eleven. The existing built-in-shader-name
  #     rule cannot see this: the names lived as constants INSIDE CosmoShaders.cs, which is
  #     exempt, and the call sites referred to them by identifier. Eight runtime sites asked for
  #     Standard, Sprites/Default, Particles/Standard Unlit, Unlit/Color, Unlit/Transparent and
  #     GUI/Text Shader that way, and G0d was green on all of them. None of those has a
  #     UniversalForward pass under URP.
  #
  # (b) `new Material(` outside CosmoShaders.cs. That is how CosmoText cloned the built-in font
  #     material -- a material carrying GUI/Text Shader, with no shader name anywhere in the file.
  #
  # ponytail: what neither can see is GameObject.CreatePrimitive's IMPLICIT default material,
  # which under URP is URP/Lit -- multi-variant, so strippable, so magenta in a player. No grep
  # reaches it; the play-mode `shaders` assertion does, by reading every enabled renderer's shader
  # name back and requiring it to be in CosmoShaders.All.
  #
  # -o, one MATCH per output line, not one line per hit. A line-based filter cannot do this: the
  # offending call is `CosmoShaders.Material(CosmoShaders.Standard)`, so the line matches an
  # allowed name as well as a forbidden one and a `grep -v` of the allowed set drops the whole
  # line -- the mutant passed until this was fixed. There is deliberately no // gate-justified:
  # hatch here: the eleven ARE the allowed set, and a member outside them is a shader that is not
  # in the build.
  local BADM
  BADM=$(grep -rnoE --include='*.cs' 'CosmoShaders\.[A-Za-z]+' "$SRC" 2>/dev/null \
         | grep -v '/CosmoShaders\.cs:' \
         | grep -vE ':CosmoShaders\.(Body|Point|Ribbon|Sky|Volume|Atmosphere|Rim|Ring|Unlit|Panel|Text|Material|Load|All)$')
  if [ -z "$BADM" ]; then pass "every CosmoShaders reference names one of the eleven"
  else bad "CosmoShaders member outside the eleven:"; printf '%s\n' "$BADM" | sed 's/^/        /' | head -8; fail=1; fi

  local BADN
  BADN=$(grep -rnE --include='*.cs' 'new Material\(' "$SRC" 2>/dev/null \
         | grep -v '/CosmoShaders\.cs:' \
         | grep -v '// gate-justified:')
  if [ -z "$BADN" ]; then pass "materials are constructed only in CosmoShaders.cs"
  else bad "new Material( outside CosmoShaders.cs:"; printf '%s\n' "$BADN" | sed 's/^/        /' | head -8; fail=1; fi
}

self_test() {
  local d rc out ok=1 R
  d=$(mktemp -d /tmp/cosmosynth-code-st.XXXXXX)
  R="$d/unity/Assets/CosmoSynth/Runtime"
  mkdir -p "$R"
  cat > "$R/CosmoShaders.cs" <<'CS'
public static class CosmoShaders { public static object Load(string n){ return Shader.Find(n); } }
CS
  cat > "$R/FloatingOrigin.cs" <<'CS'
public class FloatingOrigin { void F(){ var v = new Vector3(a.x, a.z, a.y); } }
CS
  cat > "$R/CosmoTheme.cs" <<'CS'
public static class CosmoTheme { public static object Body = new Color(1,1,1,1); public static object Accent = Color.white; }
CS
  cat > "$R/Rig.cs" <<'CS'
public class Rig { void Build(){ var c = go.AddComponent<Camera>(); c.clearFlags = CameraClearFlags.SolidColor; } }
CS
  cat > "$R/Body.cs" <<'CS'
public class Body { public float RenderedRadius; public bool IsGeometryDrawn; }
CS
  cat > "$R/Visual.cs" <<'CS'
public class Visual { void F(){ var m = CosmoShaders.Material(CosmoShaders.Body); } }
CS

  out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "SELF-TEST FAIL: the good fixture did not pass"; printf '%s\n' "$out"; rm -rf "$d"; return 1
  fi
  echo "  ok    self-test: good fixture passes"

  mutate() {  # $1 label, $2 file, $3 line to append
    printf '%s\n' "$3" >> "$R/$2"
    out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
    sed -i '$d' "$R/$2"
    if [ "$rc" -eq 0 ]; then echo "  SELF-TEST FAIL: mutant '$1' PASSED - the rule cannot fail"; ok=0
    else echo "  ok    self-test: mutant '$1' fails as it must"; fi
  }
  mutate "second camera"         Rig.cs  'public class Second { void F(){ go.AddComponent<Camera>(); } }'
  mutate "CopyFrom"              Rig.cs  'public class C2 { void F(){ cam.CopyFrom(other); } }'
  mutate "Depth-only clear"      Rig.cs  'public class C3 { void F(){ cam.clearFlags = CameraClearFlags.Depth; } }'
  mutate "targetTexture"         Rig.cs  'public class C4 { void F(){ cam.targetTexture = rt; } }'
  mutate "Graphics.DrawMesh"     Rig.cs  'public class C5 { void F(){ Graphics.DrawMesh(m, p, q, 0); } }'
  mutate "GL. immediate mode"    Rig.cs  'public class C6 { void F(){ GL.PushMatrix(); } }'
  mutate "Shader.Find elsewhere" Rig.cs  'public class C7 { void F(){ Shader.Find("x"); } }'
  mutate "axis swap elsewhere"   Rig.cs  'public class C8 { void F(){ var v = new Vector3(a.x, a.z, a.y); } }'
  mutate "colour outside theme"  Rig.cs  'public class C9 { void F(){ var c = new Color(1,0,0,1); } }'
  # This rule is file-scoped, so its mutant must be a NEW file: appending to Body.cs
  # leaves that file's own IsGeometryDrawn in place and the rule correctly stays green.
  printf '%s\n' 'public class Halo { void F(){ var r = x.RenderedRadius; } }' > "$R/Halo.cs"
  out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
  rm -f "$R/Halo.cs"
  if [ "$rc" -eq 0 ]; then echo "  SELF-TEST FAIL: mutant 'radius without drawn' PASSED - the rule cannot fail"; ok=0
  else echo "  ok    self-test: mutant 'radius without drawn' fails as it must"; fi

  # The hole the review found: the guard is a COMMENT and the real code has none. This is the
  # exact shape of BodyLabelLayer, where the rule stayed green with the guard deleted.
  cat > "$R/Halo.cs" <<'CS'
public class Halo {
  // IsGeometryDrawn is re-read here rather than trusted from when the candidate was chosen.
  void F(){ var r = x.RenderedRadius; }
}
CS
  out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
  rm -f "$R/Halo.cs"
  if [ "$rc" -eq 0 ]; then echo "  SELF-TEST FAIL: mutant 'a comment satisfies the guard' PASSED - the rule cannot fail"; ok=0
  else echo "  ok    self-test: mutant 'a comment satisfies the guard' fails as it must"; fi

  # The room-space rule, both ways: a UI file reading the raw radius must fail, and the same
  # file reading RoomRadius must pass. Without the second half the rule could be a grep for a
  # string nobody is allowed to type, which is not the same thing as a rule with an answer.
  mkdir -p "$R/UI"
  printf '%s\n' 'public class Card { void F(){ if (!v.IsPointable) return; var c = v.RenderedRadius + 0.1f; } }' > "$R/UI/Card.cs"
  out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
  if [ "$rc" -eq 0 ]; then echo "  SELF-TEST FAIL: mutant 'raw radius in room-space code' PASSED - the rule cannot fail"; ok=0
  else echo "  ok    self-test: mutant 'raw radius in room-space code' fails as it must"; fi
  printf '%s\n' 'public class Card { void F(){ var c = v.RoomRadius + 0.1f; } }' > "$R/UI/Card.cs"
  out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
  rm -rf "$R/UI"
  if [ "$rc" -eq 0 ]; then echo "  ok    self-test: RoomRadius in room-space code passes"
  else echo "  SELF-TEST FAIL: RoomRadius in room-space code should pass"; printf '%s\n' "$out"; ok=0; fi

  # ...and the positive control: IsPointable is IsGeometryDrawn plus a pick test, and counts.
  printf '%s\n' 'public class Label { void F(){ if (!v.IsPointable) return; var r = v.RenderedRadius; } }' > "$R/Label.cs"
  out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
  rm -f "$R/Label.cs"
  if [ "$rc" -eq 0 ]; then echo "  ok    self-test: IsPointable satisfies the drawn-check rule"
  else echo "  SELF-TEST FAIL: IsPointable should satisfy the drawn-check rule"; ok=0; fi

  # A CosmoShaders member outside the eleven. This is what eight call sites did for the whole
  # of the pre-migration tree while G0d reported PASS: the names were constants inside the exempt
  # file, so no built-in shader STRING ever appeared in Runtime.
  mutate "built-in shader by constant" Visual.cs 'public class V2 { void F(){ var m = CosmoShaders.Material(CosmoShaders.Standard); } }'
  mutate "material built outside CosmoShaders" Visual.cs 'public class V3 { void F(){ var m = new Material(font.material); } }'

  # And one justified hit must NOT fail, or the escape hatch is a lie.
  printf '%s\n' 'public class Just { void F(){ GL.PushMatrix(); } } // gate-justified: self-test' >> "$R/Rig.cs"
  out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
  sed -i '$d' "$R/Rig.cs"
  if [ "$rc" -eq 0 ]; then echo "  ok    self-test: a // gate-justified: hit passes"
  else echo "  SELF-TEST FAIL: a justified hit still failed"; printf '%s\n' "$out" | grep FAIL | head -3; ok=0; fi

  rm -rf "$d"
  [ "$ok" -eq 1 ] || return 1
  echo "SELF-TEST PASS"
  return 0
}

if [ "${1:-}" = "--self-test" ]; then
  self_test; exit $?
fi

echo "G0d runtime code gate on ${SRC#"$ROOT"/}"
run_tree
if [ "$fail" -ne 0 ]; then echo "G0d FAIL"; exit 1; fi
echo "G0d PASS"
exit 0
