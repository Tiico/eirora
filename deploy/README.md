# Driftsättning på produktägarens server

Backend körs med [compose.yaml](./compose.yaml) på produktägarens Ubuntu-server bakom den Traefik som redan finns där, till och med betan ([ADR-0008](../docs/adr/0008-backend-hosting-accounts-and-database.md)).
Samma fil kör staging som ett eget compose-projekt med egen databas, egna nycklar och eget värdnamn.

## Förutsättningar på servern

- Docker Engine med Compose och ett externt Docker-nätverk som Traefik lyssnar på (standard `traefik`).
- Traefik med en entrypoint för HTTPS (standard `websecure`) och en certresolver för Let's Encrypt (standard `letsencrypt`).
- Krypterad disk för Dockers volymer, automatiska säkerhetsuppdateringar, brandvägg som bara släpper in 80 och 443 och SSH med nyckel.
- Inloggning mot GitHub Container Registry med en personlig token som bara har `read:packages`:

```bash
docker login ghcr.io --username Tiico
```

## Första gången

1. Kopiera `deploy/` till servern, till exempel `/srv/eirora/`, och skapa `.env` från [.env.example](./.env.example).
2. Lägg Sign in with Apple-nyckeln från Apple Developer (Certificates, Identifiers & Profiles → Keys) som `secrets/apple_private_key.p8` och dess Key ID i `.env` (katalogen `secrets/` ignoreras av git).
3. Skapa de övriga hemligheterna och ge dem till containrarna som läser dem:

```bash
sudo ./create-secrets.sh
```

Compose monterar filhemligheter som de är på värden; `uid`, `gid` och `mode` i `compose.yaml` gäller bara i Swarm.
Servern och `migrate` körs som avbildningens icke-root-användare `app` (UID 1654), så skriptet låter den äga sina filer med läsrätt bara för ägaren; lösenordet till PostgreSQL läses som root och ägs av root.
Skriptet behåller hemligheter som redan finns, så det kan köras igen för att laga ägare och rättigheter.
CI kör de här stegen och `compose.yaml` mot varje ny avbildning ([smoke-test.sh](./smoke-test.sh)).

4. Starta:

```bash
docker compose --env-file .env up -d
```

`migrate` kör databasens migrationer och avslutas; `server` startar först när den har lyckats.

## Uppdatera

1. Sätt `EIRORA_VERSION` i `.env` till commitens sha som CI publicerade från `main`.
2. Kör samma kommando igen:

```bash
docker compose --env-file .env up -d
```

En migration kräver aldrig att den föregående versionen stoppas, så en uppdatering kan rullas tillbaka genom att sätta tillbaka den förra taggen.

## Staging

Staging har en egen `.env` med `EIRORA_PROJECT=eirora-staging`, ett eget värdnamn och en egen `secrets/`-katalog, i en egen katalog på servern.
Staging får aldrig riktiga spelares data, inte heller från en backup.

## Nycklar och vad som händer om de förloras

| Hemlighet | Om den förloras eller byts |
|---|---|
| `token_signing_key.pem`, `token_encryption_key` | Alla loggas ut och loggar in igen nästa gång de synkar; inget går förlorat. |
| `apple_private_key.p8` | En ny nyckel skapas hos Apple; tills dess kan appens åtkomst inte återkallas vid radering. |
| `postgres_password`, `connection_string` | Byts i PostgreSQL och i båda filerna. |

En hemlighet som byts eller läggs dit för hand får sin ägare och sina rättigheter med `sudo ./create-secrets.sh`.

Backupen med pgBackRest och dess krypteringsnyckel, som också ska förvaras i produktägarens lösenordshanterare, läggs till i ett eget ärende.
