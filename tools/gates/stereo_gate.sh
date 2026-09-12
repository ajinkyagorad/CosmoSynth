#!/usr/bin/env bash
# G0b - every shader that runs on the Quest must carry the single-pass-instanced
# macros, or the right eye renders the left eye's view (or nothing at all).
#
# Scope: *.shader and *.hlsl under unity/Assets/CosmoSynth/**, plus TextMesh Pro's
# TMP_SDF-Mobile.shader when it is vendored into the project.
#
#   tools/gates/stereo_gate.sh              check the working tree
#   tools/gates/stereo_gate.sh --self-test  prove the rules can fail
#
# Exit 0 pass, 1 fail.
set -uo pipefail

ROOT="${GATE_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
fail=0
pass() { printf '  ok    %s\n' "$1"; }
bad()  { printf '  FAIL  %s\n' "$1"; fail=1; }

# Count, never `grep -q`: -q exits on the first match, the writer of the pipe dies with
# SIGPIPE and `pipefail` then reports a CORRECT file as failed, racily.
cnt() { grep -c -- "$1" "$2" 2>/dev/null | tr -dc '0-9'; }
cntE() { grep -cE -- "$1" "$2" 2>/dev/null | tr -dc '0-9'; }

STEREO_MACROS="multi_compile_instancing
UNITY_VERTEX_INPUT_INSTANCE_ID
UNITY_VERTEX_OUTPUT_STEREO
UNITY_SETUP_INSTANCE_ID
UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO
UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX"

# R1 (contract 7.1 rule 2). URP's camera-position helper resolves to _WorldSpaceCameraPos,
# whose per-eye redefinition under XR is a package-version detail this project does not
# control; five render paths are wrong in BOTH eyes at once if it is the centre eye. The
# monoscopic play-mode harness cannot tell the two apart, which is exactly why the ban is
# static. The name is assembled rather than written so this script does not flag itself when
# it is ever moved under Runtime/Shaders.
BANNED_EYE="GetCamera""PositionWS"

# R12 (contract 7.1 rule 1). Object space is these three shaders' input; static batching bakes
# vertices into world space and would silently destroy all three.
NEEDS_NOBATCH="CosmoSynth/Volume
CosmoSynth/Sky
CosmoSynth/Ribbon"

# Body of ONE function, by brace depth from its definition. Used for the fragment-stage rule,
# which is about where a thing is read, not whether the file mentions it.
func_body() {
  awk -v fn="$2" '
    index($0, "#pragma") { next }
    !inb && $0 ~ ("(^|[^A-Za-z0-9_])" fn "[[:space:]]*\\(") { inb = 1 }
    inb {
      print
      d += gsub(/\{/, "{")
      d -= gsub(/\}/, "}")
      if (!started && d > 0) started = 1
      else if (started && d <= 0) { inb = 0; started = 0 }
    }
  ' "$1"
}

# The CBUFFER_START(UnityPerMaterial) ... CBUFFER_END block(s).
cbuffer_body() {
  awk '/CBUFFER_START\(UnityPerMaterial\)/ { inb = 1; next } /CBUFFER_END/ { inb = 0 } inb { print }' "$1"
}

# The Properties { ... } block, by brace depth.
properties_body() {
  awk '
    !inb && /^[[:space:]]*Properties[[:space:]]*$/ { inb = 1; next }
    !inb && /^[[:space:]]*Properties[[:space:]]*\{/ { inb = 1; d = 1; started = 1; next }
    inb {
      d += gsub(/\{/, "{")
      d -= gsub(/\}/, "}")
      if (!started && d > 0) { started = 1; next }
      if (started && d <= 0) { inb = 0; started = 0; next }
      print
    }
  ' "$1"
}

check_file() {
  local f="$1" ours="$2" rel n
  rel=${f#"$ROOT"/}

  n=$(cntE '^[[:space:]]*#pragma[[:space:]]+vertex' "$f")
  if [ "${n:-0}" -eq 0 ]; then
    pass "$rel (no vertex stage, stereo macros not required)"
    return
  fi

  local m miss=""
  while IFS= read -r m; do
    [ -n "$m" ] || continue
    n=$(cnt "$m" "$f")
    [ "${n:-0}" -gt 0 ] || miss="$miss $m"
  done <<EOF
$STEREO_MACROS
EOF
  if [ -n "$miss" ]; then
    bad "$rel missing stereo macros:$miss"
  else
    pass "$rel carries all 6 stereo macros"
  fi

  [ "$ours" = 1 ] || return

  n=$(cntE '^[[:space:]]*Shader[[:space:]]+"CosmoSynth/' "$f")
  [ "${n:-0}" -gt 0 ] || bad "$rel shader name is not under CosmoSynth/"

  n=$(cnt '"RenderPipeline"="UniversalPipeline"' "$f")
  [ "${n:-0}" -gt 0 ] || bad "$rel has no \"RenderPipeline\"=\"UniversalPipeline\" tag"

  # Variants multiply build time and shader memory on a tile GPU. Anything beyond
  # instancing needs a written reason on the same line.
  n=$(grep -nE '#pragma[[:space:]]+(multi_compile|shader_feature)' "$f" 2>/dev/null \
      | grep -vc 'multi_compile_instancing\|// variant-justified:' | tr -dc '0-9')
  [ "${n:-0}" -eq 0 ] || bad "$rel has $n unjustified multi_compile/shader_feature pragmas"

  n=$(grep -nE 'ZTest[[:space:]]+Always' "$f" 2>/dev/null | grep -vc '// ztest-justified:' | tr -dc '0-9')
  [ "${n:-0}" -eq 0 ] || bad "$rel uses ZTest Always without // ztest-justified:"

  # Additive passes that leave destination alpha alone are the whole passthrough
  # contract: `Blend One One` writes alpha too and punches an opaque hole in the
  # composited view. The regex is anchored at end of line so `Zero OneMinusSrcAlpha`
  # cannot satisfy it by prefix.
  local addn okn cmn
  # The trailing (space|comma|end) is load-bearing: `One` must be a WHOLE token. Without it
  # `Blend One OneMinusSrcAlpha` -- premultiplied over, which is the correct and required blend
  # for rings, volumes, panels and text (contract 2.2) -- matched by prefix and was reported as
  # an unguarded additive. Every premultiplied shader in the set failed on that.
  addn=$(cntE '^[[:space:]]*Blend[[:space:]]+One[[:space:]]+One([[:space:]]|,|$)' "$f")
  if [ "${addn:-0}" -gt 0 ]; then
    # Ends in (space|end of line) rather than (optional space, end of line), so a trailing
    # `// ...` comment on the Blend line is allowed -- the contract's own verbatim Point.shader
    # (Appendix A.4) comments this exact line. `Zero OneMinusSrcAlpha` still cannot satisfy it
    # by prefix, because the character after `One` is then `M`, neither a space nor end of line.
    okn=$(cntE '^[[:space:]]*Blend[[:space:]]+One[[:space:]]+One,[[:space:]]*Zero[[:space:]]+One([[:space:]]|$)' "$f")
    cmn=$(cntE '^[[:space:]]*ColorMask[[:space:]]+RGB' "$f")
    # ponytail: file-scoped, not pass-scoped. A file mixing a guarded and an unguarded
    # additive pass passes here; upgrade to a per-Pass parser if that shape ever ships.
    if [ "${okn:-0}" -ge "${addn:-0}" ] || [ "${cmn:-0}" -gt 0 ]; then
      pass "$rel additive blend keeps destination alpha"
    else
      bad "$rel has additive 'Blend One One' without ', Zero One' or ColorMask RGB"
    fi
  fi

  # ---- R1: never the URP camera-position helper, anywhere, comments included ---------------
  # A rule enforced by grep has to be grep-clean in the documentation that explains it, or the
  # first thing it flags is its own comment. CosmoCore.hlsl describes the helper in prose.
  n=$(cnt "$BANNED_EYE" "$f")
  if [ "${n:-0}" -eq 0 ]; then
    pass "$rel reads the eye through CS_EyePositionWS (R1)"
  else
    bad "$rel names the URP camera-position helper $n time(s) - use CS_EyePositionWS (R1, contract 7.1 rule 2)"
  fi

  # ---- R30: _Fade in both Properties and UnityPerMaterial ---------------------------------
  # The layer framework writes LayerState.Weight into _Fade on every owned material. Without
  # it a layer cannot fade out: it pops.
  local fp fc
  fp=$(properties_body "$f" | grep -cE '^[[:space:]]*(\[[A-Za-z]+\][[:space:]]*)*_Fade[[:space:]]*\(' | tr -dc '0-9')
  fc=$(cbuffer_body "$f" | grep -cE '^[[:space:]]*(float|half)[0-9]?[[:space:]]+_Fade[[:space:]]*;' | tr -dc '0-9')
  if [ "${fp:-0}" -gt 0 ] && [ "${fc:-0}" -gt 0 ]; then
    pass "$rel declares _Fade in Properties and UnityPerMaterial (R30)"
  else
    bad "$rel _Fade missing (Properties=${fp:-0}, UnityPerMaterial=${fc:-0}) (R30, contract 7.1 rule 11)"
  fi

  # ---- R5: no fragment function reads the object/world matrices ---------------------------
  # Under INSTANCING_ON they are arrays indexed by unity_InstanceID, which no fragment stage
  # sets up, so a fragment reads matrix 0 -- the wrong one the moment a layer is instanced.
  local fn hits="" body
  while IFS= read -r fn; do
    [ -n "$fn" ] || continue
    # Comments stripped first. The contract's own verbatim Volume.shader carries a comment
    # inside frag saying NOT to call TransformWorldToObject there; a rule that flagged the note
    # explaining it would be this project's named verification-gap pattern in miniature.
    body=$(func_body "$f" "$fn" | sed 's://.*::')
    n=$(printf '%s\n' "$body" \
        | grep -cE 'unity_ObjectToWorld|unity_WorldToObject|TransformWorldToObject|TransformObjectToWorld' \
        | tr -dc '0-9')
    [ "${n:-0}" -eq 0 ] || hits="$hits $fn($n)"
  done < <(grep -hE '^[[:space:]]*#pragma[[:space:]]+fragment' "$f" 2>/dev/null \
           | awk '{ print $3 }' | sort -u)
  if [ -z "$hits" ]; then
    pass "$rel no fragment reads unity_ObjectToWorld/WorldToObject (R5)"
  else
    bad "$rel fragment stage reads an object-space matrix:$hits (R5, contract 7.1 rule 9)"
  fi

  # ---- R12: the three object-space shaders opt out of batching ----------------------------
  local want
  while IFS= read -r want; do
    [ -n "$want" ] || continue
    n=$(cntE "^[[:space:]]*Shader[[:space:]]+\"$want\"" "$f")
    [ "${n:-0}" -gt 0 ] || continue
    n=$(cnt '"DisableBatching"="True"' "$f")
    if [ "${n:-0}" -gt 0 ]; then
      pass "$rel carries \"DisableBatching\"=\"True\" (R12)"
    else
      bad "$rel is $want and must carry \"DisableBatching\"=\"True\" (R12, contract 7.1 rule 1)"
    fi
  done <<EOF
$NEEDS_NOBATCH
EOF
}

run_tree() {
  local found=0 f
  while IFS= read -r f; do
    found=1; check_file "$f" 1
  done < <(find "$ROOT/unity/Assets/CosmoSynth" \( -name '*.shader' -o -name '*.hlsl' \) -type f 2>/dev/null | sort)

  local tmp="$ROOT/unity/Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader"
  if [ -f "$tmp" ]; then
    found=1; check_file "$tmp" 0
  else
    # Not a pass: say what was not checked, so "green" cannot mean "nothing ran".
    printf '  info  TMP_SDF-Mobile.shader is not vendored into this project - not checked\n'
  fi

  [ "$found" -eq 1 ] || bad "no shaders found under unity/Assets/CosmoSynth - is GATE_ROOT right?"
}

self_test() {
  local d rc out
  d=$(mktemp -d /tmp/cosmosynth-stereo-st.XXXXXX)
  mkdir -p "$d/unity/Assets/CosmoSynth/Runtime/Shaders"
  local S="$d/unity/Assets/CosmoSynth/Runtime/Shaders/Good.shader"
  cat > "$S" <<'SHADER'
Shader "CosmoSynth/Good"
{
  Properties
  {
    _Fade ("Layer fade", Float) = 1
  }
  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" }
    Pass
    {
      Blend One One, Zero One     // a trailing comment on this line is allowed
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing
      CBUFFER_START(UnityPerMaterial)
        float _Fade;
      CBUFFER_END
      struct A { float4 p : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
      struct V { float4 p : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
      V vert(A i){ V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.p = TransformObjectToWorld(i.p.xyz).xyzz; return o; }
      // NOT TransformWorldToObject here: a comment naming it must not trip R5, because the
      // contract's own verbatim Volume.shader carries exactly this note inside frag.
      float4 frag(V i):SV_Target{ UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return _Fade; }
      ENDHLSL
    }
    Pass
    {
      Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha   // premultiplied over: NOT an additive
      HLSLPROGRAM
      #pragma vertex vert2
      #pragma fragment frag2
      #pragma multi_compile_instancing
      struct A2 { float4 p : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
      struct V2 { float4 p : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
      V2 vert2(A2 i){ V2 o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); return o; }
      float4 frag2(V2 i):SV_Target{ UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return 1; }
      ENDHLSL
    }
  }
}
SHADER

  # A second fixture for the R12 rule, which only applies to three named shaders.
  local B="$d/unity/Assets/CosmoSynth/Runtime/Shaders/Vol.shader"
  cat > "$B" <<'SHADER'
Shader "CosmoSynth/Volume"
{
  Properties
  {
    _Fade ("Layer fade", Float) = 1
  }
  SubShader
  {
    Tags { "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
    Pass
    {
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing
      CBUFFER_START(UnityPerMaterial)
        float _Fade;
      CBUFFER_END
      struct A { float4 p : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
      struct V { float4 p : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
      V vert(A i){ V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); return o; }
      float4 frag(V i):SV_Target{ UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return _Fade; }
      ENDHLSL
    }
  }
}
SHADER

  out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "SELF-TEST FAIL: the good fixture did not pass"; printf '%s\n' "$out"; rm -rf "$d"; return 1
  fi
  echo "  ok    self-test: good fixture passes"

  # One mutant per rule. Each MUST fail, or the rule is decoration.
  local ok=1
  mutate() {   # $1 = label, $2 = sed script, $3 = file (default Good.shader)
    local m="$d/unity/Assets/CosmoSynth/Runtime/Shaders/${3:-Good.shader}"
    cp "$m" "$m.bak"; sed -i "$2" "$m"
    out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
    mv "$m.bak" "$m"
    if [ "$rc" -eq 0 ]; then echo "  SELF-TEST FAIL: mutant '$1' PASSED - the rule cannot fail"; ok=0
    else echo "  ok    self-test: mutant '$1' fails as it must"; fi
  }
  mutate "no UNITY_VERTEX_OUTPUT_STEREO"  's/UNITY_VERTEX_OUTPUT_STEREO//'
  mutate "no instancing pragma"           's/#pragma multi_compile_instancing//'
  mutate "wrong shader name prefix"       's#Shader "CosmoSynth/Good"#Shader "Hidden/Good"#'
  mutate "no URP tag"                     's/"RenderPipeline"="UniversalPipeline"//'
  mutate "unjustified multi_compile"      's/#pragma fragment frag/#pragma fragment frag\n      #pragma multi_compile _ FOO/'
  mutate "ZTest Always unjustified"       's/Blend One One, Zero One/ZTest Always\n      Blend One One, Zero One/'
  mutate "additive writes alpha"          's/Blend One One, Zero One/Blend One One/'
  # The guard now tolerates a trailing comment, so prove a COMMENTED unguarded additive is still
  # caught -- otherwise the fix for defect 2 would have become the hole.
  mutate "additive writes alpha, commented" 's#Blend One One, Zero One.*#Blend One One   // oops#'
  # And prove the premultiplied pass is judged on its own merits, not waved through: turning its
  # destination factor into One makes it a genuine alpha-writing additive.
  mutate "premultiplied turned additive"  's/Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha/Blend One One/'

  # ---- the four rules this revision adds ---------------------------------------------------
  # R1, in code and in a comment: both must fail, because the ban is on the string.
  mutate "R1 camera helper in code" \
    's#return _Fade; }#return GetCamera'"PositionWS"'().x; }#'
  mutate "R1 camera helper in a comment" \
    's#// a trailing comment on this line is allowed#// see GetCamera'"PositionWS"'#'
  # R30, each half separately: a shader with the property but not the uniform still cannot be
  # driven by the layer framework.
  mutate "R30 _Fade not in Properties"       's/    _Fade ("Layer fade", Float) = 1//'
  mutate "R30 _Fade not in UnityPerMaterial" 's/^        float _Fade;$//'
  # R5: the vertex stage may call TransformObjectToWorld (the good fixture does); the fragment
  # stage may not. The mutant moves the same call into frag, so the rule is proved to be about
  # WHERE the matrix is read, not whether the file mentions it.
  mutate "R5 fragment reads the object matrix" \
    's#UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return _Fade; }#UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return TransformWorldToObject(i.p.xyz).x; }#'
  # R12, on the fixture that is one of the three named shaders.
  mutate "R12 Volume without DisableBatching" 's/ "DisableBatching"="True"//' Vol.shader

  rm -rf "$d"
  [ "$ok" -eq 1 ] || return 1
  echo "SELF-TEST PASS"
  return 0
}

if [ "${1:-}" = "--self-test" ]; then
  self_test; exit $?
fi

echo "G0b stereo/shader gate on $ROOT"
run_tree
if [ "$fail" -ne 0 ]; then echo "G0b FAIL"; exit 1; fi
echo "G0b PASS"
exit 0
