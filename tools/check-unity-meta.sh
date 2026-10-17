#!/usr/bin/env bash
# Every file and folder in a Unity package must have a committed .meta file, or Unity
# generates a new GUID on each machine. Packages edited from .NET tooling are checked here.
set -euo pipefail

missing=0
for package in src/Eirora.Simulation src/Eirora.Contracts; do
  while IFS= read -r -d '' path; do
    if [[ ! -f "$path.meta" ]]; then
      echo "Missing $path.meta (open the client in Unity to generate it)"
      missing=1
    fi
  done < <(find "$package" -mindepth 1 ! -name '*.meta' -print0)
done
exit "$missing"
