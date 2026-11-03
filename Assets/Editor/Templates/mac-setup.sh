#!/bin/bash
# Run on the Mac from inside this folder:  bash mac-setup.sh
# (Written by the Windows export, docs/7-ios-release.md. A Windows zip loses execute bits and macOS quarantines downloads.)
set -e
cd "$(dirname "$0")"

echo "1/5  Making the build tools executable"
find . -name '*.sh' -exec chmod +x {} +
find . -name 'usymtool*' -exec chmod +x {} +
for d in Il2CppOutputProject/IL2CPP/build/deploy_*; do
  [ -d "$d" ] && find "$d" \( -name 'il2cpp' -o -name 'bee_backend' -o -name 'UnityLinker' \) -exec chmod +x {} +
done

echo "2/5  Removing the download quarantine"
xattr -dr com.apple.quarantine . || true

echo "3/5  Checking Homebrew and CocoaPods"
if ! command -v brew >/dev/null 2>&1; then
  if [ -x /opt/homebrew/bin/brew ]; then
    echo "Homebrew is installed but not on your PATH. Run these two lines, then run this script again:"
    echo "  echo 'eval \"\$(/opt/homebrew/bin/brew shellenv)\"' >> ~/.zprofile"
    echo "  eval \"\$(/opt/homebrew/bin/brew shellenv)\""
    exit 1
  fi
  echo "Homebrew not found (optional): https://brew.sh"
fi
if ! command -v pod >/dev/null 2>&1; then
  echo "CocoaPods not found. Install it with:  brew install cocoapods   then run this script again."
  exit 1
fi

echo "4/5  pod install --repo-update"
pod install --repo-update

echo "5/5  Opening Unity-iPhone.xcworkspace (always open the workspace, not the .xcodeproj)"
open Unity-iPhone.xcworkspace
