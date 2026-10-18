#!/usr/bin/env bash
# The first deployment from deploy/README.md on a throwaway Docker host (CI): the same secrets and compose.yaml, with a
# locally built image and stand-in Apple and Google ids. Passes when migrate succeeds and the server answers /health.
# Usage: deploy/smoke-test.sh <image>
set -euo pipefail

image=${1:?usage: deploy/smoke-test.sh <image>}
deploy=$(cd "$(dirname "$0")" && pwd)
work=$(mktemp -d)
project=eirora-smoke

cp "$deploy/compose.yaml" "$deploy/create-secrets.sh" "$work/"
cd "$work"
docker tag "$image" ghcr.io/tiico/eirora-server:smoke

cat > .env <<EOF
EIRORA_PROJECT=$project
EIRORA_VERSION=smoke
EIRORA_HOST=eirora.test
TRAEFIK_NETWORK=$project-traefik
APPLE_BUNDLE_ID=se.eirora.app
APPLE_SERVICES_ID=se.eirora.signin
APPLE_TEAM_ID=TEAM123456
APPLE_KEY_ID=KEY1234567
GOOGLE_SERVER_CLIENT_ID=server.apps.googleusercontent.com
GOOGLE_IOS_CLIENT_ID=ios.apps.googleusercontent.com
EOF

# The steps of "Första gången" in deploy/README.md, with a throwaway key in place of Apple's.
mkdir -p secrets
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out secrets/apple_private_key.p8
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
for _ in $(seq 30); do
    if curl --silent --fail "http://$server:8080/health" > /dev/null; then
        echo "The server is healthy."
        exit 0
    fi
    sleep 2
done

echo "The server did not answer /health within a minute." >&2
exit 1
