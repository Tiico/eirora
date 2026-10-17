# ARKIPELAGEN — Product Requirements Document (PRD)

> **Dokumentstatus:** Utkast v0.1 — för vidareutveckling med annan agent  
> **Datum:** 2026-10-17  
> **Produktnamn:** Arkipelagen (arbetsnamn, ej beslutat varumärke)  
> **Målrelease:** Version 1.0 — fullvärdigt mobilspel, inte en reducerad MVP  
> **Relaterat dokument:** [ROADMAP.md](./ROADMAP.md)

## Läsanvisning och beslutsstatus

Det här dokumentet är ett **första förslag**, inte en låst slutlig specifikation. Använd följande markörer:

- **[BESLUTAT]**: uttryckligen överenskommet i produktdiskussionen.
- **[FÖRSLAG]**: genomarbetat produkt-/arkitekturförslag; behöver godkännas.
- **[ÖPPET]**: kräver analys eller produktbeslut innan implementation av beroende funktioner.

**Styrande regel för nästa agent:** ändra inte ett [BESLUTAT] utan att först lyfta det. Markera nya antaganden som [FÖRSLAG] och logga avvägningar och beslut i avsnittet *Beslutslogg*. Tid och implementationsinsats är **inte** skäl att ta genvägar; däremot ska varje funktion fortfarande förtjäna sin plats i spelet.

---

## 1. Sammanfattning

Arkipelagen är ett **passivt, kartbaserat civilisations- och strategispel** där spelarens vanliga hälsosamma aktiviteter bidrar till en levande spelvärld. Spelaren utvecklar bosättningar, skapar produktionskedjor, planerar logistik, utforskar territorier och fattar långsiktiga strategiska beslut. Det mesta sker automatiskt under dagen. Spelaren återkommer kort för att se vad som hänt och göra meningsfulla val.

Spelet ska inte kännas som en aktivitetsmätare med kosmetiska belöningar. Det ska vara ett **bra strategispel i egen rätt**. Hälsodata är en extra motor för möjligheter och progression — inte ett krav för att få vara med.

**Den avgörande särskiljande mekaniken:** när byborna går till vila **låses aktivt spelande** under spelarens anpassningsbara viloperiod. Spelaren kan se sin sovande by, men inte bygga, forska, handla eller ge nya order. Spelet uppmuntrar aktivt återhämtning i stället för sena sessioner, tvångsmässiga streaks eller nattliga notiser.

### 1.1 Beslut som redan är fastställda

| ID | Beslut | Status |
|---|---|---|
| D-01 | Huvudgenre: **strategi och civilisationsbyggande** | [BESLUTAT] |
| D-02 | Flera hälsosamma vanor ska kunna bidra till progression | [BESLUTAT] |
| D-03 | Aktivt spelande ska vara begränsat; världen lever framför allt passivt | [BESLUTAT] |
| D-04 | V1 använder **fiktiv karta** | [BESLUTAT] |
| D-05 | Arkitekturen ska senare kunna generera spelvärld utifrån verklig geografi | [BESLUTAT] |
| D-06 | När byborna vilar är spelet **låst för aktiva spelhandlingar** | [BESLUTAT] |
| D-07 | Byggande, ekonomi, logistik och utforskning ska vara kärnan | [BESLUTAT] |
| D-08 | Ingen stressdriven bestraffning för vilodagar eller frånvaro | [FÖRSLAG, tydligt förankrat i visionen] |
| D-09 | Utvecklingstid begränsar inte omfattningen; V1 ska vara ordentligt genomarbetad | [BESLUTAT] |

### 1.2 Föreslagen produktpositionering

> **Bygg ett rike medan du lever ditt liv. Utforska på dagen. Planera när du vill. Vila när din by vilar.**

Ett lugnt, djupt strategispel för människor som uppskattar byggande och progression men inte vill behöva vara inloggade hela tiden.

---

## 2. Produktmål, målgrupp och problem

### 2.1 Problem som ska lösas

Traditionella hälsoappar reducerar ofta beteenden till mål, siffror och obrutna sviter. Många mobilspel premierar frekventa sessioner och uppmärksamhet även när spelaren borde göra annat. Arkipelagen försöker vända på incitamenten: **ett meningsfullt strategispel som får sin rytm av ett vanligt liv och aktivt respekterar vila**.

### 2.2 Målgrupp [FÖRSLAG]

**Primär:** vuxna mobilspelare som gillar city builders, automation, strategiska avvägningar och långsiktig progression, och uppskattar en låg daglig tidsinsats.

**Sekundär:** personer som vill göra promenader, träning och sömnrutiner mer engagerande utan tävlan om aktivitetsmått.

**Inkludering:** spelare med funktionsvariationer, skiftarbete, varierande aktivitetsnivåer eller ingen uppkopplad hälsokälla ska fortfarande kunna få en komplett spelupplevelse.

**Åldersgräns:** [ÖPPET] — föreslagen initial målgrupp 18+ av integritets-/produktskäl, men juridisk och kommersiell bedömning krävs.

### 2.3 Job-to-be-done

- När jag kommer tillbaka till appen efter en dag vill jag **upptäcka vad min civilisation gjort** utan att ha behövt passa den.
- När jag har ett par minuter vill jag **fatta ett viktigt strategiskt beslut**, inte klicka på hundra belöningsknappar.
- När jag rör på mig, tränar eller sover vill jag att det ska **bidra positivt, men utan tvång och utan jämförelse med andra**.
- När min vilotid börjar vill jag att spelet **tydligt stänger för kvällen** i stället för att locka mig att fortsätta.

### 2.4 Mätbara mål [FÖRSLAG – slutvärden bestäms under alfa]

1. **Strategiskt djup:** testare kan beskriva minst två tydligt olika, fungerande utvecklingsstrategier och faktiska avvägningar.
2. **Låg tidsbörda:** kärnloopen är användbar med omkring 2–5 minuters aktivt spelande per dag, utan krav på daglig inloggning.
3. **Positiv återkomst:** spelare beskriver återkomsten som nyfikenhet/lust, inte skuld eller rädsla att förlora framsteg.
4. **Rättvisa:** låg eller utebliven hälsodata gör inte kampanjen omöjlig; skillnader i aktivitet ger avtagande avkastning.
5. **Teknisk robusthet:** inga förlorade sparningar, dubbla hälsobelöningar eller vägar runt vilolåset under definierade förutsättningar.
6. **Integritet:** spelaren förstår och kan kontrollera varje uppgiftstyp som läses, lagras, delas eller raderas.

KPI:er ska även innehålla guardrails såsom rapporterad stress, sömnförskjutning, upplevt tvång, spelminuter och andel spelare som avstår hälsointegration. Undvik att ensidigt optimera sessionstid och retention.

---

## 3. Spelprinciper (icke förhandlingsbara produktprinciper)

1. **Strategi först:** ett beslut om logistik, produktion eller expansion ska ofta vara viktigare än extra steg.
2. **Passivt som standard:** planera kort, lev livet, återvänd till en meningsfull rapport.
3. **Vila är en spelregel:** nattlåset ska vara verkligt, serververifierat och positivt presenterat.
4. **Inga straff för livet:** sjukdom, resor, dålig sömn, träningsfria dagar och frånvaro ger inte permanent förlust.
5. **Olika vanor, olika möjligheter:** hälsosignaler ger kvalitativt skilda bidrag; inte bara en gemensam poängvaluta.
6. **Rättvis tillgång:** spelet får inte förutsätta att alla kan gå, har smartklocka eller delar hälsodata.
7. **Ägd progression:** ett långsiktigt rike, beständiga beslut, historik och tillförlitliga sparningar.
8. **Dataminimering:** hälsodata används bara när den gör en begriplig skillnad; aldrig för reklamprofilering.
9. **Inget pay-to-win:** [FÖRSLAG] ingen försäljning av aktivitet, vila, byggtid, makt eller belöningsmultiplikatorer.
10. **Ingen manipulerande återkomst:** inga streakstraff, missade belöningar eller skärmhetsande FOMO-mekanismer.

---

## 4. Kärnloopar och förväntad spelupplevelse

### 4.1 Huvudloop

1. **Planera:** välj byggprojekt, arbetsfördelning, forskningsmål, handelsvägar och expeditioner.
2. **Lev:** verkliga vanor registreras efter tillstånd; simuleringen fortskrider enligt spelvärldens regler.
3. **Återvänd:** en begriplig rapport visar produktion, fynd, händelser, flaskhalsar och möjligheter.
4. **Besluta:** ändra prioritering eller välj ett av flera strategiskt intressanta alternativ.
5. **Vila:** när byborna lägger sig låses aktiva spelhandlingar; nästa aktiva period tar vid när vilotiden är över.

### 4.2 Tidsskalor

| Tidsskala | Spelarupplevelse | Spelsystem |
|---|---|---|
| Minuter | Ett eller två meningsfulla beslut | Byggköer, handelsorder, prioriteringar |
| Dag | Nya resultat efter livet utanför appen | Produktion, expeditioner, hälsobidrag, rapporter |
| Vecka | Större projekt och nya områden | Specialisering, regioner, komplexare logistik |
| Månad | Civilisationens form blir tydlig | Städer, teknologier, monument, längre mål |
| Lång sikt | Personlig historia och återspelbarhet | Kampanjmål, sandlåda, nya genererade världar |

### 4.3 Exempel på ett speldygn

- **Morgon:** byn öppnar efter den inställda viloperioden; spelaren kan läsa en sammanfattning och planera.
- **Dag:** bybor arbetar enligt tidigare order; aktivitet kan bidra till exempelvis expeditionsframsteg.
- **Kväll:** spelaren löser en flaskhals och väljer nästa projekt.
- **Vilotid:** UI visar nattmiljö och när spelet öppnar igen; **inga spelkommandon accepteras**. Ingen notifiering väcker spelaren.

Ovanstående är ett exempel. Inga fasta tider är beslutade; schemat är användarstyrt.

### 4.4 Första användarsessionen [FÖRSLAG]

1. Välj språk, tillgänglighetsinställningar och spelets viloschema.
2. Se en tydlig integritetsförklaring: spelet fungerar även utan hälsointegration.
3. Börja i en liten bosättning vid en procedurgenererad kust.
4. Lär dig tre val: placera byggnad, koppla en produktionskedja, skicka expedition.
5. Få ett tydligt och begränsat första mål, exempelvis att försörja fem hushåll och färdigställa en hamn.
6. Erbjud gradvis anslutning till hälsodatakällor med separata, förståeliga tillstånd.
7. Förklara vilolåset och visa en förhandsvisning av nattläget.

---

## 5. Version 1.0 – funktionella krav

Prioriteter: **P0 = krävs för release**, **P1 = planerad V1-omfattning, men detaljer kan revideras efter validering**, **P2 = explicit efter V1**. Om en P1 tas bort krävs ett dokumenterat produktbeslut, inte tyst nedprioritering.

### 5.1 Värld och karta

| ID | Prioritet | Krav |
|---|---|---|
| MAP-01 | P0 | Generera fiktiva hexbaserade världar reproducerbart utifrån seed + generatorversion. |
| MAP-02 | P0 | Stöd terräng, biom, höjd, vatten, resurser, färdbarhet och byggbarhet. |
| MAP-03 | P0 | Ge geografiska avvägningar: kust, floder, berg, avstånd och flaskhalsar påverkar val. |
| MAP-04 | P0 | Visa dimma över outforskade områden och stöd beständiga upptäckter. |
| MAP-05 | P0 | Separera geografigenerering från simulering och rendering med versionshanterat WorldMap-format. |
| MAP-06 | P0 | Spara faktiskt genererat tillstånd, inte bara seed; äldre världar överlever generatoruppgraderingar. |
| MAP-07 | P1 | Kartlager för resurser, transportsystem och produktion samt tydliga sök-/navigationshjälpmedel. |
| MAP-08 | P1 | Verktyg för att testa och redigera kartor i utveckling och innehållsproduktion. |
| MAP-09 | P2 | Importera geodata från verkliga områden och transformera till spelvärld. |

### 5.2 Civilisation och resursekonomi

| ID | Prioritet | Krav |
|---|---|---|
| CIV-01 | P0 | Grunda och utveckla flera bosättningar. |
| CIV-02 | P0 | Placera, uppgradera, underhålla och prioritera byggnader med relevanta terräng-/närhetsregler. |
| CIV-03 | P0 | Modellera råvaror, förädling, konsumtion, lager och produktionskapacitet. |
| CIV-04 | P0 | Modellera arbetskraft och invånares basbehov utan permanent förlust p.g.a. spelarens frånvaro. |
| CIV-05 | P0 | Tillhandahålla tydlig diagnostik för produktions- och logistikflaskhalsar. |
| CIV-06 | P1 | Flera byggnadsnivåer, ortsspecialisering och sammanhängande produktionskedjor. |
| CIV-07 | P1 | Stadstillväxt, infrastrukturprojekt, monument och sammanfattande historik. |
| CIV-08 | P1 | Initialt innehållsmål: ungefär 30–40 byggnadstyper, 15–25 resurser och 8–12 produktionskedjor, **endast om varje del tillför faktisk variation**. |

### 5.3 Logistik och handel

| ID | Prioritet | Krav |
|---|---|---|
| LOG-01 | P0 | Bygg vägar och minst ett transportsätt mellan produktionspunkter, lager och bosättningar. |
| LOG-02 | P0 | Transport påverkas av avstånd, terräng, kapacitet och ruttval. |
| LOG-03 | P0 | Spelaren kan se varför en vara inte når mottagaren. |
| LOG-04 | P1 | Hamnar, handelsrutter, regionala lager och specialiserad transport. |
| LOG-05 | P1 | Begripliga automatiseringsregler, exempelvis minimilager, importprioritet och reservering. |

### 5.4 Utforskning, forskning och långsiktig progression

| ID | Prioritet | Krav |
|---|---|---|
| EXP-01 | P0 | Expeditioner har mål, vägval, tillstånd, kostnader och förutsägbara riskregler. |
| EXP-02 | P0 | Upptäckter ger nya kartområden, resurser, hinder eller spelmöjligheter. |
| EXP-03 | P1 | Flera expeditionstyper: kartläggning, handel, forskning eller expeditionsuppdrag. |
| RES-01 | P0 | Forskning öppnar meningsfulla strategier, inte endast numeriska multiplikatorer. |
| RES-02 | P1 | Ömsesidiga prioriteringar och möjlighet till varierande civilisationsinriktningar. |
| PRG-01 | P0 | Tydlig kampanjbåge från första bosättningen till minst ett större slutmål. |
| PRG-02 | P0 | Permanent historik och fortsatt spel efter huvudmålet. |
| PRG-03 | P1 | Återspelbarhet genom varierande världar och strategier. |
| NAR-01 | P1 | Kurerade platser, fynd, världshändelser och begränsade berättelsekedjor. |

### 5.5 Hälsovanor, tillstånd och belöningar

| ID | Prioritet | Krav |
|---|---|---|
| HLT-01 | P0 | Stöd minst **steg, träningspass och sömn/återhämtningssignal** som separata, frivilliga datakategorier där plattformen tillåter. |
| HLT-02 | P0 | Android: adapter för Health Connect. iOS: adapter för HealthKit. |
| HLT-03 | P0 | Begär minsta nödvändiga behörighet för varje funktion och förklara användningen innan systemdialog. |
| HLT-04 | P0 | Deduplicera och korrigera sen/ändrad källdata utan dubbelbelöningar. |
| HLT-05 | P0 | Aktivitet har avtagande avkastning; ingen obegränsad fördel av extrema volymer. |
| HLT-06 | P0 | Dålig sömn eller en vilodag medför aldrig straff, förlust av tidigare framsteg eller stängd huvudkampanj. |
| HLT-07 | P0 | Spelet är funktionellt och progression är möjlig även om spelaren nekar all hälsodataåtkomst. |
| HLT-08 | P0 | Användaren kan återkalla, pausa, granska och radera hälsorelaterad data. |
| HLT-09 | P1 | Hälsobidragen skapar kvalitativt olika möjligheter: steg → expeditionsrörelse; träning → särskilda projekt; sömn → varsamma återhämtnings-/planeringsbonusar. |
| HLT-10 | P1 | Balanserade alternativ för begränsad rörlighet och saknade sensorer utan misstänkliggörande. |
| HLT-11 | P2 | Ytterligare kategorier såsom utomhustid eller fokustid, först efter validerad insamling och värde. |

**Viktigt designval [FÖRSLAG]:** Spelet bör ha egen normal basprogression via tid och strategi. Hälsovanor ger nya val, expeditionstillfällen eller begränsade boostar; de ska inte vara enda vägen till nödvändiga resurser. Sömnkvalitet är inte en poängsatt moralisk prestation.

### 5.6 Vilolås och dygnscykel

| ID | Prioritet | Krav |
|---|---|---|
| RST-01 | P0 | Spelaren ställer in en återkommande viloperiod; skiftarbete och tidszonsbyten ska beaktas. |
| RST-02 | P0 | Under viloperioden blockeras alla spelpåverkande kommandon **i backend**, inte bara i UI. |
| RST-03 | P0 | Under vila visas en nattlig byvy, information om när spelet öppnar och ett lugnt avslutsbudskap. |
| RST-04 | P0 | Bybornas arbete/produktion pausar under deras vila; redan existerande resurser och framsteg bevaras. |
| RST-05 | P0 | Hälsodata får synkas i bakgrunden, men belöningar tillämpas vid nästa aktiva fas utan att kräva nattliga inloggningar. |
| RST-06 | P0 | Inga gameplay-notiser skickas under viloperioden. |
| RST-07 | P0 | Konto, säkerhet, integritet, tillgänglighet, hjälp och radering ska vara åtkomliga även när gameplay är låst. |
| RST-08 | P0 | Inget köp, annonsval, premiumläge eller tillstånd tillåts låsa upp gameplay under vilan. |
| RST-09 | P1 | Ändringar av viloschema får inte kringgå pågående viloperiod; legitima ändringar ska ändå vara möjliga. |
| RST-10 | P0 | Spelet får inte kräva bevisad sömn eller viss sömnlängd för att bli upplåst. |

**Regel för schemaändring [FÖRSLAG]:** vid ändring under pågående viloperiod träder nytt schema i kraft efter den redan påbörjade vilan. Nödvändiga stödåtgärder för resor/skiftarbete måste utforskas under UX-test.

### 5.7 Konton, sparning, offline och socialt

| ID | Prioritet | Krav |
|---|---|---|
| ACC-01 | P0 | Tillförlitlig kontohantering, återställning och molnsparning mellan enheter. |
| ACC-02 | P0 | Serversida är auktoritativ för spelkommandon, ekonomi, tid och vilolås. |
| ACC-03 | P0 | Konflikter vid samtidiga sessioner ger inga duplicerade föremål eller korrupt tillstånd. |
| ACC-04 | P0 | Definierat beteende vid avbrott/offline, inklusive korrekt återkomst och synk. |
| ACC-05 | P0 | Export/radering av användardata enligt fastställd dataskyddsprocess. |
| SOC-01 | P1 | Frivillig vänlista och möjlighet att besöka andras civilisationer i visningsläge. |
| SOC-02 | P1 | Minst en kooperativ, asynkron världsaktivitet (t.ex. gemensamt monument). |
| SOC-03 | P0 | Hälsodata och detaljerade hälsosignaler är aldrig synliga för vänner som standard. |

**Offline-beteende [FÖRSLAG – ADR krävs]:** när servern inte kan kontaktas erbjuds en **skrivskyddad lokal världsvy**, med köad hälsosynk men inga bygg-/handelskommandon. Detta är den enklaste konsekventa lösningen för ett absolut vilolås. Fullt spelbart offline-läge kräver en separat, grundligt säkerhetsgranskad modell och ingår inte automatiskt i V1.

### 5.8 Presentation, kvalitet och tillgänglighet

| ID | Prioritet | Krav |
|---|---|---|
| UX-01 | P0 | Mobilanpassad styrning: zoom, panorering, markering, byggplacering, avbryt/ångra där möjligt. |
| UX-02 | P0 | Daglig rapport visar relevanta händelser, fynd, flaskhalsar och nästa möjliga beslut. |
| UX-03 | P0 | Begriplig onboarding som inte kräver hälsobehörighet från start. |
| UX-04 | P0 | Gemensam visuell stil, dag/natt-gestaltning, ljud och tydlig animation. |
| UX-05 | P0 | Reducerad rörelse, läsbara kontraster, skalbar text och alternativa uttryck för färg. |
| UX-06 | P0 | Full svenska och engelska i samtliga grundflöden. |
| UX-07 | P1 | Fördjupade överlägg för ekonomi, transport, forskning och historik. |
| OPS-01 | P0 | Säker releaseprocess, övervakning, återställning och versionshanterade datamigrationer. |
| OPS-02 | P0 | Automatiserade tester på domän, integration, klientflöde och verkliga enheter. |

---

## 6. Föreslagen spelmodell – tillräckligt konkret för prototyp

### 6.1 Karta och topologi

- Axiala hexkoordinater `(q,r)`; varje bricka innehåller terräng, biom, höjd, resurser och byggnadsregler.
- Världar består av regioner/öar med kustlinje, farleder, inland, flaskhalsar och olika ekonomiska förutsättningar.
- `WorldGenerator` producerar normaliserad karta: `ProceduralGenerator` i V1; `GeographicGenerator` kan införas senare.
- Generatorns utdata valideras mot nödvändiga startresurser, anslutbarhet, expansionsvägar och förekomst av strategiskt värdefulla variationer.
- Den slutliga kartan lagras tillsammans med `generatorVersion`, seed och härkomstmetadata för reproducerbarhet och framtida migrering.
- En riktig karta får senare påverka topologin, men **spelregler och balansering får inte bero på att varje plats i världen har rättvisa verkliga resurser**.

### 6.2 Ekonomi och medvetna avvägningar

Föreslagna resurslager:

1. **Basbehov:** mat, vatten, bostäder, grundläggande tjänster.
2. **Naturresurser:** trä, sten, malm, fibrer och regionala specialresurser.
3. **Förädlade varor:** plankor, verktyg, byggmaterial, textilier.
4. **Avancerade varor:** navigationsutrustning, forskningsinstrument, monumentdelar.
5. **Kapaciteter:** invånarnas arbetstid, transportkapacitet, lager, forskningskapacitet.

Exempel på avvägning: bygga nytt sågverk nära skog **eller** först anlägga en transportled för att öka nyttjandet av befintligt sågverk. Båda kan vara rimliga beroende på läge och utvecklingsplan.

En produktionskedja behöver definiera insatsvaror, utvaror, produktionstid, arbetsbehov, lagerkrav och transportberoende. Ingen obligatorisk kärnresurs får ha ett enda, lätt permanent blockerbart sätt att produceras.

### 6.3 Invånare

- Invånare har roller/yrken, arbetskapacitet, grundbehov och en **gemensam dygnsrytm**.
- Fokus i V1 är strategiska populationer/arbetsgrupper snarare än full individ-AI för varje person. Namngivna karaktärer kan bära berättelser och expeditioners historik.
- Viloperioden ska vara en del av simuleringens domän, inte en kosmetisk klientanimation.
- Frånvaro får inte orsaka oåterkalleliga utplåningsspiraler; allvarliga ekonomiska problem ska vara återhämtningsbara.

### 6.4 Teknologi och kampanj

- Forskningsträdet innehåller minst tre distinkta inriktningar: **produktion**, **logistik/sjöfart**, **kartläggning/kultur** [FÖRSLAG].
- Spelaren kan utveckla mer än en inriktning över tid men måste prioritera här och nu.
- Kampanjen börjar i en liten öbosättning och kulminerar i etablerandet av ett större arkipelagnätverk samt färdigställandet av ett gemensamt civilisationsmonument [FÖRSLAG].
- Efter kampanjmålet fortsätter världen i sandlådeläge; nytt spel med annat seed är ett aktivt val, inte en påtvingad prestige-reset.

### 6.5 Relation mellan vanor och spel

| Verklig signal | Föreslagen effekt | Begränsning/kompensation |
|---|---|---|
| Steg | Begränsat dagligt tillskott till planerade expeditioner | Avtagande avkastning, alternativa progressionsvägar |
| Träningspass | Begränsade utvecklingsmöjligheter för större projekt | Intensitet behöver inte mätas; inga tunga pass krävs |
| Sömn | Positiv återhämtningshändelse eller mild planeringsbonus | Ingen negativ effekt vid låg/okänd sömn; inte kopplad till upplåsning |
| Ingen hälsodata | Basprogression via spelets egen ekonomi och tid | Full kampanj ska vara möjlig |

Denna mappning ska **speltestas**, inte hårdkodas som sanning. Undvik medicinska påståenden om att spelet förbättrar hälsa eller sömn innan sådant kan styrkas.

---

## 7. Domänmodell och tekniska kvalitetsprinciper [FÖRSLAG]

### 7.1 Viktiga entiteter

`Account`, `PlayerProfile`, `Civilization`, `WorldMap`, `Region`, `HexTile`, `Settlement`, `Building`, `ProductionRecipe`, `Inventory`, `TransportRoute`, `PopulationGroup`, `Expedition`, `ResearchProject`, `GameEvent`, `SimulationSnapshot`, `HealthContribution`, `RestSchedule`, `CooperativeProject`.

### 7.2 Systemgränser

- **Klient:** Godot 4.x med GDScript för världsvy, UI, animationer och lokal read-only-cache.
- **Native adapters:** Kotlin/Health Connect och Swift/HealthKit för plattformsfunktioner.
- **Backend:** Java/Spring Boot som auktoritativ domänsimulator, kommandohanterare och API.
- **Databas:** PostgreSQL för beständigt tillstånd, händelseloggar och snapshots.
- **Karta:** versionshanterad genereringsmodul utan direkt beroende till mobilklienten.

Detta är ett tekniskt **förslag**, inte ett användarbeslut. Vi ska göra ett tidigt device-spike för Android/iOS och native hälsoplugins innan vi låser stacken.

### 7.3 Simulering och tid

- Alla spelpåverkande ändringar sker som validerade kommandon med stabila idempotensnycklar.
- Serverns klocka avgör giltighet för gameplay; lokal klocka är aldrig ensam auktoritet.
- Simuleringen kan spolas fram med deterministiska steg från senaste snapshot, utan en permanent tick per aktiv spelare.
- Pengar/resurser uttrycks i heltal eller fixed-point där flyttalsavrundning annars kan göra replay olika.
- Regelversion och tillämpade spelhändelser måste gå att reproducera i tester.
- Under viloperioden räknas **ingen arbetsproduktion**; viloblocket ska ingå i den tidsbaserade projektionen.
- Sent inkommande hälsodata hanteras genom versionshanterade, idempotenta kompensationshändelser snarare än att blindskriva gammalt tillstånd.

### 7.4 Hälsodatans livscykel

1. Adapter läser enbart godkända typer från operativsystemet.
2. Klienten normaliserar och aggregerar till minimala bidragsintervall.
3. Dubbletter/korrigeringar identifieras med stabil identitet, källprioritet och ledger.
4. Backend räknar ut **begränsat gameplay-bidrag**; rådata lagras helst inte centralt.
5. Användaren kan pausa anslutning, se datahantering och begära radering.
6. Återkallat tillstånd innebär inga framtida läsningar eller försök att kringgå OS-begränsningar.

**Säkerhetsavvägning:** stark anti-cheat får inte kräva oproportionerlig insamling av känsliga uppgifter. Vi accepterar att total teknisk säkerhet mot manipulerad klientdata är en annan fråga än att säkerställa serverauktoritativ ekonomi.

### 7.5 Dataskydd och regler

- Genomför dataskyddsbedömning med fokus på känsliga hälso-/aktivitetsuppgifter, lagringsplatser, rättslig grund, gallring, barns data, underbiträden och raderingskedjor.
- Kontrollera då gällande App Store- och Play-policy innan implementerade behörigheter och release.
- Sälj inte eller använd hälsodata för annonsering eller användarprofilering.
- Definiera granularitet för telemetry så att enskild hälsodata inte hamnar i loggar, analytics, crash reports eller supportexporter.
- Dokumentera hotbild: dataexponering, replay, dubbelbelöning, auktorisationsfel, tidsmanipulation, kontoövertagande, schema-bypass.

### 7.6 Tillgänglighet och alternativ

- Om motoriska begränsningar, funktionsvariationer eller saknad enhet hindrar steg- eller träningsdata ska spelet fortfarande gå att spela och avsluta.
- Under onboarding ska det finnas ett neutralt alternativ att spela utan hälsokälla.
- Alternativ progression ska ge strategiskt innehåll — inte kännas som ett straffläge.
- Undvik formuleringar som rangordnar spelarnas kroppar eller vanor.

---

## 8. Icke-funktionella krav (NFR)

| ID | Område | Krav / verifieringsinriktning |
|---|---|---|
| NFR-01 | Tillförlitlighet | Inga kända fall av irreversibelt förlorad spelprogression i releasetester; automatisk backup och testad restore. |
| NFR-02 | Determinism | Replay av samma kommandon och versionerat starttillstånd ger samma ekonomiska tillstånd. |
| NFR-03 | Prestanda | Mät på riktiga låg-/mellanklassenheter; fastställ konkreta fps-, minnes-, laddtids- och batterimål under M1. |
| NFR-04 | Skalbarhet | Testa samtidiga spelare, on-demand-simulering, tunga världar och reconnect-stormar. |
| NFR-05 | Säkerhet | Hotmodell, behörighetskontroller, kryptering vid transport och vila, hemlighetshantering och penetrationstest. |
| NFR-06 | Integritet | Separata samtycken och ändamål; dataminimering; radering och export testade ända till backup-/gallringsrutinerna. |
| NFR-07 | Portabilitet | Android och iOS får likvärdig kärnfunktionalitet där plattformarnas API tillåter det. |
| NFR-08 | Internationalisering | Text/resurser separeras från kod; svenska och engelska stöds och testas. |
| NFR-09 | Uppgraderbarhet | Sparschema, kartformat, API och spelsimuleringsregler versionshanteras/migreras. |
| NFR-10 | Observability | Strukturerade spelhändelser, teknisk telemetry och larm utan känslig hälsodata i loggar. |
| NFR-11 | Testbarhet | Headless-simulering, fake health provider, virtuellt klocklager och automatiserade end-to-end-testflöden. |
| NFR-12 | Respektfull design | Inga notification loops under vila, inga streakstraff, inga köp som kringgår speltid, ingen obligatorisk social konkurrens. |

**Numeriska SLA/prestandabudgetar:** [ÖPPET]. De ska definieras efter device-matris och volymantaganden, inte uppfinnas i PRD-utkastet.

---

## 9. Kritiska acceptansscenarier

### AC-01 — Nattlåset kan inte kringgås via API

**Givet** att den auktoritativa serverklockan ligger inom spelarens aktiva viloperiod, **när** klienten skickar `PLACE_BUILDING`, `START_EXPEDITION` eller `CHANGE_TRADE_ROUTE`, **så** nekas kommandot med förutsägbar felkod, inga ekonomiska sidoeffekter sker, och nattvyn är fortfarande tillgänglig.

### AC-02 — Flera dagars frånvaro

**Givet** en civilisation med fungerande mat- och träproduktion, **när** spelaren återkommer efter sju dygn, **så** visar rapporten begripligt vilka aktiva tidsfönster som simulerats, viloblocken har inte producerat arbete, och inga beständiga framsteg förloras enbart på grund av frånvaron.

### AC-03 — Dubbla stegkällor

**Givet** att en telefon och en klocka rapporterar överlappande steg, **när** import körs upprepade gånger eller i annan ordning, **så** blir beräknat hälsobidrag konsistent och högst ett legitimt belöningsbidrag registreras för respektive normaliserat intervall.

### AC-04 — Nekat tillstånd

**Givet** att spelaren avböjer samtliga hälsobehörigheter, **när** hen spelar huvudkampanjen, **så** fungerar byggande, utforskning, forskning och progression via basmekaniker utan press att godkänna åtkomst.

### AC-05 — Schemaändring och tidszon

**Givet** en viloperiod som påbörjats i tidszon A, **när** mobilen byter tidszon eller spelaren försöker flytta schemat, **så** förblir pågående lås konsekvent enligt serverns regel och ändringar kan planeras utan korrupt tillstånd eller orimliga långa låsningar.

### AC-06 — Variation i strategi

**Givet** två testspelare i samma genererade värld med samma aktivitetsbidrag, **när** de väljer olika logistiska och ekonomiska strategier, **så** får de mätbart olika resultat och ingen valstrategi ska genomgående dominera utan relevant avvägning.

### AC-07 — Kartgeneratoruppgradering

**Givet** en sparning från generatorversion N, **när** app och backend uppgraderas till N+1, **så** laddas den ursprungliga kartan oförändrat medan nya världar använder den nya generatorn.

### AC-08 — Frånkopplad klient

**Givet** att klienten tappar uppkoppling nära vilans start, **när** spelaren försöker ge spelkommandon, **så** kan de inte genomföras offline enligt föreslagen V1-policy. Vid reconnect uppdateras vy och tidsstatus utan att duplicerade kommandon tillämpas.

### AC-09 — Integritetsradering

**Givet** en begäran att radera konto och tillhörande uppgifter, **när** raderingsflödet fullföljs, **så** är identifierbara primärdata raderade enligt beslutad policy, externa behandlingar hanteras och reservkopior omfattas av dokumenterad gallrings- och återställningsprocess.

---

## 10. Utanför scope för V1

- Geografiskt genererade kartor från faktiska platser (endast stödjande arkitektur).
- Kontinuerlig GPS-spårning som kärnkrav.
- Realtids-PvP, territoriella krig, attacker och plundring.
- Kontinuerliga realtidsmultiplayer-sessioner.
- Generativ AI som styr kritiska spelregler eller spelarens belöningar.
- Reklamstyrd monetisering, köp av viloupplåsning eller gameplay-fördelsköp.
- Medicinska rekommendationer eller påståenden om behandlings-/hälsoeffekt.
- Fullt, auktoritativt offline-spel (kräver separat framtida designbeslut).

---

## 11. Största produkt- och teknikriskerna

| ID | Risk | Varför viktig | Tidig åtgärd |
|---|---|---|---|
| R-01 | Spelet blir en glorifierad stegräknare | Svag strategisk kärna | Testa ekonomi/logistik utan hälsodata före integration |
| R-02 | Hälsointegrationer är otillförlitliga i bakgrunden | Försenade/bortfallna signaler | Native device-spike tidigt, stöd sen synk |
| R-03 | Vilolås går att kringgå eller känns orättvist | Produktlöftet förloras | Auktoritativ serverregel, test av resor/skiftarbete |
| R-04 | Spelare utan sensor/kapacitet missgynnas | Inkluderings- och retentionrisk | Alternativ progression och användartest |
| R-05 | Simuleringsmodellen blir svår att uppgradera | Flera månaders progression kan brytas | Versionerade regler, replay, snapshots och migrationsprov |
| R-06 | För mycket innehåll men lite meningsfull variation | Oändlig utveckling utan djup | Tidiga djupmål; varje byggnad måste fylla ett syfte |
| R-07 | Sociala system missbrukas | Integritets-/moderationsrisk | Frivilligt, minimalt, asynkront och utan hälsodelning |
| R-08 | Batteri och prestanda | Passivt mobilspel får inte belasta vardagen | Mät, budgetera och minimera bakgrundsaktivitet |
| R-09 | Felaktig behandling av hälsodata | Juridik, förtroende och releaserisk | Privacy by design; separat säkerhets-/juridisk granskning |

---

## 12. Beslutslogg och öppna frågor

### Beslutslogg (start)

| ID | Datum | Beslut | Underlag |
|---|---|---|---|
| D-01–D-07 | 2026-10-17 | Se tabellen i avsnitt 1.1 | Produktdialog |
| D-09 | 2026-10-17 | Fullvärdig V1 utan tidsdrivna genvägar | Produktdialog |

### Prioriterade öppna frågor

| ID | Fråga | Föreslagen utgångspunkt | Beslutas före |
|---|---|---|---|
| O-01 | Exakt hur lång är vila, och kan schema ändras under pågående natt? | Användarvalt schema; pågående block kan inte kortas för gameplay | M6 design |
| O-02 | Hur fungerar spelet utan hälsodata? | Fullständig basprogression med frivilliga alternativa bidrag | M0/M5 |
| O-03 | Vilka ekonomiska resurser/byggnader skapar bästa balans? | Definiera genom headless prototyp | M3 |
| O-04 | Hur mycket kan hälsosignaler påverka expedition/forskning? | Mjuka tak och avtagande effekt | M5 |
| O-05 | Android/iOS från första releasen? | Ja | M1 |
| O-06 | Val av teknisk stack? | Godot + native adapters + Spring Boot + Postgres | M1 |
| O-07 | Ska man kunna bygga offline? | Nej i V1, read-only vid nätbortfall | M1 |
| O-08 | Monetisering och ägarform? | Utred engångsköp utan pay-to-win | M0/M9 |
| O-09 | Hur mycket socialt spel i V1? | Vänner + ett asynkront samarbetsprojekt | M0/M9 |
| O-10 | Vad är den färdiga kampanjens narrativa slutmål? | Arkipelagens nätverk + stort monument | M0/M4 |
| O-11 | Vilken grafisk stil och kameraperspektiv? | Isometrisk/2.5D varm stil | M0/M7 |
| O-12 | Vilken målgrupp/åldersgräns och dataskyddsmodell? | Vuxna; juridisk bedömning | M0/M5 |
| O-13 | Vilka kvantitativa kvalitetsmål ska gälla? | Sätt budget efter tidiga mätningar | M1/M10 |
| O-14 | Hur ska schemaändringar vid resa/skiftarbete fungera? | Användartest + explicit regelverk | M6 |
| O-15 | Hur ska sömnsignalen bidra utan att skapa prestationspress? | Mjuk, valfri positiv bonus eller enbart kosmetisk påverkan | M5 |

---

## 13. Riktlinjer för nästa agent

1. **Börja med spelkärnan**, inte teknikstacken: formulera konkreta ekonomiregler och ett litet men strategiskt giltigt spelscenario.
2. Behandla fastställda beslut som invarianta krav; alla andra detaljer är förslag.
3. För varje ny mekanik: dokumentera spelarbeslut, insats, konsekvens, failure mode, fairness, simulering och test.
4. För varje öppen fråga: föreslå 2–3 alternativ, avvägningar och en tydlig rekommendation; uppdatera sedan beslutsloggen.
5. För varje implementerbar funktion: skapa en backlogpost med ID, beroenden, BDD-/acceptanstester, telemetry samt migrationspåverkan.
6. Håll **ROADMAP.md** synkroniserad med ändringar i scope, gates och prioriteringar.
7. Använd ingen extern medicinsk eller juridisk säkerhetsgaranti utan expert-/policyverifiering.

## 14. Referenser att ompröva vid implementation

- Android Health Connect – [steg](https://developer.android.com/health-and-fitness/health-connect/features/steps), [datatyper](https://developer.android.com/health-and-fitness/health-connect/data-types), [publicering](https://developer.android.com/health-and-fitness/health-connect/publish).
- Apple – [HealthKit-behörigheter](https://developer.apple.com/documentation/healthkit/authorizing-access-to-health-data), [konfigurering](https://developer.apple.com/documentation/healthkit/setting-up-healthkit).
- Godot – [Android API-integration](https://docs.godotengine.org/en/stable/tutorials/platform/android/javaclasswrapper_and_androidruntimeplugin.html), [iOS-plugins](https://docs.godotengine.org/en/stable/tutorials/platform/ios/ios_plugin.html).

Länkarna är tekniska ingångar, **inte** en garanti att framtida policyer och API-versioner är oförändrade. Granska aktuell stabil dokumentation vid implementation och inför release.
