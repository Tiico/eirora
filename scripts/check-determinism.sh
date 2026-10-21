#!/usr/bin/env bash
#
# Compares the state hashes of the determinism corpus (the replays in tests/Replays, the golden saves and the golden
# maps) from .NET with those from the IL2CPP determinism build on an Android emulator or device (TASK-021, NFR-02).
# Fails when any entry differs.
#
# Usage: scripts/check-determinism.sh [EiroraDeterminism.apk]
# Needs: dotnet, adb (or ADB=<path>), aapt2 from the Android SDK's build tools (or AAPT2=<path>) and one running
# emulator or connected device.
# Build the APK first: Unity -batchmode -quit -projectPath client -buildTarget Android
#   -executeMethod Eirora.Editor.Builds.AndroidDeterminism

set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
apk="${1:-$root/client/Builds/Android/EiroraDeterminism.apk}"
adb="${ADB:-adb}"
out="$root/artifacts/determinism"
marker="EIRORA-DETERMINISM"
timeout_seconds="${DETERMINISM_TIMEOUT:-600}"
# How long the app may take to show up as a process after it is launched.
launch_seconds=60

mkdir -p "$out"
rm -f "$out/dotnet.txt" "$out/android.txt" "$out/logcat.txt"

# An APK older than the code it is built from or the corpus would compare yesterday's code. ProjectSettings is left
# out because the build itself saves it after writing the APK.
[[ -f "$apk" ]] || { echo "::error::$apk does not exist; build it with Eirora.Editor.Builds.AndroidDeterminism."; exit 1; }
newer="$(find "$root/src/Eirora.Simulation" "$root/src/Eirora.Contracts" "$root/src/Eirora.WorldGeneration" "$root/client/Assets" "$root/client/Packages" "$root/tests/Replays" "$root/tests/Eirora.Simulation.Tests/Saving/Golden" "$root/tests/Eirora.Simulation.Tests/WorldGeneration/Golden" -type f -newer "$apk" | head -n 1)"
if [[ -n "$newer" ]]; then
  echo "::error::$apk is older than $newer; build it again."
  exit 1
fi

# The build derives its package name from the game's (Builds.AndroidDeterminism), so the APK is asked rather than told.
sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-$(dirname "$(dirname "$(command -v "$adb")")")}}"
aapt2="${AAPT2:-$(find "$sdk/build-tools" -maxdepth 2 \( -name aapt2 -o -name aapt2.exe \) 2>/dev/null | sort -V | tail -n 1)}"
[[ -n "$aapt2" ]] || { echo "::error::aapt2 was not found in $sdk/build-tools; set AAPT2."; exit 1; }
package="$("$aapt2" dump packagename "$apk" | tr -d '
')"
[[ "$package" == *.determinism ]] || { echo "::error::$apk is $package, not a determinism build."; exit 1; }

echo "Hashing the corpus with .NET..."
dotnet test --project "$root/tests/Eirora.Simulation.Tests" --filter-class "*ReplayCorpusTests"
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
"$adb" shell monkey -p "$package" -c android.intent.category.LAUNCHER 1 >/dev/null

# The app quits once it has reported, so a process that is gone before the report has crashed: no need to wait out the
# timeout.
started=0
gone=0
for ((second = 0; second < timeout_seconds; second += 2)); do
  log="$("$adb" logcat -d -s Unity)"
  if grep -q "$marker-DONE\|$marker-FAILED" <<<"$log"; then
    break
  fi
  if [[ -n "$("$adb" shell pidof "$package" | tr -d '
' || true)" ]]; then
    started=1
  elif ((started || second >= launch_seconds)); then
    gone=1
    break
  fi
  "$adb" shell am broadcast -a android.intent.action.CLOSE_SYSTEM_DIALOGS >/dev/null 2>&1 || true
  sleep 2
done

# Read again, in case the report came between the last read and the process quitting.
log="$("$adb" logcat -d -s Unity)"
"$adb" logcat -d >"$out/logcat.txt" || true
"$adb" uninstall "$package" >/dev/null || true

if grep -q "$marker-FAILED" <<<"$log"; then
  echo "::error::The determinism build failed: $(grep -o "$marker-FAILED.*" <<<"$log")"
  exit 1
fi

if ! grep -q "$marker-DONE" <<<"$log"; then
  if ((gone)); then
    echo "::error::$package $( ((started)) && echo "stopped" || echo "never started") without reporting; see the crash log below and $out/logcat.txt."
  else
    echo "::error::The determinism build did not finish within $timeout_seconds seconds; see $out/logcat.txt."
  fi
  "$adb" logcat -d -b crash || true
  exit 1
fi

grep -o "$marker [^ ]* [0-9a-f]\{64\}" <<<"$log" | cut -d ' ' -f 2- | sort > "$out/android.txt"
sort "$out/dotnet.txt" -o "$out/dotnet.txt"

if ! diff -u "$out/dotnet.txt" "$out/android.txt"; then
  echo "::error::The IL2CPP build on Android and .NET give different states for the entries above."
  exit 1
fi

echo "All $(wc -l < "$out/dotnet.txt") entries give the same state in .NET and in IL2CPP on Android."
