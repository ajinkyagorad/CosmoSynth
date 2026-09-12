#!/usr/bin/env bash
# Run any editor static method headlessly. The ONLY way lane code reaches the editor,
# and only the integrator runs it (§D3.2).
#
#   tools/editor-method.sh CosmoSynth.EditorTools.XrSetup.Run [unity args...]
#   METHOD_TIMEOUT=1800 tools/editor-method.sh ...
#   tools/editor-method.sh --self-test          exercise common.sh without starting Unity
#
# Exit: Unity's code, mapped per §D3.0, or 11 when Unity could not resolve the method.
set -uo pipefail
cd "$(dirname "$0")/.." || exit 1
source tools/common.sh

# ------------------------------------------------------------------ self-test
# Everything in common.sh except the Unity launch itself. Each check must be able to go
# both ways: a guard that only ever returns "fine" is not a guard.
if [ "${1:-}" = "--self-test" ]; then
  ok=1
  say() { printf '  %-5s %-22s %s\n' "$1" "$2" "$3"; [ "$1" = "ok" ] || ok=0; }

  # editor path
  [ -x "$UNITY" ] && say ok unity "$UNITY" || say FAIL unity "no editor at $UNITY"

  # runner_bug both ways
  UNITY_LOG=$(mktemp /tmp/cosmosynth-rb.XXXXXX.log)
  echo "Everything went fine" > "$UNITY_LOG"
  runner_bug && say FAIL runner_bug-clean "reported a bug on a clean log" \
              || say ok runner_bug-clean "a clean log is not a runner bug"
  echo "executeMethod class 'Foo.Bar' could not be found" >> "$UNITY_LOG"
  runner_bug && say ok runner_bug-dirty "Unity's resolution failure is detected" \
              || say FAIL runner_bug-dirty "a resolution failure was NOT detected"
  rm -f "$UNITY_LOG"; UNITY_LOG=""

  # guard_resources must refuse an impossible threshold and accept a trivial one
  ( guard_resources 999999 1 ) >/dev/null 2>&1
  [ $? -eq 6 ] && say ok guard-refuses "exits 6 below the disk threshold" \
                || say FAIL guard-refuses "did not refuse an impossible disk threshold"
  ( guard_resources 1 1 ) >/dev/null 2>&1
  [ $? -eq 0 ] && say ok guard-accepts "passes when there is room" \
               || say FAIL guard-accepts "refused although there is room"

  # A display in OUR band, and killed again by start_xvfb's own EXIT trap.
  GOT=$( start_xvfb >/dev/null 2>&1; printf '%s' "${DISPLAY#:}" )
  if [ -n "$GOT" ] && [ "$GOT" -ge "$BAND_LO" ] 2>/dev/null && [ "$GOT" -le "$BAND_HI" ]; then
    say ok xvfb-band "start_xvfb took :$GOT in $BAND_LO-$BAND_HI"
    # Xvfb removes its own lock file on SIGTERM, but asynchronously: checking instantly
    # reports a leak that is not one. A lock that is still there after 5 s is real, and
    # the next run would skip that display - which is what reap_band exists to clear.
    for _ in 1 2 3 4 5; do [ -e "/tmp/.X$GOT-lock" ] || break; sleep 1; done
    [ -e "/tmp/.X$GOT-lock" ] && say FAIL xvfb-cleanup "lock file :$GOT survived the trap" \
                              || say ok xvfb-cleanup "the display was killed and its lock released"
  else
    say FAIL xvfb-band "start_xvfb did not land in $BAND_LO-$BAND_HI (got '${GOT:-none}')"
  fi

  # the lock is re-entrant for a child and exclusive for a stranger
  ( COSMOSYNTH_LOCK_HELD=$$ editor_lock ) >/dev/null 2>&1 \
    && say ok lock-reentrant "a child with COSMOSYNTH_LOCK_HELD does not reopen the lock" \
    || say FAIL lock-reentrant "a child deadlocked or refused"
  ( exec 9>"$LOCK"; flock 9; sleep 4 ) &
  holder=$!
  sleep 1
  ( LOCK_WAIT=1 editor_lock ) >/dev/null 2>&1
  [ $? -eq 4 ] && say ok lock-exclusive "a second holder exits 4 after LOCK_WAIT" \
               || say FAIL lock-exclusive "a second holder was let through"
  wait "$holder" 2>/dev/null

  # require_main_clean must refuse a dirty main (it is dirty today) or accept a clean one
  ( require_main_clean ) >/dev/null 2>&1; rc=$?
  if [ -n "$(git -C "$REPO" status --porcelain)" ]; then
    [ "$rc" -eq 8 ] && say ok main-clean "refuses the dirty main tree with 8" \
                    || say FAIL main-clean "let a dirty main through (rc=$rc)"
  else
    [ "$rc" -eq 0 ] && say ok main-clean "accepts a clean main tree" \
                    || say FAIL main-clean "refused a clean main tree (rc=$rc)"
  fi

  echo "SELF-TEST $([ "$ok" -eq 1 ] && echo PASS || echo FAIL)  (the Unity launch itself is not covered)"
  [ "$ok" -eq 1 ] || exit 1
  exit 0
fi

METHOD="${1:?usage: tools/editor-method.sh <Namespace.Class.Method> [unity args...]}"
shift

editor_lock
guard_resources 5 6
start_xvfb

unity_run method "${METHOD_TIMEOUT:-1200}" "$METHOD" "$@"
rc=$?

# "could not be found" means THIS invocation named the wrong class, not that the module
# is broken. Saying FAIL here is how a resolution bug gets logged as a module defect and
# chased for an afternoon (Worldfall learned it on TerrainGate).
if runner_bug; then
  echo "### RUNNER-BUG: Unity could not resolve '$METHOD'."
  echo "###   The namespace or class is wrong in the CALLER - the module was never run."
  CLS=${METHOD%.*}; CLS=${CLS##*.}
  echo "###   Read the real namespace off the file:  grep -rn \"class $CLS\" unity/Assets"
  grep -an "could not be found\|could not be executed\|Method not found" "$UNITY_LOG" | head -5
  exit 11
fi

exit "$rc"
