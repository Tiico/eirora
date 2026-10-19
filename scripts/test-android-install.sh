#!/usr/bin/env bash
# Regression: an authorization prompt must not end installation before the user can approve it.
set -euo pipefail
repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf -- "$tmp"' EXIT
cat > "$tmp/adb" <<'ADB'
#!/usr/bin/env bash
set -eu
if [[ "$1" == devices ]]; then
    echo 'List of devices attached'
    if [[ ! -f "$ANDROID_TEST_STATE/queried" ]]; then
        touch "$ANDROID_TEST_STATE/queried"
        echo 'test-pixel unauthorized transport_id:1'
    else
        echo 'test-pixel device product:panther model:Pixel_7 device:panther'
    fi
else
    [[ "$*" == '-s test-pixel install -r '* ]] || exit 1
    touch "$ANDROID_TEST_STATE/installed"
    echo Success
fi
ADB
chmod +x "$tmp/adb"
touch "$tmp/test.apk"
export ADB="$tmp/adb" ANDROID_TEST_STATE="$tmp" ANDROID_WAIT_SECONDS=5
bash "$repo/scripts/install-android.sh" --apk "$tmp/test.apk"
[[ -f "$tmp/installed" ]] || { echo 'FAIL: install was not reached after authorization' >&2; exit 1; }
echo 'PASS: waited for authorization and then installed'
