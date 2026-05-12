#!/bin/bash
set -e

export PATH="/usr/local/share/dotnet:$PATH"

BASE_DIR="$(cd "$(dirname "$0")" && pwd)"
TEMP_DIR="$BASE_DIR/publish_arm64"
OUT_DIR="$BASE_DIR/output_arm64"
ENTITLEMENTS="$BASE_DIR/distribution/macOS/entitlements.xml"

cd "$BASE_DIR"

echo "Building Ryujinx Ava for macOS ARM64..."
dotnet publish src/Ryujinx/Ryujinx.csproj -c Release -r osx-arm64 -o "$TEMP_DIR" --self-contained true -p:DebugType=embedded

echo "Packaging Ryujinx.app bundle..."
cd distribution/macOS
./create_app_bundle.sh "$TEMP_DIR" "$OUT_DIR" "$ENTITLEMENTS"

echo "Build complete! App bundle is located at $OUT_DIR/Ryujinx.app"
