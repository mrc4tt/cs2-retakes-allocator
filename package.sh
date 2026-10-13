#!/usr/bin/env bash
# Tests, builds Release and packages dist/RetakesAllocator.zip in the CounterStrikeSharp layout:
#   addons/counterstrikesharp/plugins/RetakesAllocator/...
#   addons/counterstrikesharp/gamedata/RetakesAllocator.json
# Also writes dist/release-notes.md from the matching CHANGELOG.md section.
#
# Usage: ./package.sh [X.Y.Z] [--no-test]   (version defaults to PluginInfo.Version; when given it must match)
set -euo pipefail
cd "$(dirname "$0")"

PLUGIN=RetakesAllocator

RUN_TESTS=1
ARGS=()
for arg in "$@"; do
    if [[ "$arg" == "--no-test" ]]; then RUN_TESTS=0; else ARGS+=("$arg"); fi
done

SOURCE_VERSION="$(grep -oP 'public const string Version = "\K[^"]+' RetakesAllocatorCore/PluginInfo.cs)"
VERSION="${ARGS[0]:-$SOURCE_VERSION}"
if [[ "$VERSION" != "$SOURCE_VERSION" ]]; then
    echo "ERROR: version $VERSION does not match PluginInfo.Version ($SOURCE_VERSION)" >&2
    exit 1
fi

NOTES="$(awk -v v="$VERSION" '$0 ~ "^## " { if (found) exit; found = ($2 == v); next } found' CHANGELOG.md)"
if [[ -z "${NOTES//[[:space:]]/}" ]]; then
    echo "ERROR: CHANGELOG.md has no \"## $VERSION\" section" >&2
    exit 1
fi

rm -rf dist RetakesAllocator/bin/Release
if [[ "$RUN_TESTS" -eq 1 ]]; then
    dotnet test
fi
dotnet build RetakesAllocator/RetakesAllocator.csproj -c Release

STAGE=dist/stage
PLUGIN_DIR="$STAGE/addons/counterstrikesharp/plugins/$PLUGIN"
GAMEDATA_DIR="$STAGE/addons/counterstrikesharp/gamedata"
mkdir -p "$PLUGIN_DIR" "$GAMEDATA_DIR"
cp -r RetakesAllocator/bin/Release/net10.0/. "$PLUGIN_DIR/"
cp Resources/RetakesAllocator_gamedata.json "$GAMEDATA_DIR/$PLUGIN.json"

# Linux only, no debug symbols or backups.
find "$STAGE" \( -name '*.pdb' -o -name '*.bak' \) -delete
find "$STAGE" -type d -path '*/runtimes/*' -prune ! -name linux-x64 ! -name unix -exec rm -rf {} +

test -f "$PLUGIN_DIR/$PLUGIN.dll"
test -f "$PLUGIN_DIR/runtimes/linux-x64/native/libe_sqlite3.so"
for f in CounterStrikeSharp.API.dll RetakesPluginShared.dll; do
    if [[ -e "$PLUGIN_DIR/$f" ]]; then
        echo "ERROR: $f must not be shipped in the plugin folder" >&2
        exit 1
    fi
done
if [[ -d "$STAGE/addons/counterstrikesharp/configs" || -d "$PLUGIN_DIR/config" ]]; then
    echo "ERROR: config files must never be shipped" >&2
    exit 1
fi

(cd "$STAGE" && zip -qr "../$PLUGIN.zip" addons)
rm -rf "$STAGE"
printf '%s\n' "$NOTES" > dist/release-notes.md

unzip -l "dist/$PLUGIN.zip"
echo "==> dist/$PLUGIN.zip ($VERSION)"
