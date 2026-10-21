# De publika sidorna

Integritetspolicyn som Google Play och Health Connect länkar till ligger på `https://<värdnamn>/privacy/` (ADR-0007).
Sidorna körs med [compose.yaml](./compose.yaml) som ett eget compose-projekt bakom samma Traefik som backenden, så att policyn finns kvar även när backenden ligger nere eller inte är driftsatt.
Utvecklarnamnet, kontaktadressen och adressen till serverns raderingssida står bara i `.env` på servern och fylls i när sidan skickas.

## Första gången

1. Kopiera `deploy/site/` till servern, till exempel `/srv/eirora-site/`, och skapa `.env` från [.env.example](./.env.example).
2. Peka värdnamnet i DNS mot servern och se till att kontaktadressen tar emot e-post.
3. Starta:

```bash
docker compose --env-file .env up -d
```

4. Öppna `https://<värdnamn>/privacy/` och kontrollera att namnet, kontaktadressen och länken till raderingssidan syns.
   Samma adress anges som integritetspolicy i Play Console under **Appinnehåll** och på butikssidan.

## Ändra policyn

Policyn ska alltid beskriva den version som ligger i Google Play.
En version som börjar använda uppgifter på ett nytt sätt, till exempel konton eller molnsparning, släpps inte förrän policyn är uppdaterad här och i Play Consoles dataskyddsavsnitt.
Kopiera sedan `public/` till servern igen; nginx läser filerna direkt och behöver inte startas om.
Om `default.conf.template` eller `.env` ändras startas sidan om med samma kommando som ovan.

Workflowen [Site](../../.github/workflows/site.yml) startar sidan med samma compose-fil och kontrollerar att platshållarna fylls i och att säkerhetshuvudena finns kvar.
