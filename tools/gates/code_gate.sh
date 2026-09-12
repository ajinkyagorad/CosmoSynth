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
  echo "$NCAM" > "${TMPDIR:-/tmp}/cosmosynth-g0d.cameras"

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
  local BADR="" f a b
  while IFS= read -r f; do
    [ -n "$f" ] || continue
    a=$(grep -c 'RenderedRadius' "$f" 2>/dev/null | tr -dc '0-9')
    b=$(grep -c 'IsGeometryDrawn' "$f" 2>/dev/null | tr -dc '0-9')
    [ "${a:-0}" -gt 0 ] && [ "${b:-0}" -eq 0 ] && BADR="$BADR ${f#"$ROOT"/}"
  done < <(grep -rlE --include='*.cs' 'RenderedRadius' "$SRC" 2>/dev/null)
  if [ -z "$BADR" ]; then pass "every RenderedRadius reader also references IsGeometryDrawn"
  else bad "RenderedRadius without IsGeometryDrawn:$BADR"; fi
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
