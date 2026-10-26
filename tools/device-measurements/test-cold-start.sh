#!/usr/bin/env bash
# Regression for measure.sh cold-start (TASK-049) against a fake adb: each start is timed to Android's first frame and,
# from the log's timestamps, to the client's first frame with the world, the catch-up the client logs is read, and the
# medians are recorded; a client that never logs its first frame fails the measurement instead of hanging.
set -euo pipefail
here="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf -- "$tmp"' EXIT

# The fake phone, which parses shell commands like a phone (see test-active.sh): three starts of the year scene, Android's first frame and the client's after a few seconds.
cat >"$tmp/adb" <<'FAKE'
#!/usr/bin/env bash
state="$(dirname "$0")/starts"
starts="$(cat "$state" 2>/dev/null || echo 0)"
if [ "$1" = shell ]; then
    # Like adb: join the arguments with spaces and let a POSIX shell split the line, failing on a syntax error.
    shift
    words="$(sh -c "set -f; set -- $*; for word; do printf '%s\n' \"\$word\"; done" 2>&1)" ||
        { echo "/system/bin/sh: $words" >&2; exit 1; }
    set -f; IFS=$'\n'; set -- shell $words; set +f
fi
IFS='|'; command="$*"; unset IFS
case "$command" in
    get-state) echo device ;;
    "shell|pm|path|se.eirora.app") echo "package:/data/app/se.eirora.app/base.apk" ;;
    "shell|getprop|ro.product.manufacturer") echo samsung ;;
    "shell|getprop|ro.product.model") echo SM-A065F ;;
    "shell|getprop|ro.build.version.release") echo 14 ;;
    "shell|getprop|ro.build.version.sdk") echo 34 ;;
    "shell|dumpsys|package|se.eirora.app") echo "    versionName=0.4.0" ;;
    "shell|cmd|package|resolve-activity|--brief|se.eirora.app") printf 'priority=0 preferredOrder=0\r\nse.eirora.app/com.unity3d.player.UnityPlayerGameActivity\r\n' ;;
    "logcat|-c") : ;;
    "shell|am|start|-W|-S|-n|se.eirora.app/com.unity3d.player.UnityPlayerGameActivity|-e|unity|-eirora-measure year")
        echo $((starts + 1)) >"$state"
        printf 'Status: ok\r\nTotalTime: %s\r\n' "$(echo "1300 1100 1200" | cut -d' ' -f$((starts + 1)))" ;;
    "shell|am|start|-W|-S|"*) echo "unexpected scene: ${command//|/ }" >&2; exit 1 ;;
    "logcat|-d|-v|epoch|-s|ActivityTaskManager:I|ActivityManager:I|Unity:I")
        [ -f "$(dirname "$0")/silent" ] && exit 0
        printf '1761200000.100  1201  1201 I ActivityTaskManager: START u0 {flg=0x10000000 cmp=se.eirora.app/.Unity}\r\n'
        printf '1761200001.000  4410  4410 I Unity   : Loading the island\r\n'
        printf '1761200%03d.%03d  4410  4410 I Unity   : Eirora measure: first frame of the year scene, caught up in %s ms\r\n' \
            "$(echo "4 3 4" | cut -d' ' -f"$starts")" "$(echo "350 900 700" | cut -d' ' -f"$starts")" "$(echo "830 790 812" | cut -d' ' -f"$starts")" ;;
    "shell|am|force-stop|se.eirora.app") : ;;
    *) echo "fake adb: unexpected ${command//|/ }" >&2; exit 1 ;;
esac
FAKE
chmod +x "$tmp/adb"

export ADB="$tmp/adb" MEASURE_PAUSE_SECONDS=0
"$here/measure.sh" cold-start --light year --starts 3 --out "$tmp/year" >"$tmp/year.out" || { cat "$tmp/year.out"; echo "FAIL: the cold starts were not measured"; exit 1; }
for expected in "cold_start_median_ms	1200" "first_frame_median_ms	4250" "catch_up_median_ms	812" "light	year"; do
    grep -qxF "$expected" "$tmp/year/results.tsv" || { cat "$tmp/year/results.tsv"; echo "FAIL: expected '$expected'"; exit 1; }
done

# A client that never logs its first frame, such as a build from before TASK-049, fails.
rm -f "$tmp/starts"
touch "$tmp/silent"
if FIRST_FRAME_WAIT_SECONDS=1 "$here/measure.sh" cold-start --light year --starts 1 --out "$tmp/silent-run" >"$tmp/silent.out" 2>&1; then
    cat "$tmp/silent.out"; echo "FAIL: a start without a first frame of the world passed"; exit 1
fi
grep -q "logged no first frame" "$tmp/silent.out" || { cat "$tmp/silent.out"; echo "FAIL: the reason is not given"; exit 1; }

"$here/measure.sh" cold-start --light dusk 2>/dev/null && { echo "FAIL: an unknown light was accepted"; exit 1; }
echo "measure.sh cold-start: all checks passed"
