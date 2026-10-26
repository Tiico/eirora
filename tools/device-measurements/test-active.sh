#!/usr/bin/env bash
# Regression for measure.sh active (#30) against a fake adb that parses shell commands like a phone: adb joins the
# arguments of `adb shell` into one command line and the phone's shell splits it again, so a value from the phone that
# measure.sh passes back unquoted, such as a SurfaceFlinger layer name with parentheses, is a syntax error there.
# A short pass is held and results.tsv gets frame, memory, temperature and battery values.
set -euo pipefail
here="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf -- "$tmp"' EXIT

# The fake phone: a Pixel whose Unity layer has the brackets, parentheses and hash of a real SurfaceFlinger name.
cat >"$tmp/adb" <<'FAKE'
#!/usr/bin/env bash
set -euo pipefail
activity='se.eirora.app/com.unity3d.player.UnityPlayerGameActivity'
layer='SurfaceView[se.eirora.app/com.unity3d.player.UnityPlayerGameActivity](BLAST)#1234'
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
    "shell|getprop|ro.product.manufacturer") echo Google ;;
    "shell|getprop|ro.product.model") echo "Pixel 7" ;;
    "shell|getprop|ro.build.version.release") echo 17 ;;
    "shell|getprop|ro.build.version.sdk") echo 37 ;;
    "shell|dumpsys|package|se.eirora.app") echo "    versionName=0.2.1" ;;
    "shell|cmd|package|resolve-activity|--brief|se.eirora.app") printf 'priority=0 preferredOrder=0\r\n%s\r\n' "$activity" ;;
    "logcat|-c") : ;;
    "shell|am|start|-W|-S|-n|$activity|-e|unity|-eirora-measure day") printf 'Status: ok\r\nTotalTime: 1400\r\n' ;;
    "logcat|-d|-v|epoch|-s|ActivityTaskManager:I|ActivityManager:I|Unity:I")
        printf '1761200000.100  1201  1201 I ActivityTaskManager: START u0 {flg=0x10000000 cmp=%s}\r\n' "$activity"
        printf '1761200003.600  4410  4410 I Unity   : Eirora measure: first frame of the day scene, caught up in 40 ms\r\n' ;;
    "shell|am|start|-S|-n|$activity|-e|unity|-eirora-measure day") printf 'Starting: Intent { cmp=%s }\r\n' "$activity" ;;
    "shell|dumpsys|batterystats|--reset" | "shell|dumpsys|battery|unplug" | "shell|dumpsys|battery|reset") : ;;
    "shell|dumpsys|battery") printf 'Current Battery Service state:\r\n  level: 80\r\n  temperature: 312\r\n' ;;
    "shell|dumpsys|thermalservice")
        printf 'Current temperatures from HAL:\r\n\tTemperature{mValue=36.5, mType=3, mName=skin, mStatus=0}\r\n'
        printf '\tTemperature{mValue=31.2, mType=2, mName=battery, mStatus=0}\r\nCurrent cooling devices from HAL:\r\n' ;;
    "shell|dumpsys|SurfaceFlinger|--list")
        printf 'Display 4619827259835644672 HWC layers:\r\n'
        printf 'SurfaceView[se.eirora.app/com.unity3d.player.UnityPlayerGameActivity](BLAST)#1233 Background\r\n'
        printf '%s\r\nStatusBar#88\r\n' "$layer" ;;
    "shell|dumpsys|SurfaceFlinger|--latency|$layer")
        # Sixty-one frames presented 1/60 s apart, so sixty frame times of 16.7 ms.
        printf '16666666\r\n'
        awk 'BEGIN { for (i = 0; i <= 60; i++) printf "%d %d %d\r\n", 1e12 + i * 16666667, 1e12 + i * 16666667 + 5e6, 1e12 + i * 16666667 }' ;;
    "shell|dumpsys|meminfo|se.eirora.app") printf '** MEMINFO in pid 4410 [se.eirora.app] **\r\n           TOTAL PSS:   245760            TOTAL RSS:   301234\r\n' ;;
    "shell|cmd|package|list|packages|-U|se.eirora.app") printf 'package:se.eirora.app uid:10234\r\n' ;;
    "shell|dumpsys|batterystats|--charged") printf '  Estimated power use (mAh):\r\n    Computed drain: 410\r\n    UID u0a234: 12.3 ( cpu=10.1 screen=2.2 )\r\n' ;;
    "shell|am|force-stop|se.eirora.app") : ;;
    *) echo "fake adb: unexpected ${command//|/ }" >&2; exit 1 ;;
esac
FAKE
chmod +x "$tmp/adb"

export ADB="$tmp/adb" MEASURE_PAUSE_SECONDS=0 MEASURE_SETTLE_SECONDS=0
"$here/measure.sh" active --light day --seconds 1 --starts 1 --out "$tmp/day" >"$tmp/day.out" 2>&1 ||
    { cat "$tmp/day.out"; echo "FAIL: the active pass was not measured"; exit 1; }
for expected in "device	Google Pixel 7" "android	17 (API 37)" "build	0.2.1" "light	day" "cold_start_median_ms	1400" \
    "first_frame_median_ms	3500" "battery_temp_start_c	31.2" "skin_temp_start_c	36.5" "battery_temp_end_c	31.2" \
    "skin_temp_end_c	36.5" "fps	60.0" "frame_p50_ms	16.7" "frame_max_ms	16.7" "frames_over_50ms_pct	0.00" \
    "pss_max_mb	240" "pss_mean_mb	240" "app_battery_mah	12.3" "app_battery_mah_per_hour	44280.0"; do
    grep -qxF "$expected" "$tmp/day/results.tsv" || { cat "$tmp/day.out"; echo "FAIL: expected '$expected'"; exit 1; }
done
echo "measure.sh active: all checks passed"
