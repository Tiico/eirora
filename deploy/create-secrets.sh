#!/usr/bin/env bash
# Creates the secrets compose.yaml reads in deploy/secrets/ and hands them to the containers that read them
# (deploy/README.md). Existing secrets are kept, so running it again only repairs owners and modes.
# Usage: sudo ./create-secrets.sh, after placing Apple's key as secrets/apple_private_key.p8.
set -euo pipefail

# Outside Swarm, Compose mounts file secrets with the host file's owner and mode and does not support setting uid, gid
# or mode on them. The server image (noble-chiseled) runs as the non-root user app, so the files the server and migrate
# read must belong to it.
# 1654 is the UID of app in Microsoft's .NET images; ContainerFamily in src/Eirora.Server/Eirora.Server.csproj picks the image.
readonly app_uid=1654

if [ "$(id -u)" -ne 0 ]; then
    echo "Run as root (sudo), so that the secrets can be handed to the server's user $app_uid." >&2
    exit 1
fi

cd "$(dirname "$0")"
mkdir -p secrets
chmod 700 secrets

if [ ! -s secrets/apple_private_key.p8 ]; then
    echo "Place the Sign in with Apple key as secrets/apple_private_key.p8 first (deploy/README.md)." >&2
    exit 1
fi

# The new files are only readable by root until they are handed over below.
umask 077
rm -f secrets/*.new

# Writes the output of a command as a secret unless it already exists. The output goes to a temporary file first, so an
# interrupted run leaves no empty secret behind that the next run would keep.
create() {
    local secret=secrets/$1
    shift
    if [ ! -s "$secret" ]; then
        "$@" > "$secret.new"
        mv "$secret.new" "$secret"
    fi
}

password() {
    openssl rand -base64 32 | tr -d '\n'
}

connection_string() {
    printf 'Host=postgres;Database=eirora;Username=eirora;Password=%s' "$(cat secrets/postgres_password)"
}

create postgres_password password
create connection_string connection_string
create token_signing_key.pem openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072
create token_encryption_key password

# PostgreSQL's entrypoint reads its password as root before it drops to the postgres user, so root may keep it.
chown root:root secrets/postgres_password
chown "$app_uid:$app_uid" secrets/connection_string secrets/apple_private_key.p8 secrets/token_signing_key.pem secrets/token_encryption_key
chmod 400 secrets/*
echo "Secrets ready in $(pwd)/secrets."
