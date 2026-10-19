#!/usr/bin/env bash
# Shared paths for the Android scripts; source from Bash.
android_repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
android_version="$(sed -n 's/^m_EditorVersion: //p' "$android_repo/client/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
android_editor="${UNITY_EDITOR:-C:/Program Files/Unity/Hub/Editor/$android_version/Editor/Unity.exe}"
android_adb="${ADB:-}"
if [[ -z "$android_adb" ]]; then
    android_adb="$(command -v adb || true)"
    android_adb="${android_adb:-${android_editor%/Unity.exe}/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe}"
fi
android_fail() { printf '%s\n' "$*" >&2; exit 1; }
