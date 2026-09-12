#!/usr/bin/env bash
# Build and run the CosmoSynth.Core test suite headlessly.
#
# No standalone .NET install is required: Unity ships a full .NET SDK inside the editor
# directory, and CosmoSynth.Core is deliberately engine-free, so the whole numerical layer
# can be compiled and exercised without opening (or licensing) the Unity Editor.
#
#   ./run-tests.sh                 run everything except network tests
#   ./run-tests.sh Kepler          run only tests whose name contains "Kepler"
#   ./run-tests.sh -v              show stack traces for non-assertion errors
#   COSMOSYNTH_NETWORK_TESTS=1 ./run-tests.sh Horizons
#                                  include the live JPL Horizons validation
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

find_dotnet() {
    if command -v dotnet >/dev/null 2>&1; then
        command -v dotnet
        return 0
    fi
    # Newest Unity editor first, so we pick up the most recent bundled SDK.
    local candidate
    for candidate in $(ls -d "$HOME"/Unity/Hub/Editor/*/Editor/Data/DotNetSdk/dotnet 2>/dev/null | sort -Vr); do
        if [ -x "$candidate" ]; then
            echo "$candidate"
            return 0
        fi
    done
    return 1
}

DOTNET="$(find_dotnet)" || {
    echo "error: no dotnet SDK found." >&2
    echo "       Install the .NET 8 SDK, or a Unity 6 editor (which bundles one)." >&2
    exit 127
}

export DOTNET_ROOT="$(dirname "$DOTNET")"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

echo "Using dotnet: $DOTNET ($("$DOTNET" --version))"

# One build at a time per tree. Two concurrent builds in ONE tree share obj/ and bin/,
# which is a race in principle; separate trees have their own and need no lock, so the
# lock is keyed on the tree.
exec 8>"/tmp/cosmosynth-tests-$(printf %s "$REPO_ROOT" | md5sum | cut -c1-8).lock"
flock 8

# Compile-check the Unity runtime layer against the editor's own assemblies. This verifies
# no behaviour — it cannot, without an engine — but it catches a misremembered API or a
# changed signature in about a second instead of after a multi-minute editor import.
#
# The package DLLs come from a Library that only the MAIN tree has: a git worktree has no
# Library at all, and without this fallback the build fails with 16 CS errors about
# XRHandSubsystem, XROrigin and UnityEngine.InputSystem, so the Core suite never runs.
# Main's ScriptAssemblies are read-only here and frozen at whatever the editor last
# imported; a lane that adds a package must wait for main's editor to import it.
RUNTIME_PROJECT="$REPO_ROOT/tests/CosmoSynth.Runtime.Compile/CosmoSynth.Runtime.Compile.csproj"
if [ -d "$HOME/Unity/Hub/Editor" ] && [ -f "$RUNTIME_PROJECT" ]; then
    SA="$REPO_ROOT/unity/Library/ScriptAssemblies"
    [ -d "$SA" ] || SA=/home/triton/projects/CosmoSynth/unity/Library/ScriptAssemblies
    # A fixed /tmp path is clobbered by every other tree running at the same time, and the
    # error grep below then prints someone else's compile errors.
    LOG=$(mktemp /tmp/cosmosynth-runtime-build.XXXXXX.log)
    echo "Unity package assemblies: $SA ($(date -r "$SA/Unity.XR.Hands.dll" +%F 2>/dev/null || echo MISSING))"
    if nice -n 10 "$DOTNET" build "$RUNTIME_PROJECT" -c Release -v quiet --nologo \
            -p:UnityScriptAssemblies="$SA" > "$LOG" 2>&1; then
        echo "Unity runtime layer: compiles"
    else
        echo "Unity runtime layer: COMPILE ERRORS ($LOG)" >&2
        grep -E "error" "$LOG" | head -20 >&2
        exit 1
    fi
fi

# The editor-side layer (ServerBuild, XrSetup, PlayModeShot) is compiled by nothing outside
# Unity today. Built here when the project exists.
EDITOR_PROJECT="$REPO_ROOT/tests/CosmoSynth.Editor.Compile/CosmoSynth.Editor.Compile.csproj"
if [ -f "$EDITOR_PROJECT" ]; then
    ELOG=$(mktemp /tmp/cosmosynth-editor-build.XXXXXX.log)
    if nice -n 10 "$DOTNET" build "$EDITOR_PROJECT" -c Release -v quiet --nologo \
            -p:UnityScriptAssemblies="${SA:-}" > "$ELOG" 2>&1; then
        echo "Unity editor layer: compiles"
    else
        echo "Unity editor layer: COMPILE ERRORS ($ELOG)" >&2
        grep -E "error" "$ELOG" | head -20 >&2
        exit 1
    fi
fi
echo

nice -n 10 "$DOTNET" run \
    --project "$REPO_ROOT/tests/CosmoSynth.Core.Tests/CosmoSynth.Core.Tests.csproj" \
    --configuration Release \
    --verbosity quiet \
    -- "$@"
