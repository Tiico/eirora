# Utveckling

Hur repot är uppbyggt, hur man bygger och testar, och hur CI fungerar.
Arkitekturen bakom står i [ADR-0001](./adr/0001-local-world-server-for-shared-state.md), [ADR-0002](./adr/0002-unity-csharp-shared-simulation.md) och [ADR-0004](./adr/0004-shared-libraries-as-source-packages.md).

## Struktur

| Sökväg | Innehåll |
|---|---|
| `src/Eirora.Simulation/` | Motoroberoende, deterministisk simuleringskärna (netstandard2.1, C# 9). Också Unity-paketet `com.eirora.simulation`. |
| `src/Eirora.Contracts/` | Kontrakt mellan klient och server (netstandard2.1, C# 9). Också Unity-paketet `com.eirora.contracts`. |
| `src/Eirora.Server/` | ASP.NET Core-backend (.NET 10) som äger det delade enligt ADR-0001. |
| `tests/Eirora.Simulation.Tests/` | Headless tester av simuleringen, inklusive arkitekturtestet som håller den ren. |
| `tests/Eirora.Server.Tests/` | Integrationstester av servern. |
| `client/` | Unity-klienten (Unity 6.3 LTS). Kod under `client/Assets/Eirora/`. |
| `spikes/` | Fristående spikar som besvarar en fråga och sedan kastas, till exempel [Health Connect](../spikes/health-connect/README.md). Ingår inte i CI. |
| `Eirora.slnx` | Lösningen med alla .NET-projekt. Unity-projektet öppnas separat. |

Simuleringen och kontrakten byggs från samma källkod av både .NET och Unity.
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

Klienten läser Health Connect med Kotlin-modulen `client/Assets/Eirora/Plugins/Android/HealthConnect.androidlib` ([ADR-0007](./adr/0007-own-health-connect-adapter.md)).
AGP 9 hoppar tyst över Kotlin-koden om inte `client/Assets/Plugins/Android/gradleTemplate.properties` slår på inbyggd Kotlin, så varje Android-bygge kontrolleras efteråt (`AndroidHealthCheck`): bryggklassen måste finnas i APK:n och appen får bara läsa steg, träningspass och sömn.

Gränssnittet och dygnsljuset granskas utan telefon med skärmbildsturen.
Den bygger en Windows-spelare, spelar upp en skriptad vecka på en fast klocka och sparar bilder av de viktigaste vyerna:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath client -buildTarget Win64 -executeMethod Eirora.Editor.Builds.Windows
```

```bash
client/Builds/Windows/Eirora.exe -screen-width 540 -screen-height 1170 -screen-fullscreen 0 -eirora-screenshots artifacts/screenshots
```

Klienten sparar världen i `Application.persistentDataPath/world.save` ([ADR-0006](./adr/0006-saves-are-migrated-snapshots.md)): segmentets start-snapshot, journalen sedan dess och den senaste snapshoten, med en hash över allt.
Varje sparning skrivs bredvid den gamla och byts in, och den gamla blir `world.save.bak`; är sparningen skadad laddas reservfilen och den skadade läggs undan som `.damaged`.
En sparning från en nyare version eller en som inte går att läsa skrivs aldrig över.
En journalfil från prototypen i M1 (`world.journal`) tas över en gång och behålls som `world.journal.taken-over`.

### Migrationer och gyllene sparningar

När `RulesVersion` höjs läggs en `StateMigration` från den förra versionen till i `Migrations.All`; den gör om tillståndstexten till nästa versions, och ett test underkänner en kedja med luckor.
Gyllene sparningar ligger i `tests/Eirora.Simulation.Tests/Saving/Golden/`, en per regel- och formatversion, med det fingeravtryck som laddning med aktuell kod ska ge.
Saknas den aktuella versionens gyllene sparning skapar testerna den och underkänner körningen, så att den granskas och checkas in; förväntade fingeravtryck ändras bara när regler eller migrationer ändras.

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
| [`Unity`](../.github/workflows/unity.yml) | Push som ändrar `client/`, `src/Eirora.Simulation/`, `src/Eirora.Contracts/` eller workflowen, samt manuellt | Unity-tester, Android-APK och ett iOS-Xcode-projekt som kompileras med Xcode på macOS utan signering. |

Unity-workflowen körs bara när något som påverkar bygget ändras, eftersom Unity-byggen är långsamma och macOS-minuter kostar tio gånger mer i ett privat repo.
APK:n och Xcode-projektet laddas upp som artefakter på körningen.

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
Klienten sparar journalen i `Application.persistentDataPath`, som Android behåller vid uppdatering.
En journal från en annan regelversion laddas inte och skrivs aldrig över; appen säger det och låter spelaren börja en ny värld med den gamla undanlagd (ADR-0006).
