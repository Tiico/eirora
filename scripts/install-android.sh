#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/android-common.sh"
usage() { echo 'Usage: bash scripts/install-android.sh [--apk FILE] [--serial SERIAL]'; }
apk=''
serial=''
while (( $# )); do
    case "$1" in
        --apk|--serial)
            (( $# >= 2 )) || android_fail "Missing value for $1"
            if [[ "$1" == --apk ]]; then apk="$2"; else serial="$2"; fi
            shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) usage >&2; android_fail "Unknown argument: $1" ;;
    esac
done
[[ -f "$android_adb" ]] || android_fail 'ADB missing. Install Unity Android Build Support or set ADB to adb.exe.'
if [[ -z "$apk" ]]; then
    shopt -s nullglob
    candidates=("$android_repo/client/Builds/Android/Eirora.apk" "$android_repo"/.claude/worktrees/*/client/Builds/Android/Eirora.apk "$android_repo"/.claude/worktrees/*/artifacts/unity/Eirora.apk)
    for candidate in "${candidates[@]}"; do
        if [[ -f "$candidate" && ( -z "$apk" || "$candidate" -nt "$apk" ) ]]; then apk="$candidate"; fi
    done
fi
[[ -f "$apk" && "$apk" == *.apk ]] || android_fail 'No APK found. Build one first or pass --apk FILE.'
wait_seconds="${ANDROID_WAIT_SECONDS:-120}"
[[ "$wait_seconds" =~ ^[0-9]+$ ]] || android_fail 'ANDROID_WAIT_SECONDS must be a number.'
deadline=$((SECONDS + wait_seconds))
waiting=false
while true; do
    devices="$("$android_adb" devices -l | tr -d '\r')"
    ready=()
    pixels=()
    while read -r id state details; do
        if [[ "$state" == device ]]; then
            ready+=("$id")
            if [[ " $details " == *' model:Pixel_7 '* ]]; then pixels+=("$id"); fi
        fi
    done <<< "$devices"
    if [[ -z "$serial" ]]; then
        if (( ${#pixels[@]} == 1 )); then serial="${pixels[0]}"
        elif (( ${#ready[@]} == 1 )); then serial="${ready[0]}"
        elif (( ${#ready[@]} > 1 )); then android_fail "Multiple devices. Use --serial. $devices"
        fi
    fi
    found=false
    for id in "${ready[@]}"; do [[ "$id" != "$serial" ]] || found=true; done
    if $found; then break; fi
    (( SECONDS < deadline )) || android_fail "Timed out waiting for USB authorization. Unlock the phone and approve USB debugging, then run the install script again. $devices"
    if ! $waiting; then
        printf 'Waiting up to %s seconds for your phone. Unlock it and approve USB debugging (Always allow from this computer).\n' "$wait_seconds"
        waiting=true
    fi
    sleep 2
done
printf 'APK: %s\nDevice: %s\n' "$apk" "$serial"
if ! "$android_adb" -s "$serial" install -r "$apk"; then
    android_fail 'Install failed. Check the signing key if signatures differ. The script never uninstalls or deletes saves.'
fi
echo 'Eirora installed. Open it on your phone.'
