# EIRORA — Product Requirements Document (PRD)

> **Dokumentstatus:** Utkast v0.3 — reviderat efter produktgrillning och namnbeslut  
> **Datum:** 2026-10-17  
> **Produktnamn:** Eirora (tekniskt projektnamn `eirora`; varumärkes- och butikskontroll återstår, O-11)  
> **Slogan:** *A world worth growing.*  
> **Målrelease:** Version 1.0 — fullvärdigt kommersiellt mobilspel, samtidigt på Android och iOS  
> **Relaterade dokument:** [ROADMAP.md](./ROADMAP.md) · [GLOSSARY.md](./GLOSSARY.md) · [docs/adr/](./docs/adr/)

## Läsanvisning och beslutsstatus

- **[BESLUTAT]**: uttryckligen överenskommet med produktägaren.
- **[FÖRSLAG]**: genomarbetat förslag; behöver godkännas.
- **[ÖPPET]**: kräver analys eller produktbeslut innan beroende funktioner implementeras.

**Styrande regler för nästa agent:**

1. Ändra inte ett [BESLUTAT] utan att först lyfta det med produktägaren.
2. Markera nya antaganden som [FÖRSLAG] och logga beslut i avsnitt 12.
3. **Utvecklingstid är aldrig en faktor** (D-09).
   Arbetet utförs av AI-agenter; välj alltid den bästa lösningen, aldrig den snabbaste.
   Det som däremot *inte* kan kortas är kalendertid för speltest i riktiga dygn, produktägarens egen tid för beslut och test på fysiska enheter, samt butiksgranskningar.
   Sådant styr *ordningen*, aldrig *omfattningen*.
4. Varje funktion ska ändå förtjäna sin plats i spelet.
5. Använd termerna i [GLOSSARY.md](./GLOSSARY.md).

---

## 1. Sammanfattning

Eirora är ett **passivt, kartbaserat strategi- och civilisationsspel där din värld speglar hur du tar hand om dig själv.**
Rörelse, träning och sömn omvandlas till tre olika **krafter** som spelaren strategiskt riktar för att utforska, bygga och planera i en ögrupp på en hexkarta.
Ekonomin (produktionskedjor och logistik i Anno-anda) lever sitt eget liv; egenvården avgör hur långt och hur snabbt världen kan växa.
Precis som kroppen tackar dig, tackar din värld dig.

Spelet har **inget slut**.
Spelaren får en guidad tur och sedan introduceras nya system löpande genom **epoker** om ungefär 6–10 veckor.
Vänner kan bjuda in varandra till **grupper** om 2–6 spelare vars världar delar en gemensam **arkipelag**, med sjövägar, handel och gemensamma byggen.

**Den avgörande särskiljande mekaniken:** när byborna går till vila **låses aktivt spelande** under spelarens viloperiod.
Spelaren kan se sin sovande värld, men inte bygga, handla eller ge order.
Spelet uppmuntrar återhämtning i stället för sena sessioner, streaks eller nattliga notiser.

### 1.1 Fastställda beslut

| ID | Beslut | Status |
|---|---|---|
| D-01 | Huvudgenre: **strategi och civilisationsbyggande** | [BESLUTAT] |
| D-02 | Flera hälsosamma vanor bidrar till progression | [BESLUTAT] |
| D-03 | Aktivt spelande är begränsat; världen lever framför allt passivt. Begränsningen sker naturligt via krafterna (D-23) | [BESLUTAT] |
| D-04 | V1 använder **fiktiv, procedurgenererad karta** | [BESLUTAT] |
| D-05 | Arkitekturen ska senare kunna generera spelvärld utifrån verklig geografi (V2) | [BESLUTAT] |
| D-06 | Under viloperioden är spelet **låst för aktiva spelhandlingar** | [BESLUTAT] |
| D-07 | Byggande, ekonomi, logistik och utforskning är kärnan | [BESLUTAT] |
| D-08 | Inga straff för livet: ingen stressdriven bestraffning för vilodagar, sjukdom eller frånvaro | [BESLUTAT] |
| D-09 | **Utvecklingstid är aldrig en faktor**; V1 ska vara fullt genomarbetad | [BESLUTAT] |
| D-10 | Namn: **Eirora** (tekniskt `eirora`), slogan *A world worth growing.* och tema i nordisk mytologi; se avsnitt 1.3. Öar och sjöfart är kartans kärna; gryningen är det visuella och tematiska motivet | [BESLUTAT] |
| D-11 | Byggs av en person (produktägaren) med AI-agenter. **Kommersiell satsning** | [BESLUTAT] |
| D-12 | Spelkänsla: **Anno-ekonomi × Islanders beslutstäthet × Pikmin Blooms dygnsrytm** | [BESLUTAT] |
| D-13 | **Ingen kampanj och inget slut.** Guidad tur, löpande introduktion av system med tutorialelement, epoker om ~6–10 veckor; inget nollställs | [BESLUTAT] |
| D-14 | **Egenvård är motorn.** Världen speglar hur spelaren tar hand om sig själv | [BESLUTAT] |
| D-15 | V1-vanor: **rörelse/steg → Utforskning, träningspass → Byggkraft, sömn → Gryningskraft**. Spelaren väljer vart krafterna riktas | [BESLUTAT] |
| D-16 | Gryningskraft bygger på **sömnlängd med mjukt tak (~7–8 h)**. Ingen kvalitetspoäng, inget straff; saknad data ger neutral grundnivå | [BESLUTAT] |
| D-17 | Utan egenvård **står världen still men förfaller aldrig**; varm återkomst | [BESLUTAT] |
| D-18 | **Manuell inmatning tillåten för alla.** Bidrag till gruppen märks som verifierade eller egenrapporterade; egenrapporterade får lägre dagligt tak | [BESLUTAT] |
| D-19 | Grupper om **2–6 spelare, endast via inbjudan**. Ingen matchning med främlingar, ingen fritextchatt | [BESLUTAT] |
| D-20 | Gruppen delar en **gemensam arkipelag** med sjövägar, handel och gemensamma byggen. **Endast positiv koppling** | [BESLUTAT] |
| D-21 | Gruppmedlemmar kan dela **fulla hälsosiffror med samtycke per vana**. Ett läge med enbart bockar ska finnas som skyddsnät per plattform | [BESLUTAT] |
| D-22 | Vilolås: obligatoriskt, **6–10 h**, schemaändring gäller efter **24 h**, **ärligt skydd, inte fuskskydd** ([ADR-0003](./docs/adr/0003-rest-lock-is-honest-protection.md)). Byborna sover; **långsamma processer fortsätter** | [BESLUTAT] |
| D-23 | Aktivt spel begränsas **naturligt via krafterna**; att titta och planera är alltid gratis | [BESLUTAT] |
| D-24 | **Ingen GPS** i V1; endast stegmängd | [BESLUTAT] |
| D-25 | Intäkter: **gratis + kosmetik + supporter-prenumeration**. Aldrig kraft, tid eller vilolås | [BESLUTAT] |
| D-26 | **Android och iOS samtidigt vid release**; utveckling Android-först. iPhone anskaffas innan HealthKit-arbetet | [BESLUTAT] |
| D-27 | **Den egna världen simuleras lokalt; servern äger det delade** ([ADR-0001](./docs/adr/0001-local-world-server-for-shared-state.md)) | [BESLUTAT] |
| D-28 | **Unity 6 LTS + C#**, motoroberoende simuleringsbibliotek delat med **ASP.NET Core**-server ([ADR-0002](./docs/adr/0002-unity-csharp-shared-simulation.md)) | [BESLUTAT] |
| D-29 | **Low-poly 3D med lutad kamera. Visuell skönhet är ett uttalat kvalitetskrav** | [BESLUTAT] |
| D-30 | Åldersgräns **18+** | [BESLUTAT] |
| D-31 | **Engelska + svenska**, global release; lokalisering byggd för fler språk | [BESLUTAT] |
| D-32 | Arbetssätt: **kort design, sedan spelbar prototyp** på produktägarens Android som spelas dagligen | [BESLUTAT] |
| D-33 | Notiser: **få, valbara per typ, aldrig FOMO**; inga under viloperioden | [BESLUTAT] |
| D-34 | **Strategin kan hinna ikapp egenvården:** inom normalt spann ska måttlig egenvård med god strategi kunna mäta sig med hög egenvård och naiv strategi; vid samma strategi syns mer egenvård alltid i världen | [BESLUTAT] |
| D-35 | **Oanvända krafter sparas med mjukt tak** och förfaller aldrig | [BESLUTAT] |
| D-36 | **En korrigering nedåt av ett bidrag klipps vid noll:** kraftsaldot blir aldrig negativt och inget som redan hänt tas tillbaka | [BESLUTAT] |
| D-37 | **Viloschemat följer lokal tid:** 6–10 h gäller schemat på väggklockan; vid sommartidsövergång kan den verkliga längden bli 5–11 h | [BESLUTAT] |
| D-38 | **Rivning är ett uttryckligt kommando** med bekräftelse och det enda sättet något i världen kan minska | [BESLUTAT] |
| D-39 | **Ouppfyllda basbehov stoppar bara tillväxt;** bybor, byggnadsnivåer och grundproduktion minskar aldrig | [BESLUTAT] |
| D-40 | **Krafter räknas per spelardygn,** från gryning till gryning, inte per kalenderdygn | [BESLUTAT] |
| D-41 | **Aktiva minuter kan matas in som rörelse** och räknas om med 100 steg per minut, inom samma tak som steg | [BESLUTAT] |
| D-42 | **Ett träningspass räknas från 10 minuter;** längd och intensitet därutöver spelar ingen roll | [BESLUTAT] |
| D-43 | **Bostad finns både med och utan Byggkraft:** långhus kostar Byggkraft, stugan bara material, så tillväxten blockeras aldrig av en bortvald vana | [BESLUTAT] |
| D-44 | **LOG-06 är P0:** minimilager och prioriteringar behövs redan i prototypen | [BESLUTAT] |
| D-45 | **Startkrafter i den guidade turen:** 100 Utforskning, 100 Byggkraft, 40 Gryningskraft | [BESLUTAT] |
| D-46 | **Prototypdagboken är ett GitHub-ärende per vecka** med en kommentar per spelardygn; det bestående förs i lärdomsloggen i repot | [BESLUTAT] |
| D-47 | **Veckogenomgången görs första spelardygnet** i den nya veckan | [BESLUTAT] |
| D-48 | **Hypotes H5 Vilan känns som en gåva** prövas i prototypen tillsammans med H1–H4 | [BESLUTAT] |
| D-49 | **Första sessionen börjar med den guidade turen;** hälsokälla och viloperiod väljs efteråt | [BESLUTAT] |
| D-50 | **Den första viloperioden gäller direkt efter turen,** även vid installation under viloperioden | [BESLUTAT] |
| D-51 | **Det första spelardygnet börjar vid lokal midnatt** på installationsdagen, så att dagens egenvård räknas | [BESLUTAT] |
| D-52 | **Ingen återstartsbonus** efter frånvaro; världen och de sparade krafterna är mottagandet | [BESLUTAT] |
| D-53 | **Manuell inmatning för varje spelardygn sedan senaste besöket,** högst sju, likvärdigt med hälsokällans historik | [BESLUTAT] |
| D-54 | **Manuell inmatning är tillåten under vilan;** krafterna krediteras vid gryningen | [BESLUTAT] |
| D-55 | **Inga kosmetikaköp under vilan;** prenumerationen går alltid att hantera | [BESLUTAT] |
| D-56 | **Morgonnotisen** kommer bara när något nytt hänt, alltid tyst, och pausas efter två oöppnade i följd | [BESLUTAT] |
| D-57 | **Bidrag per spelardygn följer med i molnsparningen, end-to-end-krypterade;** servern kan aldrig läsa dem | [BESLUTAT] |
| D-58 | **18+ sköts av butikernas åldersklassning,** utan egen bekräftelse i spelet | [BESLUTAT] |
| D-59 | ~~**Art direction: riktning A Gryningsljus** med tydliga silhuetter från C och snidade gavlar och guldaccenter från B~~ | [ERSATT av D-61] |
| D-60 | **Guiden är en ljusgestalt inspirerad av Eir** som syns tydligast i gryningen | [BESLUTAT] |
| D-61 | **Art direction: Gryningsdiorama med Norrskensmytens natt.** Ljus, facetterad low-poly i falurött, björkvitt och havsturkos; under viloperioden dämpat norrsken, stjärnor och runglöd ([art-direction.md](./docs/design/art-direction.md)) | [BESLUTAT] |
| D-62 | **En korrigering nedåt drar aldrig av mer kraft än bidraget gav** under sparandets tak, för tillskott som gjorts före en senare spendering (förtydligar D-36, avgränsad av D-72) | [BESLUTAT] |
| D-63 | **Byggkraftens rullande vecka ger kurvans värde:** ett spelardygn med pass ger kurvan för passen under sju spelardygn minus det de sex föregående redan gav, aldrig under noll; mer träning ger aldrig mindre Byggkraft ([epoch-1.md](./docs/design/epoch-1.md#7-byggkraftens-rullande-vecka)) | [BESLUTAT] |
| D-64 | **Epok 1:s milstolpe är att Vårdkasen tänds,** byggd i tre etapper (grunden, stommen, elden) med egna förutsättningar ([epoch-1.md](./docs/design/epoch-1.md)) | [BESLUTAT] |
| D-65 | **Det milstolpen kräver kan byggas med handkraft** utan Byggkraft på en fast tid: 7 spelardygn för brygga och väveri, 21 för varje etapp av Vårdkasen, så att en bortvald vana aldrig blockerar milstolpen (utvidgar D-43) | [BESLUTAT] |
| D-66 | **Forskning kostar ingen kraft,** bara varor och tid | [BESLUTAT] |
| D-67 | **Nästa epok börjar vid gryningen efter milstolpen,** utan att något nollställs | [BESLUTAT] |
| D-68 | **Bara det som är markerat som manuellt inmatat är egenrapporterat:** `MANUAL_ENTRY` i Health Connect, `HKMetadataKeyWasUserEntered` i HealthKit och manuell inmatning i spelet; okänd inspelningsmetod (`UNKNOWN`) räknas som verifierat ([ADR-0007](./docs/adr/0007-own-health-connect-adapter.md)) | [BESLUTAT] |
| D-69 | **Sparandets mjuka tak räknas på kraftens läge,** allt tillskott före taket: saldot följer av läget, en korrigering nedåt tar tillbaka kraft i samma takt som den gavs och spendering sänker läget till det som är kvar, så att slutsumman aldrig beror på i vilken ordning bidrag kommer (förtydligar D-35 och D-36) | [BESLUTAT] |
| D-70 | **Hälsodata läses när spelaren öppnar appen;** spelet begär aldrig bakgrundsbehörighet, och krafterna räknas fram ur hälsokällan vid öppning ([ADR-0007](./docs/adr/0007-own-health-connect-adapter.md)) | [BESLUTAT] |
| D-71 | **Sömnlängden för ett spelardygn är hälsokällans egen aggregerade sömn över natten fram till gryningen,** inte per kalenderdag och inte en egen regel över sessionerna; källorna slås ihop efter spelarens prioritet i Health Connect ([ADR-0007](./docs/adr/0007-own-health-connect-adapter.md)) | [BESLUTAT] |
| D-72 | **Gränsen i D-62 gäller bara när kraften har spenderats sedan bidraget krediterades;** utan spendering däremellan flyttar en korrigering bara tillbaka läget, så att Byggkraft som flyttas mellan spelardygn ger samma saldo oavsett i vilken ordning pass kommer (förtydligar D-62 och D-69) | [BESLUTAT] |

### 1.2 Produktpositionering [FÖRSLAG]

> **Ta hand om dig, så tar din värld form.
> Utforska på dagen.
> Bygg med dina vänner.
> Vila när din by vilar.**

Ett vackert, lugnt och djupt strategispel för vuxna som vill att deras vardagsvanor ska betyda något, utan tävling, skuld eller oändliga sessioner.

### 1.3 Varumärkesidentitet [BESLUTAT]

| Element | Värde |
|---|---|
| Produktnamn | **Eirora** |
| Tekniskt projektnamn | `eirora` |
| Officiell slogan | *A world worth growing.* |
| Kort produktbeskrivning | *A passive fantasy civilization-building game where your world grows as you care for yourself.* |
| Genre | Passive fantasy strategy / Civilization builder / Wellness-inspired gaming |
| Tema | Nordisk mytologi, personlig utveckling, välmående, utforskning och civilisationsbyggande |

Eirora är ett konstruerat fantasynamn inspirerat av två källor:

- **Eir:** en gestalt i nordisk mytologi som förknippas med läkande, omsorg och återhämtning.
- **Aurora:** gryning, ljus och en ny början.

Namnet ska förmedla nordisk mystik, livskraft, personlig utveckling och harmoni.
Det knyter ihop egenvård och vila som motor (D-14, D-22) med gryningen som visuellt motiv (D-10) och Gryningskraften (D-15).
Tidigare arbetsnamn (Arkipelagen, Verdawn) används inte längre.
Den visuella riktningen står i [art direction](./docs/design/art-direction.md) (D-61).

---

## 2. Produktmål, målgrupp och problem

### 2.1 Problem som ska lösas

Hälsoappar reducerar ofta beteenden till siffror och obrutna sviter.
Mobilspel premierar frekventa sessioner även när spelaren borde göra annat.
Eirora vänder på incitamenten: **ett meningsfullt strategispel som växer av ett hälsosamt liv och aktivt respekterar vila**.

### 2.2 Målgrupp

**Primär [BESLUTAT 18+]:** vuxna som gillar byggspel och långsiktig progression och vill göra egenvård mer engagerande, gärna tillsammans med vänner.

**Inkludering:** spelare med funktionsvariationer, skiftarbete, varierande aktivitetsnivåer eller utan sensor ska kunna spela fullt ut via manuell inmatning (D-18).
Spelet rangordnar aldrig kroppar.

### 2.3 Job-to-be-done

- När jag kommer tillbaka efter en dag vill jag **se att min värld har vuxit av det jag gjort för mig själv**.
- När jag har ett par minuter vill jag **fatta ett viktigt beslut om vart mina krafter ska**, inte klicka på hundra knappar.
- När jag rör på mig, tränar eller sover vill jag att det **bidrar positivt utan tvång**.
- När jag och mina vänner vill peppa varandra vill jag **se att vi bygger något tillsammans**, utan att någon dras ned.
- När min viloperiod börjar vill jag att spelet **tydligt stänger för kvällen**.

### 2.4 Mätbara mål [FÖRSLAG – slutvärden bestäms under alfa]

1. **Strategiskt djup:** testare kan beskriva minst två olika fungerande sätt att rikta sina krafter och bygga sin ekonomi.
2. **Låg tidsbörda:** 2–5 minuters aktivt spel per dag räcker; ingen daglig inloggning krävs.
3. **Positiv återkomst:** återkomst beskrivs som nyfikenhet, inte skuld.
4. **Spegeln känns:** testare upplever att världens tillväxt hänger ihop med deras egenvård.
5. **Socialt utan press:** grupper upplevs peppande, inte som övervakning.
6. **Teknisk robusthet:** inga förlorade sparningar, dubbla bidrag eller vägar runt vilolåset via normala flöden.
7. **Integritet:** spelaren förstår och kontrollerar varje datatyp som läses, delas eller raderas.

Guardrails: rapporterad stress, sömnförskjutning, upplevt grupptryck, spelminuter per dag.
Optimera inte ensidigt sessionstid eller retention.

---

## 3. Spelprinciper (icke förhandlingsbara)

1. **Egenvård är motorn, strategi är ratten.**
   Egenvården ger krafterna; spelarens beslut om var och hur de används avgör hur bra världen blir.
   Spelet får aldrig reduceras till en stegräknare med dekor.
   Se [kärnfantasin](./docs/design/core-fantasy.md).
2. **Passivt som standard:** planera kort, lev livet, återvänd till en meningsfull rapport.
3. **Vila är en spelregel:** vilolåset är verkligt, gäller alla och presenteras positivt.
4. **Inga straff för livet:** sjukdom, resor, dålig sömn och frånvaro ger aldrig förlust eller förfall.
5. **Olika vanor, olika krafter:** varje vana ger kvalitativt olika möjligheter, inte en gemensam valuta.
6. **Alla får vara med:** manuell inmatning är en fullvärdig väg; ingen sensor, smartklocka eller förmåga att gå krävs.
7. **Endast positiv koppling mellan spelare:** någons egenvård kan lyfta gruppen; någons frånvaro drar aldrig ned någon.
8. **Ägd progression:** en beständig värld, historik och tillförlitliga sparningar; inget nollställs.
9. **Dataminimering:** hälsodata lämnar enheten endast när spelaren uttryckligen delar den; aldrig för reklam eller profilering.
10. **Inget pay-to-win:** ingen försäljning av krafter, tid, makt eller upplåsning av vilan.
11. **Ingen manipulerande återkomst:** inga streaks, inga missade belöningar, ingen FOMO.
12. **Vackert på riktigt:** den visuella upplevelsen är en del av belöningen och hålls till samma kvalitetskrav som systemen.

---

## 4. Kärnloopar och spelupplevelse

### 4.1 Huvudloop

1. **Lev:** rörelse, träning och sömn registreras (automatiskt eller manuellt) och blir krafter.
2. **Återvänd:** en rapport visar vad världen gjort, vad expeditionerna hittat och vad gruppen byggt.
3. **Rikta:** spendera Utforskning, Byggkraft och Gryningskraft där de gör störst nytta.
4. **Planera:** justera produktionskedjor, logistik och sjövägar (alltid gratis).
5. **Vila:** viloperioden börjar, byborna sover, långsamma processer fortgår; gryningen öppnar världen igen.

### 4.2 Tidsskalor

| Tidsskala | Spelarupplevelse | Spelsystem |
|---|---|---|
| Minuter | Rikta krafterna, ett par meningsfulla beslut | Byggköer, expeditioner, prioriteringar |
| Dag | Världen har vuxit av gårdagens egenvård | Krafter, produktion, fynd, rapport |
| Vecka | Nya öar och större projekt | Specialisering, logistik, gruppens bygge |
| Epok (6–10 v) | Ett nytt kapitel med nya system och mål | Epokmilstolpe, nya mekaniker |
| Lång sikt | En personlig historia som aldrig tar slut | Fler epoker, arkipelagens historia |

### 4.3 Exempel på ett speldygn

- **Gryning:** viloperioden tar slut.
  Gryningskraft från nattens sömn finns att använda.
  Valfri morgonnotis: "Byn har vaknat, expeditionen är tillbaka."
- **Dag:** promenader och träning ger Utforskning och Byggkraft i bakgrunden.
- **Kväll:** spelaren skickar en expedition längre ut och startar en uppgradering, ser att en vän bidragit till gruppens fyr.
- **Vila:** nattvy, besked om när världen öppnar.
  Inga kommandon, inga notiser.
  Skogen växer.

### 4.4 Första sessionen [BESLUTAT, se D-49]

Steg för steg, med integritetsförklaring: se [UX-resorna](./docs/design/ux-journeys.md#resa-1-första-sessionen).

1. Välj språk och tillgänglighet.
2. Guidad tur: en liten bosättning på en ö vid gryning.
3. Lär dig de tre krafterna genom tre konkreta handlingar: ett dagsverke (Gryningskraft), kartlägg närområdet (Utforskning), bygg en brygga (Byggkraft); planering är gratis.
4. Integritetsförklaring: hälsokälla eller manuell inmatning, båda fullvärdiga.
5. Välj viloperiod (6–10 h) med nattläget och vilolåset framför dig.
6. Erbjudande att bjuda in vänner, utan press.

---

## 5. Version 1.0 – funktionella krav

Prioriteter: **P0 = krävs för release**, **P1 = planerad V1-omfattning**, **P2 = efter V1**.
Att ta bort en P1 kräver ett dokumenterat produktbeslut.

### 5.1 Värld och karta

| ID | Prioritet | Krav |
|---|---|---|
| MAP-01 | P0 | Generera fiktiva hexbaserade världar (ögrupper) reproducerbart utifrån seed + generatorversion. |
| MAP-02 | P0 | Terräng, biom, höjd, vatten, resurser, färdbarhet och byggbarhet. |
| MAP-03 | P0 | Geografiska avvägningar: kust, farleder, berg, avstånd och flaskhalsar påverkar val. |
| MAP-04 | P0 | Dimma över outforskade områden; Utforskning lyfter den beständigt. |
| MAP-05 | P0 | Geografigenerering separerad från simulering och rendering via versionerat `WorldMap`-format. |
| MAP-06 | P0 | Faktiskt genererat tillstånd sparas, inte bara seed; äldre världar överlever generatoruppgraderingar. |
| MAP-07 | P0 | Gruppens världar placeras i en gemensam arkipelag med hav och möjliga sjövägar emellan. |
| MAP-08 | P1 | Kartlager för resurser, transport och produktion; sök- och navigationshjälp. |
| MAP-09 | P1 | Verktyg för att testa och studera kartor/seeds i utveckling. |
| MAP-10 | P2 | Generera världar från verklig geodata (D-05). |

### 5.2 Civilisation och ekonomi

| ID | Prioritet | Krav |
|---|---|---|
| CIV-01 | P0 | Grunda och utveckla flera bosättningar. |
| CIV-02 | P0 | Placera, uppgradera och prioritera byggnader med terräng- och närhetsregler. |
| CIV-03 | P0 | Råvaror, förädling, konsumtion, lager och produktionskapacitet. |
| CIV-04 | P0 | Bybornas arbete och basbehov utan förlust eller förfall vid frånvaro (D-17). |
| CIV-05 | P0 | Tydlig diagnostik för produktions- och logistikflaskhalsar. |
| CIV-06 | P0 | Långsamma processer (t.ex. skogsväxt, mognad) som fortgår även under vilan. |
| CIV-07 | P1 | Byggnadsnivåer, ortsspecialisering och sammanhängande produktionskedjor. |
| CIV-08 | P1 | Stadstillväxt, infrastrukturprojekt, monument och historik. |
| CIV-09 | [ÖPPET] | Innehållsomfång (byggnadstyper, resurser, kedjor). Islanders-beslutstäthet talar för färre men tyngre typer; fastställs efter prototyp. |

### 5.3 Logistik och handel

| ID | Prioritet | Krav |
|---|---|---|
| LOG-01 | P0 | Vägar och transport mellan produktion, lager och bosättningar. |
| LOG-02 | P0 | Transport påverkas av avstånd, terräng, kapacitet och ruttval. |
| LOG-03 | P0 | Spelaren ser varför en vara inte når mottagaren. |
| LOG-04 | P0 | Hamnar och sjövägar inom världen. |
| LOG-05 | P0 | Sjövägar och handel mellan gruppmedlemmars världar, validerade av servern. |
| LOG-06 | P0 | Automatiseringsregler: minimilager, importprioritet, reservering. |

### 5.4 Utforskning, forskning och progression

| ID | Prioritet | Krav |
|---|---|---|
| EXP-01 | P0 | Expeditioner med mål, vägval, kostnader och förutsägbara risker, drivna av Utforskning. |
| EXP-02 | P0 | Upptäckter ger nya områden, öar, resurser eller möjligheter. |
| EXP-03 | P1 | Flera expeditionstyper: kartläggning, handel, forskning. |
| RES-01 | P0 | Forskning öppnar strategier, inte bara multiplikatorer. |
| RES-02 | P1 | Ömsesidiga prioriteringar och olika inriktningar (produktion, sjöfart, kultur). |
| PRG-01 | P0 | Guidad tur vid första start. |
| PRG-02 | P0 | Nya system introduceras löpande med tydliga tutorialelement. |
| PRG-03 | P0 | Epoker om ~6–10 veckor med egna mål och milstolpe; minst [ÖPPET: antal] epoker vid release. |
| PRG-04 | P0 | Inget slut och ingen nollställning; permanent historik. |
| PRG-05 | P1 | Fler epoker kan tillföras efter release utan att bryta befintliga världar. |
| NAR-01 | P1 | Kurerade platser, fynd, världshändelser och korta berättelsekedjor. |

### 5.5 Egenvård, krafter och hälsodata

| ID | Prioritet | Krav |
|---|---|---|
| HLT-01 | P0 | Vanor: **steg, träningspass och sömnlängd** som separata, frivilliga kategorier. |
| HLT-02 | P0 | Android: Health Connect. iOS: HealthKit. Bakom ett gemensamt C#-gränssnitt. |
| HLT-03 | P0 | Minsta nödvändiga behörighet per vana; förklaring före systemdialog. |
| HLT-04 | P0 | Deduplicering och korrigering av sen eller ändrad källdata utan dubbla bidrag. |
| HLT-05 | P0 | Avtagande avkastning och mjuka tak för alla tre krafter. |
| HLT-06 | P0 | Dålig sömn, vilodag eller frånvaro ger aldrig straff eller förlust. |
| HLT-07 | P0 | **Manuell inmatning** för varje vana är en fullvärdig väg för den egna världen. |
| HLT-08 | P0 | Varje bidrag klassas som verifierat eller egenrapporterat (via `recordingMethod` i Health Connect respektive `HKMetadataKeyWasUserEntered` i HealthKit, plus manuell inmatning i spelet). |
| HLT-09 | P0 | Omvandling: steg → Utforskning, träningspass → Byggkraft, sömnlängd → Gryningskraft (mjukt tak ~7–8 h, saknad data = neutral grundnivå). |
| HLT-10 | P0 | Rå hälsodata stannar på enheten; endast det spelaren uttryckligen delar skickas till servern. |
| HLT-11 | P0 | Spelaren kan pausa, återkalla, granska och radera hälsorelaterad data. |
| HLT-12 | P0 | Spelet kräver aldrig hälsobehörighet för att få spela eller vara med i grupper. |
| HLT-13 | P2 | Fler vanor, t.ex. mindfulness eller utomhustid, efter validering. |

### 5.6 Vilolås och dygnscykel

| ID | Prioritet | Krav |
|---|---|---|
| RST-01 | P0 | Spelaren väljer en återkommande viloperiod om **6–10 timmar**; tidszon och skiftarbete beaktas. |
| RST-02 | P0 | Under viloperioden blockeras alla spelpåverkande handlingar i den lokala simuleringen, och servern nekar spelarens gruppåtgärder. |
| RST-03 | P0 | Nattvy med besked om när världen öppnar och ett lugnt budskap. |
| RST-04 | P0 | Bybornas arbete pausar; långsamma processer fortgår; inget går förlorat. |
| RST-05 | P0 | Hälsodata läses när spelaren öppnar appen, aldrig i bakgrunden (D-70); krafter blir tillgängliga vid gryningen. |
| RST-06 | P0 | Inga notiser under viloperioden. |
| RST-07 | P0 | Konto, integritet, tillgänglighet, hjälp och radering är åtkomliga även under vila. |
| RST-08 | P0 | Inget köp, annons eller premiumläge låser upp spel under vila. |
| RST-09 | P0 | Schemaändringar börjar gälla först efter **24 timmar**; en pågående viloperiod kan inte kortas. |
| RST-10 | P0 | Upplåsning kräver aldrig bevisad sömn. |
| RST-11 | P0 | Gruppinteraktioner som berör en vilande medlem köas tills medlemmen vaknar. |

Kväll, nattvy och gryning steg för steg: se [UX-resorna](./docs/design/ux-journeys.md#resa-3-kväll-med-vilolås).

### 5.7 Grupper och arkipelag

| ID | Prioritet | Krav |
|---|---|---|
| SOC-01 | P0 | Grupper om 2–6 spelare bildas endast via inbjudningslänk eller kod. |
| SOC-02 | P0 | Gruppens världar delar en arkipelag med sjövägar och handel. |
| SOC-03 | P0 | Gemensamma byggen som alla medlemmars egenvård kan bidra till. |
| SOC-04 | P0 | Endast positiv koppling: inaktivitet hos en medlem påverkar aldrig någon annan negativt. |
| SOC-05 | P0 | Delning av hälsosiffror är avstängd som standard och slås på per vana; sömn kan delas som längd, aldrig som kvalitet. |
| SOC-06 | P0 | Ett läge med enbart bockar per vana kan aktiveras per plattform om butikspolicy kräver det. |
| SOC-07 | P0 | Bidrag till gruppen visas som verifierade eller egenrapporterade; egenrapporterade har lägre dagligt tak. |
| SOC-08 | P0 | Ingen fritextchatt; fördefinierade hälsningar och uppmuntran. |
| SOC-09 | P0 | Lämna grupp, ta bort medlem och blockera; definierat utfall för världar och sjövägar [ÖPPET]. |
| SOC-10 | P0 | Allt som påverkar andra spelare valideras och rimlighetskontrolleras av servern. |

### 5.8 Konton, sparning och offline

| ID | Prioritet | Krav |
|---|---|---|
| ACC-01 | P0 | Kontohantering, återställning och molnsparning mellan enheter. |
| ACC-02 | P0 | Den egna världen simuleras lokalt; servern är auktoritativ för grupp, arkipelag, handel, gemensamma byggen, delade siffror och köp. |
| ACC-03 | P0 | Konflikter mellan enheter ger inga duplicerade föremål eller korrupt tillstånd. |
| ACC-04 | P0 | Den egna världen är fullt spelbar offline; gruppåtgärder köas tills uppkoppling finns. |
| ACC-05 | P0 | Export och radering av användardata enligt fastställd dataskyddsprocess. |

### 5.9 Intäkter

| ID | Prioritet | Krav |
|---|---|---|
| MON-01 | P0 | Gratis att ladda ner och spela fullt ut. |
| MON-02 | P0 | Kosmetika: byggnadsstilar, öteman, bybornas utseende m.m. |
| MON-03 | P0 | Valfri supporter-prenumeration med kosmetik och bekvämlighet [ÖPPET: innehåll]. |
| MON-04 | P0 | Aldrig försäljning av krafter, tid, makt eller upplåsning av vila. |
| MON-05 | P0 | Köp och prenumerationer valideras på servern. |
| MON-06 | P0 | Ingen reklam. |

### 5.10 Presentation, kvalitet och tillgänglighet

| ID | Prioritet | Krav |
|---|---|---|
| UX-01 | P0 | Mobilstyrning: zoom, panorering, rotation, markering, byggplacering, ångra där säkert. |
| UX-02 | P0 | Daglig rapport: egenvårdens effekt, händelser, fynd, gruppens framsteg och nästa beslut. |
| UX-03 | P0 | Onboarding utan krav på hälsobehörighet. |
| UX-04 | P0 | **Low-poly 3D med lutad kamera och dynamiskt dygnsljus** (gryning, dag, skymning, natt). Gemensam art bible, ljud och animation. Visuell skönhet är en kvalitetsgrind. |
| UX-05 | P0 | Reducerad rörelse, kontrast, skalbar text, färgoberoende uttryck. |
| UX-06 | P0 | Engelska och svenska i samtliga flöden; lokaliseringssystem för fler språk. |
| UX-07 | P0 | Notiser: högst en morgonnotis och viktiga grupphändelser; valbara per typ; aldrig FOMO, streaks eller tjat om aktivitet. |
| UX-08 | P1 | Fördjupade överlägg för ekonomi, transport, forskning och historik. |
| OPS-01 | P0 | Säker releaseprocess, övervakning, återställning och versionerade migrationer. |
| OPS-02 | P0 | Automatiserade tester på domän, integration, klientflöde och verkliga enheter. |

---

## 6. Spelmodell – tillräckligt konkret för prototyp

### 6.1 Karta och topologi

- Axiala hexkoordinater `(q,r)`; varje bricka har terräng, biom, höjd, resurser och byggregler.
- En **värld** är en ögrupp med kustlinjer, farleder, inland och flaskhalsar.
- `WorldGenerator` producerar en normaliserad `WorldMap`: `ProceduralGenerator` i V1, `GeographicGenerator` senare.
- Genererade kartor valideras mot startresurser, anslutbarhet och expansionsvägar.
- Kartan lagras med `generatorVersion`, seed och härkomstmetadata.
- En **arkipelag** komponeras av gruppens världar med hav emellan [ÖPPET: placering, avstånd och hur nya medlemmar fogas in].

### 6.2 Krafter och ekonomi

**Krafterna** är det enda sättet egenvård påverkar världen:

| Vana | Kraft | Driver | Begränsning |
|---|---|---|---|
| Steg | Utforskning | Expeditioners räckvidd, att lyfta dimman, nya öar | Avtagande avkastning, mjukt tak |
| Träningspass | Byggkraft | Större byggprojekt, uppgraderingar, gemensamma byggen | Ett pass är ett pass; intensitet krävs inte |
| Sömnlängd | Gryningskraft | Bybornas energi, morgonens planeringsdrag | Mjukt tak ~7–8 h; ingen kvalitetspoäng; saknad data = grundnivå |

Kurvor, tak och sparande står i [krafternas regler](./docs/design/forces.md).

**Ekonomin** (Anno-inspirerad) körs själv i grundtakt: råvaror → förädling → avancerade varor, med lager, arbetskraft och transport.
Krafterna avgör vilka stora steg som kan tas.
Resurslager [FÖRSLAG]: basbehov, naturresurser, förädlade varor, avancerade varor, kapaciteter.

**Strategiska avvägningar** ska finnas på varje nivå, t.ex.: rikta Byggkraft mot ett nytt sågverk nära skogen *eller* mot en hamn som öppnar handel med gruppen; spendera Utforskning på en lång expedition mot en ny ö *eller* kartlägga närområdet för resurser.

Ingen obligatorisk resurs får ha ett enda, lätt blockerbart sätt att produceras.

Byggnader, recept och kraftkostnader står i [ekonomi v1](./docs/design/economy-v1.md).

### 6.3 Bybor

- Bybor modelleras som arbetsgrupper med roller, kapacitet, basbehov och gemensam dygnsrytm.
- Namngivna karaktärer kan bära berättelser och expeditioners historik.
- Vilan är en del av simuleringens domän, inte en kosmetisk animation.
- Frånvaro orsakar aldrig utplåningsspiraler; brister är alltid återhämtningsbara.

### 6.4 Progression och epoker

- Spelet börjar med en guidad tur och introducerar sedan system löpande.
- Epoker om ~6–10 veckor [FÖRSLAG: bosättning → handel → upptäckt → blomstring] introducerar nya mekaniker och avslutas med en milstolpe.
  Epok 1 slutar när Vårdkasen tänds (D-64); konturen och tempot står i [epoch-1.md](./docs/design/epoch-1.md).
- Forskning med minst tre inriktningar [FÖRSLAG]: **produktion**, **sjöfart/logistik**, **kartläggning/kultur**.
- Inget nollställs.
  Nya epoker kan tillföras efter release.

### 6.5 Inaktivitet och återkomst

- Utan egenvård tillkommer inga krafter, men ekonomin går i grundtakt och långsamma processer fortgår.
- Inget vissnar, svälter eller förfaller.
- Återkomsten möts varmt, eventuellt med en liten återstartsbonus [FÖRSLAG], aldrig med skuld.

Återkomsten steg för steg: se [UX-resorna](./docs/design/ux-journeys.md#resa-2-återkomst-efter-sju-dagars-frånvaro).

Effekterna ska **speltestas**, inte hårdkodas som sanning.
Inga medicinska påståenden.

---

## 7. Domänmodell och teknik

### 7.1 Viktiga entiteter

`Account`, `PlayerProfile`, `World`, `WorldMap`, `Island`, `HexTile`, `Settlement`, `Building`, `ProductionRecipe`, `Inventory`, `TransportRoute`, `SeaRoute`, `Villagers`, `SlowProcess`, `Expedition`, `ResearchProject`, `Epoch`, `GameEvent`, `SimulationSnapshot`, `Habit`, `Contribution`, `Force`, `RestSchedule`, `Group`, `Archipelago`, `SharedProject`, `Trade`, `Entitlement`.

### 7.2 Systemgränser [BESLUTAT, se ADR-0001 och ADR-0002]

- **Klient:** Unity 6 LTS + C# för världsvy, UI, ljus, animation och lokal simulering.
- **Simuleringsbibliotek:** rent C# utan Unity-beroenden; delas mellan klient och server.
- **Native-adaptrar:** Health Connect (Kotlin) och HealthKit (Swift) bakom ett gemensamt C#-gränssnitt.
  HealthBridge (Fab) valdes bort mot kraven i HLT-03/04/08/12 ([ADR-0007](./docs/adr/0007-own-health-connect-adapter.md)).
- **Backend:** ASP.NET Core för konto, molnsparning, grupper, arkipelag, handel, gemensamma byggen, delade siffror och köpvalidering.
- **Databas:** [FÖRSLAG] PostgreSQL.
- **Karta:** versionerad genereringsmodul utan beroende till Unity.

### 7.3 Simulering och tid

- Den egna världen simuleras deterministiskt på enheten och kan spolas fram från senaste snapshot.
- Handlingar är validerade kommandon med idempotensnycklar; gruppkommandon valideras dessutom på servern.
- Resurser uttrycks i heltal eller fixed-point för deterministisk replay.
- Regelversion och tillämpade händelser ska kunna reproduceras i tester, lokalt och på servern.
- Vilolåset använder enhetens klocka offline och serverns tid online (ADR-0003).
  Servern nekar gruppåtgärder under spelarens viloperiod.
- Under viloperioden pausar bybornas arbete medan långsamma processer fortgår.
- Sent inkommande hälsodata hanteras med idempotenta kompensationshändelser.
- Det som alltid ska gälla i simuleringen står i [simuleringsinvarianterna](./docs/design/invariants.md).

### 7.4 Hälsodatans livscykel

1. Adaptern läser endast godkända typer, inklusive metadata om inspelningsmetod.
2. Klienten normaliserar, deduplicerar och aggregerar till bidrag per intervall.
3. Bidrag klassas som verifierade eller egenrapporterade.
4. Krafter beräknas lokalt med tak och avtagande avkastning.
5. Till servern skickas endast: aggregerade bidrag till gemensamma byggen och de siffror spelaren uttryckligen valt att dela.
   Rådata lämnar aldrig enheten.
   Molnsparningen innehåller bidrag per spelardygn end-to-end-krypterade, så att servern aldrig kan läsa dem (D-57).
6. Spelaren kan pausa, återkalla, granska och radera; återkallat tillstånd innebär inga nya läsningar.

### 7.5 Dataskydd och butikspolicy

Lägesbild från research 2026-10-17; ska omprövas inför implementation och release:

- **Google Play:** Health Connect-appar kräver deklaration per datatyp.
  "Health-integrated games" är ett uttryckligen godkänt användningsområde så länge huvudsyftet är spel.
  Delning av känslig data socialt kräver uttryckligt samtycke.
  Bakgrundsläsning och historik är separata behörigheter.
- **Apple:** 5.1.3 förbjuder användning av hälsodata för reklam och datautvinning och kräver tydlig redovisning.
  Developer Program License Agreement tillåter utlämnande till tredje part endast med samtycke och för hälso-/träningstjänster.
  Hur delning med vänner bedöms är en **gråzon**; därav skyddsnätet i SOC-06 och träningskompis-inramning i UI och butikstext.
  Enligt riktlinjerna får en app inte kräva att en systemfunktion aktiveras för att få tillgång till funktioner eller belöningar; därav HLT-12 och SOC-07.
  HealthKit-bakgrundsleverans är "best effort" och begränsas för steg till en gång per timme.
- Genomför en dataskyddskonsekvensbedömning (DPIA) för hälsodata (känsliga personuppgifter enligt GDPR), inklusive delning inom grupp, gallring, underbiträden och radering.
- Hälsodata får aldrig hamna i loggar, analytics, kraschrapporter eller supportexporter.
- Hotbild: dataexponering, replay, dubbla bidrag, manipulerade gruppbidrag, IDOR, kontoövertagande, klockmanipulation (accepterad risk för den egna världen).

---

## 8. Icke-funktionella krav

| ID | Område | Krav / verifiering |
|---|---|---|
| NFR-01 | Tillförlitlighet | Ingen irreversibelt förlorad progression i releasetester; backup och testad restore. |
| NFR-02 | Determinism | Samma kommandon och starttillstånd ger samma tillstånd på klient och server. |
| NFR-03 | Prestanda | Mätt på riktiga låg- och mellanklassenheter; fps-, minnes-, laddtids- och batterimål fastställs tidigt. |
| NFR-04 | Skalbarhet | Samtidiga grupper, handel, reconnect-stormar. |
| NFR-05 | Säkerhet | Hotmodell, behörighetskontroller, kryptering, hemlighetshantering, penetrationstest. |
| NFR-06 | Integritet | Separata samtycken; dataminimering; radering och export testade genom hela kedjan. |
| NFR-07 | Portabilitet | Likvärdig kärnfunktionalitet på Android och iOS. |
| NFR-08 | Internationalisering | Text separerad från kod; engelska och svenska testade. |
| NFR-09 | Uppgraderbarhet | Sparformat, kartformat, API och regler versioneras och migreras. |
| NFR-10 | Observability | Strukturerade händelser och larm utan hälsodata. |
| NFR-11 | Testbarhet | Headless simulering, fake health provider, virtuell klocka, automatiserade E2E-flöden. |
| NFR-12 | Respektfull design | Inga notiser under vila, inga streaks, inga köp som kringgår speltid, ingen social press. |
| NFR-13 | Visuell kvalitet | Art bible, ljusmiljöer för hela dygnet och visuell granskning som kvalitetsgrind; prestandabudget som håller skönheten på målenheterna. |
| NFR-14 | Agentvänlighet | Spelinnehåll, regler och konfiguration uttrycks i kod och data snarare än handredigerade Unity-scener. |

Numeriska budgetar: [ÖPPET], fastställs efter mätningar på målenheter.

---

## 9. Kritiska acceptansscenarier

**AC-01 — Vilolåset.**
Givet att spelarens viloperiod pågår, när spelaren försöker bygga, starta en expedition eller handla, så nekas handlingen lokalt utan sidoeffekter, och servern nekar varje gruppåtgärd med förutsägbar felkod.
Nattvyn är tillgänglig.

**AC-02 — Flera dagars frånvaro.**
Givet en fungerande värld, när spelaren återkommer efter sju dygn utan egenvård, så har inget förfallit, långsamma processer har fortgått, och rapporten förklarar vad som hänt.

**AC-03 — Dubbla stegkällor.**
Givet att telefon och klocka rapporterar överlappande steg, när import körs upprepade gånger i olika ordning, så blir bidraget konsistent och registreras högst en gång per intervall.

**AC-04 — Manuell inmatning.**
Givet att spelaren nekar all hälsobehörighet och loggar manuellt, när hen spelar, så fungerar alla system i den egna världen, och hen kan vara med i en grupp där hens bidrag visas som egenrapporterade.

**AC-05 — Schemaändring och tidszon.**
Givet en pågående viloperiod, när mobilen byter tidszon eller spelaren ändrar schemat, så förblir pågående lås oförändrat och det nya schemat gäller först efter 24 timmar.

**AC-06 — Variation i strategi.**
Givet två spelare med samma egenvård i liknande världar, när de riktar sina krafter olika, så får de mätbart olika världar och ingen strategi dominerar utan avvägning.

**AC-07 — Kartgeneratoruppgradering.**
Givet en sparning från generatorversion N, när appen uppgraderas till N+1, så laddas den ursprungliga kartan oförändrad.

**AC-08 — Offline.**
Givet att telefonen saknar uppkoppling, när spelaren spelar i sin egen värld, så fungerar allt; gruppåtgärder köas och tillämpas en gång vid reconnect.

**AC-09 — Integritetsradering.**
Givet en raderingsbegäran, när flödet fullföljs, så är identifierbara data raderade enligt policy, inklusive delade siffror hos gruppen och backuprutiner.

**AC-10 — Vilande gruppmedlem.**
Givet att en medlem vilar, när en annan medlem skickar en handelsorder, så köas den tills medlemmen vaknat, och ingen av dem påverkas negativt.

**AC-11 — Delningssamtycke.**
Givet att spelaren inte aktiverat delning för en vana, när gruppen visas, så syns inga siffror eller bockar för den vanan.

**AC-12 — Bara positiv koppling.**
Givet en grupp där en medlem varit inaktiv i två veckor, när övriga bygger gemensamt, så går deras framsteg i samma takt som om medlemmen inte funnits.

---

## 10. Utanför scope för V1

- Världar genererade från verklig geografi (endast stödjande arkitektur).
- GPS eller platsdata.
- PvP, territoriella konflikter, attacker och plundring.
- Öppna grupper, matchning med främlingar och fritextchatt.
- Reklam, köp av krafter, tid eller viloupplåsning.
- Generativ AI som styr spelregler eller belöningar.
- Medicinska rekommendationer eller hälsopåståenden.
- Mekaniker där en spelares frånvaro straffar andra.

---

## 11. Största riskerna

| ID | Risk | Varför viktig | Tidig åtgärd |
|---|---|---|---|
| R-01 | Spelet blir en stegräknare med dekor | Med egenvård som motor är risken högre än tidigare | Strategiska beslut om var krafterna riktas måste vara viktiga; testa i prototyp |
| R-02 | Hälsointegrationer otillförlitliga i bakgrunden | Försenade bidrag | Krafter vid gryning/öppning; tidig device-spike |
| R-03 | Vilolåset känns orättvist vid skift och resor | Produktlöftet | Användartest av schemaregler |
| R-04 | Manuella spelare känner sig misstrodda | Inkludering | Neutral märkning, inga misstänkliggörande formuleringar |
| R-05 | Simuleringen svår att uppgradera | Månader av progression | Versionerade regler, snapshots, migrationsprov |
| R-06 | Mycket innehåll men lite variation | Djup | Varje byggnad måste fylla ett syfte |
| R-07 | Grupper skapar press eller jämförelse | Fulla siffror kan delas | Endast positiv koppling, opt-in per vana, guardrails |
| R-08 | Batteri och prestanda | 3D + bakgrundsläsning | Budgetera och mät tidigt |
| R-09 | Felaktig hantering av hälsodata | Juridik och förtroende | Privacy by design, DPIA, extern granskning |
| R-10 | Apple underkänner delning av hälsosiffror | iOS-release | Skyddsnät SOC-06, tidig policygranskning |
| R-11 | Unitys editorflöde är mindre agentvänligt | Utvecklingsflöde | NFR-14: kod och data framför scener |
| R-12 | Ambitionen "vackert" nås inte | Kommersiell attraktion | Art bible tidigt, visuell kvalitetsgrind, ljus som kärnteknik |
| R-13 | Spel utan slut kräver löpande innehåll | Långsiktig retention | Epokstruktur med verktyg för att producera nya epoker |

---

## 12. Beslutslogg och öppna frågor

### Beslutslogg

| ID | Datum | Beslut | Underlag |
|---|---|---|---|
| D-01–D-09 | 2026-10-17 | Se avsnitt 1.1 | Produktdialog v0.1 |
| D-03, D-08 | 2026-10-17 | Förtydligade och D-08 upphöjt till BESLUTAT | Produktgrillning |
| D-10–D-33 | 2026-10-17 | Se avsnitt 1.1 | Produktgrillning |
| D-34–D-36 | 2026-10-17 | Se avsnitt 1.1 | Öppna frågor i TASK-001 och TASK-002 |
| D-37–D-39 | 2026-10-17 | Se avsnitt 1.1 | Öppna frågor i TASK-002 |
| D-40–D-42 | 2026-10-17 | Se avsnitt 1.1 | Öppna frågor i TASK-003 |
| D-43–D-45 | 2026-10-17 | Se avsnitt 1.1 | Öppna frågor i TASK-004 |
| D-46–D-48 | 2026-10-17 | Se avsnitt 1.1 | Öppna frågor i TASK-009 |
| D-49–D-58 | 2026-10-17 | Se avsnitt 1.1 | Öppna frågor i TASK-005 |
| — | 2026-10-17 | Första sessionens ordning i 4.4 ändrad: turen före hälsokälla och viloperiod | D-49 |
| — | 2026-10-17 | O-12 avgjord: ingen återstartsbonus | D-52 |
| D-59–D-60 | 2026-10-17 | Se avsnitt 1.1 | Öppna frågor i TASK-006 |
| D-61 | 2026-10-17 | Se avsnitt 1.1 | Produktägarens val bland tre riktningar i TASK-006 (PR #21); ersätter D-59 |
| D-62 | 2026-10-17 | Se avsnitt 1.1 | Granskning av simuleringskärnan i TASK-008 (PR #24) |
| D-63–D-67 | 2026-10-18 | Se avsnitt 1.1 | Öppna frågor i TASK-011 |
| D-68 | 2026-10-18 | Se avsnitt 1.1 | Health Connect-spiken på produktägarens telefon i TASK-007 (PR #44) |
| D-69 | 2026-10-18 | Se avsnitt 1.1 | Egenskapstestet för D-63 (PR #48) visade att ett sent pass över sparandets tak gav en annan slutsumma (INV-B1) |
| D-70–D-71 | 2026-10-18 | Se avsnitt 1.1 | Öppna frågor efter Health Connect-spiken i TASK-007 |
| D-72 | 2026-10-18 | Se avsnitt 1.1 | Egenskapstestet för D-63 utan gräns för antalet pass visade att gränsen i D-62 gav en annan slutsumma när ett sent pass tillfälligt lyfte saldot över sparandets tak (INV-B1) |
| — | 2026-10-18 | RST-05 ändrat: ingen synk i bakgrunden, hälsodata läses vid öppning | D-70 |
| — | 2026-10-18 | O-08 avgjord: egen Kotlin-adapter för Health Connect, inte HealthBridge | ADR-0007 |
| — | 2026-10-17 | LOG-06 höjt från P1 till P0 | D-44 |
| — | 2026-10-17 | Kampanjbågen (PRG-01 v0.1) ersatt av epoker utan slut | D-13 |
| — | 2026-10-17 | Princip "strategi först" ersatt av "egenvård är motorn, strategi är ratten" | D-14 |
| — | 2026-10-17 | Serverauktoritet för hela spelet ersatt av lokal värld + server för det delade | ADR-0001 |
| — | 2026-10-17 | Godot + Spring Boot ersatt av Unity + ASP.NET Core | ADR-0002 |
| — | 2026-10-17 | Absolut, serververifierat vilolås ersatt av ärligt skydd | ADR-0003 |
| — | 2026-10-17 | Socialt flyttat från P1 till kärna (P0) | D-19–D-21 |
| — | 2026-10-17 | Arbetsnamnen Arkipelagen och Verdawn ersatta av Eirora; slogan, kort beskrivning, genre och nordiskt tema fastställda | D-10, avsnitt 1.3 |

### Öppna frågor

| ID | Fråga | Utgångspunkt | Beslutas före |
|---|---|---|---|
| O-01 | Exakta värden för krafter, tak och avtagande avkastning | Prototyp med riktiga steg | M1/M5 |
| O-02 | Innehållsomfång: byggnader, resurser, kedjor | Färre men tyngre typer | M4 |
| O-03 | Antal och innehåll för epoker vid release | Minst 3–4 epoker? | M5 |
| O-04 | Hur komponeras arkipelagen av gruppens världar? | Fast havsyta, världar placeras runt ett gemensamt centrum | M8 |
| O-05 | Vad händer med sjövägar och handel när någon lämnar gruppen? | Inga förluster för någon | M8 |
| O-06 | Innehåll i supporter-prenumerationen | Kosmetik + bekvämlighet, aldrig kraft | M10 |
| O-07 | Schemaregler vid resor och skiftarbete | Användartest | M7 |
| O-09 | Hosting, auth-leverantör och databas för backend | ASP.NET Core + PostgreSQL | M2 |
| O-10 | Rimlighetskontroll av handel och gruppbidrag | Delad simulering + tak | M8 |
| O-11 | Varumärkes- och butikskontroll av "Eirora" | Kontrollera före butikssida | M2 |
| O-13 | Kvantitativa kvalitetsmål och målenheter | Mät i prototyp | M1/M2 |

---

## 13. Riktlinjer för nästa agent

1. Börja med kärndesignen (ROADMAP M0) och den spelbara prototypen (M1); produktägaren ska kunna spela dagligen så tidigt som möjligt.
2. Behandla [BESLUTAT] som invarianter.
3. För varje ny mekanik: dokumentera spelarbeslut, kraftkostnad, konsekvens, fairness, påverkan på gruppen, simulering och test.
4. För varje öppen fråga: 2–3 alternativ med avvägningar och en rekommendation; uppdatera beslutsloggen.
5. Varje implementerbar funktion blir ett GitHub-ärende med PRD-ID, beroenden, acceptanstester och migrationspåverkan.
6. Håll ROADMAP.md, GLOSSARY.md och docs/adr/ synkroniserade.
7. Inga medicinska eller juridiska garantier utan expert- eller policyverifiering.

## 14. Referenser att ompröva vid implementation

- Android Health Connect: [läsa data](https://developer.android.com/health-and-fitness/health-connect/read-data), [publicering](https://developer.android.com/health-and-fitness/health-connect/publish), [Play-policy för hälsoappar](https://support.google.com/googleplay/android-developer/answer/12991134?hl=en).
- Apple: [App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/), [Developer Program License Agreement](https://developer.apple.com/support/terms/apple-developer-program-license-agreement/), [HealthKit-behörigheter](https://developer.apple.com/documentation/healthkit/authorizing-access-to-health-data), [bakgrundsleverans](https://developer.apple.com/documentation/healthkit/hkhealthstore/enablebackgrounddelivery%28for:frequency:withcompletion:%29).
- Unity: [6.3 LTS](https://unity.com/blog/unity-6-3-lts-is-now-available), [prisvillkor](https://unity.com/products/pricing-updates).
  Hälsoplugin att utvärdera: [HealthBridge for Unity](https://www.fab.com/listings/aa269bb2-ab71-431c-a35b-0b635c12700f).

Länkarna är ingångar, inte garantier; granska aktuell dokumentation vid implementation och release.

## 15. Ändringshistorik

| Version | Datum | Ändring |
|---|---|---|
| 0.1 | 2026-10-17 | Första utkast (Arkipelagen). |
| 0.2 | 2026-10-17 | Namn Verdawn; egenvård som motor med tre krafter; inget slut, epoker; grupper och gemensam arkipelag; lokal värld + server för det delade; Unity + ASP.NET Core; low-poly 3D; intäktsmodell; vilolås som ärligt skydd. |
| 0.3 | 2026-10-17 | Namn Eirora ersätter Verdawn; varumärkesidentitet (slogan, kort beskrivning, genre, nordiskt tema) i avsnitt 1.3. |
