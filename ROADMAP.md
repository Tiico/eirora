# EIRORA — Roadmap till version 1.0

> **Dokumentstatus:** Utkast v0.8 — M0 godkänd vid grind A0  
> **Datum:** 2026-10-26  
> **Produktkrav:** [PRD.md](./PRD.md) · **Ordlista:** [GLOSSARY.md](./GLOSSARY.md) · **Beslut:** [docs/adr/](./docs/adr/)  
> **Planeringsprincip:** kvalitetsstyrda milstolpar. **Utvecklingstid är aldrig en faktor** (D-09); genvägar och halvfärdiga system accepteras inte.

## 0. Hur roadmapen används

Roadmapen är en ordnad **beroendeplan**, inte en tidsplan.
Varje milstolpe har mål, leverabler, kvalitetsgrind och beroenden.

**Det som styr ordningen** är det som agenterna inte kan korta:

- **Kalendertid i riktiga dygn.**
  Om ett passivt spel med dygnsrytm är roligt syns bara genom att spela det dag efter dag.
  Därför kommer en spelbar prototyp på produktägarens telefon tidigt (M1), och den spelas parallellt med allt annat arbete.
- **Produktägarens tid** för beslut, granskning och test på fysiska enheter.
- **Butiksgranskningar och policyprocesser** (Health Connect-deklaration, App Review).

Arbetet sker via GitHub-ärenden i [Tiico/eirora](https://github.com/Tiico/eirora) enligt [docs/agents/](./docs/agents/).

### Statusar

`NOT_STARTED` → `IN_PROGRESS` → `IN_REVIEW` → `ACCEPTED`.
En milstolpe som inte klarar sin grind återgår till `IN_PROGRESS` med dokumenterade observationer.

### Övergripande grindar

| Grind | Omfattning | Vad den bevisar |
|---|---|---|
| **A — Spegeln fungerar** | M0–M5 | Egenvård → krafter → strategiska beslut är roligt i riktiga dygn, även med enbart manuell inmatning |
| **B — Tillsammans och i vardagen** | M6–M10 | Båda plattformarna, vilolås, grupper och arkipelag, vacker presentation, intäkter |
| **C — Förtjänar release** | M11–M12 | Balanserat, säkert, stabilt, policygodkänt och verifierat med riktiga användare |

---

## 1. Milstolpsöversikt

| Milstolpe | Namn | Huvudberoende | Status |
|---|---|---|---|
| M0 | Kärndesign | Beslut D-01–D-33 | ACCEPTED |
| M1 | Spelbar prototyp i fickan | M0 | IN_PROGRESS |
| M2 | Teknisk grund | M0, lärdomar från M1 | IN_PROGRESS |
| M3 | Världsgenerering | M2 | IN_PROGRESS |
| M4 | Ekonomi och logistik | M2, M3 | IN_PROGRESS |
| M5 | Krafter, expeditioner, forskning och första epoken | M4, M1-lärdomar | NOT_STARTED |
| M6 | Hälsomotor och native-integrationer | M1-spike, M5 | NOT_STARTED |
| M7 | Vilolås och dygnscykel | M2, M4, M6 | NOT_STARTED |
| M8 | Grupper och gemensam arkipelag | M2 backend, M4, M6, M7 | NOT_STARTED |
| M9 | Visuell kvalitet, ljud och UX | Art direction från M0; full integration efter M5 | NOT_STARTED |
| M10 | Epoker, innehåll och intäkter | M5, M8 | NOT_STARTED |
| M11 | QA, balansering, alfa och beta | M0–M10 | NOT_STARTED |
| M12 | Release 1.0 | M11 | NOT_STARTED |

**Viktigt:** vilolåsets domäninvariant och krafternas regler skrivs in i simuleringskärnan redan i M1/M2, inte i efterhand.
Art direction pågår från M0 eftersom visuell skönhet är ett kvalitetskrav (D-29).

---

## 2. Detaljerade leveranser

### M0 — Kärndesign (kort)

**Mål:** spärra spelets kärna tillräckligt för att bygga en meningsfull prototyp.

1. Utöka [GLOSSARY.md](./GLOSSARY.md) med alla kärnbegrepp.
2. Simuleringsinvarianter: vad som alltid gäller för resurser, bybor, krafter, tid, vila, bidrag och kommandon.
3. Krafternas första regler: omvandling, tak, avtagande avkastning, manuell inmatning och neutral grundnivå.
4. Första ekonomin med siffror: 8–12 byggnader, 6–10 resurser, minst tre logistiska avvägningar, minst tre beslut om var krafterna ska riktas.
5. [UX-resor](./docs/design/ux-journeys.md): första sessionen, återkomst efter 7 dagar, kväll med vilolås.
6. Art direction: moodboard, palett och ljusstämningar för gryning, dag, skymning och natt i low-poly 3D, förankrade i det nordiska temat (PRD 1.3).
   Se [art-direction.md](./docs/design/art-direction.md).
7. [Kontur för epok 1](./docs/design/epoch-1.md).

**Artefakter:** GLOSSARY.md, `docs/design/` (invarianter, krafter, ekonomi v1, UX-resor, art direction, kontur för epok 1), uppdaterad PRD, GitHub-ärenden för M1.

**Kvalitetsgrind A0:** det går att beskriva en hel dag inklusive vila, med konkreta siffror.
Inget beslut som blockerar prototypen är öppet.
Godkänd 2026-10-18, se [gate-a0.md](./docs/design/gate-a0.md).

### M1 — Spelbar prototyp i fickan

**Mål:** produktägaren spelar Eirora på sin Android-telefon varje dag så tidigt som möjligt.

1. Unity 6 LTS-projekt med motoroberoende C#-simuleringsbibliotek och tester i headless-läge.
2. En liten ö på hexkarta i low-poly 3D med enkelt dygnsljus.
3. En minimal ekonomi och de tre krafterna.
4. Steg och träning via Health Connect på riktig enhet, sömn där tillgängligt, samt manuell inmatning.
5. Lokalt vilolås med nattvy.
6. Daglig rapport.
7. Spike för Health Connect (verifiera `recordingMethod`, bakgrundsläsning, dubbletter); utvärdera HealthBridge mot egen adapter (O-08).
8. Bygg till iOS från Mac i CI för att bekräfta att kedjan fungerar, även utan iPhone.
9. Mät tidiga prestanda- och batterivärden på produktägarens telefon.

**Artefakter:** installerbar Android-prototyp, [prototypdagbok](./docs/design/prototype-diary.md) från dagligt spel, device-spike-rapport, [lista över designlärdomar](./docs/design/prototype-learnings.md).

**Kvalitetsgrind A1:** produktägaren har spelat i minst 14 riktiga dygn.
Lärdomarna är dokumenterade och återförda till M0-designen enligt [veckogenomgången](./docs/design/prototype-diary.md#5-veckogenomgången).
Simuleringen är deterministisk i headless-tester.

### M2 — Teknisk grund

**Mål:** ett testbart, reproducerbart och uppgraderbart fundament.

1. Reporstruktur: Unity-klient, simuleringsbibliotek, ASP.NET Core-backend, delade kontrakt.
2. CI: tester, Android-bygge, iOS-bygge (macOS), kodanalys, beroendeskanning.
3. Simuleringskärna: `GameCommand`, `GameEvent`, idempotens, virtuell klocka, versionerade regler, snapshots, replay.
4. Sparformat med migrationsramverk och lokal persistens.
5. Backend-skelett: konto, auth, molnsparning, konfliktlösning mellan enheter (O-09).
6. Bevis att simuleringsbiblioteket körs identiskt på klient och server.
7. Mätbudgetar och lista över målenheter (O-13).
8. Varumärkes- och butikskontroll av "Eirora" (O-11).

**Kvalitetsgrind A2:** replay är deterministisk på klient och server.
En dubblett av ett kommando ändrar inte tillståndet två gånger.
En versionsuppgradering av ett sparat tillstånd fungerar.
Android- och iOS-byggen produceras i CI.

### M3 — Världsgenerering

1. Axiala hexkoordinater, grannar, area-sökning, höjd, gångbarhet, pathfinding.
2. `WorldGenerator`-gränssnitt och versionerat `WorldMap`-format.
3. Procedurgenererade ögrupper: hav, kust, farleder, berg, skog, odlingsmark, resurser.
4. Validator för startförutsättningar och expansionsvägar; bulkvalidering av tusentals seeds.
5. Dimma och upptäckt.
6. Förbered komposition av arkipelag (O-04) och `sourceType=GEOGRAPHIC` utan geodataimport.

**Kvalitetsgrind A3:** tusentals seeds valideras automatiskt.
Gamla världar laddas oförändrade.
Rendering och pathfinding fungerar på målenheter.

### M4 — Ekonomi och logistik

1. Byggplacering, bosättningar, uppgraderingar.
2. Recept, lager, arbetskraft, konsumtion, basbehov utan förfall; innehållsomfånget för byggnader, resurser och kedjor (O-02).
3. Långsamma processer.
4. Vägar, hamnar, sjövägar inom världen, transportkapacitet, flaskhalsdiagnostik.
5. Konkurrerande investeringar och minst tre scenarier med olika optimala strategier.
6. Långtidssimulering av ekonomin, med och utan krafter, med balansbotar som trimmar krafternas värden (O-01).

**Kvalitetsgrind A4:** placering och logistik ger tydliga, begripliga effekter.
Långa frånvaroperioder bryter ingenting.
Minst två strategier fungerar och ingen dominerar.

### M5 — Krafter, expeditioner, forskning och första epoken

1. Fullständiga regler för Utforskning, Styrka och Ork med tak och avtagande avkastning (O-01).
2. Expeditioner som drivs av Utforskning: rutter, fynd, nya öar.
3. Forskning med minst tre inriktningar.
4. Guidad tur och löpande introduktion med tutorialelement.
5. Epok 1 från början till milstolpe enligt [konturen](./docs/design/epoch-1.md), med forskningens inriktningar och fynden.
6. Fake health provider för testscenarier: låg, hög, ingen och manuell egenvård.
7. Återkomst efter frånvaro (O-12).

**Kvalitetsgrind A5 / GRIND A:** en extern testare förstår hur egenvård blir krafter och krafter blir beslut, och spelar epok 1 med meningsfulla val. **Spelet är roligt även med enbart manuell inmatning**, och det är inte en stegräknare med dekor (R-01).

Uppgifterna står i [Uppgifter för M5](#uppgifter-för-m5).

### M6 — Hälsomotor och native-integrationer

1. Health Connect: steg, träningspass, sömn, `recordingMethod`, bakgrund och historik.
2. HealthKit: motsvarande, inklusive `HKMetadataKeyWasUserEntered` och bakgrundsleverans. **iPhone anskaffas innan detta arbete påbörjas** (D-26).
3. Gemensamt C#-gränssnitt, normalisering, deduplicering, sen data, kompensationshändelser.
4. Klassning av verifierade och egenrapporterade bidrag.
5. Nekad, partiell och återkallad behörighet; radering.
6. Dataflödeskarta, DPIA-underlag, policykontroll för Play och App Store.

**Kvalitetsgrind B6:** båda plattformarna klarar behörighets- och importflöden på riktiga enheter.
Inga dubbla bidrag.
Rådata lämnar aldrig enheten.
Raderingen är verifierad.

### M7 — Vilolås och dygnscykel

1. Tillståndsmaskinen `ACTIVE → RESTING → DAWN → ACTIVE` i simuleringen.
2. Vilotid 6–10 h, 24 timmars fördröjning vid ändring, IANA-tidszoner och sommartid.
3. Bybornas arbete pausar och långsamma processer fortgår.
4. Servern nekar gruppåtgärder under vila.
   Interaktioner som berör en vilande medlem köas.
5. Nattvy, gryningsövergång, tyst notisregim och morgonnotis.
6. Administrativa funktioner tillgängliga under vila.
7. Regler för resor och skiftarbete med användartest (O-07).

**Kvalitetsgrind B7:** inga spelpåverkande handlingar under vila via normala flöden.
Tidszon, sommartid, resor och skiftarbete är verifierade.
Nattläget upplevs lugnt i användartest.

### M8 — Grupper och gemensam arkipelag

1. Grupper om 2–6 via inbjudningslänk eller kod.
   Lämna, ta bort och blockera (O-05).
2. Komposition av arkipelag (O-04), sjövägar och handel mellan världar med validering på servern (O-10).
3. Gemensamma byggen med endast positiv koppling.
4. Delning av siffror per vana med samtycke, läget med enbart bockar per plattform, märkning av verifierat och egenrapporterat, tak för egenrapporterade bidrag.
5. Fördefinierade hälsningar och uppmuntran.
6. Säkerhetstest: IDOR, manipulerade bidrag, dubbla klick, replay.

**Kvalitetsgrind B8:** en grupp över Android och iOS bygger tillsammans utan dupliceringar eller läckor.
Ingen medlem påverkas negativt av en annans frånvaro.
Vilan respekteras.

### M9 — Visuell kvalitet, ljud och UX

1. Art bible: arkitektur, biomer, bybor, sjöfart, palett och ljus över hela dygnet.
2. Produktionsflöde för low-poly-assets, shaders, efterbehandling och ljussättning.
3. Terräng, byggnader i alla nivåer, bybor, skepp, expeditioner, nattmiljö, gryningsövergång.
4. Huvud-UI: krafter, rapport, byggkatalog, ekonomi, karta, forskning, grupp, butik.
5. Touchstyrning, kamera, ångra, felmeddelanden.
6. Musik, ambience för dag och natt, UI-ljud.
7. Lokalisering för engelska och svenska.
   Tillgänglighet.
8. Usabilitytest med nya spelare.

**Kvalitetsgrind B9:** visuell granskning godkänd.
Spelet är vackert på målenheterna inom prestandabudgeten.
Kritiska handlingar går att hitta utan instruktion.

### M10 — Epoker, innehåll och intäkter

1. Epoker för release (O-03), med verktyg för att producera nya epoker efter release.
2. Balansgranskade byggnader, resurser, kedjor, forskningsgrenar och fynd inom omfånget från M4 (O-02).
3. Historik för världen och arkipelagen.
4. Innehållsvalidatorer i CI.
5. Kosmetika, supporter-prenumeration (O-06), serverbaserad köpvalidering, inga reklam-SDK:er.

**Kvalitetsgrind B10 / GRIND B:** hela spelet fungerar på båda plattformarna, i grupp och över kontobyte.
Inget innehåll låser fast spelaren.
Intäktsmodellen bryter inte mot MON-04.

### M11 — QA, balansering, alfa och beta

1. Komplett testmatris för domän, integration, mobil, backend, tidszoner och behörigheter.
2. Massimulering över år av speltid med låg, hög, ingen och manuell egenvård.
3. Property-based-test av invarianter, idempotens och tid.
4. Belastningstest av backend, reconnect och handel.
5. Profilering av batteri, minne, fps och starttid.
6. Sluten alfa med djupintervjuer.
7. Beta i 30–60 dagar med grupper av vänner med olika rutiner och datakällor.
8. Mätning av motivation och negativa effekter: stress, grupptryck, förskjuten läggtid.
9. Frys nytt innehåll inför releasekandidaten.

**Kvalitetsgrind C11:** inga blockerande eller allvarliga fel.
Kvalitetsmålen är uppfyllda.
Testerna stödjer tesen om ett roligt och hälsosamt spel.

### M12 — Release 1.0

1. Varumärke (uppdatera [namnkontrollen](./docs/research/eirora-name-check.md) och säkra namnet, O-11), butikssidor, skärmbilder, trailer, supportmaterial.
2. Deklarationer för Health Connect och HealthKit, integritetsetiketter, dataskydd.
3. Oberoende säkerhetsgranskning av hälsodata och gruppflöden.
4. Katastrofåterställning, migrationstest, rollbackplan.
5. Stegvis utrullning på båda plattformarna samtidigt.
6. Underhållsplan och backlog för 1.1 (nya epoker) och V2 (geografiska kartor).

**Kvalitetsgrind C12 / RELEASE:** alla P0-krav och godkända P1-krav är verifierade, butikskraven uppfyllda och rollback och restore testade.

---

## 3. Parallella arbetsströmmar

| Ström | Start | Fokus |
|---|---|---|
| Speldesign/balans | M0 | Krafter, ekonomi, epoker, grupper |
| Dagligt prototypspel | M1 | Produktägaren spelar och dokumenterar i [prototypdagboken](./docs/design/prototype-diary.md), kontinuerligt fram till beta |
| Simulering/backend | M1 | Delad C#-simulering, ASP.NET Core, beständighet |
| Klient/UX | M1 | Unity, kamera, UI, rapporter |
| Art/ljud | M0 | Art direction, ljus, assets, musik |
| Hälsa/OS | M1-spike | Health Connect, HealthKit, integritet |
| Drift/QA/privacy | M2 | CI, observability, DPIA, policy |

---

## 4. Beroendekarta

```mermaid
flowchart TD
    M0["M0 Kärndesign"] --> M1["M1 Prototyp i fickan"]
    M0 --> M2["M2 Teknisk grund"]
    M1 -. lärdomar .-> M0
    M1 -. lärdomar .-> M2
    M2 --> M3["M3 Världsgenerering"]
    M3 --> M4["M4 Ekonomi & logistik"]
    M4 --> M5["M5 Krafter, expeditioner, epok 1"]
    M5 --> GATEA{"Grind A: spegeln fungerar?"}
    M1 --> M6["M6 Hälsomotor"]
    GATEA --> M6
    M4 --> M7["M7 Vilolås"]
    M6 --> M7
    M2 --> M8["M8 Grupper & arkipelag"]
    M6 --> M8
    M7 --> M8
    M0 --> M9["M9 Visuell kvalitet & UX"]
    GATEA --> M9
    M5 --> M10["M10 Epoker, innehåll, intäkter"]
    M8 --> M10
    M8 --> GATEB{"Grind B"}
    M9 --> GATEB
    M10 --> GATEB
    GATEB --> M11["M11 QA, alfa, beta"]
    M11 --> M12["M12 Release 1.0"]
```

---

## 5. Epics och första backlog

| Epic | Milstolpe | Beskrivning | PRD-ID |
|---|---|---|---|
| EP-01 | M0 | Kärndesign, invarianter, krafter, ekonomi | CIV-*, EXP-*, HLT-09 |
| EP-02 | M1 | Spelbar prototyp i fickan | Tvärs |
| EP-03 | M2 | Simuleringskärna, sparformat, CI, backend-skelett | NFR-01/02/09/11, ACC-* |
| EP-04 | M3 | Kartdata, generator, hexgeometri | MAP-* |
| EP-05 | M4 | Bygg, produktion, logistik | CIV-*, LOG-01…04/06 |
| EP-06 | M5 | Krafter, expeditioner, forskning, onboarding, epok 1 | HLT-05/09, EXP-*, RES-*, PRG-* |
| EP-07 | M6 | Health Connect, HealthKit, bidrag, integritet | HLT-* |
| EP-08 | M7 | Vilolås och dygnscykel | RST-* |
| EP-09 | M8 | Grupper, arkipelag, handel, gemensamma byggen | SOC-*, LOG-05, MAP-07 |
| EP-10 | M9 | Art, ljud, UX, lokalisering, tillgänglighet | UX-*, NFR-13 |
| EP-11 | M10 | Epoker, innehåll, intäkter | PRG-03/05, NAR-01, MON-* |
| EP-12 | M2–M12 | Drift, säkerhet, integritet, test | OPS-*, NFR-* |

### Första uppgifterna

1. **TASK-001 — Kärnfantasi:** 1–2 sidor om varför "din värld speglar din egenvård" blir ett bra strategispel och inte en stegräknare.
2. **TASK-002 — Simuleringsinvarianter.**
3. **TASK-003 — Krafternas regler v1:** omvandling, tak, avtagande avkastning, manuell inmatning, grundnivå.
4. **TASK-004 — Ekonomi v1 med siffror:** byggnader, resurser, avvägningar och kraftbeslut.
5. **TASK-005 — UX-resor:** första sessionen, sju dagars frånvaro, kväll med vilolås.
6. **TASK-006 — Art direction v0:** moodboard, palett, ljus över dygnet: [art-direction.md](./docs/design/art-direction.md).
7. **TASK-007 — Spike för Health Connect** på produktägarens telefon; HealthBridge eller egen adapter (O-08).
8. **TASK-008 — Unity-prototyp:** ö, ekonomi, krafter, vilolås, rapport.
9. **TASK-009 — Format för prototypdagbok** och rutin för att återföra lärdomar: [prototype-diary.md](./docs/design/prototype-diary.md).
10. **TASK-010 — Reporstruktur och CI** för Android och iOS.

### Nästa uppgifter

Uppgifterna täcker det som återstår till grind A0 och A1 och hela M2 fram till grind A2.
Beroendena står som GitHub-beroenden på ärendena; M2-ärenden som bygger på prototypen väntar på TASK-008.

| Uppgift | Milstolpe | Blockeras av |
|---|---|---|
| [TASK-011 — Kontur för epok 1](https://github.com/Tiico/eirora/issues/26) | M0 | – |
| [TASK-012 — Grind A0: en hel dag med siffror](https://github.com/Tiico/eirora/issues/27) | M0 | TASK-011 |
| [TASK-013 — Hälsodata i prototypen via Health Connect](https://github.com/Tiico/eirora/issues/28) | M1 | TASK-007, TASK-008 |
| [TASK-014 — Installera och uppdatera utan att världen går förlorad](https://github.com/Tiico/eirora/issues/29) | M1 | TASK-008 |
| [TASK-015 — Prestanda och batteri på produktägarens telefon](https://github.com/Tiico/eirora/issues/30) | M1 | TASK-008 |
| [TASK-016 — Grind A1: 14 spelade dygn och lärdomar återförda](https://github.com/Tiico/eirora/issues/31) | M1 | TASK-013, TASK-014 |
| [TASK-017 — ADR: versionering av regler, innehåll och sparningar](https://github.com/Tiico/eirora/issues/32) | M2 | – |
| [TASK-018 — ADR: backend-hosting, auth och databas (O-09)](https://github.com/Tiico/eirora/issues/33) | M2 | – |
| [TASK-019 — Snapshots och replay i simuleringskärnan](https://github.com/Tiico/eirora/issues/34) | M2 | TASK-008, TASK-017 |
| [TASK-020 — Sparformat med migrationer och lokal persistens](https://github.com/Tiico/eirora/issues/35) | M2 | TASK-017, TASK-019 |
| [TASK-021 — Determinism: samma replay på klient och server](https://github.com/Tiico/eirora/issues/36) | M2 | TASK-019 |
| [TASK-022 — Backend-skelett: konto och inloggning](https://github.com/Tiico/eirora/issues/37) | M2 | TASK-018 |
| [TASK-023 — Molnsparning och konflikter mellan enheter](https://github.com/Tiico/eirora/issues/38) | M2 | TASK-020, TASK-022 |
| [TASK-024 — Mätbudgetar och målenheter (O-13)](https://github.com/Tiico/eirora/issues/39) | M2 | TASK-015 |
| [TASK-025 — Varumärkes- och butikskontroll av "Eirora" (O-11)](https://github.com/Tiico/eirora/issues/40) | M2 | – |
| [TASK-026 — Grind A2: teknisk grund](https://github.com/Tiico/eirora/issues/41) | M2 | TASK-020, TASK-021, TASK-023, TASK-024 |

M2 punkt 1 och 2 (reporstruktur och CI med kodanalys och beroendeskanning) levererades i TASK-010.

### Uppgifter för M3

Validatorn kommer före generatorn, så att generatorn byggs mot ett mätbart mål från ekonomi v1 och [epok 1](./docs/design/epoch-1.md).

| Uppgift | Milstolpe | Blockeras av |
|---|---|---|
| [TASK-027 — ADR: kartformat och generatorversion](https://github.com/Tiico/eirora/issues/61) | M3 | – |
| [TASK-028 — Hexgeometri och pathfinding i simuleringen](https://github.com/Tiico/eirora/issues/62) | M3 | – |
| [TASK-029 — WorldMap-format och WorldGenerator-gränssnitt](https://github.com/Tiico/eirora/issues/63) | M3 | TASK-020, TASK-027, TASK-028 |
| [TASK-030 — Validator för kartor och bulkvalidering av seeds](https://github.com/Tiico/eirora/issues/64) | M3 | TASK-029 |
| [TASK-031 — Procedurgenererade ögrupper (generator v1)](https://github.com/Tiico/eirora/issues/65) | M3 | TASK-030 |
| [TASK-032 — Dimma och upptäckt på kartan](https://github.com/Tiico/eirora/issues/66) | M3 | TASK-029 |
| [TASK-033 — Den genererade världen i klienten](https://github.com/Tiico/eirora/issues/67) | M3 | TASK-024, TASK-031, TASK-032 |
| [TASK-034 — Verktyg för att studera seeds och kartor](https://github.com/Tiico/eirora/issues/68) | M3 | TASK-030 |
| [TASK-035 — Förbered arkipelag och geografisk källa](https://github.com/Tiico/eirora/issues/69) | M3 | TASK-029 |
| [TASK-036 — Grind A3: världsgenerering](https://github.com/Tiico/eirora/issues/70) | M3 | TASK-026, TASK-031, TASK-033, TASK-035 |

### Uppgifter för M4

M4 bygger vidare på det som redan finns i `src/Eirora.Simulation/Economy/`.
Byggplatser på varje landruta kom med [TASK-037](https://github.com/Tiico/eirora/issues/79), och avstånd, extraarbete och nivå 2 skalar alla byggnader sedan [TASK-038](https://github.com/Tiico/eirora/issues/88); uppgraderingar, vägar per byggnad, ett fast minimilager, sjövägen till Nyön och långsamma processer finns också.
Uppgifterna nedan beskriver det som återstår.
Produktägaren valde Anno-bredd med gradvis upplåsning, en bosättning per ö och bärare på land (D-105–D-107, TASK-039–041).
Ekonomi v2 (TASK-058) räknar fram den ekonomin innan bärarna och innehållet byggs.
Vad rivning ger tillbaka är avgjort (D-108, TASK-059), och ett produktbeslut återstår: krafternas värden efter balansbotarna (TASK-055).
Uppgifter utan blockerare i tabellen kan påbörjas direkt och går parallellt med att M2 och M3 stängs.
TASK-043–047 byggs ovanpå innehållskatalogen i TASK-042, eftersom alla ändrar `Settlement`.
Scenarierna och balansen (TASK-053–056) väntar på M4:s regler, så att de prövar det som lämnas till M5.
Varje uppgift som tillför tillstånd utökar frånvarotestet från TASK-049, så att långa frånvaroperioder prövas med hela M4.

| Uppgift | Milstolpe | Blockeras av |
|---|---|---|
| [TASK-039 — Beslut: innehållsomfång för byggnader, resurser och kedjor (O-02)](https://github.com/Tiico/eirora/issues/122) | M4 | – |
| [TASK-040 — Beslut: vad en bosättning är (CIV-01)](https://github.com/Tiico/eirora/issues/123) | M4 | – |
| [TASK-041 — Beslut: vägar och transport på land (LOG-01, LOG-02)](https://github.com/Tiico/eirora/issues/124) | M4 | – |
| [TASK-042 — Byggnader och recept som versionerad innehållsdata](https://github.com/Tiico/eirora/issues/125) | M4 | – |
| [TASK-043 — Flaskhalsdiagnostik: varför en byggnad står still](https://github.com/Tiico/eirora/issues/126) | M4 | TASK-042 |
| [TASK-044 — Arbetsfördelning: pausa och prioritera byggnader, ordna byggkön](https://github.com/Tiico/eirora/issues/127) | M4 | TASK-042, TASK-049 |
| [TASK-045 — Varje ö har eget lager och egen sjöväg](https://github.com/Tiico/eirora/issues/128) | M4 | TASK-042, TASK-049 |
| [TASK-046 — Minimilager, importprioritet och reservering (LOG-06)](https://github.com/Tiico/eirora/issues/129) | M4 | TASK-043, TASK-045 |
| [TASK-047 — Rivning av byggnader (D-38)](https://github.com/Tiico/eirora/issues/130) | M4 | TASK-042, TASK-049 |
| [TASK-048 — Balansbotar som spelar den riktiga simuleringen](https://github.com/Tiico/eirora/issues/131) | M4 | – |
| [TASK-049 — Långa frånvaroperioder i en utbyggd värld](https://github.com/Tiico/eirora/issues/132) | M4 | – |
| [TASK-050 — Flera bosättningar (CIV-01)](https://github.com/Tiico/eirora/issues/133) | M4 | TASK-040, TASK-043, TASK-045 |
| [TASK-051 — Bärare och vägar på land (LOG-01, LOG-02)](https://github.com/Tiico/eirora/issues/134) | M4 | TASK-041, TASK-043, TASK-048, TASK-049, TASK-058 |
| [TASK-052 — Anno-bredd med gradvis upplåsning (D-105)](https://github.com/Tiico/eirora/issues/135) | M4 | TASK-039, TASK-042, TASK-043, TASK-048, TASK-049, TASK-058 |
| [TASK-053 — Tre scenarier med olika bästa strategi](https://github.com/Tiico/eirora/issues/136) | M4 | TASK-044, TASK-046, TASK-048, TASK-050, TASK-051, TASK-052 |
| [TASK-054 — Långtidssimulering och förslag till krafternas värden (O-01)](https://github.com/Tiico/eirora/issues/137) | M4 | TASK-053 |
| [TASK-055 — Beslut: krafternas värden efter balansbotarna (O-01 i M4)](https://github.com/Tiico/eirora/issues/138) | M4 | TASK-054 |
| [TASK-056 — De beslutade krafternas värden i reglerna](https://github.com/Tiico/eirora/issues/139) | M4 | TASK-055 |
| [TASK-057 — Grind A4: ekonomi och logistik](https://github.com/Tiico/eirora/issues/140) | M4 | TASK-036, TASK-039–056, TASK-058 och TASK-059 |
| [TASK-058 — Ekonomi v2: befolkningsnivåer, behov, kedjor, upplåsning och bärare](https://github.com/Tiico/eirora/issues/145) | M4 | – |
| [TASK-059 — Beslut: vad rivning ger tillbaka (D-38)](https://github.com/Tiico/eirora/issues/147) | M4 | – |

### Uppgifter för M5

M5 bygger på M4:s ekonomi, så all kod i M5 väntar på grind A4 (TASK-057).
Designen av expeditioner, fynd och forskning (TASK-068, TASK-069) och protokollet för testet med extern testare (TASK-088) kan påbörjas direkt.
Epok 1 räknas om ovanpå dem och krafternas värden från M4 (TASK-070), innan Vårdkasen byggs.
Expeditionerna, forskningens inriktningar och Vårdkasens etapper kommer som vertikala skivor, var och en med sin introduktion i systemet från TASK-072.
Fake health provider (TASK-071) kommer först, eftersom den guidade turen, återkomsten och balansbotarna prövas med den.
Krafternas slutliga värden (O-01) beslutas när balansbotarna spelat hela epok 1 (TASK-085, TASK-086), och regeländringarna väntar på M4:s värden i TASK-056.
Grind A5 prövas med en extern testare (TASK-089) och är också den övergripande grind A för M0–M5.

| Uppgift | Milstolpe | Blockeras av |
|---|---|---|
| [TASK-068 — Design: expeditioner och fynd i epok 1 (EXP-01–03, NAR-01)](https://github.com/Tiico/eirora/issues/194) | M5 | – |
| [TASK-069 — Design: forskningens tre inriktningar (RES-01, RES-02)](https://github.com/Tiico/eirora/issues/196) | M5 | – |
| [TASK-070 — Epok 1 v2: tempo och siffror med ekonomi v2, expeditioner, fynd och forskning](https://github.com/Tiico/eirora/issues/197) | M5 | TASK-068, TASK-069, TASK-055 |
| [TASK-071 — Fake health provider med profilerna låg, hög, ingen och manuell (NFR-11)](https://github.com/Tiico/eirora/issues/198) | M5 | TASK-057 |
| [TASK-072 — Introduktionerna för hela epok 1 (PRG-02)](https://github.com/Tiico/eirora/issues/199) | M5 | TASK-057 |
| [TASK-073 — Den guidade turen enligt resa 1 med ekonomi v2 (PRG-01, UX-03)](https://github.com/Tiico/eirora/issues/200) | M5 | TASK-071, TASK-072, TASK-057 |
| [TASK-074 — Expedition med mål: Ytterön (EXP-01)](https://github.com/Tiico/eirora/issues/201) | M5 | TASK-068, TASK-072, TASK-057 |
| [TASK-075 — Fynd i dimman (EXP-02, NAR-01)](https://github.com/Tiico/eirora/issues/202) | M5 | TASK-068, TASK-057 |
| [TASK-076 — Expeditionstyper i epok 1 (EXP-03)](https://github.com/Tiico/eirora/issues/203) | M5 | TASK-074, TASK-075, TASK-057 |
| [TASK-077 — Runstenen och forskningens första inriktning: produktion (RES-01)](https://github.com/Tiico/eirora/issues/204) | M5 | TASK-069, TASK-072, TASK-057 |
| [TASK-078 — Forskningens andra inriktning: sjöfart och logistik (RES-02)](https://github.com/Tiico/eirora/issues/205) | M5 | TASK-077, TASK-057 |
| [TASK-079 — Forskningens tredje inriktning: kartläggning och kultur (RES-02)](https://github.com/Tiico/eirora/issues/206) | M5 | TASK-077, TASK-075, TASK-057 |
| [TASK-080 — Epoker som innehåll och Vårdkasens grund (PRG-03, D-64)](https://github.com/Tiico/eirora/issues/207) | M5 | TASK-070, TASK-072, TASK-057 |
| [TASK-081 — Vårdkasens stomme och eld med sina förutsättningar](https://github.com/Tiico/eirora/issues/208) | M5 | TASK-080, TASK-074, TASK-077, TASK-057 |
| [TASK-082 — Vårdkasen tänds och epok 2 börjar nästa gryning (D-67, PRG-04)](https://github.com/Tiico/eirora/issues/209) | M5 | TASK-081, TASK-057 |
| [TASK-083 — Sedan sist: återkomst efter frånvaro enligt resa 2 (O-12, AC-02)](https://github.com/Tiico/eirora/issues/210) | M5 | TASK-071, TASK-057 |
| [TASK-084 — Se veckan: tidsförlopp över ön efter frånvaro](https://github.com/Tiico/eirora/issues/211) | M5 | TASK-083, TASK-057 |
| [TASK-085 — Balansbotar spelar epok 1 till milstolpen (O-01, D-34, AC-06)](https://github.com/Tiico/eirora/issues/212) | M5 | TASK-071, TASK-076, TASK-078, TASK-079, TASK-082, TASK-057 |
| [TASK-086 — Beslut: krafternas slutliga värden för epok 1 (O-01 i M5)](https://github.com/Tiico/eirora/issues/213) | M5 | TASK-085, TASK-056 |
| [TASK-087 — Krafternas slutliga värden i reglerna (O-01)](https://github.com/Tiico/eirora/issues/214) | M5 | TASK-086, TASK-056, TASK-057 |
| [TASK-088 — Testprotokoll för grind A5 med extern testare](https://github.com/Tiico/eirora/issues/215) | M5 | – |
| [TASK-089 — Extern testare spelar epok 1 (grind A5)](https://github.com/Tiico/eirora/issues/216) | M5 | TASK-088, TASK-073, TASK-082, TASK-083, TASK-087 |
| [TASK-090 — Grind A5 / Grind A: spegeln fungerar](https://github.com/Tiico/eirora/issues/217) | M5 | TASK-016, TASK-057 och TASK-068–089 |

**Format för backlogpost:** ett GitHub-ärende med Epic, Milstolpe, PRD-referens, Prioritet, Förutsättningar, Beskrivning, Invariant, Acceptanskriterier (Given/When/Then), Testnivå, Integritetspåverkan, Migration och Definition of Done.

---

## 6. Arkitekturbeslut

| ADR | Beslut | Status |
|---|---|---|
| [ADR-0001](./docs/adr/0001-local-world-server-for-shared-state.md) | Den egna världen simuleras lokalt; servern äger det delade | Beslutat |
| [ADR-0002](./docs/adr/0002-unity-csharp-shared-simulation.md) | Unity 6 LTS + C#, delad simulering, ASP.NET Core | Beslutat |
| [ADR-0003](./docs/adr/0003-rest-lock-is-honest-protection.md) | Vilolåset är ett ärligt skydd, inte fuskskydd | Beslutat |
| [ADR-0004](./docs/adr/0004-shared-libraries-as-source-packages.md) | De delade biblioteken kompileras från samma källkod av både .NET och Unity | Beslutat |
| [ADR-0005](./docs/adr/0005-time-arrives-as-readings.md) | Tiden kommer in som avläsningar med tidszon och offset | Beslutat |
| [ADR-0006](./docs/adr/0006-saves-are-migrated-snapshots.md) | Versionering av regler, innehåll och sparningar: sparningen är en snapshot som migreras framåt; gamla regler finns bara som frysta byggen | Beslutat |
| [ADR-0007](./docs/adr/0007-own-health-connect-adapter.md) | Health Connect läses med en egen Kotlin-adapter bakom ett C#-gränssnitt, inte med HealthBridge (O-08, [TASK-007](https://github.com/Tiico/eirora/issues/8)) | Beslutat |
| [ADR-0008](./docs/adr/0008-backend-hosting-accounts-and-database.md) | Backend som containrar med PostgreSQL, hemma till beta och i Azure Sverige vid release; egna konton via Apple och Google (O-09, [TASK-018](https://github.com/Tiico/eirora/issues/33)) | Beslutat |
| [ADR-0010](./docs/adr/0010-world-map-format-and-generator-version.md) | Kartan är en oföränderlig hexskiva med radie 24 som sparas hel; generatorn körs en gång per värld och hålls fast vid sin version ([TASK-027](https://github.com/Tiico/eirora/issues/61)) | Beslutat |
| kommande | Komposition av arkipelag och handelsvalidering (O-04, O-10) | Inför M8 |

---

## 7. Teststrategi

| Testtyp | Start | Verifierar |
|---|---|---|
| Unit + property-based | M1 | Resurskonservering, krafter, idempotens |
| Deterministisk replay, klient = server | M2 | Samma indata ger samma tillstånd |
| Generatorfuzzning | M3 | Rimliga kartor, startresurser |
| Långtidssimulering | M4 | Ekonomi och vila vid lång frånvaro, ingen förfall |
| Balansbotar | M4/M5 | Dominerande strategier, kraftfördelning |
| Native-hälsa på enhet | M1/M6 | Behörigheter, metadata, bakgrund, dubbletter |
| Tid, tidszon och sommartid | M2/M7 | Vilolåsets invariant |
| Säkerhet och flera konton | M8 | Gruppbidrag, handel, IDOR, replay |
| Visuell granskning och prestanda | M1/M9 | Skönhet inom budget på målenheter |
| UI och tillgänglighet | M9 | Touch, läsbarhet, språk |
| Longitudinell beta | M11 | Motivation, grupptryck, sömnrytm |
| Restore och rollback | M2/M12 | Migration och återställning |

### Definition of Done

- Länkat till PRD-krav och acceptanstest.
- Domänregler och gränsfall dokumenterade, med ordlistans termer.
- Inga Unity-beroenden i simuleringsbiblioteket.
- Testat på relevant nivå, inklusive determinism och idempotens där det gäller.
- Migrationspåverkan analyserad.
- Integritet och observability granskade.
- Användarflödet manuellt verifierat på enhet.

---

## 8. Releasechecklista

- [ ] Alla P0-krav verifierade på Android och iOS.
- [ ] Godkända P1-krav levererade; ändringar dokumenterade.
- [ ] Spelet är fullt spelbart med enbart manuell inmatning.
- [ ] Vilolåset håller via normala flöden, med rimliga regler för skift, resor och sommartid.
- [ ] Inga dubbla bidrag.
      Rådata lämnar aldrig enheten.
- [ ] Gruppspel fungerar mellan plattformarna utan negativ koppling.
- [ ] Delning av siffror sker endast med samtycke per vana.
      Läget med enbart bockar är testat.
- [ ] Offline, reconnect, flera enheter, kontobyte och migrering testade.
- [ ] Visuell kvalitetsgrind godkänd inom prestandabudget.
- [ ] Säkerhets- och dataskyddsgranskning genomförd.
- [ ] Health Connect-deklaration och App Review-krav uppfyllda.
- [ ] En beta i 30–60 dagar stödjer långsiktig motivation utan negativ stress.
- [ ] Drift, rollback, incidenthantering, backup och support fungerar.

---

## 9. Efter 1.0

**1.x:** nya epoker, fler kosmetika, fler expeditionskedjor, fler språk, fler vanor (HLT-13).

**V2:** världar genererade från verklig geografi (D-05), givet att samma `WorldMap`-kontrakt används och att licens-, integritets- och balansfrågor är lösta.

Ingen feature får användas som ursäkt för att skjuta grundläggande arkitektur- eller integritetsproblem till senare.

---

## 10. Överlämningsprompt till nästa agent

> Du arbetar på Eirora: *A world worth growing.*
> Läs PRD.md, ROADMAP.md, GLOSSARY.md och docs/adr/ som källor till sanning, och AGENTS.md för arbetsflödet.
> Respektera allt som är markerat [BESLUTAT].
> Utvecklingstid är aldrig en faktor; välj alltid den bästa lösningen.
> Kärnan: egenvård (steg, träning, sömn) blir tre krafter som spelaren strategiskt riktar i en ögrupp på en hexkarta i vacker low-poly 3D med nordiskt mytologiskt tema; inget slut, epoker; grupper om 2–6 delar en arkipelag med endast positiv koppling; vilolås 6–10 h.
> Börja med M0 (TASK-001–006) och förbered M1 så att produktägaren kan spela dagligen på sin Android så snart som möjligt.
> Skapa GitHub-ärenden enligt docs/agents/.

---

## 11. Ändringshistorik

| Version | Datum | Ändring |
|---|---|---|
| 0.1 | 2026-10-17 | Första planutkast M0–M11 (Arkipelagen). |
| 0.2 | 2026-10-17 | Verdawn. Ny ordning med tidig prototyp i fickan (M1). Hälsa, vilolås, grupper och arkipelag som kärna. Unity + ASP.NET Core. Visuell kvalitet som egen ström och grind. M0–M12. |
| 0.3 | 2026-10-17 | Namn Eirora ersätter Verdawn; art direction förankras i det nordiska temat. |
| 0.4 | 2026-10-18 | Nästa uppgifter TASK-011–026: luckorna till grind A0 och A1 och hela M2 till grind A2. |
| 0.5 | 2026-10-18 | Uppgifter för M3 TASK-027–036 fram till grind A3. |
| 0.6 | 2026-10-18 | Grind A0 godkänd: M0 `ACCEPTED`, M1–M3 `IN_PROGRESS`; O-01 i M4 och M5, O-02 i M4. |
| 0.7 | 2026-10-23 | Uppgifter för M4 TASK-039–059 fram till grind A4, med ekonomi v2 efter D-105–D-107; M4 `IN_PROGRESS`. |
| 0.8 | 2026-10-26 | Uppgifter för M5 TASK-068–090 fram till grind A5 och grind A. |
