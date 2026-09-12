#!/usr/bin/env bash
# The only way a CosmoSynth APK is made. G0 -> G1a -> G1b -> G1c -> G2 -> G3 -> notes.
#
#   tools/build.sh "<caption>"
#
# There are NO skip flags. A gate that can be skipped is how bad builds ship. The build
# runs WITH a graphics device: -nographics omits the Vulkan xr-* entries from boot.config
# and the APK shows black instead of a tracked world.
#
# Exit (D §D3.0): 3 no APK, 4 lock, 6 resources, 7 version gate, 8 not main/dirty,
# 9 timeout, 10 G0, 11 runner bug, 12 G1a/G1b, 13 G3, 14 G1c.
set -uo pipefail
cd "$(dirname "$0")/.." || exit 1
source tools/common.sh

CAPTION="${1:-}"
[ -n "$CAPTION" ] || { echo "usage: tools/build.sh \"<caption>\"" >&2; exit 1; }

step() { echo; echo "=============== $* ==============="; }

# ---------------------------------------------------------- 1. where and what
step "G0-main: where and what"
require_main_clean
TOP=$(git rev-parse --show-toplevel 2>/dev/null)
[ "$TOP" = "$REPO" ] || {
  echo "### REFUSED: this is $TOP, not $REPO. Builds happen in main only - a worktree has" >&2
  echo "###          no Library, and the APK must trace to the tree that owns the editor." >&2
  exit 8
}

SB="$PROJECT/Assets/CosmoSynth/Editor/ServerBuild.cs"
VER=$(sed -n 's/.*string Version = "\([^"]*\)".*/\1/p' "$SB" | head -1)
CODE=$(sed -n 's/.*int VersionCode = \([0-9]*\);.*/\1/p' "$SB" | head -1)
[ -n "$VER" ] && [ -n "$CODE" ] || { echo "### could not read Version/VersionCode from $SB" >&2; exit 1; }
COMMIT=$(git -C "$REPO" rev-parse --short HEAD)
echo "version $VER  code $CODE  commit $COMMIT"

# --------------------------------------------------------- 2. the version gate
# Reads ARTEFACTS, not names: an APK whose filename lies still blocks the build.
step "G0-main: version gate"
MAX=0; MAXF=""
while IFS= read -r a; do
  [ -n "$a" ] || continue
  c=$("$BT/aapt2" dump badging "$a" 2>/dev/null | sed -n "s/.*versionCode='\([0-9]*\)'.*/\1/p" | head -1)
  c=$(printf '%s' "${c:-0}" | tr -dc '0-9')
  if [ "${c:-0}" -gt "$MAX" ]; then MAX=$c; MAXF=$a; fi
done < <(find "$OUTDIR" "$STAGE" -name '*.apk' 2>/dev/null)
echo "highest published versionCode: $MAX ${MAXF:+($(basename "$MAXF"))}"
[ "$CODE" -gt "$MAX" ] || { echo "### REFUSED: VersionCode $CODE <= published $MAX" >&2; exit 7; }
for p in "$OUTDIR/CosmoSynth-$VER.apk" "$STAGE/CosmoSynth-$VER.apk"; do
  [ ! -e "$p" ] || { echo "### REFUSED: would collide with $p" >&2; exit 7; }
done

# ------------------------------------------------------------------- 3. G0
step "G0: static gates"
EXCUSED=$(grep -vcE '^[[:space:]]*(#|$)' tools/gates/known-red.txt 2>/dev/null | tr -dc '0-9')
if [ "${EXCUSED:-0}" -gt 0 ]; then
  echo "### REFUSED: $EXCUSED gate(s) excused in tools/gates/known-red.txt - a build never runs past one:" >&2
  grep -vE '^[[:space:]]*(#|$)' tools/gates/known-red.txt | sed 's/^/###   /' >&2
  exit 10
fi
G0OUT=$(bash tools/gate-all.sh --full 2>&1); g0rc=$?
printf '%s\n' "$G0OUT"
[ "$g0rc" -eq 0 ] || { echo "### RESULT=G0_FAIL"; exit 10; }

# --------------------------------------------------- 4. lock, guards, display
step "resources and the editor lock"
editor_lock
guard_resources 8 10
echo "### $(date -Is) starting; $(free -g | awk '/^Mem:/{print $7" GB available"}'), $(df -h / | awk 'NR==2{print $4" free"}')"
start_xvfb

# ------------------------------------------------------------------ 5. G1a
step "G1a: XrSetup"
unity_run xrsetup 1200 CosmoSynth.EditorTools.XrSetup.Run; rc=$?
if runner_bug; then echo "### RUNNER-BUG: XrSetup.Run did not resolve"; exit 11; fi
XRLOG="$UNITY_LOG"
n_ok=$(grep -ac 'XRSETUP_OK' "$XRLOG" | tr -dc '0-9')
n_mq=$(grep -ac 'MetaQuestFeature' "$XRLOG" | tr -dc '0-9')
[ "$rc" -eq 0 ] && [ "${n_ok:-0}" -gt 0 ] || { echo "### RESULT=G1A_FAIL (no XRSETUP_OK, rc=$rc)"; exit 12; }
echo "### XRSETUP_OK, MetaQuestFeature mentions: ${n_mq:-0}"

# ------------------------------------------------------------------ 6. G1b
step "G1b: ServerBuild.Diagnostics"
unity_run diag 1200 CosmoSynth.EditorTools.ServerBuild.Diagnostics; rc=$?
if runner_bug; then echo "### RUNNER-BUG: ServerBuild.Diagnostics did not resolve"; exit 11; fi
DIAGLOG="$UNITY_LOG"
n_ok=$(grep -ac 'DIAGNOSTICS_OK' "$DIAGLOG" | tr -dc '0-9')
n_bad=$(grep -ac 'DIAGNOSTICS_FAIL' "$DIAGLOG" | tr -dc '0-9')
n_cs=$(grep -ac 'error CS' "$DIAGLOG" | tr -dc '0-9')
[ "$rc" -eq 0 ] && [ "${n_ok:-0}" -gt 0 ] && [ "${n_bad:-0}" -eq 0 ] && [ "${n_cs:-0}" -eq 0 ] || {
  echo "### RESULT=G1B_FAIL (rc=$rc, OK=${n_ok:-0}, FAIL=${n_bad:-0}, error CS=${n_cs:-0})"; exit 12; }

# ------------------------------------------------------------------ 7. G1c
step "G1c: play-mode capture and assertions"
SHOTDIR="$STAGE/shots-$VER"
bash tools/shot.sh --views release --out "$SHOTDIR"; rc=$?
[ "$rc" -eq 0 ] || { echo "### RESULT=G1C_FAIL (rc=$rc)"; exit 14; }
REPORT=$(ls -1 "$SHOTDIR"/*.report.txt 2>/dev/null | head -1)
if [ -n "$REPORT" ]; then
  head -1 "$REPORT"
  HEADSHA=$(git -C "$REPO" rev-parse HEAD)
  if [ "$(grep -ac "commit $HEADSHA" "$REPORT" | tr -dc '0-9')" -eq 0 ] \
     || [ "$(grep -ac 'dirty 0' "$REPORT" | tr -dc '0-9')" -eq 0 ]; then
    echo "### RESULT=G1C_FAIL: the report does not name HEAD with dirty 0"; exit 14
  fi
else
  echo "### RESULT=G1C_FAIL: no report file in $SHOTDIR"; exit 14
fi

# ------------------------------------------------------------------- 8. G2
step "G2: BuildAndroid"
mkdir -p "$STAGE"
BUILDLOG=$(mktemp "$PROJECT/Logs/build-$(date +%Y%m%d-%H%M%S).XXXX.log")
TIMELOG=$(mktemp /tmp/cosmosynth-buildtime.XXXXXX)

# Signing. The keystore lives OUTSIDE the project so it is never synced and never
# committed. Today no CosmoSynth keystore exists: the APK is debug-signed and sideload
# only, and this says so loudly rather than letting it pass as shippable.
SECRETS="$HOME/projects/CosmoSynth_secrets"
if [ -r "$SECRETS/cosmosynth.keystore" ] && [ -r "$SECRETS/keystore.pass" ]; then
  export COSMOSYNTH_KEYSTORE="$SECRETS/cosmosynth.keystore"
  export COSMOSYNTH_KEYSTORE_PASS="$(cat "$SECRETS/keystore.pass")"
  export COSMOSYNTH_KEY_ALIAS="cosmosynth"
  export COSMOSYNTH_KEY_ALIAS_PASS="$COSMOSYNTH_KEYSTORE_PASS"
  SIGNING="release keystore"
else
  SIGNING="debug (sideload only)"
fi
echo "### SIGNING=$SIGNING"

COSMOSYNTH_OUTDIR="$STAGE" /usr/bin/time -v -o "$TIMELOG" \
  timeout -k 60 3600 "$UNITY" -batchmode -quit -buildTarget Android \
    -projectPath "$PROJECT" \
    -executeMethod CosmoSynth.EditorTools.ServerBuild.BuildAndroid \
    -logFile "$BUILDLOG"
rc=$?
case "$rc" in 124|137) echo "### RESULT=TIMEOUT after 3600s"; exit 9 ;; esac
grep -aE "^\[[A-Za-z][A-Za-z0-9]*\]|BUILD_OK|BUILD FAILED|error CS|Exception" "$BUILDLOG" | tail -30
RSS=$(sed -n 's/.*Maximum resident set size (kbytes): //p' "$TIMELOG" | head -1)
WALL=$(sed -n 's/.*Elapsed (wall clock) time.*: //p' "$TIMELOG" | head -1)
echo "### unity exit=$rc  peak RSS ${RSS:-?} kB  wall ${WALL:-?}  (log $BUILDLOG)"
[ "$(grep -ac 'BUILD_OK' "$BUILDLOG" | tr -dc '0-9')" -gt 0 ] || { echo "### RESULT=BUILD_FAIL"; tail -25 "$BUILDLOG"; exit 1; }

APK="$STAGE/CosmoSynth-$VER.apk"      # exactly this path, never `ls -t` guessing
[ -f "$APK" ] || { echo "### RESULT=NO_ARTIFACT ($APK missing)"; exit 3; }
ls -lh "$APK"

# ------------------------------------------------------------------- 9. G3
step "G3: verify-apk"
VOUT=$(bash tools/verify-apk.sh "$APK" 2>&1); vrc=$?
printf '%s\n' "$VOUT"
[ "$vrc" -eq 0 ] || { echo "### RESULT=VERIFY_FAIL"; exit 13; }

# ---------------------------------------------------------------- 10. notes
step "notes"
{
  echo "CosmoSynth $VER (code $CODE)"
  echo "Built $(date '+%d %b %Y %H:%M %Z') on $(hostname) WITH a graphics device"
  echo "Commit  $COMMIT on $(git -C "$REPO" rev-parse --abbrev-ref HEAD)"
  echo "Signing $SIGNING"
  echo "Caption $CAPTION"
  echo
  echo "Recent changes:"
  git -C "$REPO" log --oneline -5 | sed 's/^/  /'
  echo
  echo "-- gates ------------------------------------------------------------------"
  printf '%s\n' "$G0OUT" | sed 's/^/  /'
  echo
  echo "-- artefact ---------------------------------------------------------------"
  printf '%s\n' "$VOUT" | sed 's/^/  /'
  echo
  echo "  peak RSS ${RSS:-?} kB, build wall ${WALL:-?}"
  echo
  echo "Nothing above is a promise about behaviour: it is the artefact's own contents"
  echo "and the gates' own output. G1c runs flat on llvmpipe and proves nothing about"
  echo "multiview, MSAA, foveation, the compositor, hands or performance. Awaiting G5"
  echo "on the headset."
} > "$APK.notes.txt"
cat "$APK.notes.txt"

step "G4: publish"
echo "NOT IMPLEMENTED in the tooling lane: publishing needs tools/shotname.py (D §D3.11)"
echo "and the handoff writer (D §D6.7), which the integrator owns."
echo "The APK is STAGED, not published:"
echo "  $APK"
echo "  $APK.notes.txt"
echo "  shots in $SHOTDIR"
echo
echo "### RESULT=STAGED CosmoSynth-$VER.apk"
exit 0
