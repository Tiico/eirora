#!/usr/bin/env bash
#
# Compares the state hashes of the replay corpus (tests/Replays) from .NET with those from the IL2CPP determinism
# build on an Android emulator or device (TASK-021, NFR-02). Fails when any replay differs.
#
# Usage: scripts/check-determinism.sh [EiroraDeterminism.apk]
# Needs: dotnet, adb (or ADB=<path>) and one running emulator or connected device.
# Build the APK first: Unity -batchmode -quit -projectPath client -buildTarget Android
#   -executeMethod Eirora.Editor.Builds.AndroidDeterminism

set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
apk="${1:-$root/client/Builds/Android/EiroraDeterminism.apk}"
adb="${ADB:-adb}"
package="com.eirora.game.determinism"
out="$root/artifacts/determinism"
marker="EIRORA-DETERMINISM"
timeout_seconds="${DETERMINISM_TIMEOUT:-600}"

mkdir -p "$out"
rm -f "$out/dotnet.txt" "$out/android.txt"

# An APK older than the simulation or the corpus would compare yesterday's code.
[[ -f "$apk" ]] || { echo "::error::$apk does not exist; build it with Eirora.Editor.Builds.AndroidDeterminism."; exit 1; }
newer="$(find "$root/src/Eirora.Simulation" "$root/tests/Replays" -type f \( -name '*.cs' -o -name '*.replay' \) -newer "$apk" | head -n 1)"
if [[ -n "$newer" ]]; then
  echo "::error::$apk is older than $newer; build it again."
  exit 1
fi

echo "Hashing the corpus with .NET..."
dotnet test --project "$root/tests/Eirora.Simulation.Tests" --filter-class "*ReplayCorpusTests" >/dev/null
[[ -s "$out/dotnet.txt" ]] || { echo "::error::.NET wrote no hashes to $out/dotnet.txt"; exit 1; }

echo "Playing the corpus in the IL2CPP build on $("$adb" get-serialno)..."
"$adb" wait-for-device

# A slow emulator shows "isn't responding" dialogs that take the focus, and Unity waits for it; hide them and keep the
# screen on and unlocked.
"$adb" shell settings put global hide_error_dialogs 1
"$adb" shell svc power stayon true
"$adb" shell input keyevent KEYCODE_WAKEUP
"$adb" shell wm dismiss-keyguard

"$adb" install -r "$apk" >/dev/null
"$adb" logcat -c
"$adb" shell monkey -p "$package" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1

for ((second = 0; second < timeout_seconds; second += 2)); do
  log="$("$adb" logcat -d -s Unity)"
  if grep -q "$marker-DONE\|$marker-FAILED" <<<"$log"; then
    break
  fi
  "$adb" shell am broadcast -a android.intent.action.CLOSE_SYSTEM_DIALOGS >/dev/null 2>&1 || true
  sleep 2
done

"$adb" uninstall "$package" >/dev/null || true

if grep -q "$marker-FAILED" <<<"$log"; then
  echo "::error::The determinism build failed: $(grep -o "$marker-FAILED.*" <<<"$log")"
  exit 1
fi

if ! grep -q "$marker-DONE" <<<"$log"; then
  echo "::error::The determinism build did not finish within $timeout_seconds seconds."
  exit 1
fi

grep -o "$marker [^ ]* [0-9a-f]\{64\}" <<<"$log" | cut -d ' ' -f 2- | sort > "$out/android.txt"
sort "$out/dotnet.txt" -o "$out/dotnet.txt"

if ! diff -u "$out/dotnet.txt" "$out/android.txt"; then
  echo "::error::The IL2CPP build on Android and .NET give different states for the replays above."
  exit 1
fi

echo "All $(wc -l < "$out/dotnet.txt") replays give the same state in .NET and in IL2CPP on Android."
