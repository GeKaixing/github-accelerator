#!/bin/zsh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DIST="$ROOT/dist"
APPDIR="$DIST/mac-app/GitHubAccelerator.app"
SWIFT_SRC="$ROOT/packaging/mac/MenuBarHost.swift"
ICON_SRC="$ROOT/assets/AppIcon.icns"

mkdir -p "$APPDIR/Contents/MacOS" "$APPDIR/Contents/Resources"
rsync -a --delete "$DIST/osx-arm64/" "$APPDIR/Contents/Resources/runtime/"
cp "$ICON_SRC" "$APPDIR/Contents/Resources/AppIcon.icns"

cat > "$APPDIR/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>GitHub Accelerator</string>
  <key>CFBundleDisplayName</key><string>GitHub Accelerator</string>
  <key>CFBundleIdentifier</key><string>com.kaixing.github-accelerator</string>
  <key>CFBundleVersion</key><string>1.0.0</string>
  <key>CFBundleShortVersionString</key><string>1.0.0</string>
  <key>CFBundleExecutable</key><string>GitHubAccelerator</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>LSUIElement</key><false/>
</dict>
</plist>
PLIST

mkdir -p /tmp/clang-module-cache
CLANG_MODULE_CACHE_PATH=/tmp/clang-module-cache \
SDKROOT="$(xcrun --sdk macosx --show-sdk-path)" \
xcrun --sdk macosx swiftc "$SWIFT_SRC" -framework Cocoa -O -o "$APPDIR/Contents/MacOS/GitHubAccelerator"

ditto -c -k --sequesterRsrc --keepParent "$APPDIR" "$DIST/GitHubAccelerator-macOS-arm64.zip"
echo "done: $DIST/GitHubAccelerator-macOS-arm64.zip"
