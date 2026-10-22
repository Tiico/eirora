#!/usr/bin/env bash
# Replaces client/Assets/Plugins/BouncyCastle/BouncyCastle.Cryptography.dll with the given NuGet version and prints the
# hashes for its README.md. Usage: scripts/update-bouncycastle.sh 2.7.0
set -euo pipefail
version="${1:?usage: $0 <version>}"
repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
plugin="$repo/client/Assets/Plugins/BouncyCastle"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
curl -sSfL "https://api.nuget.org/v3-flatcontainer/bouncycastle.cryptography/$version/bouncycastle.cryptography.$version.nupkg" -o "$work/package.nupkg"
unzip -q "$work/package.nupkg" -d "$work/package"
cp "$work/package/lib/netstandard2.0/BouncyCastle.Cryptography.dll" "$plugin/BouncyCastle.Cryptography.dll"
cp "$work/package/LICENSE.md" "$plugin/LICENSE.md"
printf 'Version %s\n' "$version"
printf 'SHA-256 of the DLL     %s\n' "$(sha256sum "$plugin/BouncyCastle.Cryptography.dll" | cut -d' ' -f1)"
printf 'SHA-256 of the package %s\n' "$(sha256sum "$work/package.nupkg" | cut -d' ' -f1)"
printf 'Now update %s and Directory.Packages.props.\n' "$plugin/README.md"
