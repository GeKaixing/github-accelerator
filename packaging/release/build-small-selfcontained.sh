#!/bin/zsh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DIST="$ROOT/dist-small"

rm -rf "$DIST"
mkdir -p "$DIST"

publish_small() {
  local rid="$1"
  local out="$DIST/$rid"
  dotnet publish "$ROOT/github-accelerator.csproj" \
    -c Release -r "$rid" \
    --self-contained true \
    -p:UseAppHost=true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial \
    -p:EnableCompressionInSingleFile=true \
    -p:DebuggerSupport=false \
    -p:InvariantGlobalization=true \
    -p:StripSymbols=true \
    -o "$out"
}

publish_small osx-arm64
publish_small win-x64

# Package
cd "$DIST"
tar -czf github-accelerator-osx-arm64-small.tar.gz osx-arm64
zip -qr github-accelerator-win-x64-small.zip win-x64

echo "== size summary =="
ls -lh "$DIST"/*.tar.gz "$DIST"/*.zip

du -sh "$DIST"/osx-arm64 "$DIST"/win-x64
