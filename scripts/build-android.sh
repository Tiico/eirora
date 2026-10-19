#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/android-common.sh"
install=false
debug=false
while (( $# )); do
    case "$1" in
        --install) install=true ;;
        --debug) debug=true ;;
        -h|--help) echo 'Usage: bash scripts/build-android.sh [--install] [--debug]'; exit 0 ;;
        *) android_fail 'Usage: bash scripts/build-android.sh [--install] [--debug]' ;;
    esac
    shift
done
[[ -f "$android_editor" ]] || android_fail "Unity missing: $android_editor. Install the project version with Android Build Support, or set UNITY_EDITOR."
# The signing wizard writes shell-quoted values outside the repository.
signing="${EIRORA_KEY_DIR:-$HOME/eirora-keys}/android-signing.env"
if $debug; then
    unset EIRORA_ANDROID_KEYSTORE EIRORA_ANDROID_KEYSTORE_PASS EIRORA_ANDROID_KEYALIAS_NAME EIRORA_ANDROID_KEYALIAS_PASS
elif [[ -f "$signing" ]]; then
    set -a
    source "$signing"
    set +a
fi
if [[ -z "${EIRORA_ANDROID_KEYSTORE:-}" ]]; then
    echo 'No release key configured; Unity will use its debug key.'
fi
cd "$android_repo"
apk="$android_repo/client/Builds/Android/Eirora.apk"
printf 'Building with Unity %s. Progress follows below.\n' "$android_version"
# Stream output immediately and require this run's completion marker, so an old APK cannot count as success.
# pipefail also preserves Unity failures even when it printed the completion marker.
if ! "$android_editor" -batchmode -nographics -quit -projectPath "$android_repo/client" -buildTarget Android -executeMethod Eirora.Editor.Builds.Android -logFile - 2>&1 |
    awk '{ print; fflush(); if (index($0, "Build Succeeded:") > 0) succeeded = 1 } END { exit !succeeded }'; then
    android_fail "Unity build failed or did not report success. See the output above. Close the project's Unity editor before building."
fi
[[ -s "$apk" ]] || android_fail 'Unity reported success but no APK was found.'
printf 'APK ready: %s\n' "$apk"
if $install; then bash "$android_repo/scripts/install-android.sh" --apk "$apk"; fi
