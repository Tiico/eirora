#!/usr/bin/env bash
# The first deployment from deploy/README.md on a throwaway Docker host (CI): the same secrets and compose.yaml, with a
# locally built image and stand-in Apple and Google ids. Passes when migrate succeeds, the server answers /health and
# Sign in with Apple is on or off as configured.
# Usage: deploy/smoke-test.sh <image> <google|google-and-apple>
#   google            Google only: no Apple ids in .env and no Apple key, as before the product owner has an Apple
#                     Developer account.
#   google-and-apple  Google and Sign in with Apple, with a throwaway key in place of Apple's.
set -euo pipefail

image=${1:?usage: deploy/smoke-test.sh <image> <google|google-and-apple>}
mode=${2:?usage: deploy/smoke-test.sh <image> <google|google-and-apple>}
case "$mode" in
    google | google-and-apple) ;;
    *) echo "Unknown mode $mode; use google or google-and-apple." >&2; exit 1 ;;
esac

deploy=$(cd "$(dirname "$0")" && pwd)
work=$(mktemp -d)
project=eirora-smoke-$mode

cp "$deploy/compose.yaml" "$deploy/create-secrets.sh" "$work/"
cd "$work"
docker tag "$image" ghcr.io/tiico/eirora-server:smoke

# The .env from .env.example, as deploy/README.md says, with each value filled in.
cp "$deploy/.env.example" .env
set_env() {
    grep --quiet "^$1=" .env || { echo "$1 is not in deploy/.env.example." >&2; exit 1; }
    sed --in-place "s|^$1=.*|$1=$2|" .env
}
set_env EIRORA_PROJECT "$project"
set_env EIRORA_VERSION smoke
set_env EIRORA_HOST eirora.test
set_env TRAEFIK_NETWORK "$project-traefik"
set_env GOOGLE_SERVER_CLIENT_ID server.apps.googleusercontent.com
set_env GOOGLE_IOS_CLIENT_ID ios.apps.googleusercontent.com

# The steps of "Första gången" in deploy/README.md, with a throwaway key in place of Apple's.
install -d -m 700 secrets
if [ "$mode" = google-and-apple ]; then
    set_env APPLE_BUNDLE_ID se.eirora.app
    set_env APPLE_SERVICES_ID se.eirora.signin
    set_env APPLE_TEAM_ID TEAM123456
    set_env APPLE_KEY_ID KEY1234567
    openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out secrets/apple_private_key.p8
fi
sudo ./create-secrets.sh

docker network create "$project-traefik" > /dev/null
cleanup() {
    docker compose --env-file .env logs --no-color || true
    docker compose --env-file .env down --volumes > /dev/null 2>&1 || true
    docker network rm "$project-traefik" > /dev/null 2>&1 || true
    sudo rm -rf "$work"
}
trap cleanup EXIT

# Starts the server only after migrate has completed successfully, and fails otherwise.
docker compose --env-file .env up --detach

# The server is never published; on a Linux host its address on the Traefik network is reachable directly.
server=$(docker inspect --format "{{(index .NetworkSettings.Networks \"$project-traefik\").IPAddress}}" "$project-server-1")
healthy=false
for _ in $(seq 30); do
    if curl --silent --fail --max-time 5 "http://$server:8080/health" > /dev/null; then
        healthy=true
        break
    fi
    sleep 2
done

if [ "$healthy" != true ]; then
    echo "The server did not answer /health in 30 attempts." >&2
    exit 1
fi

# Apple's web callback is only there with Apple on, and the deletion page only loads Apple's script then.
callback=$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 5 --request POST "http://$server:8080/signin/apple/callback")
deletion_page=$(curl --silent --fail --max-time 5 "http://$server:8080/account/delete")
if [ "$mode" = google ]; then
    expected_callback=404
    if grep --quiet appleid <<< "$deletion_page"; then
        echo "The deletion page offers Apple, which is off." >&2
        exit 1
    fi
else
    expected_callback=302
    grep --quiet appleid <<< "$deletion_page" || { echo "The deletion page does not offer Apple, which is on." >&2; exit 1; }
fi

if [ "$callback" != "$expected_callback" ]; then
    echo "Apple's web callback answered $callback, expected $expected_callback with $mode." >&2
    exit 1
fi

echo "The server is healthy with $mode."
