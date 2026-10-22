#!/usr/bin/env bash
# Regression for measure.sh budget (TASK-024): a run within the budgets passes, a run over any budget fails and names
# it, the warming is worked out from the start and end temperatures, an unmeasured metric does not fail, and each of
# several results files is held to the budgets on its own.
set -euo pipefail
here="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf -- "$tmp"' EXIT

printf '%s\t%s\n' device "Google Pixel 7" apk_mb 61.2 cold_start_median_ms 1840 fps 30.0 frame_p95_ms 34.9 \
    frames_over_50ms_pct 0.40 pss_max_mb 352 skin_temp_start_c 30.1 skin_temp_end_c 33.0 >"$tmp/good.tsv"
sed -e 's/^frame_p95_ms\t34.9/frame_p95_ms\t44.0/' -e 's/^skin_temp_end_c\t33.0/skin_temp_end_c\t36.5/' "$tmp/good.tsv" >"$tmp/slow.tsv"
printf '%s\t%s\n' app_share_of_drain_pct 3.10 >"$tmp/day.tsv"

"$here/measure.sh" budget "$tmp/good.tsv" --tier reference >"$tmp/good.out" || { cat "$tmp/good.out"; echo "FAIL: a run within budget failed"; exit 1; }
grep -q '^skin_warming_c *2.9 ' "$tmp/good.out" || { cat "$tmp/good.out"; echo "FAIL: warming not worked out"; exit 1; }
grep -q '^battery_warming_c .*not measured' "$tmp/good.out" || { cat "$tmp/good.out"; echo "FAIL: unmeasured metric not reported"; exit 1; }

if "$here/measure.sh" budget "$tmp/slow.tsv" --tier reference >"$tmp/slow.out"; then
    cat "$tmp/slow.out"; echo "FAIL: a run over budget passed"; exit 1
fi
grep -q '^frame_p95_ms .*OVER BUDGET' "$tmp/slow.out" && grep -q '^skin_warming_c .*OVER BUDGET' "$tmp/slow.out" ||
    { cat "$tmp/slow.out"; echo "FAIL: the metrics over budget are not named"; exit 1; }

# The same slow frames are within the floor's budget, but the warming is not.
"$here/measure.sh" budget "$tmp/slow.tsv" --tier floor >"$tmp/floor.out" && { echo "FAIL: warming over budget passed on the floor"; exit 1; }
grep -q '^frame_p95_ms .* ok$' "$tmp/floor.out" || { cat "$tmp/floor.out"; echo "FAIL: floor frame budget"; exit 1; }

# Several results files are read together, so a day in the background counts with the active pass.
if "$here/measure.sh" budget "$tmp/good.tsv" "$tmp/day.tsv" --tier base >"$tmp/both.out"; then
    cat "$tmp/both.out"; echo "FAIL: the day's share over budget passed"; exit 1
fi
grep -q '^app_share_of_drain_pct .*OVER BUDGET' "$tmp/both.out" || { cat "$tmp/both.out"; echo "FAIL: second file not read"; exit 1; }

# Each file is held to the budgets on its own: a day pass over budget fails even when a night pass after it is within.
if "$here/measure.sh" budget "$tmp/slow.tsv" "$tmp/good.tsv" --tier reference >"$tmp/day-night.out"; then
    cat "$tmp/day-night.out"; echo "FAIL: a pass over budget passed because a later pass was within"; exit 1
fi
grep -q "^frame_p95_ms .*OVER BUDGET  $tmp/slow.tsv\$" "$tmp/day-night.out" && grep -q "^frame_p95_ms .* ok  $tmp/good.tsv\$" "$tmp/day-night.out" ||
    { cat "$tmp/day-night.out"; echo "FAIL: the rows do not say which pass broke the budget"; exit 1; }

"$here/measure.sh" budget "$tmp/good.tsv" --tier top 2>/dev/null && { echo "FAIL: an unknown tier was accepted"; exit 1; }
echo "measure.sh budget: all checks passed"
