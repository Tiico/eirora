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
| `tools/` | Skript för utvecklare, till exempel [mätningar på enhet](./design/device-measurements.md). Ingår inte i CI. |
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

Prestanda och batteri mäts på telefonen med mätscenen (`-eirora-measure day|night`) och skriptet i `tools/device-measurements/`, enligt [mätningar på enhet](./design/device-measurements.md).

Klienten sparar världen som en journal i `Application.persistentDataPath/world.journal` och spelar upp den vid start ([ADR-0005](./adr/0005-time-arrives-as-readings.md)).

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

## Inloggning

Spelaren loggar in med Apple eller Google under kontoknappen längst till höger i nederkanten ([ADR-0008](./adr/0008-backend-hosting-accounts-and-database.md)).
Kontot behövs aldrig för att spela, och knappen syns bara i ett bygge som har en server och minst en leverantör.

| Plattform | Apple | Google |
|---|---|---|
| iOS | Inbyggd Sign in with Apple (`EiroraSignIn.mm`) | Googles OAuth-flöde med PKCE i `ASWebAuthenticationSession` och appens iOS-klient |
| Android | Webbflödet med ett Services ID i en Custom Tab; Apple postar svaret till serverns `/signin/apple/callback`, som lämnar det till appen | Credential Manager med "Sign in with Google" och appens webbklient |

Allt ligger bakom `ILoginProvider` i `client/Assets/Eirora/Runtime/Accounts/`, och `AccountSession` sköter token, förnyelse, koppling, utloggning och radering.
Refresh-token ligger i iOS nyckelring eller krypterad med en nyckel i Android Keystore (`SignIn.androidlib`), aldrig i en vanlig fil.
Webbsidan för radering som Google Play kräver finns på `/account/delete` på servern.

### Identifierare hos Apple och Google

Produktägaren skapar dessa en gång; ingen av dem är hemlig utom Apples nyckel.
`<värd>` är serverns publika värdnamn (`EIRORA_HOST`).

1. **Apple Developer → Identifiers:** slå på Sign in with Apple för App ID `com.eirora.game`.
2. **Apple Developer → Identifiers → Services IDs:** skapa ett Services ID (till exempel `com.eirora.game.signin`), slå på Sign in with Apple och ange domänen `<värd>` med returadresserna `https://<värd>/signin/apple/callback` och `https://<värd>/account/delete`.
3. **Apple Developer → Keys:** skapa en nyckel med Sign in with Apple, se [deploy/README.md](../deploy/README.md).
4. **Google Cloud → Google Auth Platform:** skapa appens varumärkesprofil med bara den icke-känsliga omfattningen `openid`.
5. **Google Cloud → Clients:** skapa tre OAuth-klienter.
   - **Web:** med `https://<värd>` som tillåtet JavaScript-ursprung, för raderingssidan; Android begär ID-token till den här klienten.
   - **Android:** paketnamnet `com.eirora.game` och SHA-1 för Play-appsigneringens certifikat (Play Console → Testa och släpp → Appintegritet), och en till för uppladdningsnyckeln så att byggen från CI och datorn kan logga in.
   - **iOS:** bundle-id `com.eirora.game`.
6. Fyll i `client/Assets/Eirora/Resources/Accounts/AccountConfig.json` och committa:

   ```json
   {
     "server": "https://<värd>",
     "appleServicesId": "<Services ID>",
     "googleWebClientId": "<webbklientens id>",
     "googleIosClientId": "<iOS-klientens id>"
   }
   ```

7. Fyll i samma värden och App ID:t i serverns `.env` enligt [deploy/.env.example](../deploy/.env.example).

Ett tomt värde stänger av sin del: utan `server` visas inget konto, utan `appleServicesId` saknas Apple på Android och utan `googleIosClientId` saknas Google på iOS.

### Kontrollera på riktig enhet

EditMode-testerna kör hela klientflödet mot en falsk server (`FakeAccountServer`), men leverantörernas egna dialoger och nyckellagren finns bara på en telefon.
Kontrollera detta på både Android och iOS innan en version med inloggning släpps:

1. Första inloggningen med varje leverantör skapar kontot, och nästa inloggning med samma konto når samma konto.
2. Apples dialog på iOS frågar inte efter namn eller e-post.
3. Apple på Android: Custom Tab öppnas, svaret kommer tillbaka till spelet, och att stänga fliken utan att logga in avbryter utan felmeddelande.
4. Google på Android visar kontoväljaren, och avbrott i den avbryter utan felmeddelande.
5. Efter omstart av appen är spelaren fortfarande inloggad, och efter 15 minuter förnyas token tyst.
6. Efter en avinstallation och ominstallation, eller en återställning till en ny telefon, är spelaren utloggad men världen finns kvar.
7. Koppla den andra leverantören, logga ut och logga in med den; samma konto nås.
8. Radera kontot från inställningarna: ett konto med Apple-inloggning bekräftas med Apple, och appen visas inte längre under Apple-ID → Logga in med Apple.
9. Allt ovan fungerar under viloperioden (RST-07), och i flygplansläge säger appen att servern inte går att nå utan att spelet påverkas.
10. Raderingssidan på `https://<värd>/account/delete` raderar ett konto med Google och med Apple från en dators webbläsare.

## CI

| Workflow | Körs vid | Gör |
|---|---|---|
| [`.NET`](../.github/workflows/dotnet.yml) | Varje push | Restore med sårbarhetskontroll, formatkontroll, bygge och alla .NET-tester, med serverns tester mot PostgreSQL i Testcontainers. Därefter byggs serverns containeravbildning; på `main` publiceras den som `ghcr.io/tiico/eirora-server:<commit>`. |
| [`Unity`](../.github/workflows/unity.yml) | Push som ändrar `client/`, `src/Eirora.Simulation/`, `src/Eirora.Contracts/` eller workflowen, samt manuellt | Unity-tester, Android-bygget och ett iOS-Xcode-projekt som kompileras med Xcode på macOS utan signering. På `main` blir Android-bygget ett app-paket som går till Google Plays interna test. |

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
Klienten sparar journalen i `Application.persistentDataPath`, som Android behåller vid uppdatering.
En journal från en annan regelversion laddas inte och skrivs aldrig över; appen säger det och låter spelaren börja en ny värld med den gamla undanlagd (ADR-0006).

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

Android säkerhetskopierar appens data, journalen med, till spelarens eget Google-konto som vilken app som helst, så världen kan följa med till en ny telefon (D-78).

| Fel | Orsak |
|---|---|
| `Google Play secret missing` | `PLAY_SERVICE_ACCOUNT_JSON` saknas; jobbet stoppar innan något byggs. |
| `Only releases with status draft may be created on draft app` | Appens första version är inte släppt för hand än, se steg 6. |
| `Version code N has already been used` | Samma commit byggdes två gånger; nästa commit på `main` får ett högre nummer. |
| Play Store erbjuder ingen uppdatering | Nya versioner i internt test syns oftast inom några minuter; sök igen under **Hantera appar och enheter**. |
| Play Store kan inte installera appen | En APK från CI eller datorn är installerad; se steg 7. |
