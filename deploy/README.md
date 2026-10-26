# Driftsättning på produktägarens server

Backend körs med [compose.yaml](./compose.yaml) på produktägarens Ubuntu-server bakom den Traefik som redan finns där, till och med betan ([ADR-0008](../docs/adr/0008-backend-hosting-accounts-and-database.md)).
Samma fil kör staging som ett eget compose-projekt med egen databas, egna nycklar och eget värdnamn.

## Förutsättningar på servern

- Docker Engine med Compose och ett externt Docker-nätverk som Traefik lyssnar på (standard `traefik`).
- Traefik med en entrypoint för HTTPS (standard `websecure`) och en certresolver för Let's Encrypt (standard `letsencrypt`).
- Krypterad disk för Dockers volymer, automatiska säkerhetsuppdateringar, brandvägg som bara släpper in 80 och 443 och SSH med nyckel.
  Under prototypen gäller ett undantag för krypteringen, se [Undantag: okrypterad disk under prototypen](#undantag-okrypterad-disk-under-prototypen).
- Inloggning mot GitHub Container Registry med en personlig token som bara har `read:packages`:

```bash
docker login ghcr.io --username Tiico
```

## Första gången

1. Kopiera `deploy/` till servern, till exempel `/srv/eirora/`, och skapa `.env` från [.env.example](./.env.example).
2. Skapa katalogen för hemligheterna, stängd för alla utom dig, innan något läggs i den (`secrets/` ignoreras av git):

```bash
install -d -m 700 secrets
```

3. Välj inloggning, se [Google ensam eller Google och Apple](#google-ensam-eller-google-och-apple).
   Med Apple: lägg Sign in with Apple-nyckeln från Apple Developer (Certificates, Identifiers & Profiles → Keys) som `secrets/apple_private_key.p8` och fyll i alla `APPLE_*` i `.env`.
   Med bara Google: hoppa över steget.
4. Skapa de övriga hemligheterna och ge dem till containrarna som läser dem:

```bash
sudo ./create-secrets.sh
```

Utanför Swarm monterar Compose filhemligheter med samma ägare och rättigheter som filen har på värden, och de går inte att sätta i `compose.yaml`.
Servern och `migrate` körs som avbildningens icke-root-användare `app` (UID 1654), så skriptet låter den äga sina filer med läsrätt bara för ägaren; lösenordet till PostgreSQL läses som root och ägs av root.
En användare på värden med UID 1654 kan därför läsa de filerna om den kommer in i `secrets/`; ge ingen användare på värden det UID:t och låt katalogen vara stängd.
Skriptet behåller hemligheter som redan finns, så det kan köras igen för att laga ägare och rättigheter.
Utan Apples nyckel lägger skriptet en tom `secrets/apple_private_key.p8` som platshållare, eftersom Compose inte kan hoppa över en filhemlighet; servern läser en tom nyckel som ingen nyckel.
CI kör de här stegen och `compose.yaml` mot varje ny avbildning, både med bara Google och med Google och Apple ([smoke-test.sh](./smoke-test.sh)).
Där läser en Traefik serverns etiketter som på servern, och testet kräver att båda routrarna är påslagna, att inloggningsroutern har sin hastighetsbegränsning och att den svarar 429 efter sin skur.

5. Starta:

```bash
docker compose --env-file .env up -d
```

`migrate` kör databasens migrationer och avslutas; `server` startar först när den har lyckats.

## Google ensam eller Google och Apple

Servern kan köras med bara Google, utan någon Apple-konfiguration alls, tills produktägaren har ett Apple Developer-konto.
Android-prototypen loggar bara in med Google.

| Inloggning | `.env` | `secrets/apple_private_key.p8` |
|---|---|---|
| Bara Google | `GOOGLE_SERVER_CLIENT_ID` och `GOOGLE_IOS_CLIENT_ID` ifyllda; `APPLE_BUNDLE_ID`, `APPLE_SERVICES_ID`, `APPLE_TEAM_ID` och `APPLE_KEY_ID` tomma eller borttagna | Tom, skapas av `create-secrets.sh` |
| Google och Apple | Som ovan och dessutom alla fyra `APPLE_*` ifyllda | Nyckeln från Apple Developer |

Med bara Google tar servern inte emot inloggningar med Apple, Apples webbflöde på `/signin/apple/callback` svarar 404, raderingssidan visar bara Google och en radering försöker aldrig återkalla något hos Apple.
Ett konto som fått en Apple-inloggning medan Apple var påslaget raderas då med en av kontots andra inloggningar.

En delvis ifylld Apple-konfiguration stoppar servern vid start med ett fel som räknar upp vad som saknas.
Det gäller också om Services ID inte är ett av Apples klient-id.

Så slås Apple på senare:

1. Lägg nyckeln över platshållaren och ge den rätt ägare:

```bash
sudo install -m 400 /sökväg/till/AuthKey_<KEY_ID>.p8 secrets/apple_private_key.p8
```

```bash
sudo ./create-secrets.sh
```

2. Fyll i alla fyra `APPLE_*` i `.env` och starta om med `docker compose --env-file .env up -d`.

## Undantag: okrypterad disk under prototypen

Produktägaren har beslutat om ett dokumenterat undantag (2026-10-26).
Under prototypen körs servern på en disk utan kryptering, trots förutsättningen ovan om krypterad disk för Dockers volymer.
Servern innehåller då bara produktägarens egna data, och bidragen är ändå krypterade från ände till ände (D-57).
Undantaget måste hävas, och disken krypteras, innan andra spelare får konton.

## Traefiks etiketter

Etiketterna i `compose.yaml` är en lista med `"nyckel=värde"`, inte en mappning.
Compose fyller bara i variabler i värden, aldrig i nycklar, så `${EIRORA_PROJECT}` i en mappningsnyckel når Traefik som bokstavlig text.
Då heter routrarna `${EIRORA_PROJECT:-eirora}`, produktion och staging krockar, och Traefik stänger av inloggningsroutern eftersom dess middleware inte finns under det ifyllda namnet, så att inloggningen går genom huvudroutern utan begränsning.
Routrar, tjänst och middleware heter efter `EIRORA_PROJECT`, så att produktion och staging har egna namn.
[check-labels.sh](./check-labels.sh) kontrollerar båda compose-filerna i CI: inga variabler kvar i etiketterna, varje middleware som en router använder finns, och två projekt delar inga namn.

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
| `apple_private_key.p8` | En ny nyckel skapas hos Apple; tills dess kan appens åtkomst inte återkallas vid radering. Med bara Google är filen en tom platshållare. |
| `postgres_password`, `connection_string` | Byts i PostgreSQL och i båda filerna. |

En hemlighet som byts eller läggs dit för hand får sin ägare och sina rättigheter med `sudo ./create-secrets.sh`.

Backupen med pgBackRest och dess krypteringsnyckel, som också ska förvaras i produktägarens lösenordshanterare, läggs till i ett eget ärende.
