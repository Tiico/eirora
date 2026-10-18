#!/usr/bin/env bash
# Creates the secrets compose.yaml reads in deploy/secrets/ and hands them to the containers that read them
# (deploy/README.md). Existing secrets are kept, so running it again only repairs owners and modes.
# Usage: sudo ./create-secrets.sh, after placing Apple's key as secrets/apple_private_key.p8.
set -euo pipefail

# Compose mounts file secrets as they are on the host: uid, gid and mode in compose.yaml only apply in Swarm. The server
# image (noble-chiseled) runs as the non-root user app, so the files the server and migrate read must belong to it.
readonly app_uid=1654

if [ "$(id -u)" -ne 0 ]; then
    echo "Run as root (sudo), so that the secrets can be handed to the server's user $app_uid." >&2
    exit 1
fi

cd "$(dirname "$0")"
mkdir -p secrets
chmod 700 secrets

if [ ! -f secrets/apple_private_key.p8 ]; then
    echo "Place the Sign in with Apple key as secrets/apple_private_key.p8 first (deploy/README.md)." >&2
    exit 1
fi

# The new files are only readable by root until they are handed over below.
umask 077

if [ ! -f secrets/postgres_password ]; then
    openssl rand -base64 32 | tr -d '\n' > secrets/postgres_password
fi

if [ ! -f secrets/connection_string ]; then
    printf 'Host=postgres;Database=eirora;Username=eirora;Password=%s' "$(cat secrets/postgres_password)" > secrets/connection_string
fi

if [ ! -f secrets/token_signing_key.pem ]; then
    openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out secrets/token_signing_key.pem
fi

if [ ! -f secrets/token_encryption_key ]; then
    openssl rand -base64 32 | tr -d '\n' > secrets/token_encryption_key
fi

# PostgreSQL's entrypoint reads its password as root before it drops to the postgres user, so root may keep it.
chown root:root secrets/postgres_password
chown "$app_uid:$app_uid" secrets/connection_string secrets/apple_private_key.p8 secrets/token_signing_key.pem secrets/token_encryption_key
chmod 400 secrets/*
echo "Secrets ready in $(pwd)/secrets."
