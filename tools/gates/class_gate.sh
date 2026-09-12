#!/usr/bin/env bash
# G0e - MonoScript filename / class-name rules (Worldfall integrate.sh:53-65).
#
# Unity binds a MonoScript to the FIRST type whose name matches the file name. A second
# class in the same file is silently dropped from any prefab saved headlessly, and the
# app "partially works, with no errors" on device (memory: unity-monoscript-filename-trap).
# Warnings are failures here for exactly that reason.
#
# Scope: unity/Assets/CosmoSynth/**/*.cs
#
#   tools/gates/class_gate.sh              check the working tree
#   tools/gates/class_gate.sh --self-test  prove each rule can fail
#
# Exit 0 pass, 1 fail.
set -uo pipefail

ROOT="${GATE_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
SRC="$ROOT/unity/Assets/CosmoSynth"
fail=0
pass() { printf '  ok    %s\n' "$1"; }
bad()  { printf '  FAIL  %s\n' "$1"; fail=1; }

# Type declarations, ignoring comments and the `class` inside a doc comment.
types() {   # $1 = file -> "kind<TAB>name" per line
  sed -e 's://.*::' "$1" 2>/dev/null \
    | grep -oE '\b(class|struct|interface|enum)[[:space:]]+[A-Za-z_][A-Za-z0-9_]*' \
    | awk '{print $1"\t"$2}'
}
mono_classes() {   # classes with a MonoBehaviour base
  sed -e 's://.*::' "$1" 2>/dev/null \
    | grep -oE '\bclass[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*(<[^>]*>)?[[:space:]]*:[[:space:]]*[A-Za-z0-9_., <>]*MonoBehaviour' \
    | awk '{print $2}' | sed 's/<.*//'
}

run_tree() {
  local f base n names mb nmb checked=0
  [ -d "$SRC" ] || { bad "no source at $SRC"; return; }
  while IFS= read -r f; do
    base=$(basename "$f" .cs)
    checked=$((checked + 1))
    names=$(types "$f" | cut -f2)
    n=$(printf '%s\n' "$names" | grep -cx -- "$base" | tr -dc '0-9')
    if [ "${n:-0}" -eq 0 ]; then
      bad "${f#"$ROOT"/} declares no type named '$base'"
    fi
    mb=$(mono_classes "$f")
    nmb=$(printf '%s' "$mb" | grep -c . | tr -dc '0-9')
    if [ "${nmb:-0}" -gt 1 ]; then
      bad "${f#"$ROOT"/} declares $nmb MonoBehaviours ($(printf '%s' "$mb" | tr '\n' ' '))"
    elif [ "${nmb:-0}" -eq 1 ]; then
      if [ "$(printf '%s' "$mb")" != "$base" ]; then
        bad "${f#"$ROOT"/} MonoBehaviour is '$(printf '%s' "$mb")', not '$base' - Unity will not bind it"
      fi
    fi
  done < <(find "$SRC" -name '*.cs' -type f 2>/dev/null | sort)
  [ "$checked" -gt 0 ] || bad "no .cs files under $SRC - is GATE_ROOT right?"
  [ "$fail" -eq 0 ] && pass "$checked files: every type matches its filename, at most one MonoBehaviour each"
}

self_test() {
  local d rc out ok=1 S
  d=$(mktemp -d /tmp/cosmosynth-class-st.XXXXXX)
  S="$d/unity/Assets/CosmoSynth/Runtime"
  mkdir -p "$S"
  cat > "$S/Rig.cs" <<'CS'
using UnityEngine;
public class Rig : MonoBehaviour { void Start(){} }
CS
  cat > "$S/Catalog.cs" <<'CS'
public static class Catalog { public const int N = 1; }
CS
  out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "SELF-TEST FAIL: the good fixture did not pass"; printf '%s\n' "$out"; rm -rf "$d"; return 1
  fi
  echo "  ok    self-test: good fixture passes"

  check_bad() {   # $1 label, $2 file, $3 content
    printf '%s\n' "$3" > "$S/$2"
    out=$(GATE_ROOT="$d" bash "$0" 2>&1); rc=$?
    rm -f "$S/$2"
    if [ "$rc" -eq 0 ]; then echo "  SELF-TEST FAIL: '$1' PASSED - the rule cannot fail"; ok=0
    else echo "  ok    self-test: '$1' fails as it must"; fi
  }
  check_bad "two MonoBehaviours in one file" Two.cs \
'public class Two : MonoBehaviour {}
public class Hidden : MonoBehaviour {}'
  check_bad "MonoBehaviour named unlike its file" Wrong.cs \
'public class NotWrong : MonoBehaviour {}'
  check_bad "no type named after the file" Empty.cs \
'public class Something {}'

  rm -rf "$d"
  [ "$ok" -eq 1 ] || return 1
  echo "SELF-TEST PASS"
  return 0
}

if [ "${1:-}" = "--self-test" ]; then
  self_test; exit $?
fi

echo "G0e MonoScript filename/class gate on ${SRC#"$ROOT"/}"
run_tree
if [ "$fail" -ne 0 ]; then echo "G0e FAIL"; exit 1; fi
echo "G0e PASS"
exit 0
