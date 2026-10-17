# Health Connect läses med en egen Kotlin-adapter bakom ett C#-gränssnitt, inte med HealthBridge

> **Förslag.** Beslutas när spiken i TASK-007 har körts på produktägarens telefon; rapporten kan ändra konsekvenserna men knappast valet.

Spelet läser steg, träningspass och sömn från Health Connect med en egen Android-biblioteksmodul i Kotlin (`.androidlib`) som Unity-klienten anropar via ett C#-gränssnitt; HealthKit får en motsvarande Swift-adapter bakom samma gränssnitt (HLT-02).
Vi valde detta framför HealthBridge (Healthwired Games, Fab och Unity Asset Store) därför att det vi behöver för HLT-03, HLT-04, HLT-08 och HLT-12 inte finns i HealthBridge enligt dess egen dokumentation, medan en egen adapter visade sig vara liten: spiken klarar allt med drygt 600 rader Kotlin och ett C#-gränssnitt på ett femtiotal rader.

## Vad spiken visade

Spiken (`spikes/health-connect/`) kördes först på en emulator med Android 14 och Health Connect-modulen 14-10541443, med testdata från två källor som en separat seeder-app skrev (`spikes/health-connect-seeder/`).
Sedan kördes den på produktägarens Pixel 7 med Android 17 och riktig data (rapporten står i [spikens README](../../spikes/health-connect/README.md#rapport-från-riktig-enhet)).

### På produktägarens telefon

- **Tre källor skriver steg samtidigt:** Google Fit, telefonens inbyggda stegräknare i Health Connect och HeyTap Health (en klockapp).
  Alla sju dagarna hade flera källor, och rå summa var större än Health Connects aggregat varje dag.
- **Dubbletter även inom en källa:** utöver 2 712 överlappande par mellan källor fanns 595 par inom samma källa, så inte ens summan per källa går att lita på.
- **Telefonens egen stegräknare** har källan `com.android.healthconnect.phone.<hash>`, ett namn som är unikt per enhet och inte motsvarar någon app.
- **`UNKNOWN` är vanligt:** HeyTap Health skriver alla steg och all sömn med `recordingMethod` `UNKNOWN` och utan enhet, trots att datan kommer från en klocka.
  Klassas `UNKNOWN` som egenrapporterat blir den mest trovärdiga källan egenrapporterad; därför räknas `UNKNOWN` som verifierat (D-68).
- **Sömn från en enda källa** gav ändå en annan summa än aggregatet två dagar av sex.
  Det tyder på att aggregatet räknar sömnlängd på annat sätt än sessionens början och slut (till exempel utan vakenfaser) och delar natten vid midnatt.
- **Bakgrundsläsning och historik** finns i telefonens Health Connect, till skillnad från emulatorns.

### På emulatorn

- **`recordingMethod`, källa och enhet** följer med varje post och går att klassa som verifierat eller egenrapporterat (HLT-08).
  `recordingMethod` anges av appen som skrev posten och kontrolleras inte av plattformen, så klassningen är en förtroendefråga, inte ett bevis.
- **Dubbletter:** två källor som räknar samma steg ger en naiv summa som är mer än 50 % för hög.
  Health Connects aggregat deduplicerar per tidsintervall efter användarens källprioritet: där poster överlappar vinner den högst prioriterade källan och den andra fyller bara luckorna.
  Det gäller steg, träningspass och sömn.
- **Aggregat per kalenderdag** delar en natt vid midnatt, men krafterna räknas per spelardygn (gryning till gryning); spelet måste därför aggregera över egna intervall, inte ta Health Connects dagsvärden.
- **Historik:** utan `READ_HEALTH_DATA_HISTORY` returnerade `readRecords` bara poster från 30 dagar före första beviljandet, men `aggregateGroupByPeriod` returnerade ändå värden 45 och 200 dagar bakåt.
  Spelet behöver ingen historik (D-51, D-53), så vi begär den inte och förlitar oss inte på läckan.
- **Ändringar:** `getChanges` gav den redigerade posten som ny version och den raderade som bara ett id, utan vana eller tid.
- **Bakgrund:** samma schemalagda läsning gav 24 poster med appen öppen och 0 poster i bakgrunden utan bakgrundsbehörighet, utan något fel.
  Funktionen för bakgrundsläsning och historik saknades helt i emulatorns Health Connect-version och finns bara på enheter med nyare Health Connect.
- **Nekad behörighet** är ett vanligt svar, inte ett undantag; efter två nekanden visar Android inte dialogen igen och appen måste länka till Health Connects inställningar.

## Considered Options

- **HealthBridge för Unity** (v1.22 på Asset Store 2026-09-14, v1.10 på Fab 2026-09-01; 55–120 USD beroende på butik och nivå).
  Enligt dokumentationen läser den bara rådata 1–90 dagar bakåt och exponerar varken `recordingMethod`, aggregat med deduplicering, ändrings-API, bakgrunds- eller historikbehörighet.
  Den kräver dessutom `READ_EXERCISE` och `READ_HEART_RATE` för att initieras, vilket bryter mot minsta behörighet per vana (HLT-03) och mot att en spelare ska kunna ge bara stegbehörighet.
  Fab-annonsen är markerad som AI-genererad och versionerna i butikerna skiljer sig åt.
- **En fork av HealthBridge.** Licensen tillåter ändringar och Kotlin- och Swift-källkoden följer med, men vi skulle äga en stor kodbas för 129 datatyper vi inte behöver och förlora uppströmsuppdateringar ändå.
- **Kotlin som lösa `.kt`-filer i `Assets/`.** Unity kompilerar dem, men beroenden (Health Connect, WorkManager) och manifestet måste då läggas i Unitys globala Gradle-mallar i stället för i en egen modul.

## Consequences

- Adaptern är en `.androidlib` med `androidx.health.connect:connect-client` 1.1.0 (stabil sedan 2025-10-08; 1.2.0-alpha06 är senaste förhandsversion) och kräver minSdk 26; produktionsklienten har i dag 25 och höjs när adaptern flyttar in.
- Unity 6.3 (6000.3.26f1, AGP 9.1.1) stänger av AGP:s inbyggda Kotlin med `android.builtInKotlin=false`, och det äldre `org.jetbrains.kotlin.android`-pluginet fungerar inte med AGP 9:s nya DSL.
  Utan åtgärd hoppas Kotlin-koden tyst över och bygget lyckas ändå; därför slår en egen `gradleTemplate.properties` på inbyggd Kotlin igen, och bygget underkänns om bryggklassen saknas i APK:n.
  Detta prövas på nytt vid varje Unity-uppgradering.
- C#-gränssnittet returnerar poster med källa, enhet och `recordingMethod`; klassningen till verifierat eller egenrapporterat görs i C#, så att samma regel gäller för HealthKit och manuell inmatning.
  Bara `MANUAL_ENTRY` blir egenrapporterat, eftersom ordlistan definierar egenrapporterat som det hälsokällan markerat som manuellt inmatat; `UNKNOWN` är inte markerat och blir verifierat (D-68).
- Spelet identifierar aldrig källor med fasta paketnamn: telefonens egen stegräknare har ett namn per enhet.
- Bidrag per spelardygn beräknas med Health Connects aggregat över spelardygnets intervall (deduplicering efter användarens prioritet) i stället för att summera poster (INV-B1).
- Korrigeringar (HLT-04, INV-B3) läses med ändrings-API:t; eftersom en radering bara har ett id måste klienten minnas vilket intervall varje läst post-id hörde till, utan att spara värdena (HLT-10).
  En ändringstoken gäller i 30 dagar; har den gått ut läses de senaste 30 dagarna om.
- Krafterna kan beräknas när spelaren öppnar appen, så bakgrundsbehörighet är inte nödvändig för kärnan (RST-05 säger "kan").
  Om vi senare läser i bakgrunden får 0 poster utan behörighet aldrig tolkas som ingen egenvård.
- Vi begär bara läsbehörighet för de vanor spelaren väljer, aldrig historik, och skriver aldrig till Health Connect.
- Google Play kräver en deklaration per datatyp och att Health Connects länk till integritetspolicyn visar samma policy som butikssidan; spikens förklaringssida är bara en platshållare.
- Vi äger underhållet av adaptern och måste följa Health Connect-klientens versioner själva; ytan är liten och spiken visar vilka API:er som räcker.
