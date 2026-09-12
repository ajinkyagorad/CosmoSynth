#!/usr/bin/env bash
# Shared paths, editor queue, resource guards and the Unity runner for every
# CosmoSynth tool. SOURCED, never executed.
#
# Spec: docs/design/2026-09-11-D-process.md §D3.1. Exit codes: §D3.0.
#   1 generic / no display   3 artefact missing   4 lock busy or foreign editor
#   6 resource guard         7 version gate       8 not main / dirty / detached
#   9 timeout               10 G0 red           11 runner bug (method unresolved)
#  12 G1a/G1b red           13 G3 red           14 G1c red        15 publish refused

REPO=/home/triton/projects/CosmoSynth            # main: the only tree with a Library
PROJECT=$REPO/unity
UNITY=/home/triton/Unity/Hub/Editor/6000.5.5f1/Editor/Unity
OUTDIR=/home/triton/builds/cosmosynth            # published; the build page reads it on every load
STAGE=/home/triton/.cache/cosmosynth/staging     # OUTSIDE ~/builds: serve.py walks every dir there
DIAG=/home/triton/builds/cosmosynth/diagnostics  # working captures worth keeping
RAW=/home/triton/.cache/cosmosynth/raw           # importer downloads, deleted after bake
LOCK=/tmp/cosmosynth-editor.lock
DETACHED=/tmp/cosmosynth-main-detached           # written by shot.sh --rev while main is off master
BAND_LO=461; BAND_HI=490                         # claimed by no other script on the box
FONT_ASSET="LiberationSans SDF"                  # what G3 looks for
SDK=$HOME/Unity/Hub/Editor/6000.5.5f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK
BT=$(ls -d "$SDK"/build-tools/* 2>/dev/null | sort -V | tail -1)
UNITY_LOG=""                                     # set by unity_run, read by callers

# ---------------------------------------------------------------- editor path
# Resolve the editor rather than trusting the constant: a Hub update renames the
# directory and every tool then fails with "no such file", naming nothing useful.
resolve_unity() {
  if [ -x "$UNITY" ]; then return 0; fi
  local c
  c=$(ls -d "$HOME"/Unity/Hub/Editor/*/Editor/Unity 2>/dev/null | sort -Vr | head -1)
  if [ -n "$c" ] && [ -x "$c" ]; then
    echo "### note: $UNITY is gone, using $c" >&2
    UNITY="$c"
    SDK=$(dirname "$c")/Data/PlaybackEngines/AndroidPlayer/SDK
    BT=$(ls -d "$SDK"/build-tools/* 2>/dev/null | sort -V | tail -1)
    return 0
  fi
  echo "### no Unity editor found under $HOME/Unity/Hub/Editor" >&2
  exit 1
}
resolve_unity

# ------------------------------------------------------------------- displays
# Take the first display in OUR band whose lock file is absent, keep it only if it
# survived 3 s, and kill only the server we started. Never a pattern pkill.
start_xvfb() {
  local n
  for n in $(seq "$BAND_LO" "$BAND_HI"); do
    [ -e "/tmp/.X${n}-lock" ] && continue
    Xvfb ":$n" -screen 0 1920x1080x24 >"/tmp/xvfb-$n.log" 2>&1 &
    XVFB_PID=$!
    sleep 3
    if kill -0 "$XVFB_PID" 2>/dev/null; then
      export DISPLAY=":$n"
      trap 'kill "$XVFB_PID" 2>/dev/null' EXIT
      echo "### display $DISPLAY (xvfb pid $XVFB_PID)"
      return 0
    fi
  done
  echo "### no free X display in $BAND_LO-$BAND_HI" >&2
  exit 1
}

# Called from editor_lock only. Holding the only CosmoSynth editor lock means every
# Xvfb in our band is by definition stale, because nothing else on the box uses it
# (build-pipeline §6, VERIFIED). PIDs come from the lock files, so this is never a
# pattern kill.
reap_band() {
  local n pid
  [ -n "${XVFB_PID:-}" ] && return 0        # never reap the display we are using
  for n in $(seq "$BAND_LO" "$BAND_HI"); do
    [ -e "/tmp/.X${n}-lock" ] || continue
    pid=$(tr -dc '0-9' < "/tmp/.X${n}-lock" 2>/dev/null)
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
      echo "### reaping stale Xvfb :$n (pid $pid)"
      kill "$pid" 2>/dev/null
      sleep 1
    fi
    rm -f "/tmp/.X${n}-lock"
  done
}

# ----------------------------------------------------------------- editor lock
# One editor per project directory, so every tool that launches Unity queues here.
#
# A child (shot.sh run by build.sh) must NOT reopen the lock file: flock locks belong
# to the open file description, so a fresh `exec 9>` in the child would block against
# its own parent for LOCK_WAIT seconds and then exit 4.
editor_lock() {
  if [ -n "${COSMOSYNTH_LOCK_HELD:-}" ]; then
    echo "### editor lock already held by pid $COSMOSYNTH_LOCK_HELD"
    return 0
  fi
  exec 9>"$LOCK"
  if ! flock -w "${LOCK_WAIT:-3600}" 9; then
    echo "### editor lock busy for more than ${LOCK_WAIT:-3600}s" >&2
    exit 4
  fi
  export COSMOSYNTH_LOCK_HELD=$$
  if pgrep -f "Editor/Unit[y].*projectPath $PROJECT( |$)" >/dev/null 2>&1; then
    echo "### a foreign editor holds $PROJECT (not started by these tools)" >&2
    exit 4
  fi
  reap_band
}

# --------------------------------------------------------------------- guards
# ponytail: thresholds are guesses; tune from the first measured IL2CPP peak (build.sh logs it)
guard_resources() {   # $1 = min free disk GB, $2 = min MemAvailable GB
  local d m
  d=$(df --output=avail -BG / | tail -1 | tr -dc '0-9')
  m=$(awk '/MemAvailable/ {print int($2/1048576)}' /proc/meminfo)
  echo "### disk ${d:-?} GB free, ${m:-?} GB MemAvailable (need $1 / $2)"
  [ "${d:-0}" -ge "$1" ] || { echo "### REFUSED: under $1 GB free disk" >&2; exit 6; }
  [ "${m:-0}" -ge "$2" ] || { echo "### REFUSED: under $2 GB MemAvailable" >&2; exit 6; }
}

require_main_clean() {
  local b
  b=$(git -C "$REPO" symbolic-ref --short -q HEAD)
  [ "$b" = "master" ] || { echo "### REFUSED: main is on '${b:-<detached>}', not master" >&2; exit 8; }
  [ -z "$(git -C "$REPO" status --porcelain)" ] || {
    echo "### REFUSED: main tree is dirty:" >&2
    git -C "$REPO" status --porcelain >&2
    exit 8
  }
  [ ! -e "$DETACHED" ] || {
    echo "### REFUSED: main is checked out elsewhere: $(cat "$DETACHED" 2>/dev/null)" >&2
    exit 8
  }
}

# --------------------------------------------------------------- unity runner
# unity_run TAG TIMEOUT METHOD [unity args...]
# NEVER -nographics: it omits the Vulkan xr-* entries from boot.config and the APK
# shows black instead of a tracked world. Always -buildTarget Android.
unity_run() {
  local tag="$1" tmo="$2" method="$3"; shift 3
  mkdir -p "$PROJECT/Logs"
  UNITY_LOG=$(mktemp "$PROJECT/Logs/$tag-$(date +%Y%m%d-%H%M%S).XXXX.log")
  local quit=-quit
  [ "${NOQUIT:-0}" = 1 ] && quit=          # PlayModeShot enters play mode and exits itself
  echo "### $(date -Is) $method  (timeout ${tmo}s, log $UNITY_LOG)"
  timeout -k 60 "$tmo" "$UNITY" -batchmode $quit -buildTarget Android \
      -projectPath "$PROJECT" -executeMethod "$method" -logFile "$UNITY_LOG" "$@"
  local rc=$?
  case "$rc" in 124|137) rc=9 ;; esac
  # Any [Bracketed] tag a module chooses for its own logs, not a fixed list. A gate
  # printing "[CatalogGate] ..." used to be invisible here, so a script reading this
  # stdout saw an empty string and concluded nothing was wrong.
  grep -aE "^\[[A-Za-z][A-Za-z0-9]*\]|_OK|_FAIL|ASSERT_FAIL|error CS|Exception" "$UNITY_LOG" | tail -60
  echo "### unity exit=$rc (log $UNITY_LOG)"
  return "$rc"
}

# True when Unity could not resolve the -executeMethod at all. That is a defect in the
# CALLER, not in the module it was trying to run, and it must never be reported as a
# module failure (Worldfall gate-all.sh:92-96).
runner_bug() {
  local n
  [ -n "${UNITY_LOG:-}" ] && [ -f "$UNITY_LOG" ] || return 1
  n=$(grep -ac "could not be found\|could not be executed\|Method not found" "$UNITY_LOG" 2>/dev/null || true)
  [ "${n:-0}" -gt 0 ]
}
