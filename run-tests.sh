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

# Compile-check the Unity runtime layer against the editor's own assemblies, when one is
# present. This verifies no behaviour — it cannot, without an engine — but it catches a
# misremembered API or a changed signature in about a second instead of after a multi-minute
# editor import. Skipped silently if no Unity install is found.
RUNTIME_PROJECT="$REPO_ROOT/tests/CosmoSynth.Runtime.Compile/CosmoSynth.Runtime.Compile.csproj"
if [ -d "$HOME/Unity/Hub/Editor" ] && [ -f "$RUNTIME_PROJECT" ]; then
    if "$DOTNET" build "$RUNTIME_PROJECT" -c Release -v quiet --nologo > /tmp/cosmosynth-runtime-build.log 2>&1; then
        echo "Unity runtime layer: compiles"
    else
        echo "Unity runtime layer: COMPILE ERRORS" >&2
        grep -E "error" /tmp/cosmosynth-runtime-build.log | head -20 >&2
        exit 1
    fi
fi
echo

"$DOTNET" run \
    --project "$REPO_ROOT/tests/CosmoSynth.Core.Tests/CosmoSynth.Core.Tests.csproj" \
    --configuration Release \
    --verbosity quiet \
    -- "$@"
