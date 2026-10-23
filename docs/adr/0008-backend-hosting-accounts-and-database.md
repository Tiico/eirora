# Backend körs som containrar med PostgreSQL, hemma till beta och i Azure Sverige vid release; konton är egna och skapas vid inloggning med Apple eller Google

> **Beslutat 2026-10-18** av produktägaren: hosting, kontomodell och bidragsnyckeln valdes bland alternativen nedan (O-09).
> Priser och plattformsregler är kontrollerade 2026-10-18 och prövas på nytt inför flytten till molnet och inför release.

Backend är en enda ASP.NET Core-container och en PostgreSQL 18-databas, beskrivna i en Docker Compose-fil som är likadan i alla miljöer.
Prototyp och beta körs på produktägarens egen Ubuntu-server bakom Traefik, med krypterad backup till objektlagring hos ett EU-bolag; före den publika butiksreleasen flyttas samma container och databas till Azure Container Apps och Azure Database for PostgreSQL i Sweden Central.
Konton är egna, i `Eirora.Server` med OpenIddict, och skapas först när spelaren loggar in med Sign in with Apple eller Google; dessförinnan finns ingenting om spelaren på servern, och inga lösenord eller e-postadresser lagras någonsin.
Bidrag per spelardygn ligger i en egen, end-to-end-krypterad del av molnsparningen med en bidragsnyckel som bara spelarens enheter har (D-57); förloras nyckeln går bidragshistoriken förlorad, aldrig världen.

Vi valde detta därför att servern bara äger det delade (ADR-0001) och har låg last långt efter release, så en container och en vanlig PostgreSQL räcker och kan flyttas med en dump och en ny driftsättning.
Egna konton med enbart federerad inloggning ger minst personuppgifter, all kontodata i EU och ingen inlåsning, medan de färdiga tjänsterna antingen lagrar inloggningar utanför EU eller saknar export av användare.

## Hosting

| Fas | Var | Varför |
|---|---|---|
| Prototyp (produktägaren) | Produktägarens Ubuntu-server, Docker Compose bakom Traefik med Let's Encrypt på port 443 | Finns redan, kostar nästan inget och TLS termineras hemma, så ingen tredje part ser trafiken. |
| Beta (cirka 100–1 000 spelare) | Samma server och samma compose-fil | Klienten spelar offline när servern är nere (ACC-04), så ett avbrott hemma stoppar bara molnsparning och gruppåtgärder en stund. |
| Release (10 000–50 000 spelare per månad) | Azure Container Apps och Azure Database for PostgreSQL Flexible Server i Sweden Central | Data i Sverige, hanterad PostgreSQL med återställning till en tidpunkt i upp till 35 dagar, huvudversionsuppgradering på plats och förstklassigt stöd för .NET. |

- Flytten görs före den första publika butiksreleasen och övas en gång mot en kopia under betan.
  Den är en `pg_dump` och `pg_restore` under ett kort underhållsfönster och ett byte av DNS; klienterna märker bara att molnsparningen är borta en stund.
- Valet av Azure prövas på nytt vid flytten mot UpCloud Stockholm, om det då finns serverlösa containrar och bekräftad återställning till en tidpunkt där, eftersom Microsoft som amerikanskt bolag omfattas av CLOUD Act.
  Hälsodatan påverkas inte av det valet, eftersom bidragen är end-to-end-krypterade; konton, grupper och handel är däremot personuppgifter i klartext på servern.
- Hemmaservern ska ha krypterad disk för databasens volym, automatiska säkerhetsuppdateringar, en brandvägg som bara släpper in 80 och 443 till Traefik och SSH med nyckel.
  PostgreSQL publiceras aldrig utanför compose-nätverket.
- Authelia skyddar administrativa ytor (till exempel instrumentpaneler för övervakning) men aldrig `/api`, som mobilklienterna når med egna token.

## Databas

- PostgreSQL 18 med den officiella containeravbildningen, fäst till en minor-version som uppdateras med beroendebotten.
  En huvudversion uppgraderas med `pg_upgrade` hemma och på plats i Azure.
- Åtkomst från `Eirora.Server` med EF Core och Npgsql.
  Migrationer skrivs som EF Core-migrationer, granskas som genererad SQL i PR:en och körs som ett eget steg före den nya containern, aldrig av appen vid start.
  En migration får inte kräva att den föregående appversionen stoppas, så att en driftsättning kan rullas tillbaka (OPS-01).
- Backup med pgBackRest: kontinuerligt WAL-arkiv och en daglig basbackup, krypterade på servern innan de lämnar den, till S3-kompatibel objektlagring hos ett EU-bolag och till en lokal disk för snabb återställning.
  Retention är 30 dagar, så raderade konton är borta även ur backuperna inom 30 dagar (ACC-05).
  En schemalagd återställning till en tillfällig container varje vecka kontrolleras mot en känd fråga och larmar om den misslyckas (NFR-01).
- I Azure ersätts pgBackRest av tjänstens egen backup med återställning till en tidpunkt och samma retention på 30 dagar, och den veckovisa återställningsövningen körs mot en tillfällig server.
- Ingen tabell innehåller bidrag i klartext, rådata eller e-post.
  Refresh-token lagras bara som hash.

## Konton och inloggning

- Spelaren spelar utan konto och utan att något om hen finns på servern (ACC-04).
  Kontot skapas vid första inloggningen, och då laddas den lokala världen upp som kontots molnsparning.
  _Ändrat 2026-10-23 (#154, TASK-060): först när spelaren svarat på frågan om bidragsdelen; se Bidragen i molnsparningen._
- Inloggning sker med Sign in with Apple eller Google på båda plattformarna, så att en värld kan flytta mellan Android och iOS.
  På iOS kräver App Store-riktlinje 4.8 Sign in with Apple när Google erbjuds; på Android körs Sign in with Apple som webbflöde med ett Services ID.
- Klienten lämnar Apples eller Googles ID-token till `Eirora.Server`, som kontrollerar signatur, `iss`, `aud`, `exp` och nonce mot leverantörens nycklar och utfärdar egna token via OpenIddict.
  En access-token är en JWT som gäller i 15 minuter, och en refresh-token roteras vid varje användning och går ut efter 60 dagar utan användning.
  Det påverkar aldrig den egna världen; utgångna token betyder bara en ny inloggning nästa gång spelaren synkar.
- Kontots id är ett slumpat UUID som inte härleds ur någon leverantör.
  Ett konto kan ha flera kopplade inloggningar (Apple och Google); kontoåterställning är att logga in med någon av dem, och den sista kan bara tas bort genom att radera kontot.
- Servern lagrar per inloggning bara leverantör och leverantörens `sub`, aldrig namn eller e-post, även när leverantören skickar dem.
- Radering finns i appen och på en webbsida för Google Play (Play kräver båda).
  Den tar bort kontot, kopplade inloggningar, token, molnsparningen och de krypterade bidragen, återkallar Apples token via Sign in with Apple REST API och fungerar även under vila (RST-07).
  Klienten tar samtidigt bort bidragsnyckeln från enheten och plattformens nyckelring.
- Apples `sub` är unikt per utvecklarteam.
  Byter appen Apple-team måste varje konto flyttas med Apples överföringsflöde, så appen ska ligga kvar i samma team.
- Game Center och Play Games används inte som kontoinloggning, eftersom identiteterna är bundna till en plattform; de kan kopplas till kontot senare utan att något ändras här.

## Bidragen i molnsparningen (D-57)

- Molnsparningen har två delar: världsdelen och bidragsdelen.
  Bidragsdelen innehåller allt som avslöjar egenvården per spelardygn: bidragen, den kraft varje spelardygn har krediterats och journalens avläsningar av egenvård.
  Världsdelen innehåller resten, med krafternas saldon men inget per spelardygn som går att räkna tillbaka till egenvården.
  _Ändrat 2026-10-21 (#38, D-100): bidragsdelen bär också de använda idempotensnycklarna, segmentens start och hela journalen, så att världsdelen bara är kartan och senaste snapshoten utom det som hålls per spelardygn._
- Bidragsdelen krypteras på enheten med XChaCha20-Poly1305 och en slumpad nonce på 192 bitar, med konto-id, nyckelepok och sparningens version som associerade data, så att servern inte kan byta delar mellan spelare eller sparningar.
  Krypteringen kommer från BouncyCastle.Cryptography, som är netstandard och fungerar likadant i .NET och under IL2CPP; .NET:s `AesGcm` finns inte i Unitys Mono.
  Servern lagrar bara chiffertext och dekrypterar aldrig.
  _Ändrat 2026-10-22 (#38, del 3): en förseglad bidragsdel är texten `eirora-sealed 1`, en radbrytning, noncen, chiffertexten och taggen; de associerade data är UTF-8-texten `eirora-contributions <konto-id> <nyckelepok> <version>`, där versionen är den uppladdningen blir (basversionen + 1). Koden är `ContributionCipher` i `src/Eirora.CloudSaving/`, som Unity bygger mot assemblyn under `client/Assets/Plugins/BouncyCastle/`._
- Bidragsnyckeln är en slumpad 256-bitarsnyckel per konto med en nyckelepok.
  Den följer med till en ny telefon på det här sättet:
  - **Samma plattform:** tyst, via iCloud-nyckelringen på iOS och Block Store med end-to-end-krypterad backup på Android.
    Block Store krypterar bara end-to-end när telefonen har skärmlås; utan det sparas nyckeln bara lokalt och spelet föreslår QR-överföring eller återställningskod.
  - **Mellan iOS och Android:** QR-överföring.
    Den nya telefonen visar en QR-kod med en engångsnyckel, den gamla skannar och skickar bidragsnyckeln krypterad till den, och servern förmedlar bara förseglade byte.
  - **Återställningskod:** frivillig, och erbjuds i inställningarna och när plattformens backup saknas.
  _Ändrat 2026-10-22 (#108, del 1): överföringen går via `/api/account/key-transfers`. Den nya telefonen skapar en överföring med ett X25519-engångsnyckelpar och visar biljetten `eirora-key-transfer 1 <id> <publik nyckel> <hemlighet>`, där hemligheten på 128 bitar aldrig går till servern; den gamla förseglar bidragsnyckeln för den (X25519-överenskommelse följd av hemligheten in i HKDF-SHA256, saltad med båda publika nycklarna, XChaCha20-Poly1305 med konto-id och överförings-id som associerade data) och levererar den; en nyckel utifrån, från en överföring eller en återställningskod, behålls bara när molnsparningen bekräftar den (epoken är molnsparningens och bidragsdelen öppnas); servern lämnar ut den förseglade nyckeln en gång och glömmer överföringen efter tio minuter, eller när kontot raderas. Koden är `KeyTransferCipher`, `KeyTransferReceiver` och `KeyTransferSender` i `src/Eirora.CloudSaving/`. Återställningskoden (`RecoveryCode`) är 64 tecken Crockford-base32 i åtta grupper om åtta: version, nyckelepok, nyckeln och en kontrollsumma, så att ett feltecken upptäcks; O, I och L läses som 0 och 1. QR-visningen och skanningen i klienten samt den tysta synken via nyckelring och Block Store kommer i egna delar._
    _Ändrat 2026-10-22 (#108, del 2): i klienten visar den nya telefonen biljetten som en QR-kod under frågan om nyckeln (`Hud.Cloud`), och frågar servern varannan sekund medan koden är på skärmen; den gamla telefonen skannar koden från kontoarket med bakkameran (`WebCamQrScanner`, Unitys `WebCamTexture`), visar biljettens kontrollkod på sex siffror, som också står under QR-koden på den nya telefonen, och skickar nyckeln först när spelaren bekräftat; så kan inte den som kan skapa en överföring för kontot samla in nyckeln genom att få sin kod framför kameran. QR-koderna görs och läses med ZXing.Net i `Eirora.CloudSaving` (`QrCodes`), som Unity läser från `client/Assets/Plugins/ZXing/` och .NET hämtar från NuGet, så att .NET-testerna prövar rundturen. Kameran körs bara medan skannerarket är öppet och bilderna stannar i telefonen; Android ber om kameratillstånd vid första användning, iOS med texten i spelarinställningarna (`ProjectSetup`). Den tysta synken via nyckelring och Block Store kommer i del 3._
    _Ändrat 2026-10-22 (#108, del 3): den tysta synken.
    Klienten lägger bidragsnyckeln i plattformens backup (`IKeyBackup`) när molnsparningen håller den: en ny nyckel direkt, och annars en gång vid varje start och inloggning, så att också en nyckel från före backupen eller före skärmlåset kommer dit.
    Namnet är `eirora-contribution-key-<konto-id>` på båda plattformarna.
    På iOS är det ett synkbart objekt i nyckelringen (`kSecAttrSynchronizable`, läsbart efter första upplåsningen, tjänsten `com.eirora.contribution-key`, `EiroraKeyBackup.mm`), som iCloud-nyckelringen för end-to-end-krypterat till spelarens andra enheter med samma Apple-konto.
    På Android är det Block Store (`KeyBackup.kt`, `play-services-auth-blockstore`): till molnbackupen bara när `isEndToEndEncryptionAvailable` säger ja, annars bara på telefonen, där nyckeln ändå följer med en överföring från telefon till telefon.
    Innan en telefon frågar efter nyckeln, för att den saknas eller inte öppnar bidragsdelen, läser den backupen; en nyckel därifrån behålls bara när molnsparningen bekräftar den, som en återställningskod.
    Utan skärmlås (ingen lösenkod på iOS, ingen end-to-end-kryptering i Block Store) säger kontoarket att nyckeln inte följer med av sig själv och föreslår återställningskoden eller QR-överföringen.
    Kan plattformen inte säga om nyckeln når molnet, för att Block Store inte svarar på frågan om end-to-end-kryptering eller iPhonen inte kan avgöra om den har lösenkod, stannar nyckeln på telefonen och kontoarket säger inget.
    När kontot raderas i spelet tas nyckeln bort ur telefonens backup, och iCloud-nyckelringen för borttagningen vidare till spelarens andra Apple-enheter.
    Block Store gör det inte: en telefon som redan satts upp från den gamla behåller sin kopia.
    Den andra plattformens backup och en radering på webbsidan lämnar också nyckeln kvar, vilket hotmodellen tar upp som en accepterad risk, eftersom servern redan raderat allt nyckeln öppnar.
    Telefonen kan inte se om iCloud-nyckelringen eller Googles säkerhetskopiering är påslagen: med skärmlås men utan den stannar nyckeln på telefonen, och den nya telefonen frågar som förut._
- Saknar en ny telefon nyckeln börjar en ny nyckelepok och världen fortsätter.
  Bidragsdelen från den gamla epoken markeras som låst och sparas i 90 dagar om nyckeln skulle dyka upp, och raderas sedan eller tidigare om spelaren väljer det.
  Världen, krafternas saldon och allt byggt finns kvar; spelardygnen före den nya epoken kan inte längre rättas i efterhand och räknas som tomma i Byggkraftens rullande vecka (D-63), som för en ny värld.
  Det gäller spelardygnen till och med världsdelens senaste spelardygn, eftersom det dygnet redan kan ha krediterats (D-101).
  _Ändrat 2026-10-22 (#38, D-102): har telefonen redan en annan värld, eller öppnas bidragsdelen inte med telefonens nyckel för dess epok, väljer spelaren mellan att hämta kontots ö och att behålla telefonens. Den som inte väljs sparas undan på telefonen och raderas inte. En bidragsdel som inte öppnas slås aldrig ihop; väljer spelaren telefonens ö låses den under en ny nyckelepok, som när nyckeln saknas._
  _Ändrat 2026-10-22 (#108, del 1): en ny telefon utan nyckeln frågar först efter den (från den gamla telefonen eller som återställningskod) och börjar en ny epok bara när spelaren väljer att fortsätta utan._
- Chiffertexten är fortfarande personuppgifter av särskild kategori enligt GDPR, eftersom den hör till ett känt konto.
  Krypteringen är en skyddsåtgärd i DPIA:n (artikel 32), och ett dataintrång som bara rör den kräver inte att spelarna underrättas (artikel 34.3 a); radering sker genom att raderna tas bort och backuperna går ut.
  _Ändrat 2026-10-23 (#154, TASK-060): bidragsdelen lagras bara med spelarens uttryckliga samtycke (artikel 9.2 a).
  Före den första uppladdningen frågar appen, i ordlistans termer och med två likvärdiga svar, och laddar inte upp något förrän spelaren svarat.
  Svaret sparas på kontot via `/api/account/contribution-consent` som en egen rad per svar i `ContributionConsents`, med frågans version (`ContributionConsent.CurrentVersion`) och serverns tidpunkt, så att det går att visa vad spelaren godkänt; raderna går med kontot.
  Utan samtycke är molnsparningen bara världsdelen: uppladdningen har ingen bidragsdel och nyckelepok 0, molnsparningens nyckelepok står kvar, och servern svarar 409 på en uppladdning vars delar inte stämmer med svaret eller som kommer innan spelaren svarat.
  Ett nej eller en återkallelse under Konto raderar bidragsdelen och de låsta bidragsdelarna i samma transaktion, under kontots radlås.
  Ett nej frågas aldrig igen av sig självt (HLT-12); ett ja till en äldre version av frågan frågas igen.
  Utan bidragsdel finns inga journaler att slå ihop, så en telefon som inte själv laddade upp molnsparningens version låter spelaren välja ö, och en ny telefon hämtar världsdelen med spelardygnen hittills stängda, som utan nyckeln (D-101), men utan att fråga efter nyckeln.
  Befintliga konton har inget svar: en bidragsdel som redan laddats upp ligger kvar tills spelaren svarat, och ett nej raderar den.
  En server med samtycket kan inte rullas tillbaka till en version utan det när något konto sagt nej, eftersom den äldre servern inte läser en molnsparning utan bidragsdel._

## Hemligheter och miljöer

| Miljö | Databas | Hemligheter |
|---|---|---|
| Lokal utveckling | PostgreSQL i compose på utvecklarens dator | `dotnet user-secrets`; testklienter hos Apple och Google |
| Test och CI | PostgreSQL 18 i Testcontainers per testkörning | Inga riktiga; tester signerar Apple- och Google-liknande ID-token med egna testnycklar |
| Staging | Eget compose-projekt på hemmaservern, egen databas | Egna signeringsnycklar och klient-id; aldrig riktiga spelares data |
| Produktion | Hemmaservern, sedan Azure | Hemma: Docker Compose-secrets som filer som bara root och den container som läser dem kommer åt; i Azure: Key Vault via managed identity |

- Hemma ägs varje hemlighet av den användare som läser den, med läsrätt bara för ägaren, i en katalog som bara den som driftsätter och root kommer in i.
  Servern körs som avbildningens icke-root-användare (UID 1654), och Compose utanför Swarm monterar filerna med värdens ägare och rättigheter; PostgreSQL:s lösenord läses som root och ägs av root.
  _Ändrat 2026-10-18 (#74): beslutet sade först att bara root läser filerna, vilket en server som inte körs som root inte kan starta med._
- Hemligheterna är OpenIddicts signerings- och krypteringsnycklar, Apples privata nyckel för Sign in with Apple, Googles klient-id, databasens lösenord och pgBackRest-förrådets krypteringsnyckel.
- Förrådets krypteringsnyckel förvaras också offline i produktägarens lösenordshanterare.
  Utan den är alla backuper oläsbara; utan OpenIddicts nycklar loggas bara alla ut.
- Containeravbildningen byggs i CI och publiceras i GitHub Container Registry med commitens sha.
  Servern hämtar en angiven tagg; CI når aldrig in till hemmaservern.

## Kostnad

Listpriser 2026-10-18, utan moms; Apples utvecklarprogram (99 USD per år) och Google Play-kontot (25 USD en gång) behövs oavsett.

| Fas | Uppskattning per månad |
|---|---|
| Prototyp | Under 5 €: objektlagring för backup och domän; servern och strömmen finns redan |
| Beta | Under 10 €: mer backup |
| Release utan hög tillgänglighet | Cirka 50–70 USD: PostgreSQL B2s (cirka 29 USD), en alltid varm container med 0,5 vCPU (cirka 35 USD), lagring och Key Vault |
| Release med hög tillgänglighet | Cirka 300 USD: PostgreSQL General Purpose med 2 vCore och standby, som Burstable saknar |

Hög tillgänglighet slås på när intäkterna motiverar det; utan den ger ett avbrott i databasen samma upplevelse som ett avbrott hemma under betan.

## Vad som kan bytas senare

- **Lätt:** var containern och databasen körs, backupens mål, tokenlivslängder, fler inloggningar (Game Center, Play Games, passkeys) och hur bidragsnyckeln förs över.
- **Svårt:** att börja lagra e-post eller lösenord (kräver nytt samtycke och nya flöden), sparningens uppdelning i världsdel och bidragsdel (en ändring i sparformatet med migration enligt ADR-0006), krypteringens format (versioneras per nyckelepok) och att byta Apple-team.
- **Inget att ändra:** kontots id är vårt eget, så ingen leverantör äger spelarens identitet.

## Considered Options

- **Hosting i Azure, Google Cloud, AWS, UpCloud eller Scaleway redan från prototypen:** fungerar, men kostar från första dagen för en enda spelare och tillför inget som hemmaservern saknar före betan.
  AWS App Runner tar inte emot nya kunder sedan 2026-04-30, och ECS kräver en lastbalanserare för cirka 20 USD i månaden; Scaleway saknar nordisk region; Google Cloud är billigast men nordiska priser och Cloud SQL-detaljer gick inte att bekräfta.
- **Allt på hemmaservern även efter release:** billigast, men tillgängligheten beror på hemmets ström, uppkoppling och en enda maskin när tiotusentals spelare har sina molnsparningar där.
- **Egen drift av PostgreSQL i molnet (till exempel Hetzner):** billigt men lägger replikering, uppgraderingar och återställning av känsliga personuppgifter på en person.
- **Anonymt serverkonto för varje installation:** ger molnbackup utan inloggning, men Apple kräver att även gästkonton kan raderas, servern fylls av konton utan ägare och backupen går ändå förlorad om telefonen försvinner före inloggningen.
- **Firebase Authentication:** anonym start och koppling finns, men inloggningsdata behandlas enbart i USA.
- **Unity Authentication:** gratis och med koppling till Apple, Google, Game Center och Play Games, men lagringsplatsen är inte dokumenterad och användare kan inte exporteras.
- **Microsoft Entra External ID:** gratis upp till 50 000 aktiva användare och med data i EU, men utan anonym start, dess inbyggda inloggning i appen stödjer inte Apple eller Google, och lösenord kan inte exporteras.
- **ASP.NET Core Identitys egna token (`MapIdentityApi`):** token är proprietära, inte standard-JWT, och passar inte federerad inloggning; OpenIddict är Apache-licensierat och används i stället.
- **E-post och lösenord eller magiska länkar:** ger återställning utan Apple eller Google, men kräver e-post på servern, utskick och skydd mot kontoövertagande, för något som Apple- och Google-inloggning redan löser.
- **Hela molnsparningen end-to-end-krypterad:** servern skulle inte kunna läsa något, men en förlorad nyckel skulle förlora världen, vilket bryter NFR-01.
- **Obligatorisk återställningskod:** säkrare mot förlust, men vardagsspelare tappar koder och kontoskapandet får ett formulärsteg.
- **Nyckelvalv med PIN (som Signal SVR3 och WhatsApps Backup Key Vault):** bäst återställning, men kräver HSM eller enklaver hos flera leverantörer, vilket inte går att driva säkert av ett litet team.
- **Passkeys med PRF-tillägget:** kan senare bli ytterligare en kopia av nyckeln, men inte den enda, eftersom en raderad passkey då förlorar datan och stödet mellan iOS och Android ännu är ojämnt.

## Consequences

- TASK-022 bygger konto och inloggning enligt ovan: OpenIddict, Apple- och Google-token, radering med återkallelse hos Apple, EF Core-migrationer, Testcontainers i CI och compose-filen med Traefik-etiketter.
- TASK-023 lagrar en molnsparning per konto med en version som varje uppladdning höjer med ett.
  En uppladdning som bygger på en annan version eller en äldre nyckelepok får 409 med den nuvarande versionen, så att klienten slår ihop journalerna (D-99); sparningens version i bidragsdelens associerade data är den version uppladdningen blir.
  Servern tar högst 1 MiB världsdel och 8 MiB bidragsdel, och kontrollerar bara att världsdelen laddas ([hotmodell](../security/cloud-save-threat-model.md)).
- TASK-020 och TASK-023 delar sparningen i världsdel och bidragsdel; TASK-019:s snapshot och fingeravtryck måste därför kunna laddas med bidragsdelen saknad, och spelardygnen före en ny nyckelepok räknas som stängda.
- Klienten får två nya native-delar: iCloud-nyckelringen och Block Store, vid sidan av hälsoadaptrarna (ADR-0007).
- Spelare utan konto har ingen molnbackup.
  Spelet föreslår inloggning i lugna lägen, till exempel efter turen, men kräver den aldrig (ACC-04, HLT-12).
- DPIA:n i M6 utgår från att kontodata och krypterade bidrag lagras hos produktägaren fram till beta och hos Microsoft i Sverige därefter, med backup hos ett EU-bolag som personuppgiftsbiträde.
- Integritetspolicyn säger att servern aldrig kan läsa bidragen, men att appen själv hanterar dem i klartext på enheten.
- Kompletterar [ADR-0001](./0001-local-world-server-for-shared-state.md) (servern äger bara det delade), [ADR-0002](./0002-unity-csharp-shared-simulation.md) (ASP.NET Core) och [ADR-0006](./0006-saves-are-migrated-snapshots.md) (sparningens format och migrationer).

## Källor

Kontrollerade 2026-10-18.

- Apple: [App Review Guidelines 4.8 och 5.1.1(v)](https://developer.apple.com/app-store/review/guidelines/), [radering av konto i appen](https://developer.apple.com/support/offering-account-deletion-in-your-app/), [återkalla token](https://developer.apple.com/documentation/signinwithapplerestapi/revoke-tokens), [iCloud-nyckelringens synk](https://support.apple.com/guide/security/secure-keychain-syncing-sec0a319b35f/web).
- Google: [radering av konto i Play](https://support.google.com/googleplay/android-developer/answer/13327111), [verifiera Googles ID-token](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token), [Block Store](https://developer.android.com/identity/block-store).
- Azure: [Container Apps-priser](https://azure.microsoft.com/en-us/pricing/details/container-apps/), [retail-priser för Sweden Central](https://prices.azure.com/api/retail/prices), [backup och återställning i PostgreSQL Flexible Server](https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-backup-restore), [EU Data Boundary](https://blogs.microsoft.com/on-the-issues/2025/02/26/microsoft-completes-landmark-eu-data-boundary-offering-enhanced-data-residency-and-transparency/).
- Andra leverantörer: [AWS App Runner stänger för nya kunder](https://docs.aws.amazon.com/apprunner/latest/dg/apprunner-availability-change.html), [UpCloud Stockholm](https://upcloud.com/blog/upcloud-launches-swedish-dc/), [Scaleways tillgänglighet](https://www.scaleway.com/en/docs/account/reference-content/products-availability), [Cloud Run-platser](https://docs.cloud.google.com/run/docs/locations).
- Inloggningstjänster: [Firebase och var data behandlas](https://firebase.google.com/support/privacy), [Unity Authentication FAQ](https://docs.unity.com/en-us/authentication/faq), [Entra External ID-priser](https://learn.microsoft.com/en-us/entra/external-id/external-identities-pricing), [Identity API-token i ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization).
- Kryptering: [libsodium om XChaCha20-Poly1305](https://doc.libsodium.org/secret-key_cryptography/aead), [BouncyCastle.Cryptography](https://www.nuget.org/packages/BouncyCastle.Cryptography), [`AesGcm` i Unitys Mono](https://github.com/Unity-Technologies/mono/blob/unity-main/mcs/class/corlib/corefx/AesGcm.cs), [EDPB:s riktlinjer 01/2025 om pseudonymisering](https://www.edpb.europa.eu/system/files/2025-01/edpb_guidelines_202501_pseudonymisation_en.pdf), [WhatsApps krypterade backuper](https://www.whatsapp.com/security/WhatsApp_Security_Encrypted_Backups_Whitepaper.pdf).
