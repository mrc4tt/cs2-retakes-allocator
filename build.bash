#!/usr/bin/env bash
#
# Local build script — mirrors .github/workflows/build.yml (no GitHub Actions needed).
# Restores, tests, builds Release, and packages the plugin into
# RetakesAllocator/bin/Release/RetakesAllocator (same layout the CI artifact uses).
#
# Usage: ./build.bash [--no-test]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

RUN_TESTS=1
if [[ "${1:-}" == "--no-test" ]]; then
    RUN_TESTS=0
fi

echo "==> Restore dependencies"
dotnet restore

if [[ "$RUN_TESTS" -eq 1 ]]; then
    echo "==> Test"
    dotnet test
fi

echo "==> Build (Release)"
dotnet build --no-restore -c Release

echo "==> Package"
cd "$SCRIPT_DIR/RetakesAllocator"
chmod +x release.bash
./release.bash

echo "==> Done: $SCRIPT_DIR/RetakesAllocator/bin/Release/RetakesAllocator"
