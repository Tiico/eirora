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

## Innan policyn publiceras

Appen frågar om uttryckligt samtycke (artikel 9.2 a) innan den laddar upp bidragsdelen första gången, och samtycket kan tas tillbaka under Konto (TASK-060, #154).
Policyn beskriver det steget.
Att samtycke är rätt rättslig grund för bidragsdelen ska ändå bekräftas av produktägaren, med stöd av någon med juridisk kompetens, innan `public/privacy/` kopieras till servern första gången.

## Ändra policyn

Policyn ska alltid beskriva den version som ligger i Google Play.
En version som börjar använda uppgifter på ett nytt sätt, till exempel konton eller molnsparning, släpps inte förrän policyn är uppdaterad här och i Play Consoles dataskyddsavsnitt.
Kopiera sedan `public/` till servern igen; nginx läser filerna direkt och behöver inte startas om.
Om `default.conf.template` eller `.env` ändras startas sidan om med samma kommando som ovan.

Workflowen [Site](../../.github/workflows/site.yml) startar sidan med samma compose-fil och kontrollerar att platshållarna fylls i och att säkerhetshuvudena finns kvar.
