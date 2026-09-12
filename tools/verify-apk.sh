#!/usr/bin/env bash
# G3 - gate a CosmoSynth APK before it is treated as shippable. Missing OpenXR boot
# entries, a wrong xr block, missing passthrough, a wrong package, a permission the app
# does not use, mixed ABIs, a missing data file, a stripped shader, a fontless build or
# an unexpected baked MSAA level all fail. A "successful" build that fails here does NOT
# ship.
#
#   tools/verify-apk.sh <apk>          sideload gate
#   STORE=1 tools/verify-apk.sh <apk>  store gate: hygiene warnings become failures
#   tools/verify-apk.sh --self-test    run the whole verifier on the .KEEP 1.9.0 and
#                                      compare every named verdict with the table below
#
# Every check reads the ARTEFACT, never the build log. Two traps are designed out:
#   * `grep -q` exits on the first match, the writer of the pipe dies with SIGPIPE, and
#     `pipefail` then reports a CORRECT build as failed - racily, depending on where in
#     the output the match sits. Everything here counts matches instead.
#   * `aapt2 dump badging` cannot print intent categories at all, so the VR category is
#     read from `dump xmltree`; a badging-based check reports every correct APK as flat.
#     The orientation value is parsed out from AFTER the attribute id, because matching
#     the whole token matches "0x0" inside the id and reports landscape for anything.
#
# Exit 0 OK, 1 FAIL, 3 no APK.
set -uo pipefail
cd "$(dirname "$0")/.." || exit 1
source tools/common.sh

STORE="${STORE:-0}"
PROMOTE_ALL="${PROMOTE_ALL:-0}"    # INFERRED checks (shaders, urp) start as warn; see the promotion rule
BASELINE=tools/xr-baseline.txt

fail=0
pass() { printf '  ok    %-16s %s\n' "$1" "$2"; }
bad()  { printf '  FAIL  %-16s %s\n' "$1" "$2"; fail=1; }
warn() { printf '  warn  %-16s %s\n' "$1" "$2"; }
info() { printf '  info  %-16s %s\n' "$1" "$2"; }
# Hygiene: a warning for sideload, a blocker for the store.
hyg()  { if [ "$STORE" = 1 ]; then bad "$1" "$2 (STORE=1)"; else warn "$1" "$2"; fi; }
# INFERRED pattern: warn until it has been seen passing once on a build whose G1b dump
# confirms the property, then set PROMOTE_ALL/list it. A guard that cannot pass is as
# broken as one that cannot fail.
inf()  { if [ "$PROMOTE_ALL" = 1 ]; then bad "$1" "$2"; else warn "$1" "$2 (INFERRED pattern, not yet promoted)"; fi; }

# has PATTERN TEXT -> true when PATTERN occurs at least once. Counts, never -q.
has() {
    local n
    n=$(printf '%s\n' "$2" | grep -c -- "$1" 2>/dev/null || true)
    n=$(printf '%s' "$n" | tr -dc '0-9')
    [ "${n:-0}" -gt 0 ]
}
# Same, for a corpus too big to want in a shell variable (the strings dump).
hasf() {
    local n
    n=$(grep -ac -- "$1" "$2" 2>/dev/null || true)
    n=$(printf '%s' "$n" | tr -dc '0-9')
    [ "${n:-0}" -gt 0 ]
}

# ------------------------------------------------------------------ self-test
if [ "${1:-}" = "--self-test" ]; then
    REF=/home/triton/builds/cosmosynth/recovered/CosmoSynth-release.apk
    echo "SELF-TEST: the whole verifier on the .KEEP 1.9.0 reference"
    [ -f "$REF" ] || { echo "SELF-TEST FAIL: $REF is missing"; exit 1; }
    OUT=$(PROMOTE_ALL=1 STORE=0 bash "$0" "$REF" 2>&1)
    printf '%s\n' "$OUT" | sed 's/^/    /'
    verdict() { printf '%s\n' "$OUT" | sed -n "s/^  \(ok\|FAIL\|warn\|info\)  *$1 .*/\1/p" | head -1; }
    st=0
    # id                expected  what it proves
    expect() {
        local got; got=$(verdict "$1")
        if [ "$got" = "$2" ]; then
            printf '  ok    self-test %-16s %-5s  %s\n' "$1" "$2" "$3"
        else
            printf '  FAIL  self-test %-16s want %s got %s  (%s)\n' "$1" "$2" "${got:-<none>}" "$3"
            st=1
        fi
    }
    expect xrmd5    ok   "the baseline comparison can pass"
    expect mf-vr    ok   "the VR category check can pass"
    expect lib-meta ok   "the lib check can pass"
    expect vername  FAIL "the versionName check can fail (filename says 'release')"
    expect font     FAIL "the font check can fail (1.9.0 is legacy TextMesh)"
    expect urp      FAIL "the URP check can fail (1.9.0 is Built-in)"
    expect shaders-absent ok "the shader matcher reports a name that is genuinely absent"

    # The baked-MSAA scan must decode against a known project YAML.
    QY=$(mktemp /tmp/cosmosynth-qs.XXXXXX.asset)
    if git -C "$REPO" show bcb2886:unity/ProjectSettings/QualitySettings.asset > "$QY" 2>/dev/null; then
        QOUT=$(python3 tools/gates/qsettings_scan.py "$REF" "$QY" 2>&1); qrc=$?
        printf '%s\n' "$QOUT" | sed 's/^/    /'
        if [ "$qrc" -eq 0 ]; then
            printf '  ok    self-test %-16s %-5s  %s\n' qsettings PASS "the scan decodes and self-checks"
        else
            printf '  FAIL  self-test %-16s        %s\n' qsettings "the scan did not self-check against bcb2886"
            st=1
        fi
    else
        printf '  warn  self-test %-16s        %s\n' qsettings "commit bcb2886 not in this repo - skipped"
    fi
    rm -f "$QY"
    if [ "$st" -ne 0 ]; then echo "SELF-TEST FAIL"; exit 1; fi
    echo "SELF-TEST PASS"
    exit 0
fi

APK="${1:-}"
[ -n "$APK" ] && [ -f "$APK" ] || { echo "No APK at '${APK:-<none>}'" >&2; exit 3; }
export PATH="$PATH:$HOME/Unity/Hub/Editor/6000.5.5f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin"

echo "Verifying $(basename "$APK") ($(du -h "$APK" | cut -f1))  STORE=$STORE"
VER=$(basename "$APK" .apk | sed -n 's/^CosmoSynth-//p')

WORK=$(mktemp -d /tmp/cosmosynth-verify.XXXXXX)
trap 'rm -rf "$WORK"' EXIT

# ------------------------------------------------------- boot.config / xr block
BOOT=$(unzip -p "$APK" assets/bin/Data/boot.config 2>/dev/null)
XR_COUNT=$(printf '%s\n' "$BOOT" | grep -c '^xr-' || true)
XR_COUNT=$(printf '%s' "$XR_COUNT" | tr -dc '0-9')
if has '^xrsdk-pre-init-library=UnityOpenXR' "$BOOT"; then
    pass bootcfg "boot.config wired for OpenXR ($XR_COUNT xr-* entries)"
else
    bad  bootcfg "boot.config has no xrsdk-pre-init-library - XR is not initialised"
fi
if [ "${XR_COUNT:-0}" -ge 10 ]; then
    pass xrcount "xr-* entry count $XR_COUNT"
else
    bad  xrcount "only ${XR_COUNT:-0} xr-* entries - the Meta Vulkan config was dropped"
fi
if has '^xr-use-vulkan-offscreen-swapchain' "$BOOT"; then
    pass vulkanswap "Meta Vulkan offscreen-swapchain entry present"
else
    bad  vulkanswap "xr-use-vulkan-offscreen-swapchain missing - black-passthrough signature"
fi

# The xr block as a whole, against a headset-confirmed baseline. Individual greps pass
# while a *value* inside the block has changed; the MD5 does not.
XRBLOCK=$(printf '%s\n' "$BOOT" | grep -i 'xr-')
XRMD5=$(printf '%s\n' "$XRBLOCK" | md5sum | cut -d' ' -f1)
if [ -f "$BASELINE" ] && [ "$(grep -c "^$XRMD5" "$BASELINE" | tr -dc '0-9')" -gt 0 ]; then
    pass xrmd5 "xr block $XRMD5 = $(grep "^$XRMD5" "$BASELINE" | head -1 | cut -d' ' -f2-)"
else
    bad  xrmd5 "xr block $XRMD5 is not in $BASELINE"
    printf '%s\n' "$XRBLOCK" | sort > "$WORK/got.txt"
    # The line diff, against the real baseline block read back out of the .KEEP reference
    # APK rather than a hand-copied text file that can drift from the MD5 beside it.
    REFAPK=/home/triton/builds/cosmosynth/recovered/CosmoSynth-release.apk
    if [ -f "$REFAPK" ] && [ "$APK" != "$REFAPK" ]; then
        unzip -p "$REFAPK" assets/bin/Data/boot.config 2>/dev/null | grep -i 'xr-' | sort > "$WORK/ref.txt"
        echo "        line diff against the 1.9.0 reference block (< reference, > this APK):"
        diff "$WORK/ref.txt" "$WORK/got.txt" | sed 's/^/          /' | head -30
    else
        echo "        got:"; sed 's/^/          /' "$WORK/got.txt" | head -25
    fi
fi

# -------------------------------------------------------------- libs and ABI
ZIPL=$(unzip -l "$APK")
for so in libUnityARFoundationMeta.so libUnityOpenXR.so libopenxr_loader.so libUnityOpenXRHands.so; do
    case "$so" in
        libUnityARFoundationMeta.so) id=lib-meta ;;
        libUnityOpenXR.so)           id=lib-openxr ;;
        libopenxr_loader.so)         id=lib-loader ;;
        libUnityOpenXRHands.so)      id=lib-hands ;;
    esac
    if has "$so" "$ZIPL"; then pass "$id" "$so present"
    else bad "$id" "$so MISSING - XR init dies on device"; fi
done
if has 'lib/arm64-v8a/' "$ZIPL"; then pass abi-arm64 "arm64-v8a in lib/"; else bad abi-arm64 "no arm64-v8a libs"; fi
V7A=$(printf '%s\n' "$ZIPL" | grep -c 'armeabi-v7a' || true); V7A=$(printf '%s' "$V7A" | tr -dc '0-9')
if [ "${V7A:-0}" -eq 0 ]; then pass abi-v7a "no armeabi-v7a entries"
else bad abi-v7a "zip still contains $V7A armeabi-v7a entries"; fi
if has 'libswappywrapper.so' "$ZIPL"; then pass hyg-swappy "libswappywrapper.so present"
else hyg hyg-swappy "libswappywrapper.so absent - frame pacing falls back"; fi

# ---------------------------------------------------------------- the manifest
if [ -x "$BT/aapt2" ]; then
    BADGE=$("$BT/aapt2" dump badging "$APK" 2>/dev/null)
    TREE=$("$BT/aapt2" dump xmltree "$APK" --file AndroidManifest.xml 2>/dev/null)

    has "package: name='com.triton.cosmosynth'" "$BADGE" \
        && pass pkg "package com.triton.cosmosynth" \
        || bad  pkg "unexpected package name: $(printf '%s\n' "$BADGE" | head -1)"

    if [ -n "$VER" ]; then
        has "versionName='$VER'" "$BADGE" \
            && pass vername "versionName $VER matches the filename" \
            || bad  vername "versionName does not match the filename ($VER)"
    else
        bad vername "filename is not CosmoSynth-<ver>.apk"
    fi

    if has "application-debuggable" "$BADGE"; then
        bad debuggable "the APK is debuggable"
    else
        pass debuggable "not debuggable"
    fi

    has "com.oculus.intent.category.VR" "$TREE" \
        && pass mf-vr "com.oculus.intent.category.VR (launches immersive, not flat)" \
        || bad  mf-vr "VR intent category missing - the app would launch as a flat panel"
    has "com.oculus.feature.PASSTHROUGH" "$BADGE" \
        && pass mf-passthrough "passthrough feature declared" \
        || bad  mf-passthrough "com.oculus.feature.PASSTHROUGH not in badging"
    # The real string is android.hardware.vr.headtracking, not a com.oculus.* name: the
    # first version of this check looked for the Oculus spelling and reported a correct
    # 1.9.0 as a 3DOF panel.
    has "headtracking" "$TREE" \
        && pass mf-headtracking "headtracking declared" \
        || bad  mf-headtracking "no headtracking feature - the app is a 3DOF panel"
    has "HAND_TRACKING\|handtracking" "$TREE" \
        && pass mf-handtracking "hand tracking declared" \
        || bad  mf-handtracking "no hand tracking declaration"
    has "OPENXR\|openxr" "$TREE" \
        && pass mf-openxr "OpenXR declared" \
        || bad  mf-openxr "no OpenXR declaration in the manifest"

    # VRC.Quest.Security.2: the app must claim no spatial or sensor capability it does not
    # use. All of these are off in the OpenXR feature set, so a hit means a feature came
    # back on (eye-tracked foveation, scene, anchors...).
    for perm in EYE_TRACKING USE_SCENE USE_ANCHOR_API RECORD_AUDIO \
                android.permission.CAMERA IOT_MAP_DATA; do
        if has "$perm" "$TREE"; then bad "perm-$perm" "manifest declares $perm"
        else pass "perm-$perm" "no $perm"; fi
    done
    # Store blockers, sideload warnings.
    has "android.permission.INTERNET" "$TREE" \
        && hyg hyg-internet "manifest declares INTERNET" \
        || pass hyg-internet "no INTERNET permission"
    has "BOUNDARY_VISIBILITY" "$TREE" \
        && hyg hyg-boundary "manifest declares BOUNDARY_VISIBILITY" \
        || pass hyg-boundary "no BOUNDARY_VISIBILITY"
    has "passthrough-contextual" "$TREE" \
        && pass hyg-presplash "passthrough pre-splash declared" \
        || hyg hyg-presplash "no passthrough pre-splash - VRC.Quest.Functional.14 wants one"

    TSDK=$(printf '%s\n' "$BADGE" | sed -n "s/.*targetSdkVersion:'\([0-9]*\)'.*/\1/p" | head -1)
    if [ -n "$TSDK" ] && [ "$TSDK" -le 34 ]; then pass hyg-targetsdk "targetSdkVersion $TSDK"
    else hyg hyg-targetsdk "targetSdkVersion ${TSDK:-?} - the upload validator rejects > 34"; fi

    # The upload validator wants landscape, and landscape is ActivityInfo 0, not 6. The
    # value sits AFTER the attribute id: "...:screenOrientation(0x0101001e)=0".
    if has "screenOrientation" "$TREE"; then
        ORI=$(printf '%s\n' "$TREE" | sed -n 's/.*screenOrientation([^)]*)=\(.*\)$/\1/p' | head -1 | tr -d ' \r')
        case "$ORI" in
            0|0x0|0x00000000) pass orientation "screenOrientation landscape (value $ORI)" ;;
            "")               warn orientation "screenOrientation present but unparsable" ;;
            *)                warn orientation "screenOrientation is $ORI - the validator wants 0" ;;
        esac
    else
        warn orientation "no android:screenOrientation"
    fi
else
    bad pkg "no aapt2 at $BT - the entire manifest section did not run"
fi

# ------------------------------------------------------------------- signing
if [ -x "$BT/apksigner" ]; then
    SIGN=$("$BT/apksigner" verify --print-certs "$APK" 2>&1)
    if has "certificate DN:" "$SIGN"; then
        DN=$(printf '%s\n' "$SIGN" | sed -n 's/.*certificate DN: //p' | head -1)
        info signing "$DN"
        if has "Android Debug" "$DN"; then
            hyg hyg-debugsign "signed with the ANDROID DEBUG key - sideload only"
        else
            pass hyg-debugsign "release-signed"
        fi
    else
        warn signing "signature could not be read"
    fi
fi

# --------------------------------------------------------------- data payload
# Derived from the tree, never hand-kept: every StreamingAssets file must be inside the
# APK at the same size. A catalogue that half-shipped is a gallery with holes in it.
SA_TREE="$PROJECT/Assets/StreamingAssets/CosmoSynth"
EXCL="${APK%.apk}.excluded.txt"
if [ -d "$SA_TREE" ]; then
    printf '%s\n' "$ZIPL" | awk '$1 ~ /^[0-9]+$/ {n=$4; for(i=5;i<=NF;i++) n=n" "$i; print $1"\t"n}' \
        | grep -a '^[0-9]*	assets/CosmoSynth/' > "$WORK/zip.tsv"
    nmiss=0; nsize=0; nok=0; nskip=0
    : > "$WORK/data-bad.txt"
    while IFS= read -r f; do
        rel=${f#"$SA_TREE"/}
        if [ -f "$EXCL" ] && [ "$(grep -Fxc "$rel" "$EXCL" | tr -dc '0-9')" -gt 0 ]; then
            nskip=$((nskip + 1)); continue
        fi
        want=$(stat -c %s "$f")
        got=$(awk -F'\t' -v k="assets/CosmoSynth/$rel" '$2==k {print $1; exit}' "$WORK/zip.tsv")
        if [ -z "$got" ]; then
            nmiss=$((nmiss + 1)); echo "missing  $rel" >> "$WORK/data-bad.txt"
        elif [ "$got" != "$want" ]; then
            nsize=$((nsize + 1)); echo "size     $rel ($want tree, $got apk)" >> "$WORK/data-bad.txt"
        else
            nok=$((nok + 1))
        fi
    done < <(find "$SA_TREE" -type f ! -name '*.meta' | sort)
    if [ "$nmiss" -eq 0 ] && [ "$nsize" -eq 0 ]; then
        pass data "$nok StreamingAssets files present at the right size ($nskip excluded by profile)"
    else
        bad data "$nmiss missing, $nsize wrong-sized of $((nok + nmiss + nsize)) StreamingAssets files"
        sed 's/^/        /' "$WORK/data-bad.txt" | head -10
    fi
else
    bad data "no StreamingAssets tree at $SA_TREE to compare against"
fi

# ------------------------------------------- strings in the serialised data
# Shader stripping, the font and the render pipeline are all invisible in the build log
# and total on the device. Read them out of the payload instead.
unzip -o -q "$APK" 'assets/bin/Data/*' -d "$WORK" 2>/dev/null
if [ -d "$WORK/assets/bin/Data" ]; then
    find "$WORK/assets/bin/Data" -maxdepth 2 -type f -print0 \
        | xargs -0 -r strings -a > "$WORK/strings.txt" 2>/dev/null

    # Every shader the runtime looks up by name, read off the source, not a hand-kept list.
    SHSRC="$PROJECT/Assets/CosmoSynth/Runtime/Rendering/CosmoShaders.cs"
    if [ -f "$SHSRC" ]; then
        # The identifiers inside CosmoShaders.All, resolved through their const declarations.
        sed -n '/All[[:space:]]*=/,/};/p' "$SHSRC" | grep -oE '^[[:space:]]*[A-Za-z_][A-Za-z0-9_]*,' \
            | tr -d ' ,' > "$WORK/shader-ids.txt"
        : > "$WORK/shader-missing.txt"; nsh=0
        while IFS= read -r id; do
            [ -n "$id" ] || continue
            name=$(sed -n "s/.*const string $id = \"\([^\"]*\)\".*/\1/p" "$SHSRC" | head -1)
            [ -n "$name" ] || continue
            nsh=$((nsh + 1))
            hasf "$name" "$WORK/strings.txt" || echo "$name" >> "$WORK/shader-missing.txt"
        done < "$WORK/shader-ids.txt"
        nmissing=$(grep -c . "$WORK/shader-missing.txt" | tr -dc '0-9')
        if [ "$nsh" -eq 0 ]; then
            bad shaders "could not parse any shader names out of CosmoShaders.All"
        elif [ "${nmissing:-0}" -eq 0 ]; then
            pass shaders "all $nsh CosmoShaders.All names occur in the built data"
        else
            inf shaders "$nmissing of $nsh shader names are absent: $(tr '\n' ' ' < "$WORK/shader-missing.txt")"
        fi
    else
        bad shaders "no CosmoShaders.cs at $SHSRC"
    fi

    # A name that is guaranteed absent. If THIS reports present, the matcher is broken and
    # every string check above is meaningless.
    if hasf "CosmoSynth/__NOT_A_SHADER__" "$WORK/strings.txt"; then
        bad shaders-absent "the string matcher reports an impossible shader name as present"
    else
        pass shaders-absent "the string matcher can report a name as absent"
    fi

    if hasf "$FONT_ASSET" "$WORK/strings.txt"; then pass font "$FONT_ASSET in the built data"
    else bad font "$FONT_ASSET is NOT in the built data - every label renders as nothing"; fi
    if hasf "TMP_SDF-Mobile" "$WORK/strings.txt"; then pass font-tmp "TMP_SDF-Mobile shader in the built data"
    else bad font-tmp "TMP_SDF-Mobile is NOT in the built data - text renders magenta or not at all"; fi

    # The URP asset the project actually points at, read off GraphicsSettings - NOT the
    # type name. `UniversalRenderPipelineAsset` is in the built data of any project that
    # merely has the URP package installed, so a check on it passes on a Built-in build:
    # it is green for a reason unrelated to its name. D §D3.4 offers it as an alternative
    # pattern; the 1.9.0 self-test caught it passing on a Built-in APK, so it is gone.
    GSA="$PROJECT/ProjectSettings/GraphicsSettings.asset"
    URPGUID=$(sed -n 's/.*m_CustomRenderPipeline: {fileID: [0-9]*, guid: \([0-9a-f]*\).*/\1/p' "$GSA" 2>/dev/null | head -1)
    URPNAME=""
    if [ -n "$URPGUID" ]; then
        URPMETA=$(grep -rl --include='*.asset.meta' "guid: $URPGUID" "$PROJECT/Assets" 2>/dev/null | head -1)
        [ -n "$URPMETA" ] && URPNAME=$(basename "${URPMETA%.meta}" .asset)
    fi
    if [ -z "$URPNAME" ]; then
        inf urp "GraphicsSettings names no custom render pipeline - this is a Built-in build"
    elif hasf "$URPNAME" "$WORK/strings.txt"; then
        pass urp "the project's URP asset '$URPNAME' is referenced in the built data"
    else
        inf urp "the project's URP asset '$URPNAME' is NOT in the built data"
    fi
else
    bad shaders "could not extract assets/bin/Data from the APK"
fi

# ------------------------------------------------------- baked quality settings
QY="$PROJECT/ProjectSettings/QualitySettings.asset"
if [ -f "$QY" ] && [ -f tools/gates/qsettings_scan.py ]; then
    QOUT=$(python3 tools/gates/qsettings_scan.py "$APK" "$QY" 2>&1); qrc=$?
    printf '%s\n' "$QOUT" | sed 's/^/        /'
    if [ "$qrc" -eq 0 ]; then
        pass qsettings "baked QualitySettings decode and match the project YAML"
    else
        bad qsettings "the baked QualitySettings scan did not self-check - its numbers mean nothing"
    fi
    # MSAA on a tile GPU with more than one camera is the resolve-per-camera trap.
    CAMS=$(cat "${TMPDIR:-/tmp}/cosmosynth-g0d.cameras" 2>/dev/null | tr -dc '0-9')
    AAMAX=$(printf '%s\n' "$QOUT" | sed -n "s/.*'antiAliasing': \([0-9]*\).*/\1/p" | sort -n | tail -1)
    if [ -n "${CAMS:-}" ] && [ "${CAMS:-0}" -gt 1 ] && [ "${AAMAX:-0}" -gt 1 ]; then
        bad qsettings-msaa "AA $AAMAX baked while G0d counted $CAMS cameras"
    else
        info qsettings-msaa "max baked antiAliasing ${AAMAX:-?}, G0d cameras ${CAMS:-unknown}"
    fi
else
    bad qsettings "no QualitySettings.asset or qsettings_scan.py to check against"
fi

# --------------------------------------------------------------- the self-test
# Every check above must be able to fail. Feed the match helper a string that should NOT
# match and a pattern that must, so a helper broken by a future edit (always-true or
# always-false) is caught here rather than by silence.
if has "com.oculus.intent.category.VR" "nothing here" || ! has "abc" "xx abc yy"; then
    bad selftest "the match helper is broken - every check above is meaningless"
else
    pass selftest "match helper distinguishes present from absent"
fi

if [ "$fail" -ne 0 ]; then echo "VERIFY FAIL"; exit 1; fi
echo "VERIFY OK"
exit 0
