#!/usr/bin/env bash
# Measures the Eirora prototype on an Android phone over adb (TASK-015, docs/design/device-measurements.md).
#
#   measure.sh active [--apk PATH] [--light day|night] [--seconds N] [--starts N] [--out DIR]
#       Installs the APK if given, measures cold start N times, then holds the measurement scene for N seconds
#       while sampling frames, memory, temperature and the app's share of the battery.
#   measure.sh cold-start [--apk PATH] [--light day|night|year] [--starts N] [--out DIR]
#       Only the cold starts, to Android's first frame and to the client's first frame with the world, which for
#       --light year includes catching up a year away (TASK-049).
#   measure.sh background-start [--out DIR]
#   measure.sh background-end   [--out DIR]
#       Resets the battery statistics, and a day later reads the app's share of the battery and the total drain.
#   measure.sh compare A/results.tsv B/results.tsv [--margin PERCENT]
#       Checks that two runs of the same build agree within the margin (default 10 %), or within 1 percentage point
#       for shares and 1 °C for temperatures, which are too small for a relative margin.
#   measure.sh budget RESULTS.tsv... --tier floor|base|reference [--budgets FILE]
#       Checks measured results against the budgets of a tier of target device (TASK-024, budgets.tsv beside this
#       script) and fails when any is over; a metric that was not measured is reported and does not fail.
#
# Set ADB to the adb binary if it is not on PATH, and ANDROID_SERIAL to pick a device when several are attached.
# MEASURE_PACKAGE measures another package than the game's, such as the measurement build (se.eirora.app.measure), which
# installs beside the game from Google Play instead of over it.
# MEASURE_PAUSE_SECONDS sets the pause between cold starts (default 3), FIRST_FRAME_WAIT_SECONDS how long to wait for
# the client's first frame with the world (default 120), MEASURE_SETTLE_SECONDS how long the scene settles before the
# session is measured (default 10).
set -euo pipefail

PACKAGE="${MEASURE_PACKAGE:-se.eirora.app}"
PAUSE_SECONDS="${MEASURE_PAUSE_SECONDS:-3}"
FIRST_FRAME_WAIT_SECONDS="${FIRST_FRAME_WAIT_SECONDS:-120}"
SETTLE_SECONDS="${MEASURE_SETTLE_SECONDS:-10}"
ADB="${ADB:-adb}"
if ! command -v "$ADB" >/dev/null 2>&1 && [ -x "${LOCALAPPDATA:-}/Android/Sdk/platform-tools/adb.exe" ]; then
    ADB="$LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe"
fi

die() { echo "measure.sh: $*" >&2; exit 1; }
shell() { "$ADB" shell "$@" | tr -d '\r'; }
# adb joins the arguments of `adb shell` into one command line that the phone's shell splits again, so every value that
# is not a fixed word, such as a name read from the phone, is passed through quote to arrive there as one string.
quote() { printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"; }
record() { printf '%s\t%s\n' "$1" "$2" >>"$OUT/results.tsv"; printf '  %-28s %s\n' "$1" "$2"; }

parse_options() {
    APK="" LIGHT="day" SECONDS_TO_HOLD=300 STARTS=5 OUT="" MARGIN=10 TIER="" BUDGETS="$(dirname "$0")/budgets.tsv"
    while [ $# -gt 0 ]; do
        case "$1" in
            --apk) APK="$2"; shift 2 ;;
            --light) LIGHT="$2"; shift 2 ;;
            --seconds) SECONDS_TO_HOLD="$2"; shift 2 ;;
            --starts) STARTS="$2"; shift 2 ;;
            --out) OUT="$2"; shift 2 ;;
            --margin) MARGIN="$2"; shift 2 ;;
            --tier) TIER="$2"; shift 2 ;;
            --budgets) BUDGETS="$2"; shift 2 ;;
            *) POSITIONAL+=("$1"); shift ;;
        esac
    done
    case "$LIGHT" in day | night | year) ;; *) die "--light must be day, night or year" ;; esac
}

require_device() {
    "$ADB" get-state >/dev/null 2>&1 || die "no device; connect the phone with USB debugging or set ANDROID_SERIAL"
    OUT="${OUT:-measurements/$(date +%Y%m%d-%H%M%S)}"
    mkdir -p "$OUT"
    : >"$OUT/results.tsv"
}

describe_device() {
    record device "$(shell getprop ro.product.manufacturer) $(shell getprop ro.product.model)"
    record android "$(shell getprop ro.build.version.release) (API $(shell getprop ro.build.version.sdk))"
    record build "$(shell dumpsys package "$PACKAGE" | awk -F= '/versionName=/ { print $2; exit }')"
}

app_uid() { shell cmd package list packages -U "$PACKAGE" | sed -n "s/^package:$PACKAGE uid:\([0-9]*\).*/\1/p" | head -n 1; }

# Estimated mAh for the app's uid, from the battery statistics since the last reset.
app_mah() {
    local uid; uid="$(app_uid)"
    [ -n "$uid" ] || { echo 0; return; }
    shell dumpsys batterystats --charged | awk -v id="UID u0a$((uid - 10000)):" '
        index($0, id) { sub(/.*UID u0a[0-9]+: */, ""); print $1 + 0; found = 1; exit }
        END { if (!found) print 0 }'
}

total_drain_mah() {
    shell dumpsys batterystats --charged | awk 'match($0, /Computed drain: [0-9.]+/) { print substr($0, RSTART + 16, RLENGTH - 16) + 0; exit }'
}

# Battery and skin temperature in °C; the skin sensor is missing on some phones and emulators.
temperatures() {
    local battery skin
    battery="$(shell dumpsys battery | awk '/temperature:/ { printf "%.1f", $2 / 10; exit }')"
    skin="$(shell dumpsys thermalservice | awk '
        /Current temperatures from HAL/ { inside = 1; next }
        inside && /mType=3/ { match($0, /mValue=[0-9.]+/); v = substr($0, RSTART + 7, RLENGTH - 7) + 0; if (v > max) max = v; seen = 1 }
        inside && /^[^ \t]/ && !/Temperature/ { inside = 0 }
        END { if (seen) printf "%.1f", max; else print "n/a" }')"
    echo "$battery $skin"
}

pss_kb() {
    shell dumpsys meminfo "$PACKAGE" | awk '
        /TOTAL PSS:/ { print $3; exit }
        $1 == "TOTAL" && $2 ~ /^[0-9]+$/ { print $2; exit }'
}

unity_activity() { shell cmd package resolve-activity --brief "$PACKAGE" | tail -n 1; }

# The SurfaceFlinger layer the app renders into; Unity draws into a SurfaceView, which gfxinfo does not see. Its BLAST
# layer is the one that presents frames, not the SurfaceView itself or its Background. Up to Android 16 --list prints
# bare layer names; Android 17 prints RequestedLayerState{NAME parentId=... z=...}, where NAME starts with a hash
# (`231cb9a SurfaceView[...](BLAST)#13991`) and only the whole of it is a name that --latency knows.
app_layer() {
    shell dumpsys SurfaceFlinger --list | awk -v view="SurfaceView[$PACKAGE" '
        {
            name = $0
            if (sub(/^RequestedLayerState[{]/, "", name)) sub(/ parentId=.*| relativeParentId=.*| z=.*|[}]$/, "", name)
        }
        !found && index(name, view) && index(name, "BLAST") && !index(name, "Background") { print name; found = 1 }'
}

# The Unity extra for the measurement scene.
scene_argument() { quote "-eirora-measure $LIGHT"; }

median() { sort -n | awk '{ v[NR] = $1 } END { print v[int((NR + 1) / 2)] }'; }

# The milliseconds from the activity's start to the client's first frame with the world, and how long catching up with
# the clock took before it, from the log's timestamps. Android's first frame comes before Unity has loaded the scene,
# so only this one includes the catch-up. Waits for the client to log the frame (MeasurementScene).
first_frame() {
    local i log
    for i in $(seq 1 "$FIRST_FRAME_WAIT_SECONDS"); do
        log="$("$ADB" logcat -d -v epoch -s ActivityTaskManager:I ActivityManager:I Unity:I | tr -d '\r')"
        if printf '%s\n' "$log" | grep -q 'Eirora measure: first frame'; then
            printf '%s\n' "$log" | awk '
                / START u0 / && start == "" { start = $1 }
                /Eirora measure: first frame/ {
                    match($0, /caught up in [0-9]+ ms/); catch_up = substr($0, RSTART + 13, RLENGTH - 16); end = $1; exit
                }
                END { if (start == "") exit 1; printf "%.0f %s\n", (end - start) * 1000, catch_up }'
            return
        fi
        sleep 1
    done
    die "the client logged no first frame of the world; is the build older than TASK-049?"
}

measure_cold_start() {
    local activity="$1" i total frame times=() frames=() catch_ups=()
    echo "Cold start ($STARTS times):"
    for i in $(seq 1 "$STARTS"); do
        "$ADB" logcat -c
        total="$(shell am start -W -S -n "$(quote "$activity")" -e unity "$(scene_argument)" | awk '/TotalTime:/ { print $2 }')"
        [ -n "$total" ] || die "am start reported no TotalTime"
        frame="$(first_frame)" || die "the log has no start of the activity before the first frame of the world"
        times+=("$total")
        frames+=("${frame%% *}")
        catch_ups+=("${frame##* }")
        sleep "$PAUSE_SECONDS"
    done
    printf '%s\n' "${times[@]}" >"$OUT/cold-start-ms.txt"
    printf '%s\n' "${frames[@]}" >"$OUT/first-frame-ms.txt"
    record cold_start_median_ms "$(printf '%s\n' "${times[@]}" | median)"
    record first_frame_median_ms "$(printf '%s\n' "${frames[@]}" | median)"
    record catch_up_median_ms "$(printf '%s\n' "${catch_ups[@]}" | median)"
}

measure_session() {
    local activity="$1" layer start end i temps
    echo "Holding the $LIGHT scene for $SECONDS_TO_HOLD s:"
    shell am start -S -n "$(quote "$activity")" -e unity "$(scene_argument)" >/dev/null
    sleep "$SETTLE_SECONDS" # Let the scripted week play and the scene settle before measuring.

    shell dumpsys batterystats --reset >/dev/null
    shell dumpsys battery unplug >/dev/null # Count the session as on battery even while USB is attached.
    trap '"$ADB" shell dumpsys battery reset >/dev/null 2>&1' EXIT # Also when the session fails or is interrupted.
    temps="$(temperatures)"
    record battery_temp_start_c "${temps%% *}"
    record skin_temp_start_c "${temps##* }"

    layer="$(app_layer)"
    [ -n "$layer" ] || die "found no SurfaceFlinger layer for $PACKAGE; is the app in the foreground?"
    : >"$OUT/frames.txt" ; : >"$OUT/pss-kb.txt"
    start="$(date +%s)"; end=$((start + SECONDS_TO_HOLD)); i=0
    while [ "$(date +%s)" -lt "$end" ]; do
        # The latency dump holds the last 127 frames, about 4 s at 30 fps, so read it every 2 s.
        shell dumpsys SurfaceFlinger --latency "$(quote "$layer")" | awk 'NR > 1 && $2 > 0 && $2 < 9e18 { print $2 }' >>"$OUT/frames.txt"
        if [ $((i % 5)) -eq 0 ]; then pss_kb >>"$OUT/pss-kb.txt"; fi
        i=$((i + 1)); sleep 2
    done
    # Frame times between consecutive presents; gaps over a second are breaks between latency dumps.
    sort -n -u "$OUT/frames.txt" | awk 'NR > 1 { d = ($1 - prev) / 1e6; if (d > 0 && d < 1000) printf "%.3f\n", d } { prev = $1 }' |
        sort -n >"$OUT/frame-ms.txt"
    [ -s "$OUT/frame-ms.txt" ] ||
        die "read no frames from the SurfaceFlinger layer '$layer' during the pass; was the app in the foreground, and does dumpsys SurfaceFlinger --latency know the layer?"

    temps="$(temperatures)"
    record battery_temp_end_c "${temps%% *}"
    record skin_temp_end_c "${temps##* }"
    record app_battery_mah "$(app_mah)"
    record app_battery_mah_per_hour "$(awk -v m="$(app_mah)" -v s="$SECONDS_TO_HOLD" 'BEGIN { printf "%.1f", m * 3600 / s }')"
    shell dumpsys battery reset >/dev/null
    trap - EXIT

    awk '
        { n++; ms[n] = $1; sum += $1; if ($1 > 50) slow++ }
        END {
            printf "fps %.1f\nframe_p50_ms %.1f\nframe_p95_ms %.1f\nframe_max_ms %.1f\nframes_over_50ms_pct %.2f\n",
                n * 1000 / sum, ms[int(n * 0.5) + 1], ms[int(n * 0.95) + 1], ms[n], 100 * slow / n
        }' "$OUT/frame-ms.txt" | while read -r key value; do record "$key" "$value"; done
    record pss_max_mb "$(sort -n "$OUT/pss-kb.txt" | tail -n 1 | awk '{ printf "%.0f", $1 / 1024 }')"
    record pss_mean_mb "$(awk '{ s += $1 } END { printf "%.0f", s / NR / 1024 }' "$OUT/pss-kb.txt")"
}

active() {
    require_device
    if [ -n "$APK" ]; then
        "$ADB" install -r "$APK" >/dev/null
        record apk_mb "$(awk -v b="$(wc -c <"$APK")" 'BEGIN { printf "%.1f", b / 1048576 }')"
    fi
    shell pm path "$PACKAGE" >/dev/null 2>&1 || die "$PACKAGE is not installed; pass --apk"
    describe_device
    record light "$LIGHT"
    local activity; activity="$(unity_activity)"
    measure_cold_start "$activity"
    measure_session "$activity"
    shell am force-stop "$PACKAGE"
    echo "Results in $OUT/results.tsv"
}

cold_start() {
    require_device
    if [ -n "$APK" ]; then
        "$ADB" install -r "$APK" >/dev/null
    fi
    shell pm path "$PACKAGE" >/dev/null 2>&1 || die "$PACKAGE is not installed; pass --apk"
    describe_device
    record light "$LIGHT"
    measure_cold_start "$(unity_activity)"
    shell am force-stop "$PACKAGE"
    echo "Results in $OUT/results.tsv"
}

background_start() {
    require_device
    describe_device
    shell dumpsys batterystats --reset >/dev/null
    date +%s >"$OUT/started"
    record battery_level_start_pct "$(shell dumpsys battery | awk '/level:/ { print $2; exit }')"
    echo "Unplug the phone, use it as usual, and run 'measure.sh background-end --out $OUT' about a day later."
}

background_end() {
    [ -n "$OUT" ] && [ -f "$OUT/started" ] || die "pass the --out folder from background-start"
    "$ADB" get-state >/dev/null 2>&1 || die "no device"
    local hours; hours="$(awk -v s="$(cat "$OUT/started")" -v e="$(date +%s)" 'BEGIN { printf "%.1f", (e - s) / 3600 }')"
    record hours "$hours"
    record battery_level_end_pct "$(shell dumpsys battery | awk '/level:/ { print $2; exit }')"
    record app_battery_mah "$(app_mah)"
    record total_drain_mah "$(total_drain_mah)"
    record app_share_of_drain_pct "$(awk -v a="$(app_mah)" -v t="$(total_drain_mah)" 'BEGIN { printf "%.2f", (t > 0 ? 100 * a / t : 0) }')"
}

compare() {
    [ ${#POSITIONAL[@]} -eq 2 ] || die "compare needs two results.tsv files"
    awk -F'\t' -v margin="$MARGIN" '
        NR == FNR { a[$1] = $2; next }
        ($1 in a) && a[$1] ~ /^[0-9]+(\.[0-9]+)?$/ && $2 ~ /^[0-9]+(\.[0-9]+)?$/ {
            base = (a[$1] + $2) / 2
            diff = base > 0 ? 100 * ($2 - a[$1]) / base : 0
            if (diff < 0) diff = -diff
            absolute = $2 - a[$1]
            if (absolute < 0) absolute = -absolute
            close_enough = ($1 ~ /_pct$/ && absolute <= 1) || ($1 ~ /_c$/ && absolute <= 1)
            verdict = diff <= margin || close_enough ? "ok" : "OUTSIDE"
            if (verdict != "ok") failed = 1
            printf "%-28s %10s %10s %6.1f %%  %s\n", $1, a[$1], $2, diff, verdict
        }
        END { exit failed }' "${POSITIONAL[0]}" "${POSITIONAL[1]}"
}

budget() {
    [ ${#POSITIONAL[@]} -ge 1 ] || die "budget needs at least one results.tsv"
    case "$TIER" in floor | base | reference) ;; *) die "--tier must be floor, base or reference" ;; esac
    [ -f "$BUDGETS" ] || die "no budgets in $BUDGETS"
    # Results from several runs (a day and a night pass, the day in the background) are read together. Each file is
    # held to the budgets on its own, so a pass over budget fails even when another pass of the same metric is within.
    awk -F'\t' -v tier="$TIER" '
        FNR == NR && /^#/ { next }
        FNR == NR && $2 == tier { kind[$1] = $3; limit[$1] = $4; order[++n] = $1; next }
        FNR == NR { next }
        FILENAME != current { current = FILENAME; name[++files] = FILENAME }
        $2 ~ /^-?[0-9]+(\.[0-9]+)?$/ { value[files, $1] = $2; seen[$1] = 1 }
        END {
            split("skin battery", parts, " ")
            for (f = 1; f <= files; f++) {
                for (p in parts) {
                    start = parts[p] "_temp_start_c"; end = parts[p] "_temp_end_c"; warming = parts[p] "_warming_c"
                    if (((f, start) in value) && ((f, end) in value)) {
                        value[f, warming] = sprintf("%.1f", value[f, end] - value[f, start]); seen[warming] = 1
                    }
                }
            }
            printf "%-28s %10s %12s  %s\n", "metric (" tier ")", "measured", "budget", "verdict"
            for (i = 1; i <= n; i++) {
                m = order[i]
                bound = (kind[m] == "min" ? ">= " : "<= ") limit[m]
                if (!(m in seen)) { printf "%-28s %10s %12s  %s\n", m, "-", bound, "not measured"; continue }
                for (f = 1; f <= files; f++) {
                    if (!((f, m) in value)) continue
                    over = kind[m] == "min" ? value[f, m] + 0 < limit[m] + 0 : value[f, m] + 0 > limit[m] + 0
                    if (over) failed = 1
                    printf "%-28s %10s %12s  %s%s\n", m, value[f, m], bound, over ? "OVER BUDGET" : "ok", (files > 1 ? "  " name[f] : "")
                }
            }
            exit failed
        }' "$BUDGETS" "${POSITIONAL[@]}"
}

[ $# -gt 0 ] || die "usage: measure.sh active|cold-start|background-start|background-end|compare|budget [options]"
COMMAND="$1"; shift
POSITIONAL=()
parse_options "$@"
case "$COMMAND" in
    active) active ;;
    cold-start) cold_start ;;
    background-start) background_start ;;
    background-end) background_end ;;
    compare) compare ;;
    budget) budget ;;
    *) die "unknown command $COMMAND" ;;
esac
