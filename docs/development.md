# Utveckling

Hur repot är uppbyggt, hur man bygger och testar, och hur CI fungerar.
Arkitekturen bakom står i [ADR-0001](./adr/0001-local-world-server-for-shared-state.md), [ADR-0002](./adr/0002-unity-csharp-shared-simulation.md) och [ADR-0004](./adr/0004-shared-libraries-as-source-packages.md).

## Struktur

| Sökväg | Innehåll |
|---|---|
| `src/Eirora.Simulation/` | Motoroberoende, deterministisk simuleringskärna (netstandard2.1, C# 9). Också Unity-paketet `com.eirora.simulation`. |
| `src/Eirora.Contracts/` | Kontrakt mellan klient och server (netstandard2.1, C# 9). Också Unity-paketet `com.eirora.contracts`. |
| `src/Eirora.WorldGeneration/` | Kartgeneratorerna och deras slumpgenerator (netstandard2.1, C# 9, [ADR-0010](./adr/0010-world-map-format-and-generator-version.md)). Också Unity-paketet `com.eirora.worldgeneration`. |
| `src/Eirora.CloudSaving/` | Molnsparningen på enheten: bidragsnyckeln, krypteringen av bidragsdelen, avstämningen mot servern, nyckelöverföringen mellan telefoner och återställningskoden (netstandard2.1, C# 9, [ADR-0008](./adr/0008-backend-hosting-accounts-and-database.md)). Också Unity-paketet `com.eirora.cloudsaving`, som bygger mot `client/Assets/Plugins/BouncyCastle/`. |
| `src/Eirora.Server/` | ASP.NET Core-backend (.NET 10) som äger det delade enligt ADR-0001. |
| `tests/Eirora.Simulation.Tests/` | Headless tester av simuleringen och kartgeneratorerna, inklusive arkitekturtestet som håller dem rena. |
| `tests/Eirora.CloudSaving.Tests/` | Headless tester av krypteringen och avstämningen, med en minnesserver som följer serverns regler. |
| `tests/Eirora.Server.Tests/` | Integrationstester av servern, inklusive två simulerade enheter som synkar genom den riktiga servern. |
| `benchmarks/` | Prestandamätningar med BenchmarkDotNet, se [Bygga och testa](#bygga-och-testa). |
| `client/` | Unity-klienten (Unity 6.3 LTS). Kod under `client/Assets/Eirora/`. |
| `tools/` | Verktyg för utvecklare: `Eirora.MapCheck` validerar kartor (byggs i CI), och skript som [mätningar på enhet](./design/device-measurements.md), som körs för hand; i CI prövas bara `measure.sh budget` med sitt test och mot storleken på release-bygget. |
| `spikes/` | Fristående spikar som besvarar en fråga och sedan kastas, till exempel [Health Connect](../spikes/health-connect/README.md). Ingår inte i CI. |
| `Eirora.slnx` | Lösningen med alla .NET-projekt. Unity-projektet öppnas separat. |

Simuleringen, kartgeneratorerna, kontrakten och molnsparningen byggs från samma källkod av både .NET och Unity.
Därför gäller C# 9 och netstandard2.1 där, och inga Unity-API:er; `dotnet build` och arkitekturtestet underkänner det som inte håller.
Molnsparningen är det enda paketet med ett beroende utanför netstandard: BouncyCastle.Cryptography, som .NET hämtar från NuGet och Unity läser från `client/Assets/Plugins/BouncyCastle/` (samma version, se dess `README.md` och `scripts/update-bouncycastle.sh`).

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

Bygg och installera från Git Bash (Windows) med skripten:

```bash
bash scripts/build-android.sh
bash scripts/install-android.sh
```

Bygg och installera i ett steg med `bash scripts/build-android.sh --install`.
Om telefonens befintliga app är signerad med datorns debugnyckel, använd `bash scripts/build-android.sh --debug --install` så att sparningen kan behållas.
`--debug` väljer Unitys debugnyckel och hoppar över releasevärdena i filen och miljön.
Skripten kan anropas från valfri katalog med sin fullständiga sökväg.
Stäng projektet i Unity innan du bygger.
Byggskriptet hittar projektets Unity-version, sparar APK:n i `client/Builds/Android/Eirora.apk` och visar Unitys byggutdata direkt i terminalen.
Det läser signeringsvärden från `~/eirora-keys/android-signing.env` om filen finns, annars används värdena i miljön eller Unitys debugnyckel.
`EIRORA_KEY_DIR` kan ange en annan nyckelkatalog och `UNITY_EDITOR` en annan sökväg till Unity.

Installationsskriptet hittar ADB via PATH eller projektets Unity-installation och väljer den senast ändrade `Eirora.apk` i `client/Builds/Android/` eller arbetskopiorna under `.claude/worktrees/` (även `artifacts/unity/`).
Det skriver ut vald fil och enhet före installationen och föredrar en ansluten Pixel 7.
Telefonen måste ha USB-felsökning aktiverad och datorn godkänd.
Skriptet väntar upp till två minuter på återanslutning och USB-godkännande, även när Unity har startat om ADB efter bygget.
Väntetiden kan ändras med miljövariabeln `ANDROID_WAIT_SECONDS` (sekunder).
Regressionstestet för fördröjt godkännande körs med `bash scripts/test-android-install.sh`.
Ange vid behov `--apk "C:/byggen/Eirora.apk"` eller `--serial "enhetens serienummer"`, och sätt miljövariabeln `ADB` för en annan sökväg till adb.exe.
Installationen uppdaterar med `adb install -r`; skriptet avinstallerar aldrig appen och försöker inte kringgå signaturkonflikter eller versionskontroller.
PowerShell-varianten finns också kvar i `scripts/install-android.ps1`.

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

På en Mac byggs spelaren med `Eirora.Editor.Builds.MacOS` och startas med `open`, eftersom spelaren pausar när fönstret saknar fokus:

```bash
/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath client -buildTarget OSXUniversal -executeMethod Eirora.Editor.Builds.MacOS
```

```bash
open -W -n client/Builds/MacOS/Eirora.app --args -screen-width 540 -screen-height 1170 -screen-fullscreen 0 -eirora-screenshots "$PWD/artifacts/screenshots"
```

Turen slutar med en ny värld på en genererad karta, nära och på långt håll.

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

### Determinism

Simuleringen ska ge samma tillstånd på servern (.NET) och i klienten (IL2CPP på telefonen) för samma indata (INV-D1, NFR-02).
Arkitekturtestet underkänner det som kan skilja mellan körmiljöer eller enheter:

- flyttal och `System.Math`;
- väggklocka, slump, filer, nätverk, miljövariabler och reflektion (som avslöjar vilken runtime som kör);
- `string.GetHashCode` och iteration över `Dictionary` och `HashSet`, vars ordning beror på hashning och runtimens implementation;
- `Keys`, `Values` och nyckel–värde-par genom `IDictionary` och `IReadOnlyDictionary`, med `foreach`, LINQ eller en samling som byggs av dem, eftersom IL inte visar om ordboken bakom är sorterad (gå över en sorterad samling med `foreach`, eller över nycklarna i fast ordning);
- tal som formateras eller tolkas utan `CultureInfo.InvariantCulture`, också tal som läggs till med `StringBuilder.Append` eller `Insert`;
- strängjämförelser utan `StringComparison`, och strängar som sorteras utan jämförare: sorterade strängsamlingar, `OrderBy`, `OrderByDescending`, `ThenBy` och `ThenByDescending` med en strängnyckel, samt `List<string>.Sort` och `Array.Sort` för strängar.

Testet kan inte se allt.
Det här är också förbjudet men upptäcks bara av determinismkorpusen nedan, kulturtestet och granskning:

- ordning som beror på något annat än nycklarna, till exempel sortering med lika nycklar;
- en `HashSet` som gås igenom som `IEnumerable<T>` eller `ISet<T>`, till exempel med LINQ, eftersom IL inte visar vilken samling som ligger bakom;
- strängar som byggs med `$"{tal}"`, `string.Format` eller `string.Concat` med ett tal som objekt, som formateras med aktuell kultur, och sedan påverkar tillståndet; i IL syns bara att talet görs till ett objekt, vilket varje felmeddelande också gör;
- sortering efter en nyckel som jämför strängar på annat sätt, till exempel en tupel med en sträng, en egen `IComparable` eller en `Comparison<string>` som jämför med aktuell kultur.

Determinismkorpusen är det som determinismbygget spelar upp på Android och `ReplayCorpusTests` i .NET, med samma namn:

- `replay/…`: replays i `tests/Replays/`, handgjorda fall från UX-resorna, ekonomin och dimman plus slumpade sekvenser, som start och indata utan regelversion;
- `save/…`: de gyllene sparningarna, som laddas (och migreras) och sparas igen; prototypjournalen spelas inte, eftersom regelversion 4 nekar den;
- `map/…`: de gyllene kartorna, som deras generator gör om från seeden, så att även `ProceduralArchipelagoV1` prövas.

Varje replay blir en sparning som skrivs till bytes och återställs innan tillståndet hashas, som när klienten sparar världen.
Varje kommando i en handgjord replay måste godtas, utom de nekanden som fallet handlar om och listar (vilolåset i `ux-evening-with-rest-lock`, Nyön i dimman i `epoch-fog-towards-nyon`).
De slumpade sekvenserna kommer från `Scenario`, som anpassar tre av fyra kommandon efter världen och väntar till efter gryningen innan de flesta kommandon som annars skulle komma under vilan; de övriga är blinda, så att nekanden också spelas.
Testet kräver att korpusens slumpade replays godtar mer än hälften av sina kommandon och att var och en nekar något.

`ReplayCorpusTests` skriver tillståndets hash för varje post till `artifacts/determinism/dotnet.txt`, och determinismbygget (IL2CPP för ARM64 och x86_64, spelets paketnamn plus `.determinism`) gör samma sak på Android.
Servern, spelarnas telefoner och emulatorn (en-US) kör i olika kulturer, så `ReplayCorpusTests` spelar också upp korpusen under sv-SE och tr-TR och kräver samma hash som under invariant kultur.
Med ICU skriver sv-SE minustecken som U+2212, och tr-TR gör om `I` till ett prickfritt `ı`.
Jämför .NET och IL2CPP på en emulator eller telefon:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath client -buildTarget Android -executeMethod Eirora.Editor.Builds.AndroidDeterminism
```

```bash
scripts/check-determinism.sh
```

Skriptet läser paketnamnet ur APK:n med `aapt2` från Android SDK:ns build-tools (eller `AAPT2=<sökväg>`), och installerar och avinstallerar bara determinismbygget, aldrig spelet.
Om appen försvinner innan den har rapporterat underkänner skriptet direkt och skriver ut kraschloggen; hela logcat sparas i `artifacts/determinism/logcat.txt`.
Determinismbygget har en egen scen med bara `DeterminismBootstrap`, som `Builds.AndroidDeterminism` gör för bygget och tar bort efteråt; korpusen ligger i `client/Assets/Eirora/Determinism/` bara medan bygget pågår.
En telefon kör byggets ARM64-kod och en x86_64-emulator dess x86_64-kod.
Androids ARM-översättning i emulatorn kan inte köra Unitys ARM64-motor: den saknar instruktionen SEVL (`0xd50320bf`) som `libunity.so` använder, och appen dör med SIGILL innan någon scenkod körs.
Emulatorn prövar därför IL2CPP för x86_64; IL2CPP för ARM64 prövas bara på en telefon.
Bygget renderar med OpenGL ES 3, som emulatorns programvarurenderare (SwiftShader) ger.
Det har ingen version, så det behöver ingen git-historik.
Skriptet vägrar en APK som är äldre än någon fil i `src/Eirora.Simulation/`, `src/Eirora.Contracts/`, `src/Eirora.WorldGeneration/`, `client/Assets/`, `client/Packages/`, `tests/Replays/` eller de gyllene sparningarna och kartorna.
`client/ProjectSettings/` jämförs inte, eftersom bygget självt sparar den efter att APK:n skrivits; bygg om efter en ändring där.

Replays är indata, inte resultat, så de ändras inte när reglerna ändras.
Saknas en replay i korpusen skapar testet den och underkänner körningen, så att den granskas och checkas in.
När journalens format för start eller indata ändras går replays inte längre att läsa.
Ta då bort `tests/Replays/*.replay` och kör `ReplayCorpusTests`, som skriver om hela korpusen från de handgjorda fallen och de fasta fröna för de slumpade sekvenserna; granska och checka in den nya korpusen tillsammans med formatändringen.

Unity-workflowen jämför korpusen i två jobb, vid en release och när den körs manuellt, inte på pull requests: tillsammans tar de över två timmar av Actions-budgeten.
`Determinism build` bygger APK:n med GameCI på en runner som frigör allt diskutrymme som Unitys Android-avbildning behöver.
`Determinism` laddar ner APK:n på en ny runner, som behåller sin Android SDK, och kör skriptet på en x86_64-emulator med API 34.
Repot är privat på GitHubs gratisplan och har inget grenskydd, så jobbet kan bara visa rött; det kan inte stoppa en merge.
Kör därför Unity-workflowen manuellt på grenen (**Actions → Unity → Run workflow**) innan en PR som rör simuleringen eller korpusen mergas.
Välj `determinism` under *What to build after the tests*, så körs bara testerna och determinismjobben på Ubuntu, utan Android-bygget och iOS-bygget på macOS:

```bash
gh workflow run unity.yml --ref <gren> -f builds=determinism
```

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

Seedverktyget (TASK-034) skriver en fristående HTML-sida som öppnas i webbläsaren.
För en seed visar den kartan med startön, expeditionernas öar, hamnlägen, krönet, innerhavet och farlederna, och validatorns rapport.
För ett intervall visar den dessutom per regel de kartor som ligger närmast gränsen, så att generatorn kan förbättras där den är svagast:

```bash
dotnet run -c Release --project tools/Eirora.MapCheck -- --generator procedural-archipelago --html seed.html --seed 42
```

```bash
dotnet run -c Release --project tools/Eirora.MapCheck -- --generator procedural-archipelago --html seeds.html --from 0 --seeds 1000
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
Hur servern körs på produktägarens server står i [deploy/README.md](../deploy/README.md), hotmodellen för inloggningen i [docs/security/sign-in-threat-model.md](./security/sign-in-threat-model.md) och den för molnsparningen i [docs/security/cloud-save-threat-model.md](./security/cloud-save-threat-model.md).

## Inloggning

Spelaren loggar in med Apple eller Google under kontoknappen längst till höger i nederkanten ([ADR-0008](./adr/0008-backend-hosting-accounts-and-database.md)).
Kontot behövs aldrig för att spela, och knappen syns bara i ett bygge som har en server och minst en leverantör.

| Plattform | Apple | Google |
|---|---|---|
| iOS | Inbyggd Sign in with Apple (`EiroraSignIn.mm`) | Googles OAuth-flöde med PKCE i `ASWebAuthenticationSession` och appens iOS-klient |
| Android | Webbflödet med ett Services ID i en Custom Tab; Apple postar svaret till serverns `/signin/apple/callback`, som lämnar det till appen | Credential Manager med "Sign in with Google" och appens webbklient |

Allt ligger bakom `ILoginProvider` i `client/Assets/Eirora/Runtime/Accounts/`, och `AccountSession` sköter token, förnyelse, koppling och bortkoppling, utloggning och radering.
Refresh-token ligger i iOS nyckelring eller krypterad med en nyckel i Android Keystore (`SignIn.androidlib`), aldrig i en vanlig fil.
Webbsidan för radering som Google Play kräver finns på `/account/delete` på servern.

### Identifierare hos Apple och Google

Produktägaren skapar dessa en gång; ingen av dem är hemlig utom Apples nyckel.
`<värd>` är serverns publika värdnamn (`EIRORA_HOST`).

1. **Apple Developer → Identifiers:** slå på Sign in with Apple för App ID `se.eirora.app`.
2. **Apple Developer → Identifiers → Services IDs:** skapa ett Services ID (till exempel `se.eirora.app.signin`), slå på Sign in with Apple och ange domänen `<värd>` med returadresserna `https://<värd>/signin/apple/callback` och `https://<värd>/account/delete`.
3. **Apple Developer → Keys:** skapa en nyckel med Sign in with Apple, se [deploy/README.md](../deploy/README.md).
4. **Google Cloud → Google Auth Platform:** skapa appens varumärkesprofil med bara den icke-känsliga omfattningen `openid`.
5. **Google Cloud → Clients:** skapa tre OAuth-klienter.
   - **Web:** med `https://<värd>` som tillåtet JavaScript-ursprung, för raderingssidan; Android begär ID-token till den här klienten.
   - **Android:** paketnamnet `se.eirora.app` och SHA-1 för Play-appsigneringens certifikat (Play Console → Testa och släpp → Appintegritet), och en till för uppladdningsnyckeln så att byggen från CI och datorn kan logga in.
   - **iOS:** bundle-id `se.eirora.app`.
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

EditMode-testerna kör hela klientflödet mot en falsk server (`FakeAccountServer`).
Kontraktstestet `ClientRequestTests` finns på båda sidor och håller ihop dem: förfrågningarna i `src/Eirora.Contracts/Requests~/` är byte för byte vad klienten skickar, och servertesterna skickar samma förfrågningar till den riktiga servern.
Ändras ett anrop ändras filen där, och båda testerna måste gå igenom.
Leverantörernas egna dialoger och nyckellagren finns däremot bara på en telefon.
Kontrollera detta på både Android och iOS innan en version med inloggning släpps:

1. Första inloggningen med varje leverantör skapar kontot, och nästa inloggning med samma konto når samma konto.
2. Apples dialog på iOS frågar inte efter namn eller e-post.
3. Apple på Android: Custom Tab öppnas, svaret kommer tillbaka till spelet, och att stänga fliken utan att logga in avbryter utan felmeddelande.
4. Google på Android visar kontoväljaren, och avbrott i den avbryter utan felmeddelande.
5. Efter omstart av appen är spelaren fortfarande inloggad, och efter 15 minuter förnyas token tyst.
6. Efter en avinstallation och ominstallation, eller en återställning till en ny telefon, är spelaren utloggad men världen finns kvar.
7. Koppla den andra leverantören efter att ha bekräftat med den första, logga ut och logga in med den; samma konto nås.
   Ta sedan bort en av inloggningarna efter att ha bekräftat med den som blir kvar.
8. Radera kontot från inställningarna: ett konto med Apple-inloggning bekräftas med Apple, och appen visas inte längre under Apple-ID → Logga in med Apple.
9. Allt ovan fungerar under viloperioden (RST-07), och i flygplansläge säger appen att servern inte går att nå utan att spelet påverkas.
10. Raderingssidan på `https://<värd>/account/delete` raderar ett konto med Google och med Apple från en dators webbläsare.

## CI

| Workflow | Körs vid | Gör |
|---|---|---|
| [`.NET`](../.github/workflows/dotnet.yml) | Pull requests och push till `main`, utom ändringar som bara rör `client/`, `docs/` eller Markdown, samt manuellt | Restore med sårbarhetskontroll, formatkontroll, bygge och alla .NET-tester, med serverns tester mot PostgreSQL i Testcontainers. Därefter byggs serverns containeravbildning och driftsätts på försök enligt `deploy/README.md` ([smoke-test.sh](../deploy/smoke-test.sh)); på `main` publiceras den som `ghcr.io/tiico/eirora-server:<commit>`. |
| [`Unity`](../.github/workflows/unity.yml) | Pull requests som ändrar `client/`, `src/Eirora.Simulation/`, `src/Eirora.Contracts/`, `src/Eirora.WorldGeneration/`, `src/Eirora.CloudSaving/`, workflowen eller dess actions ([`free-disk-space`](../.github/actions/free-disk-space/action.yml), [`build-version`](../.github/actions/build-version/action.yml)); publicerade releaser; manuellt | På pull requests bara Unity-testerna. Vid en release och manuellt också jämförelsen av determinismkorpusen mellan .NET och IL2CPP på en Android-emulator, Android-bygget och ett iOS-Xcode-projekt som kompileras med Xcode på macOS utan signering; en manuell körning med `builds=determinism` gör bara jämförelsen. En release blir ett app-paket som går till Google Plays interna test, se [Släppa en version](#släppa-en-version). |
| [`Site`](../.github/workflows/site.yml) | Pull requests och push till `main` som ändrar `deploy/site/` eller workflowen, samt manuellt | Startar de publika sidorna med serverns compose-fil och kontrollerar att integritetspolicyn fylls i och har sina säkerhetshuvuden. |

Repot är privat och har en budget på 2000 Actions-minuter i månaden, och macOS-minuter räknas tio gånger.
En release kostar runt 75 minuter, varav 40 för Xcode på macOS, och en körning av Unity-testerna runt 7; därför bygger CI appen bara när en version släpps.
En gren utan pull request kör ingen CI; öppna en pull request, gärna som utkast, eller kör en workflow manuellt under **Actions → \<workflow\> → Run workflow**.
Android-bygget och Xcode-projektet laddas upp som artefakter på körningen.

Testerna, determinismbygget, Android-bygget och iOS-projektet startar samtidigt när förkontrollen har gått igenom, så en körning tar ungefär så lång tid som det längsta av dem.
Bara släppet till Google Play väntar på testerna: vid en release laddas app-paketet upp av ett eget jobb när både testerna och Android-bygget är gröna.

Varje Unity-jobb återställer projektets `Library` från GitHubs cache, och Android-jobbet också Gradles cache.
En körning kan bara läsa cache från sin egen gren eller tagg och från `main`, och bara körningar på `main` sparar, eftersom repot har 10 GB cache och en `Library` är runt 2 GB per jobb.
`main` kör inte Unity av sig själv, så kör Unity-workflowen manuellt på `main` efter en merge som ändrar klienten; annars börjar pull requests och releaser utan cache och tar längre tid.
Med en `Library` från cachen kompilerar IL2CPP bara om C++-koden för ändrade assemblies; utan den tar Android-bygget runt 20 minuter längre.
Unitys avbildningar är större än det diskutrymme en GitHub-runner har ledigt, så jobben tar först bort förinstallerade SDK:er de inte använder med [free-disk-space](../.github/actions/free-disk-space/action.yml), snabbast först och bara tills jobbets uppmätta behov är ledigt.

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
   En manuell körning bygger en APK att sidladda men släpper inget i Google Play.

Licensfilen är bunden till datorn där den skapades, så CI avvisar den och aktiverar i stället med `UNITY_EMAIL` och `UNITY_PASSWORD`.
Kontot får därför inte ha tvåstegsverifiering; licensfilen behövs ändå, eftersom GameCI börjar med den.

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

Android-versionskoden är antalet commits, både i CI och lokalt (`BuildVersion`), så varje senare bygge installeras över det förra.
En release heter som sin tagg utan `v`, till exempel `0.3.0`; ett lokalt bygge eller en manuell körning i CI heter `0.0.<antal commits>` ([`build-version`](../.github/actions/build-version/action.yml)).
Appen visar version och regelversion i rapporten och på startkorten, så att dagboken kan hänvisa till byggen.
Klienten sparar världen i `Application.persistentDataPath`, som Android behåller vid uppdatering.
En sparning från äldre regler migreras; en från en nyare version laddas inte och skrivs aldrig över, och appen säger det och låter spelaren börja en ny värld med den gamla undanlagd (ADR-0006).

### Google Play

Telefonen får nya versioner genom Google Plays interna test (D-98).
Varje publicerad release blir ett app-paket som Unity-workflowen signerar med uppladdningsnyckeln och släpper i det interna testet, se [Släppa en version](#släppa-en-version).
Play Store uppdaterar sedan appen som vilken app som helst, utan kabel och utan avinstallation, och världen finns kvar.
Versionen i appen är samma som versionens namn i Play Console, i artefakten `eirora-android-<version>` på körningen och i app-paketet `eirora-<version>.aab` som bifogas releasen.

Det här gör produktägaren en gång; inga nycklar eller lösenord lämnas till en agent.

1. Skapa nyckeln med guiden under [Signering](#signering) om den inte redan finns.
2. Skapa appen **Eirora** i [Play Console](https://play.google.com/console) med paketnamnet `se.eirora.app`, samma som `applicationIdentifier` i Unity-projektet, och låt Google skapa och förvara appens signeringsnyckel (Play App Signing, D-76).
   Paketnamnet kan aldrig ändras i Google Play.
   Nyckeln från guiden blir uppladdningsnyckel och kan bytas ut via Play Console om den försvinner.
3. Publicera integritetspolicyn på servern enligt [deploy/site](../deploy/site/README.md).
   Fyll sedan i **Appinnehåll**: policyns adress, dataskydd och deklarationen för Health Connect-behörigheterna.
   Appen läser steg, träningspass och sömn när spelaren öppnar den, för spelets krafter, och skickar inga hälsodata till någon server (HLT-10).
   Google släpper ingen version med Health Connect-behörigheter förrän deklarationen är godkänd.
4. Lägg till ditt Google-konto som testare under **Testa → Internt test → Testare**, öppna länken för att gå med på telefonen och godkänn.
5. Skapa ett tjänstkonto i Google Cloud med en JSON-nyckel och bjud in dess e-postadress under **Användare och behörigheter** i Play Console, med behörighet att släppa till testspår för Eirora.
   Lägg in nyckeln och radera sedan JSON-filen från datorn:

   ```bash
   gh secret set PLAY_SERVICE_ACCOUNT_JSON < play-service-account.json
   ```

6. Släpp en version enligt [Släppa en version](#släppa-en-version).
   Workflowen laddar upp app-paketet till det interna testet, också den första versionen.
   Under **Appinnehåll** dyker då deklarationen för hälsobehörigheterna upp; fyll i den och skicka in.
   Testarna får versionen först när Google har godkänt deklarationen, vilket kan ta några dagar.
7. Installera Eirora från Play Store via testlänken från steg 4.
   De sidladdade prototyperna har paketnamnet `com.eirora.game` och är en annan app än den från Google Play, så båda kan vara installerade; världen följer inte med från den gamla (D-77).

Android säkerhetskopierar appens data, sparningen med, till spelarens eget Google-konto som vilken app som helst, så världen kan följa med till en ny telefon (D-78).

| Fel | Orsak |
|---|---|
| `Google Play secret missing` | `PLAY_SERVICE_ACCOUNT_JSON` saknas; jobbet stoppar innan något byggs. |
| `Package not found` | Byggets paketnamn finns inte som app i Play Console; appen ska ha `se.eirora.app`, se steg 2. |
| `APK-filen eller AAB-arkivet måste ha paketnamnet …` | Bygget och appen i Play Console har olika paketnamn; Play Consoles kan inte ändras, så bygget följer det. |
| `Only releases with status draft may be created on draft app` | Google vill att appens första version släpps för hand: ladda upp `.aab`-filen från körningens artefakt under **Internt test → Skapa ny version**. |
| `Version code N has already been used` | Samma commit släpptes två gånger, eller taggen pekar på en äldre commit än förra releasen; tagga en senare commit på `main`. |
| `Release tag not a version` | Taggen ser inte ut som `v0.3.0`; ta bort releasen och taggen och släpp igen med rätt namn. |
| `Release tag not on main` | Taggen pekar på en commit som inte finns på `main`; merga först och tagga sedan `main`. |
| Play Store erbjuder ingen uppdatering | Nya versioner i internt test syns oftast inom några minuter; sök igen under **Hantera appar och enheter**. |
| Play Store kan inte installera appen | En APK med `se.eirora.app` från CI eller datorn är installerad och har en annan signatur än appen från Google Play; avinstallera den först. |

### Släppa en version

En ny version till telefonen är en GitHub-release med en tagg på `main` (D-98).
Taggen namnger versionen: `v0.3.0` blir `0.3.0` i appen och i Play Console.
Välj ett högre nummer än förra releasen; Android-versionskoden är ändå antalet commits.

```bash
gh release create v0.3.0 --target main --generate-notes
```

Samma sak går under **Releases → Draft a new release** på GitHub; Unity-workflowen startar när releasen publiceras, inte när den sparas som utkast.
Workflowen kör Unity-testerna, bygger Android och iOS, släpper app-paketet i det interna testet och bifogar det till releasen som `eirora-<version>.aab`.
Testarna får versionen i Play Store några minuter efter att körningen blivit grön.
