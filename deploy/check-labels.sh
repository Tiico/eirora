#!/usr/bin/env bash
# Checks the Traefik labels of deploy/compose.yaml and deploy/site/compose.yaml as Compose hands them to Traefik.
# Compose only interpolates values, never mapping keys, so a variable in a label key reaches Traefik as literal text: the
# routers get names like ${EIRORA_PROJECT:-eirora} while the values that refer to them are filled in, and Traefik turns
# off the router whose middleware it cannot find. Each compose file is rendered for two projects, as production and
# staging run side by side, and passes when:
#   - no label key or value has a variable left in it,
#   - every middleware a router uses is defined by the same file,
#   - the two projects share no router, service or middleware name.
# Usage: deploy/check-labels.sh
set -euo pipefail

deploy=$(cd "$(dirname "$0")" && pwd)
failed=0

fail() {
    echo "$1" >&2
    failed=1
}

# The labels of every service in a compose file, rendered by Compose with stand-in values and nothing from the caller's
# environment or a .env next to the file.
labels() {
    local file=$1
    shift
    env -i PATH="$PATH" HOME="$HOME" DOCKER_HOST="${DOCKER_HOST:-}" "$@" \
        docker compose --file "$file" --env-file /dev/null config --format json |
        jq '[.services[] | .labels // {}] | add // {}'
}

# The Traefik routers, services and middlewares the labels define, one kind/name per line.
traefik_names() {
    jq --raw-output 'keys[] | select(startswith("traefik.http.")) | split(".") | "\(.[2])/\(.[3])"' | sort --unique
}

check() {
    local file=$1 project_variable=$2
    shift 2
    local first second
    first=$(labels "$file" "$project_variable=eirora-check" "$@")
    second=$(labels "$file" "$project_variable=eirora-check-staging" "$@")

    local left
    left=$(jq --raw-output 'to_entries[] | select((.key + .value) | contains("$")) | .key' <<< "$first")
    if [ -n "$left" ]; then
        fail "$file: Compose leaves a variable in these labels, which Traefik then reads literally:"$'\n'"$left"
    fi

    local missing
    missing=$(jq --raw-output '
        (keys | map(select(startswith("traefik.http.middlewares.")) | split(".")[3]) | unique) as $defined
        | to_entries[]
        | select(.key | test("^traefik\\.http\\.routers\\.[^.]+\\.middlewares$"))
        | (.value | split(",")[] | gsub("^\\s+|\\s+$"; "")) as $middleware
        | select(($middleware | endswith("@docker") or (contains("@") | not)) and
                 ([$middleware | sub("@docker$"; "")] | inside($defined) | not))
        | "\(.key) uses \($middleware)"' <<< "$first")
    if [ -n "$missing" ]; then
        fail "$file: routers use middlewares the file does not define, so Traefik turns the routers off:"$'\n'"$missing"
    fi

    local shared
    shared=$(comm -12 <(traefik_names <<< "$first") <(traefik_names <<< "$second"))
    if [ -n "$shared" ]; then
        fail "$file: two projects, such as production and staging, would share these Traefik names:"$'\n'"$shared"
    fi
}

check "$deploy/compose.yaml" EIRORA_PROJECT \
    EIRORA_VERSION=check EIRORA_HOST=eirora.test \
    GOOGLE_SERVER_CLIENT_ID=server.apps.googleusercontent.com GOOGLE_IOS_CLIENT_ID=ios.apps.googleusercontent.com
check "$deploy/site/compose.yaml" EIRORA_SITE_PROJECT \
    EIRORA_SITE_HOST=site.eirora.test EIRORA_DEVELOPER_NAME=Developer EIRORA_PRIVACY_CONTACT=privacy@eirora.test \
    EIRORA_ACCOUNT_DELETION_URL=https://eirora.test/account/delete

if [ "$failed" = 1 ]; then
    exit 1
fi
echo "The Traefik labels of both compose files reach Traefik filled in, complete and per project."
