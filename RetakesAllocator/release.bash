#!/usr/bin/env bash

TARGET_NAME="RetakesAllocator"
# Auto-detect the framework output dir (net8.0/net10.0/...) so this survives
# TargetFramework bumps instead of hardcoding a version that goes stale.
TARGET_DIR="$(ls -d ./bin/Release/net*/ 2>/dev/null | head -1)"
TARGET_DIR="${TARGET_DIR%/}"
NEW_DIR="./bin/Release/RetakesAllocator"

if [[ -z "$TARGET_DIR" || ! -d "$TARGET_DIR" ]]; then
    echo "ERROR: no ./bin/Release/net*/ output dir found. Build Release first." >&2
    exit 1
fi

echo $TARGET_NAME
echo $TARGET_DIR
echo $NEW_DIR

ls $TARGET_DIR/**

echo rm -rf "$NEW_DIR"
rm -rf "$NEW_DIR"
echo cp -r $TARGET_DIR $NEW_DIR
cp -r $TARGET_DIR $NEW_DIR
echo rm -rf "$NEW_DIR/runtimes"
rm -rf "$NEW_DIR/runtimes"
echo mkdir "$NEW_DIR/runtimes"
mkdir "$NEW_DIR/runtimes"
echo cp -rf "$TARGET_DIR/runtimes/linux-x64" "$NEW_DIR/runtimes"
cp -rf "$TARGET_DIR/runtimes/linux-x64" "$NEW_DIR/runtimes"
echo cp -rf "$TARGET_DIR/runtimes/win-x64" "$NEW_DIR/runtimes"
cp -rf "$TARGET_DIR/runtimes/win-x64" "$NEW_DIR/runtimes"

# Remove unnecessary files
rm -f "$NEW_DIR/CounterStrikeSharp.API.dll"
