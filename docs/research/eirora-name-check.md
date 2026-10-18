# Namnkontroll av "Eirora"

> **Status:** Underlag, inte beslut · **Datum:** 2026-10-18  
> **Ärende:** [TASK-025 (#40)](https://github.com/Tiico/eirora/issues/40) · **Milstolpe:** M2 punkt 8 · **PRD:** D-10, avsnitt 1.3, O-11  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md)

## Sammanfattning

Inget hinder har hittats i varumärkesregistren.
EUIPO, PRV, USPTO och WIPO Global Brand Database har inget varumärke "EIRORA" [1][2][3][5].
TMview, som samlar ett åttiotal myndigheter, har ett enda: en turkisk ansökan "eirora design" i klass 4, 21 och 35 (ljus, porslin, detaljhandel), alltså inte i spelklasserna [4].
De närmaste levande märkena i klass 9, 28 och 41 är EURORA (EU-märke för programvara, och ett amerikanskt märke för dockor), IRORA, AIRORA, EIRA och EIR-märken, alla för andra varor eller tjänster än spel; AURORA är mycket vanligt och ligger ljudmässigt nära.
Ingen app eller inget spel heter Eirora i App Store, Google Play, Steam eller itch.io [6][7][8][9].
Domänerna eirora.com och eirora.org är registrerade av okänd innehavare och parkerade hos GoDaddy, medan eirora.se, .net, .io, .app, .game, .gg, eiroragame.com och playeirora.com inte är registrerade [10]–[15].
Handtaget "eirora" är upptaget på YouTube, TikTok, Twitch, Facebook och GitHub, oftast av små eller inaktiva konton; "eiroragame" verkar ledigt på de plattformar som gick att kontrollera [16]–[23].

**Rekommendation: säkra namnet.**
Före butikssidan: registrera de lediga domänerna och handtagen "eiroragame", låt en varumärkesjurist titta på EURORA-märkena och lämna in en EU-varumärkesansökan i klass 9 och 41 (och 28 om merchandise är aktuellt) innan butikssidan blir offentlig.
Detaljer i [Rekommendation](#rekommendation).

## Metod och avgränsning

Rapporten svarar på O-11 i PRD ("Kontrollera före butikssida").
Den är en **faktainsamling, inte en juridisk bedömning**.
Frågan om förväxlingsrisk avgörs av en varumärkesjurist eller av myndigheten vid invändning, inte av en sökning.

Alla sökningar gjordes 2026-10-18 utan inloggning, dels med `curl`, WebFetch och WebSearch, dels i en webbläsare för de register som blockerar skriptade anrop.
För USPTO och PRV frågades först samma bakomliggande sökgränssnitt som myndigheternas egna söksidor anropar; resultatet för EIRORA bekräftades sedan i söksidorna tmsearch.uspto.gov och search.prv.se.
EUIPO eSearch plus, TMview och WIPO Global Brand Database söktes i webbläsaren; i TMview användes sök-API:t som webbsidan själv anropar, för att kunna filtrera på klass och status.
Varje nollresultat kontrollerades mot en sökning som ska ge träffar (till exempel EURORA, eller en annan sökstrategi i samma register), så att "inga träffar" inte beror på en trasig sökning.
Varianterna som söktes var EIRORA, EIRA, EIROA, EYRORA, AIRORA, IRORA, EIR, EIRRA, EIORA samt jokerteckenformer, i klasserna 9 (programvara, appar, spel), 28 (spel och leksaker), 41 (underhållning, onlinespel) och för hälsovinkeln 42 och 44.
Där en plattform blockerade kontrollen står det "ej avgjort"; ingen sökning redovisas som noll träffar om resultatet inte syntes.
Plattformskontroller av handtag gjordes mot en påhittad kontrollanvändare (`zqxwv8817kk`) för att skilja "finns inte" från "sidan säger alltid samma sak".
Personnamn på sökande och innehavare återges inte.

## Varumärken

| Källa | Sökning (2026-10-18) | Klasser | Resultat |
|---|---|---|---|
| EUIPO eSearch plus [3] | Enkel sökning `eirora` (innehåller) | alla | **EIRORA: 0 träffar** (kontroll: `eurora` gav 40) |
| TMview, tmdn.org [4] | Ord `eirora`; innehåller `eirora`; fuzzy `eirora`; ord `eir` och `aurora` filtrerat på EUIPO, PRV, USPTO och WIPO, klass 9/28/41, status ansökt eller registrerat | alla; ett åttiotal myndigheter, bland dem EUIPO, PRV, USPTO och WIPO (Madrid) | **Ord: 1 träff**, "eirora design" (Turkiet, klass 4, 21, 35). Innehåller: dessutom "EIRORAC" (Japan, ansökt 2026, klass 25). Fuzzy: 215 träffar, närmast se nedan. `eir`: 11, `aurora`: 298 |
| WIPO Global Brand Database [5] | Varumärkesnamn `eirora` med strategierna Embedded, Phonetic och Fuzzy | alla | **Embedded: 0 träffar.** Phonetic: 32 434 träffar, de första är AURORA-märken. Fuzzy: 1 654 träffar, inga EIRORA |
| USPTO, tmsearch.uspto.gov [1] | Söksidan: ordmärke `eirora`, levande och döda (kontroll: `eurora` gav 2). Sökgränssnittet: `query_string` på `wordmark`: `eirora`, `eirora*`, `*irora`, `eira`, `eiroa`, `eyrora`, `airora`, `eir`, `eirra`, `eiora`, `eir*ra`, `eyr*ra`, `aurora` | alla; levande märken i 9/28/41/42/44 redovisas | **EIRORA: 0 träffar.** Närmast: se tabellen nedan |
| PRV, search.prv.se [2] | Söksidan: `eirora` och `eir`. Sökgränssnittet (`simpleSearchText`): `eirora`, `eiror*`, `*irora`, `eira`, `eiroa`, `eyrora`, `airora`, `irora`, `eir`, `eir aurora`, `aurora` | alla; svenska nationella märken och internationella registreringar som designerar Sverige | **EIRORA: 0 träffar**, `eiror*`: 0. Jokertecken fungerar (`auror*` gav 329). `eir` gav 8 märken, inget levande i 9/28/41. Närmast: se nedan |

PRV:s databas täcker inte EU-varumärken, som också gäller i Sverige; därför behövs både PRV- och EUIPO-sökningen.
WIPO Global Brand Database visade inte den turkiska ansökan som TMview har, så registren täcker inte varandra helt.

### Närmaste träffar i EUIPO och TMview [3][4]

Urval ur fuzzy-sökningen i TMview: levande märken (ansökta eller registrerade) i klass 9, 28, 41, 42 eller 44.

| Märke | Register, nummer | Klasser | Status | Varor/tjänster i korthet |
|---|---|---|---|---|
| EURORA | EUIPO 018305349 (även WIPO och Storbritannien) | 9, 35, 36, 39, 42 | Registrerat | Tullhantering och logistik; klass 9 är brett skriven ("programvara, nedladdningsbara programvaruapplikationer") |
| EURORA | USPTO 90756655 | 28 | Registrerat | Dockor och lekset |
| EIRONA | Tyskland 302026107215.8 | 12, 25, 28 | Registrerat 2026 | Cyklar, cykelkläder, leksaker |
| AIRORA | Storbritannien och WIPO | 9, 11, 37, 40 | Registrerat | Luftrening |
| IRORA | Sydkorea och WIPO; Japan | 9; 28 | Registrerat | Glasögon; träningsredskap och leksaker |
| EXRORA | Thailand | 28, 35 | Ansökt 2025 | – |
| HÍRÓRA | Ungern | 9, 38, 41 | Registrerat | Innehavare ATV (tv-kanal); varorna inte genomgångna |
| ELIORA | Japan | 41, 44 | Ansökt 2026 | – |
| EIR | EUIPO (två märken) | 3, 5, 9, 21, 35, 38, 44 | Registrerat | Innehavare Orkla Home & Personal Care; varorna inte genomgångna |
| BRAGI EIR | EUIPO och WIPO | 9 | Registrerat | Innehavare Bragi GmbH; varorna inte genomgångna |
| EIR SMART DEVICES | EUIPO | 9, 11, 42 | Ansökt | Varorna inte genomgångna |
| AURORA | EUIPO, WIPO och USPTO (298 träffar) | 9, 28, 41 | Registrerade | Innehavare bland annat Dell (klass 9, 28, 41), adp Merkur (9, 28), Aurora World (28) och Jyllands-Posten (9, 16, 38, 41); varorna inte genomgångna |

Varken EUIPO eller TMview har något levande märke med namnet EIROA, EYRORA eller EIRORA i spelklasserna.

### Närmaste träffar i USPTO [1]

| Märke | Nr (serie/reg.) | Klasser | Status | Varor/tjänster i korthet |
|---|---|---|---|---|
| IRORA | 79312420 / 6823062 | 9 | Registrerat (internationell registrering, Sydkorea) | Glasögon, linser, programvara för syntest |
| AIRORA | 79341022 / 7265242 | 9, 11, 37, 40 | Registrerat (internationell registrering, Storbritannien) | Luftrening |
| EIRA | 98119283 / 7462202 | 9 | Registrerat | Datorer, skärmar, pekskärmar |
| EIRA | 79451441 | 8, 11, 20, 21, 31, 37, 44 | Under handläggning | Terapi och inredningsvaror |
| DEMIRORA | 97425650 / 7104581 | 28 | Registrerat | Ballonger, festartiklar, sällskapsspel |
| VIRORA MAHJONG | 99385455 / 8220454 | 28 | Registrerat | Brädspel, mahjong |
| EIR | 88875575, 77655139 | 41 | Registrerade | Utbildning och underhållning (två innehavare) |
| EIR | 99511933 | 9, 42, 45 | Notice of allowance | Programvara |
| EIR m.fl. | flera | 42, 44 | Registrerade eller under handläggning | Hälso- och vårdtjänster |
| AURORA | 1 598 träffar totalt | – | – | Ordet är mycket vanligt; bland de 500 första fanns 51 levande märken i 9/28/41, varav 2 nämner spel (IGT Global Solutions, Aurora First) |

EIROA finns bara som levande märke i klass 34 (cigarrer).

### Närmaste träffar i PRV [2]

| Märke | Ansökan / reg. | Klasser | Status |
|---|---|---|---|
| Eira Eyewear | 2021-00451 / 613338 | 9 | Registrerat |
| Studio Eira | 2025-03880 / 637645 | 35, 42 | Registrerat |
| EIRA psykoterapi | 2026-04000 / 643137 | 44 | Registrerat |
| EIRA PSYCHOTHERAPY … | 2025-01458 / 635749 | 44 | Registrerat |
| EIRA | 2026-02941 | 42 | Avskriven (pre-dismissed) |
| WORLD OF EIR | – | 1, 3 | Registrerat |
| EIR of Sweden | 2015-00670 | 41, 42 | Avskriven |

Inga träffar för EIROA, EYRORA, AIRORA eller IRORA i PRV.

**Bedömning utan juridisk verkan:** de närmaste märkena skiljer sig i stavning, uttal eller varor (tullprogramvara, dockor, glasögon, luftrening, datorer, terapi).
Det som mest förtjänar en jurists blick är:

- **EURORA** (EUIPO 018305349): skiljer sig från EIRORA med en bokstav, och klass 9 är skriven brett ("programvara, nedladdningsbara programvaruapplikationer") trots att verksamheten är tullhantering.
  Det amerikanska EURORA-märket gäller dockor i klass 28.
- **AURORA:** WIPO:s fonetiska sökning behandlar Eirora som nära Aurora, och AURORA är registrerat i klass 9, 28 och 41 av flera innehavare.
  Ordet är så vanligt att varje enskilt märke troligen har ett smalt skydd, men det är en bedömning för en jurist.
- **EIR, IRORA och AIRORA:** "Eirora" kan på engelska uttalas nära "Airora"; EIR-märken finns i klass 9 och 41 (bland annat den irländska teleoperatören eir [24]).

## Appbutiker

| Källa | Sökning (2026-10-18) | Resultat |
|---|---|---|
| App Store, iTunes Search API [6] | `term=eirora&entity=software`, länder se, us, gb; dessutom de, fr, no, fi, dk med `software,iPadSoftware,macSoftware` | **Ingen app heter Eirora.** se: 0 träffar; us och gb: 4 orelaterade (t.ex. "EirMed Companion", "Efferon"); övriga länder: 0 |
| App Store [6] | `eira`, `eiroa`, `eyrora`, `airora`, `eir aurora` (se, us, gb) | `eyrora`: 0. `eira`: 22–28 appar, flera inom hälsa ("Eira: Calm & focus", "Weight Loss Hypnosis: Eira", "Eira - Budget & Money Coach"). `airora` och `eir aurora` ger norrskensappar |
| Google Play [7] | `play.google.com/store/search?q=eirora&c=apps`, `gl=se` och `gl=us`; även `eirora game` och `eyrora` | **Ingen app heter Eirora**, och inget paket-id innehåller "eirora". Närmast: "Eirra", "EIRRO", "irora" (`com.watech.irora`), "Hrora". `com.eirora` och `com.tiico.eirora` ger 404 |
| Steam [8] | `store.steampowered.com/api/storesearch/?term=eirora` och `eyrora` (cc=SE) | 0 träffar |
| itch.io [9] | `itch.io/search?q=eirora`; `eirora.itch.io` | Inga spel med namnet (9 orelaterade träffar); `eirora.itch.io` ger 404 |

## Domäner

RDAP 404 eller WHOIS "not found" betyder ej registrerad.
Ej registrerad betyder inte säkert ledig till normalpris (premium- eller reserverade namn); det syns först hos registraren.

| Domän | Källa (2026-10-18) | Resultat |
|---|---|---|
| eirora.com | RDAP Verisign via rdap.org [10] | **Registrerad** 2025-11-20 hos GoDaddy, utgår 2026-11-20, innehavare dold. Visar GoDaddys parkeringssida [10][25] |
| eirora.org | RDAP via rdap.org [10] | **Registrerad** 2025-11-20 hos GoDaddy, utgår 2026-11-20, parkerad |
| eirora.net | RDAP Verisign [11] | Ej registrerad (404) |
| eirora.se | WHOIS whois.iis.se [12] | Ej registrerad ("not found"); Internetstiftelsen har ingen RDAP-tjänst i rdap.org |
| eirora.io | RDAP Identity Digital och WHOIS whois.nic.io [13] | Ej registrerad (404, "Domain not found") |
| eirora.app | RDAP Google Registry [14] | Ej registrerad (404) |
| eirora.game | RDAP CentralNic [14] | Ej registrerad (404) |
| eirora.gg | WHOIS whois.gg [13] | Ej registrerad ("NOT FOUND") |
| eiroragame.com | RDAP Verisign [11] | Ej registrerad (404) |
| playeirora.com | RDAP Verisign [11] | Ej registrerad (404) |
| eiroragame.se | WHOIS whois.iis.se [12] | Ej registrerad |

Wayback Machine har en enda ögonblicksbild av eirora.com, 2025-07-11, och den visar också en parkeringssida [15].
Domänen har alltså funnits före nuvarande registrering och verkar inte ha använts för en verksamhet.
En sökmotorsammanfattning beskrev eirora.com som en B2B-byrå i London, men det gick inte att bekräfta: sidan är parkerad och Companies House har inget bolag som heter Eirora [26].
Det är **overifierat**.

## Sociala plattformar

| Plattform | Kontrollerad adress (2026-10-18) | "eirora" | "eiroragame" / "playeirora" |
|---|---|---|---|
| Instagram | instagram.com/eirora/ och profil-API:t [16] | **Ej avgjort** (sidan ser likadan ut som för kontrollanvändaren; API:t svarade 429) | Ej avgjort |
| Threads | threads.com/@eirora [16] | **Ej avgjort** (inloggningsvägg); handtaget följer Instagram | Ej avgjort |
| TikTok | tiktok.com/@eirora [17] | **Upptaget**: konto `eirora`, 6 följare, statuskod 10222 (troligen privat) | `eiroragame`: ledigt (statuskod 10221 "finns inte", samma som kontrollen) |
| X | x.com/eirora [18] | Inget aktivt konto ("User Profile Not Found", 404; `x.com/github` ger profilen). Kan vara spärrat eller vilande; ledigheten syns först vid registrering | `eiroragame`: samma, inget konto |
| YouTube | youtube.com/@eirora [19] | **Upptaget**: japansk kanal "えーロラチャンネル" (`UClA2yswYxmJfahWDCjObgBQ`) | `@eiroragame` och `@playeirora`: 404, lediga |
| Facebook | facebook.com/eirora [20] | **Upptaget**: sidan "Eirora", 99 följare, "Relax. Reflect. Reconnect." med AI-skapade naturfilmer | `eiroragame`: "content isn't available", samma som kontrollen; troligen ledigt |
| Reddit | r/eirora och u/eirora [21] | **Ej avgjort** (Reddit blockerade alla oinloggade anrop med 403) | Ej avgjort |
| Discord | discord.com/api/v10/invites/eirora [22] | Ingen inbjudan med koden ("Unknown Invite"). En vanity-adress kräver en server med boostnivå 3, så det går inte att reservera nu | – |
| Bluesky | resolveHandle för `eirora.bsky.social` [22] | Ledigt ("Unable to resolve handle"). Handtaget kan också bli `eirora.se` om domänen registreras | – |
| Twitch | twitch.tv/eirora [23] | **Upptaget**: kanalen "Eirora" finns (sidtiteln "Eirora - Twitch"; kontrollen ger bara "Twitch"); aktivitet okänd | `eiroragame`: ledigt (samma som kontrollen) |
| GitHub | api.github.com/users/eirora [23] | **Upptaget**: användaren `EIrora`, skapad 2023-09-14, 0 repon, 0 följare | `eiroragame` och `playeirora`: lediga (404). Inget organisationskonto `eirora` |

Facebook-sidan "Eirora" ligger nära spelets välmåendevinkel (avkoppling och natur), men är liten och verkar inte ha något varumärke i de register som gick att söka.

## Övrigt

- **Webben:** en exakt sökning på "Eirora" gav inget företag, spel eller produkt med namnet [26].
  Träffarna var felstavningar av cigarrmärket Eiroa (C.L.E. Cigar Company), en medeltida irländsk person "mac Eirora" i ett släktforskningsregister och eirora.com.
- **Bolag:** Companies House (Storbritannien) har inget bolag "Eirora" [26].
  Bolagsverket i Sverige är inte kontrollerat.
- **Betydelse:** ingen ordbok eller ordlista i sökningen har "Eirora" som ord på något språk [27].
  Delarna har neutral eller positiv betydelse: Eir är fornnordiska för "hjälp, nåd" och namnet på en gudinna knuten till läkekonst [27].
  Inget negativt eller anstötligt i andra språk hittades, men sökningen täcker inte alla språk; kontrollera gärna med modersmålstalare på målmarknaderna (D-31) innan lansering.
- **Uttal:** på svenska blir det naturligt "ej-RO-ra", på engelska kan det bli "ai-RO-ra" eller "i-RO-ra", nära de registrerade AIRORA och IRORA (andra varor).
  Detta är en iakttagelse, inte en sökning.

## Rekommendation

**Säkra namnet.**
Inget i sökningarna talar för att byta: inget register har ett EIRORA-märke i spelklasserna, och ingen app eller inget spel heter Eirora.
Namnet är ändå inte skyddat förrän det är registrerat, .com är upptagen och några liknande märken (EURORA, AURORA) bör bedömas av en jurist innan ansökan.

1. **Kort juristgranskning (före ansökan):** låt en varumärkesjurist titta på EURORA-märkena, AURORA i klass 9, 28 och 41 och varu- och tjänsteförteckningen.
   Sökningarna här är underlaget; juristen avgör förväxlingsrisken.
2. **Domäner nu:** registrera eirora.se, eirora.app och eirora.game; gärna även eirora.net, eirora.io, eirora.gg, eiroragame.com och playeirora.com som skydd.
   Använd eirora.se eller eirora.app som spelets webbplats och för butikernas utvecklar- och integritetssidor.
3. **eirora.com:** bevaka utgången 2026-11-20 (backorder hos en registrar).
   Ett köp av den nuvarande innehavaren är möjligt men inte nödvändigt.
4. **Handtag nu:** registrera `eiroragame` på Instagram (om ledigt), TikTok, X, YouTube, Facebook, Twitch och GitHub (organisation), samt `eirora.bsky.social` eller domänhandtaget på Bluesky.
   Prova `eirora` på X och Instagram vid registreringen; kontrollera r/eirora inloggad på Reddit och skapa den om den är ledig.
5. **EU-varumärke före butikssidan:** ansök om ordmärket EIRORA hos EUIPO.
   Grundavgiften online är 850 € för en klass, 50 € för andra klassen och 150 € för varje klass därefter [28].
   Klass 9 och 41 kostar alltså 900 €; med 28 blir det 1 050 €.
   Klass 42 och 44 behövs bara om spelet säljs som hälso- eller molntjänst, vilket det inte gör enligt PRD; de är därför inte med i rekommendationen.
   Ett EU-varumärke gäller i Sverige, så en separat PRV-ansökan (2 700 kr för en klass, 1 000 kr per extra klass via e-tjänst [29]) behövs inte.
   EUIPO:s SME Fund-kupong för varumärken är slut för 2026 [30].
6. **Tidpunkt:** lämna in ansökan innan butikssidan (förregistrering på Google Play eller App Store) blir offentlig, så att ansökningsdagen ligger före första offentliga användningen.
   Enligt Pariskonventionen kan samma prioritetsdag användas för ansökningar i andra länder, till exempel USA och Storbritannien, inom sex månader från EU-ansökan (allmän regel, inte kontrollerad här; bekräfta med jurist).
7. **Butiksnamn:** använd gärna en undertitel i butikerna, till exempel "Eirora: A world worth growing", så att listningen skiljer sig från de många "Eira"-apparna inom hälsa.

## När en varumärkesjurist bör konsulteras

- **Före EU-ansökan**, för att bedöma förväxlingsrisken mot EURORA (EUIPO 018305349, bred klass 9) och AURORA-märkena i klass 9, 28 och 41.
- Om en ny sökning hittar ett **levande märke som är identiskt eller likt** EIRORA (till exempel EIRORA, EIROA, AIRORA, IRORA eller ett EIR-märke) i klass 9, 28 eller 41.
- Om den turkiska ansökan "eirora design" eller den japanska "EIRORAC" breddas till spelklasserna, eller om spelet ska lanseras i Turkiet eller Japan.
- Innan **varu- och tjänsteförteckningen** skrivs, om det är osäkert vilka termer som täcker spelet, hälsodatan och prenumerationen (D-25).
- Om ansökan får en **invändning** eller ett föreläggande från EUIPO.
- Om någon, till exempel innehavaren av eirora.com eller Facebook-sidan "Eirora", **hör av sig** om namnet.
- Före ansökningar **utanför EU** (USA, Storbritannien) eller innan pengar läggs på att köpa eirora.com.
- Om ett namnbyte övervägs: låt en jurist göra en fullständig sökning på det nya namnet innan det beslutas.

## Källor

Alla källor hämtades 2026-10-18.

1. USPTO Trademark Search, https://tmsearch.uspto.gov/ — söksidan (Wordmark, Live och Dead) med `eirora` och kontrollen `eurora`; bakomliggande sökgränssnitt `POST https://tmsearch.uspto.gov/prod-stage-v1-0-0/tmsearch` med `query_string` på `wordmark` och `wordmarkPseudoText`; frågor enligt tabellen.
2. PRV, Sök i PRV:s databaser (beta), https://search.prv.se/#/trademark — söksidan med `eirora` och `eir` (databasen uppdaterad 2026-10-18 05:20); bakomliggande sökgränssnitt `POST https://dv-search-api.prv.se/searchtrademark/tmsimplesearch/` med `simpleSearchText`; länkad från https://www.prv.se/sv/varumarke/forbered-dig-infor-varumarkesansokan/databaser/.
3. EUIPO eSearch plus, https://euipo.europa.eu/eSearch/#basic/1+1+1+1/100+100+100+100/eirora (webbläsare; kontrollen `…/eurora` gav 40 varumärken).
4. TMview, https://www.tmdn.org/tmview/#/tmview/results?page=1&pageSize=30&criteria=W&basicSearch=eirora (webbläsare) och sök-API:t som sidan anropar, `POST https://www.tmdn.org/tmview/api/search/results` med `criteria` W, C och F, `basicSearch` `eirora`, `eir` och `aurora`, samt filtren `fOffices` EM, SE, WO, US, `fNiceClass` 9, 28, 41 och `fTMStatus` Filed, Registered; detaljer via `GET /tmview/api/trademark/detail/{ST13}`.
5. WIPO Global Brand Database, https://branddb.wipo.int/en/similarname (webbläsare), varumärkesnamn `eirora` med strategierna Embedded, Phonetic och Fuzzy.
6. Apple iTunes Search API, https://itunes.apple.com/search?term=eirora&entity=software&country=se (samt `us`, `gb`, `de`, `fr`, `no`, `fi`, `dk` och termerna `eira`, `eiroa`, `eyrora`, `airora`, `eir aurora`).
7. Google Play, https://play.google.com/store/search?q=eirora&c=apps&hl=en&gl=se (samt `gl=us`, `q=eirora game`, `q=eyrora`); https://play.google.com/store/apps/details?id=com.eirora.
8. Steam, https://store.steampowered.com/api/storesearch/?term=eirora&l=english&cc=SE (samt `term=eyrora`).
9. itch.io, https://itch.io/search?q=eirora och https://eirora.itch.io/.
10. RDAP, https://rdap.org/domain/eirora.com och https://rdap.org/domain/eirora.org.
11. RDAP Verisign, https://rdap.verisign.com/net/v1/domain/eirora.net, https://rdap.verisign.com/com/v1/domain/eiroragame.com, https://rdap.verisign.com/com/v1/domain/playeirora.com.
12. Internetstiftelsen WHOIS, whois.iis.se port 43, frågor `eirora.se` och `eiroragame.se`; https://rdap.org/domain/eirora.se ("No RDAP service is available").
13. RDAP Identity Digital, https://rdap.identitydigital.services/rdap/domain/eirora.io; WHOIS whois.nic.io och whois.gg port 43.
14. RDAP Google Registry, https://pubapi.registry.google/rdap/domain/eirora.app; RDAP CentralNic, https://rdap.centralnic.com/game/domain/eirora.game.
15. Wayback Machine, https://web.archive.org/cdx/search/cdx?url=eirora.com och https://web.archive.org/web/20250711183620/http://eirora.com/.
16. Instagram, https://www.instagram.com/eirora/ och https://www.instagram.com/api/v1/users/web_profile_info/?username=eirora; Threads, https://www.threads.com/@eirora.
17. TikTok, https://www.tiktok.com/@eirora och https://www.tiktok.com/@eiroragame.
18. X, https://x.com/eirora, https://x.com/eiroragame och kontrollen https://x.com/github.
19. YouTube, https://www.youtube.com/@eirora, https://www.youtube.com/@eiroragame, https://www.youtube.com/@playeirora.
20. Facebook, https://www.facebook.com/eirora och https://www.facebook.com/eiroragame.
21. Reddit, https://www.reddit.com/r/eirora/, https://www.reddit.com/user/eirora/ och https://www.reddit.com/api/username_available.json?user=eirora (403).
22. Discord, https://discord.com/api/v10/invites/eirora; Bluesky, https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle?handle=eirora.bsky.social.
23. Twitch, https://www.twitch.tv/eirora och https://www.twitch.tv/eiroragame; GitHub, https://api.github.com/users/eirora, https://api.github.com/orgs/eirora, https://api.github.com/users/eiroragame, https://api.github.com/users/playeirora.
24. Adworld.ie om teleoperatören eir, https://www.adworld.ie/2016/07/15/decision-eir-account-two-weeks/ (WebSearch `"Eirora" company London agency`).
25. eirora.com och https://eirora.com/lander (GoDaddy-parkering, `ap: "parking"`).
26. WebSearch `"Eirora"` (utökad) och `Eirora game OR app OR studio`; Companies House, https://find-and-update.company-information.service.gov.uk/search/companies?q=eirora ("No results found").
27. WebSearch `"Eirora" meaning word`; Wikipedia, https://en.wikipedia.org/wiki/Eir.
28. EUIPO, Fees and payments, https://www.euipo.europa.eu/en/trade-marks/before-applying/fees-payments.
29. PRV, Avgifter för varumärke, https://www.prv.se/sv/varumarke/forbered-dig-infor-varumarkesansokan/avgifter-for-varumarke/.
30. EUIPO, SME Fund 2026, https://www.euipo.europa.eu/en/sme-corner/sme-fund/2026 ("Voucher 2 (Trade marks and Designs) are unavailable for new applications").
