#!/usr/bin/env bash
# G0 - run every static gate and print one line each.
#
# A gate that is not present is reported ABSENT, never skipped silently: "all gates green"
# must never be able to mean "no gates ran". The absent count is printed at the end and,
# with --full, makes the run red - because a build must not go out past gates that were
# never written.
#
#   tools/gate-all.sh                 every gate that exists, in this tree
#   tools/gate-all.sh --full          same, but ABSENT is also a failure (build.sh uses this)
#   tools/gate-all.sh --self-test     run every gate's own self-test, nothing else
#   tools/gate-all.sh b d             only these ids
#
# Exit 0 all green, 10 anything red. A gate named in tools/gates/known-red.txt is printed
# but does not set the exit code (Phase 0 only).
set -uo pipefail
cd "$(dirname "$0")/.." || exit 1

FULL=0; SELFTEST=0
want=()
for a in "$@"; do
  case "$a" in
    --full) FULL=1 ;;
    --self-test) SELFTEST=1 ;;
    *) want+=("$a") ;;
  esac
done

KNOWN_RED=tools/gates/known-red.txt
FLOOR=$(grep -oE '^[0-9]+' tools/gates/test-floor.txt 2>/dev/null | head -1); FLOOR="${FLOOR:-0}"

# id : what it is : how to run it : how to self-test it ("-" = none)
# Every G0 id from D §D3.7 is listed, including the ones this lane did not write, so they
# are reported ABSENT rather than being invisible.
GATES="
G0a|test suite (run-tests.sh, floor $FLOOR)|./run-tests.sh|-
G0b|shader stereo macros|tools/gates/stereo_gate.sh|tools/gates/stereo_gate.sh --self-test
G0c|settings YAML|tools/gates/settings_gate.py|tools/gates/settings_gate.py --self-test
G0d|runtime code invariants|tools/gates/code_gate.sh|tools/gates/code_gate.sh --self-test
G0e|MonoScript filename/class|tools/gates/class_gate.sh|tools/gates/class_gate.sh --self-test
G0f|data documented + droppable|tools/gates/data_gate.py|tools/gates/data_gate.py --self-test
G0g|unique input bindings|tools/gates/binding_gate.py|tools/gates/binding_gate.py --self-test
G0h|lane ownership|tools/gates/own_gate.py|tools/gates/own_gate.py --self-test
G0i|Atlas parity table|tools/gates/parity_gate.py|tools/gates/parity_gate.py --self-test
"

wanted() {
  [ "${#want[@]}" -eq 0 ] && return 0
  local w; for w in "${want[@]}"; do
    [ "$w" = "$1" ] && return 0
    [ "G0$w" = "$1" ] && return 0
  done
  return 1
}

known_red() {
  [ -f "$KNOWN_RED" ] || return 1
  [ "$(grep -cE "^${1}([[:space:]]|$)" "$KNOWN_RED" | tr -dc '0-9')" -gt 0 ]
}

runner() {   # prints the command's first token so ABSENT can name the missing file
  printf '%s' "${1%% *}"
}

ran=0; failed=0; absent=0; excused=0
LOGDIR=$(mktemp -d /tmp/cosmosynth-g0.XXXXXX)

printf '%-5s %-34s %-10s %s\n' GATE WHAT RESULT DETAIL
printf '%-5s %-34s %-10s %s\n' ----- ---------------------------------- ---------- --------------------------------

while IFS='|' read -r id what cmd stcmd; do
  [ -n "$id" ] || continue
  wanted "$id" || continue

  script=$(runner "$cmd")
  if [ ! -f "$script" ]; then
    printf '%-5s %-34s %-10s %s\n' "$id" "$what" ABSENT "$script does not exist - NOT CHECKED"
    absent=$((absent + 1)); continue
  fi

  if [ "$SELFTEST" -eq 1 ]; then
    if [ "$stcmd" = "-" ]; then
      printf '%-5s %-34s %-10s %s\n' "$id" "$what" SKIP "no self-test for this gate"
      continue
    fi
    out=$(bash -c "$stcmd" 2>&1); rc=$?
    printf '%s\n' "$out" > "$LOGDIR/$id.selftest"
    line=$(printf '%s\n' "$out" | grep -E 'SELF-TEST (PASS|FAIL)' | tail -1)
    if [ "$rc" -eq 0 ]; then
      printf '%-5s %-34s %-10s %s\n' "$id" "$what" OK "${line:-self-test passed}"
    else
      printf '%-5s %-34s %-10s %s\n' "$id" "$what" FAIL "${line:-self-test failed} ($LOGDIR/$id.selftest)"
      failed=$((failed + 1))
    fi
    ran=$((ran + 1)); continue
  fi

  out=$(bash -c "$cmd" 2>&1); rc=$?
  printf '%s\n' "$out" > "$LOGDIR/$id.log"
  ran=$((ran + 1))

  detail=""
  if [ "$id" = "G0a" ]; then
    # The floor exists so a deleted test cannot pass silently.
    n=$(printf '%s\n' "$out" | sed -n 's/.*[^0-9]\([0-9]\+\) passed.*/\1/p' | tail -1 | tr -dc '0-9')
    f=$(printf '%s\n' "$out" | sed -n 's/.*[^0-9]\([0-9]\+\) failed.*/\1/p' | tail -1 | tr -dc '0-9')
    detail="${n:-?} passed, ${f:-?} failed (floor $FLOOR)"
    if [ "$rc" -eq 0 ] && { [ -z "${n:-}" ] || [ "${n:-0}" -lt "$FLOOR" ]; }; then
      rc=1; detail="$detail - BELOW THE FLOOR"
    fi
  else
    detail=$(printf '%s\n' "$out" | grep -E '(PASS|FAIL)( |$)|FAIL ' | tail -1 | cut -c1-90)
  fi

  if [ "$rc" -eq 0 ]; then
    printf '%-5s %-34s %-10s %s\n' "$id" "$what" PASS "${detail:-ok}"
  elif known_red "$id"; then
    printf '%-5s %-34s %-10s %s\n' "$id" "$what" KNOWN-RED "${detail:-red, excused by known-red.txt}"
    excused=$((excused + 1))
  else
    printf '%-5s %-34s %-10s %s\n' "$id" "$what" FAIL "${detail:-rc=$rc} ($LOGDIR/$id.log)"
    failed=$((failed + 1))
  fi
done <<EOF
$GATES
EOF

echo
echo "gates run=$ran failed=$failed absent=$absent excused=$excused   logs in $LOGDIR"
if [ "$absent" -gt 0 ]; then
  echo "ABSENT gates were NOT checked. 'All green' above covers only the $ran that ran."
fi
[ "$excused" -gt 0 ] && echo "KNOWN-RED gates are excused by $KNOWN_RED - Phase 0 only."

if [ "$failed" -gt 0 ]; then exit 10; fi
if [ "$FULL" -eq 1 ] && [ "$absent" -gt 0 ]; then
  echo "--full: refusing to report green with $absent gate(s) missing"
  exit 10
fi
exit 0
