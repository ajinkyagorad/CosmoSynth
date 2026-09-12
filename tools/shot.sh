#!/usr/bin/env bash
# G1c and lane captures: run the REAL app in play mode and screenshot it, with a
# diagnostic report. The only check on this box that has ever caught what device testing
# catches, because it drives the app's own API in play mode rather than a scene the tool
# built for itself.
#
#   tools/shot.sh [--views v1,v2|release] [--out DIR] [--hide PATTERNS] [--profile clean]
#   tools/shot.sh --rev <commit> [same options]     # detached checkout of <commit>
#   tools/shot.sh --queue                           # serve /tmp/cosmosynth-shotq/*.req
#
# Default output /tmp/cosmosynth-shots/<sha7>/. NEVER the published gallery: only
# build.sh's G4 step writes under ~/builds/shots. Lanes do not run this; they leave a
# request in the queue and the integrator serves it.
#
# Exit 0 OK, 1 bad arguments or no display, 3 no PNG, 4 lock, 8 not main/dirty, 9 timeout,
# 14 the harness reported ASSERT_FAIL.
set -uo pipefail
cd "$(dirname "$0")/.." || exit 1
source tools/common.sh

VIEWS=default
OUT=""
HIDE="${COSMOSYNTH_HIDE:-}"
PROFILE=full
REV=""
QUEUE=0
QDIR=/tmp/cosmosynth-shotq
SHOTROOT=/tmp/cosmosynth-shots

while [ $# -gt 0 ]; do
  case "$1" in
    --views)   VIEWS="${2:?--views needs a list}"; shift 2 ;;
    --out)     OUT="${2:?--out needs a dir}"; shift 2 ;;
    --hide)    HIDE="${2:-}"; shift 2 ;;
    --profile) PROFILE="${2:?--profile needs a name}"; shift 2 ;;
    --rev)     REV="${2:?--rev needs a commit}"; shift 2 ;;
    --queue)   QUEUE=1; shift ;;
    *) echo "unknown argument: $1" >&2; exit 1 ;;
  esac
done

# Only build.sh's publish step may write into the gallery. A capture tool that can write
# there will eventually write there, and then a shot nobody reviewed is on the build page.
case "${OUT:-}" in
  "$HOME/builds/shots"*|/home/triton/builds/shots*)
    echo "### REFUSED: --out may not be under ~/builds/shots (only build.sh G4 publishes)" >&2
    exit 1 ;;
esac

capture() {   # $1 = commit sha, $2 = dirty 0|1, $3 = out dir
  local sha="$1" dirty="$2" out="$3" rc
  mkdir -p "$out"
  editor_lock                 # a no-op when build.sh already holds it
  guard_resources 5 6
  start_xvfb
  NOQUIT=1 unity_run shot 1800 CosmoSynth.EditorTools.PlayModeShot.Run \
      -shotViews "$VIEWS" -shotOut "$out" -shotCommit "$sha" -shotDirty "$dirty" \
      -shotHide "$HIDE" -shotProfile "$PROFILE"
  rc=$?
  if runner_bug; then
    echo "### RUNNER-BUG: Unity could not resolve CosmoSynth.EditorTools.PlayModeShot.Run"
    return 11
  fi
  local n
  n=$(ls -1 "$out"/*.png 2>/dev/null | grep -c . | tr -dc '0-9')
  ls -l "$out" 2>/dev/null | sed 's/^/    /'
  if [ "${n:-0}" -eq 0 ]; then echo "### NO IMAGE in $out"; return 3; fi
  if [ -n "${UNITY_LOG:-}" ] && [ "$(grep -ac 'ASSERT_FAIL' "$UNITY_LOG" | tr -dc '0-9')" -gt 0 ]; then
    echo "### ASSERT_FAIL - the harness rejected this run"
    grep -a 'ASSERT_FAIL' "$UNITY_LOG" | sed 's/^/    /' | head -20
    return 14
  fi
  echo "### $n image(s) in $out"
  return "$rc"
}

# ------------------------------------------------------------------ --queue
if [ "$QUEUE" -eq 1 ]; then
  mkdir -p "$QDIR"
  n=0
  for req in $(ls -1tr "$QDIR"/*.req 2>/dev/null); do
    n=$((n + 1))
    lane=$(sed -n 's/^lane=//p' "$req" | head -1)
    commit=$(sed -n 's/^commit=//p' "$req" | head -1)
    VIEWS=$(sed -n 's/^views=//p' "$req" | head -1); VIEWS="${VIEWS:-default}"
    HIDE=$(sed -n 's/^hide=//p' "$req" | head -1)
    PROFILE=$(sed -n 's/^profile=//p' "$req" | head -1); PROFILE="${PROFILE:-full}"
    base="${req%.req}"
    echo "=== request $(basename "$req"): lane=$lane commit=$commit views=$VIEWS"
    if [ -z "$commit" ] || ! git -C "$REPO" cat-file -e "$commit^{commit}" 2>/dev/null; then
      echo "rc=1 unknown commit '$commit'" > "$base.failed"; rm -f "$req"; continue
    fi
    out="$SHOTROOT/${lane:-queue}/$(git -C "$REPO" rev-parse --short=7 "$commit")"
    ( REV="$commit" OUT="$out" bash "$0" --rev "$commit" --out "$out" --views "$VIEWS" \
        --hide "$HIDE" --profile "$PROFILE" ) > "$base.out" 2>&1
    rc=$?
    if [ "$rc" -eq 0 ]; then
      { echo "rc=0 $out"; tail -40 "$base.out"; } > "$base.done"
    else
      { echo "rc=$rc"; tail -40 "$base.out"; } > "$base.failed"
    fi
    rm -f "$req" "$base.out"
  done
  echo "### served $n request(s)"
  exit 0
fi

# -------------------------------------------------------------------- --rev
if [ -n "$REV" ]; then
  require_main_clean
  SHA=$(git -C "$REPO" rev-parse "$REV") || { echo "### unknown commit $REV" >&2; exit 1; }
  SHORT=$(git -C "$REPO" rev-parse --short=7 "$SHA")
  OUT="${OUT:-$SHOTROOT/$SHORT}"
  # The sentinel is what makes every other tool refuse to touch main while it is detached.
  # `clean` is safe because require_main_clean guaranteed no untracked files before the
  # switch: what it removes are the .meta files and assets the import of this commit left.
  echo "$SHA pid $$ since $(date -Is)" > "$DETACHED"
  trap 'git -C "$REPO" switch master >/dev/null 2>&1; git -C "$REPO" clean -fdq -- unity/Assets; rm -f "$DETACHED"' EXIT
  git -C "$REPO" switch --detach "$SHA" >/dev/null 2>&1 || { echo "### could not detach main" >&2; exit 1; }
  echo "### main detached at $SHORT (the next editor run on master will recompile)"
  capture "$SHA" 0 "$OUT"
  exit $?
fi

# ------------------------------------------------------------------- current
SHA=$(git -C "$REPO" rev-parse HEAD)
DIRTY=0
[ -z "$(git -C "$REPO" status --porcelain)" ] || DIRTY=1
OUT="${OUT:-$SHOTROOT/$(git -C "$REPO" rev-parse --short=7 HEAD)}"
capture "$SHA" "$DIRTY" "$OUT"
exit $?
