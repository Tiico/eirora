#!/usr/bin/env bash
# Replaces client/Assets/Plugins/ZXing/zxing.dll with the given NuGet version and prints the hashes for its README.md.
# Usage: scripts/update-zxing.sh 0.16.11
set -euo pipefail
version="${1:?usage: $0 <version>}"
repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
plugin="$repo/client/Assets/Plugins/ZXing"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
curl -sSfL "https://api.nuget.org/v3-flatcontainer/zxing.net/$version/zxing.net.$version.nupkg" -o "$work/package.nupkg"
unzip -q "$work/package.nupkg" -d "$work/package"
cp "$work/package/lib/netstandard2.0/zxing.dll" "$plugin/zxing.dll"
printf 'Version %s\n' "$version"
printf 'SHA-256 of the DLL     %s\n' "$(sha256sum "$plugin/zxing.dll" | cut -d' ' -f1)"
printf 'SHA-256 of the package %s\n' "$(sha256sum "$work/package.nupkg" | cut -d' ' -f1)"
printf 'Now update %s.\n' "$plugin/README.md"
