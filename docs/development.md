# Utveckling

Hur repot är uppbyggt, hur man bygger och testar, och hur CI fungerar.
Arkitekturen bakom står i [ADR-0001](./adr/0001-local-world-server-for-shared-state.md), [ADR-0002](./adr/0002-unity-csharp-shared-simulation.md) och [ADR-0004](./adr/0004-shared-libraries-as-source-packages.md).

## Struktur

| Sökväg | Innehåll |
|---|---|
| `src/Eirora.Simulation/` | Motoroberoende, deterministisk simuleringskärna (netstandard2.1, C# 9). Också Unity-paketet `com.eirora.simulation`. |
| `src/Eirora.Contracts/` | Kontrakt mellan klient och server (netstandard2.1, C# 9). Också Unity-paketet `com.eirora.contracts`. |
| `src/Eirora.WorldGeneration/` | Kartgeneratorerna och deras slumpgenerator (netstandard2.1, C# 9, [ADR-0010](./adr/0010-world-map-format-and-generator-version.md)). Också Unity-paketet `com.eirora.worldgeneration`. |
| `src/Eirora.Server/` | ASP.NET Core-backend (.NET 10) som äger det delade enligt ADR-0001. |
| `tests/Eirora.Simulation.Tests/` | Headless tester av simuleringen och kartgeneratorerna, inklusive arkitekturtestet som håller dem rena. |
| `tests/Eirora.Server.Tests/` | Integrationstester av servern. |
| `benchmarks/` | Prestandamätningar med BenchmarkDotNet, se [Bygga och testa](#bygga-och-testa). |
| `client/` | Unity-klienten (Unity 6.3 LTS). Kod under `client/Assets/Eirora/`. |
| `tools/` | Verktyg för utvecklare: `Eirora.MapCheck` validerar kartor (byggs i CI), och skript som [mätningar på enhet](./design/device-measurements.md), som inte ingår i CI. |
| `spikes/` | Fristående spikar som besvarar en fråga och sedan kastas, till exempel [Health Connect](../spikes/health-connect/README.md). Ingår inte i CI. |
| `Eirora.slnx` | Lösningen med alla .NET-projekt. Unity-projektet öppnas separat. |

Simuleringen, kartgeneratorerna och kontrakten byggs från samma källkod av både .NET och Unity.
Därför gäller C# 9 och netstandard2.1 där, och inga Unity-API:er; `dotnet build` och arkitekturtestet underkänner det som inte håller.

## Förutsättningar

- **.NET SDK 10** (versionen styrs av `global.json`).
- **Unity 6000.3.26f1** med modulerna Android Build Support (inklusive SDK, NDK och OpenJDK) och iOS Build Support.
  Versionen styrs av `client/ProjectSettings/ProjectVersion.txt`.

## Bygga och testa

```bash
dotnet build Eirora.slnx
```

```bash
dotnet test --solution Eirora.slnx
```

```bash
dotnet format Eirora.slnx --verify-no-changes
```

Varningar är fel, och `dotnet restore` underkänner paket med kända sårbarheter (NuGetAudit).
Byggutdata hamnar i `artifacts/`.

Prestandan i simuleringen mäts med BenchmarkDotNet i `benchmarks/`.
CI bygger mätningarna men kör dem inte, eftersom tider på delade maskiner varierar för mycket för att underkänna ett bygge.
Kör dem i Release när koden de mäter ändras:

```bash
dotnet run -c Release --project benchmarks/Eirora.Simulation.Benchmarks -- --filter '*'
```

| Mätning | Budget på en utvecklardator | Senast uppmätt |
|---|---|---|
| Sökväg över hela kartan (radie 24), värsta fallet | Högst 1 ms och inga allokeringar | 0,41 ms, 0 byte (i7-7700K, .NET 10) |

Budgetarna på telefonen sätts i TASK-024.

Unity-testerna (EditMode) körs headless:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -nographics -projectPath client -runTests -testPlatform EditMode -testResults ../artifacts/unity-editmode.xml
```

Projektinställningarna (URP, linjär färgrymd, stående läge, Input System) sätts i kod och kan appliceras igen:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath client -executeMethod Eirora.Editor.ProjectSetup.Apply
```

En APK till en Android-telefon byggs lokalt med:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath client -buildTarget Android -executeMethod Eirora.Editor.Builds.Android
```

Bygget får version enligt [Versioner](#versioner) och signeras med prototypens nyckel när den finns i miljön, se [Signering](#signering).
`Eirora.Editor.Builds.AndroidBundle` bygger i stället den `.aab` som CI laddar upp till Google Play, se [Google Play](#google-play).

Klienten läser Health Connect med Kotlin-modulen `client/Assets/Eirora/Plugins/Android/HealthConnect.androidlib` ([ADR-0007](./adr/0007-own-health-connect-adapter.md)).
AGP 9 hoppar tyst över Kotlin-koden om inte `client/Assets/Plugins/Android/gradleTemplate.properties` slår på inbyggd Kotlin, så varje Android-bygge kontrolleras efteråt (`AndroidHealthCheck`): bryggklassen måste finnas i APK:n eller app-paketet och appen får bara läsa steg, träningspass och sömn.

Gränssnittet och dygnsljuset granskas utan telefon med skärmbildsturen.
Den bygger en Windows-spelare, spelar upp en skriptad vecka på en fast klocka och sparar bilder av de viktigaste vyerna:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath client -buildTarget Win64 -executeMethod Eirora.Editor.Builds.Windows
```

```bash
client/Builds/Windows/Eirora.exe -screen-width 540 -screen-height 1170 -screen-fullscreen 0 -eirora-screenshots artifacts/screenshots
```

Byggnader och natur ritas från Look-assets i `client/Assets/Eirora/Resources/Looks/` ([ADR-0009](./adr/0009-looks-are-a-modular-kit-as-data.md)).
De granskas i galleriet **Eirora → Look Gallery**, och varje steg kan sparas som PNG i `artifacts/looks/` (renderar, så inte med `-nographics`):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -quit -projectPath client -executeMethod Eirora.Editor.LookGallery.Export
```

Hur en designer arbetar med dem står i [looks.md](./design/looks.md).

Prestanda och batteri mäts på telefonen med mätscenen (`-eirora-measure day|night`) och skriptet i `tools/device-measurements/`, enligt [mätningar på enhet](./design/device-measurements.md).

Klienten sparar världen i `Application.persistentDataPath/world.save` ([ADR-0006](./adr/0006-saves-are-migrated-snapshots.md)): segmentets start-snapshot, journalen sedan dess och den senaste snapshoten, med en hash över allt, rubriken med formatversionen inräknad.
Varje sparning skrivs bredvid den gamla och byts in, och den gamla blir `world.save.bak`.
Är sparningen skadad eller saknas laddas reservfilen, eftersom bytet på telefonen är två namnbyten och kan avbrytas mellan dem; en skadad sparning läggs undan som `.damaged`.
En sparning från en nyare version eller en som inte går att läsa skrivs aldrig över.
En journalfil från prototypen i M1 (`world.journal`) tas över en gång och döps om till `world.journal.taken-over` först när sparningen är skriven, så att ett avbrutet övertagande görs om.

### Migrationer och gyllene sparningar

När `RulesVersion` höjs läggs en `StateMigration` från den förra versionen till i `Migrations.All`; den gör om tillståndstexten till nästa versions, och ett test underkänner en kedja med luckor.
Gyllene sparningar ligger i `tests/Eirora.Simulation.Tests/Saving/Golden/`, en per regel- och formatversion, med det fingeravtryck som laddning med aktuell kod ska ge.
Saknas den aktuella versionens gyllene sparning skapar testerna den och underkänner körningen, så att den granskas och checkas in; förväntade fingeravtryck ändras bara när regler eller migrationer ändras.

### Kartor och gyllene kartor

En världs karta skapas en gång och sparas hel i sparningen ([ADR-0010](./adr/0010-world-map-format-and-generator-version.md)).
Generatorerna ligger i `src/Eirora.WorldGeneration`, och `WorldGenerators.All` listar varje version.
Gyllene kartor ligger i `tests/Eirora.Simulation.Tests/WorldGeneration/Golden/`: `maps.txt` har hashen av kartan som varje generatorversion gör av några fasta seeds, och `prototype-home-1.map` är prototypens handgjorda karta i sin helhet.
En låst generator ändras aldrig, så en hash som ändras underkänner bygget; en ny generatorversion får sina gyllene kartor skrivna av testerna första gången, för granskning och incheckning.

Validatorn och dess regler beskrivs i [kartvalidering](./design/map-validation.md).
En karta valideras regel för regel, och tusentals seeds sammanfattas per regel, med:

```bash
dotnet run --project tools/Eirora.MapCheck -- --seed 42
```

```bash
dotnet run -c Release --project tools/Eirora.MapCheck -- --generator procedural-archipelago --version 1 --from 0 --seeds 10000
```

Ett kontaktark med kartor ritade ovanifrån i art directions palett, för att välja bland seeds innan en generatorversion låses:

```bash
dotnet run -c Release --project tools/Eirora.MapCheck -- --generator procedural-archipelago --version 1 --sheet maps.svg --seeds 12
```

## Servern

Servern behöver PostgreSQL 18, och kontona följer [ADR-0008](./adr/0008-backend-hosting-accounts-and-database.md).
Lokalt räcker en container:

```bash
docker run --name eirora-postgres -e POSTGRES_PASSWORD=eirora -p 5432:5432 -d postgres:18.6
```

```bash
dotnet user-secrets set ConnectionStrings:Eirora "Host=localhost;Database=eirora;Username=postgres;Password=eirora" --project src/Eirora.Server
```

Migrationerna körs som ett eget steg, aldrig av servern vid start:

```bash
dotnet run --project src/Eirora.Server -- migrate
```

```bash
dotnet run --project src/Eirora.Server
```

Lokalt signeras token med nycklar som slängs när processen avslutas.
Inloggning med Apple eller Google kräver att appens klient-id står under `SignIn:Providers:<apple|google>:Audiences` (till exempel med `dotnet user-secrets`); utan dem är leverantören avstängd.

En ändring i datamodellen blir en migration med verktyget i `dotnet-tools.json`; den genererade koden redigeras aldrig för hand:

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <Namn> --project src/Eirora.Server --output-dir Data/Migrations
```

Serverns integrationstester startar PostgreSQL med Testcontainers och kräver Docker.
På en dator utan Docker kan de köras mot en befintlig databas genom att miljövariabeln `EIRORA_TEST_POSTGRES` sätts till en anslutningssträng.
Hur servern körs på produktägarens server står i [deploy/README.md](../deploy/README.md), och hotmodellen för inloggningen i [docs/security/sign-in-threat-model.md](./security/sign-in-threat-model.md).

## CI

| Workflow | Körs vid | Gör |
|---|---|---|
| [`.NET`](../.github/workflows/dotnet.yml) | Varje push | Restore med sårbarhetskontroll, formatkontroll, bygge och alla .NET-tester, med serverns tester mot PostgreSQL i Testcontainers. Därefter byggs serverns containeravbildning; på `main` publiceras den som `ghcr.io/tiico/eirora-server:<commit>`. |
| [`Unity`](../.github/workflows/unity.yml) | Push som ändrar `client/`, `src/Eirora.Simulation/`, `src/Eirora.Contracts/`, `src/Eirora.WorldGeneration/` eller workflowen, samt manuellt | Unity-tester, Android-bygget och ett iOS-Xcode-projekt som kompileras med Xcode på macOS utan signering. På `main` blir Android-bygget ett app-paket som går till Google Plays interna test. |

Unity-workflowen körs bara när något som påverkar bygget ändras, eftersom Unity-byggen är långsamma och macOS-minuter kostar tio gånger mer i ett privat repo.
Android-bygget och Xcode-projektet laddas upp som artefakter på körningen.

### Secrets

Unity-workflowen kräver en Unity-licens och misslyckas med en tydlig instruktion om någon av dessa saknas:

| Secret | Innehåll |
|---|---|
| `UNITY_LICENSE` | Innehållet i licensfilen `Unity_lic.ulf`. |
| `UNITY_EMAIL` | E-postadressen till Unity-kontot. |
| `UNITY_PASSWORD` | Lösenordet till Unity-kontot. |

Så skapar du licensfilen och lägger in den:

1. Öppna Unity Hub, logga in med kontot CI ska använda och gå till **Preferences → Licenses**.
2. Välj **Add → Get a free personal license**, även om en licens redan visas; annars skapas ingen `.ulf`-fil.
3. Kontrollera att `C:\ProgramData\Unity\Unity_lic.ulf` finns (mappen är dold).
4. Lägg in de tre secrets med GitHub CLI (du får frågor om e-post och lösenord):

```bash
gh secret set UNITY_LICENSE < "C:/ProgramData/Unity/Unity_lic.ulf"
```

```bash
gh secret set UNITY_EMAIL
```

```bash
gh secret set UNITY_PASSWORD
```

5. Kör Unity-workflowen manuellt under **Actions → Unity → Run workflow** och kontrollera att alla jobb blir gröna.

> Unity har i omgångar begränsat manuell aktivering av Personal-licenser i CI.
> Om aktiveringen misslyckas trots korrekta secrets krävs Unity Pro eller Plus och `UNITY_SERIAL`, se [GameCI:s aktiveringsguide](https://game.ci/docs/github/activation).

Android-bygget kräver dessutom prototypens signeringsnyckel, se [Signering](#signering).

### Signering

En ny version kan bara installeras över den gamla om båda är signerade med samma nyckel.
Annars måste appen avinstalleras först, och då raderas världen.
Därför signeras både CI-byggen och lokala byggen med samma nyckel, och Android-jobbet i CI misslyckas med en instruktion om nyckeln saknas.
För appen från Google Play är nyckeln en uppladdningsnyckel: Google signerar om varje version med appens egen signeringsnyckel, så en APK från CI eller datorn kan inte installeras över appen från Google Play (D-76).

Skapa nyckeln och lägg in den som secrets med guiden, som också säger hur den säkerhetskopieras:

```bash
scripts/create-android-keystore.sh
```

Guiden sparar nyckeln och lösenordet i `~/eirora-keys/`, aldrig i repot, och sätter `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME` och `ANDROID_KEYALIAS_PASS`.
Ett lokalt bygge använder nyckeln när värdena finns i miljön:

```bash
set -a; . ~/eirora-keys/android-signing.env; set +a
```

Utan dem signeras ett lokalt bygge med datorns debugnyckel och kan inte installeras över ett bygge från CI.

### Versioner

Versionen är `0.0.<antal commits>` och Android-versionskoden är antalet commits, både i CI och lokalt (`BuildVersion`), så varje senare bygge installeras över det förra.
Appen visar version och regelversion i rapporten och på startkorten, så att dagboken kan hänvisa till byggen.
Klienten sparar världen i `Application.persistentDataPath`, som Android behåller vid uppdatering.
En sparning från äldre regler migreras; en från en nyare version laddas inte och skrivs aldrig över, och appen säger det och låter spelaren börja en ny värld med den gamla undanlagd (ADR-0006).

### Google Play

Telefonen får nya versioner genom Google Plays interna test (D-75).
Varje push till `main` som ändrar bygget blir ett app-paket som Unity-workflowen signerar med uppladdningsnyckeln och släpper i det interna testet.
Play Store uppdaterar sedan appen som vilken app som helst, utan kabel och utan avinstallation, och världen finns kvar.
Versionen i appen är samma som versionens namn i Play Console och i artefakten `eirora-android-<version>` på körningen.

Det här gör produktägaren en gång; inga nycklar eller lösenord lämnas till en agent.

1. Skapa nyckeln med guiden under [Signering](#signering) om den inte redan finns.
2. Skapa appen **Eirora** i [Play Console](https://play.google.com/console) och låt Google skapa och förvara appens signeringsnyckel (Play App Signing, D-76).
   Nyckeln från guiden blir uppladdningsnyckel och kan bytas ut via Play Console om den försvinner.
3. Fyll i **Appinnehåll**: integritetspolicy, dataskydd och deklarationen för Health Connect-behörigheterna.
   Appen läser steg, träningspass och sömn när spelaren öppnar den, för spelets krafter, och skickar inga hälsodata till någon server (HLT-10).
   Google släpper ingen version med Health Connect-behörigheter förrän deklarationen är godkänd.
4. Lägg till ditt Google-konto som testare under **Testa → Internt test → Testare**, öppna länken för att gå med på telefonen och godkänn.
5. Skapa ett tjänstkonto i Google Cloud med en JSON-nyckel och bjud in dess e-postadress under **Användare och behörigheter** i Play Console, med behörighet att släppa till testspår för Eirora.
   Lägg in nyckeln och radera sedan JSON-filen från datorn:

   ```bash
   gh secret set PLAY_SERVICE_ACCOUNT_JSON < play-service-account.json
   ```

6. Kör Unity-workflowen på `main` under **Actions → Unity → Run workflow**.
   Google tar inte emot en färdig version via API:t förrän appens första version är släppt, så uppladdningen misslyckas den här gången.
   Ladda ner artefakten från körningen, ladda upp `.aab`-filen under **Internt test → Skapa ny version** och släpp den.
   Från och med nästa push till `main` sköter workflowen uppladdningen.
7. Avinstallera den sidladdade prototypen från telefonen en gång och installera Eirora från Play Store (D-77).
   Byggena hittills har andra signaturer än appen från Google Play, så Android vägrar installera den ena över den andra, och världen från de tidigare byggena försvinner med avinstallationen.

Android säkerhetskopierar appens data, sparningen med, till spelarens eget Google-konto som vilken app som helst, så världen kan följa med till en ny telefon (D-78).

| Fel | Orsak |
|---|---|
| `Google Play secret missing` | `PLAY_SERVICE_ACCOUNT_JSON` saknas; jobbet stoppar innan något byggs. |
| `Only releases with status draft may be created on draft app` | Appens första version är inte släppt för hand än, se steg 6. |
| `Version code N has already been used` | Samma commit byggdes två gånger; nästa commit på `main` får ett högre nummer. |
| Play Store erbjuder ingen uppdatering | Nya versioner i internt test syns oftast inom några minuter; sök igen under **Hantera appar och enheter**. |
| Play Store kan inte installera appen | En APK från CI eller datorn är installerad; se steg 7. |
