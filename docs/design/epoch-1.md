# Epok 1: Vårdkasen

> **Status:** [FÖRSLAG] v0.2, tempot omräknat med byggplatser på brickor (TASK-037), väntar på granskning av produktägaren  
> **Ärende:** TASK-011, TASK-037 · **Milstolpe:** M0, M3 · **PRD:** PRG-02–PRG-05, D-79, D-13, D-17, D-34, D-43, HLT-01, HLT-06, MAP-04, EXP-01, RES-01, O-03, D-63–D-67  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Ekonomi:** [economy-v1.md](./economy-v1.md) · **Krafter:** [forces.md](./forces.md) · **Resor:** [ux-journeys.md](./ux-journeys.md)  
> **Kalkyl:** [models/epoch_1.py](./models/epoch_1.py) (`python -I epoch_1.py`)

Konturen för epok 1: mål och milstolpe, i vilken ordning systemen möter spelaren, var ekonomi v1 hamnar och hur fort det går med olika mycket egenvård.
Tempot är räknat timme för timme med samma motor som ekonomi v1 och samma strategi för alla profiler, fram till milstolpen.
Alla siffror är startvärden för prototypen och trimmas i M4 och M5 (O-01, O-02).
Innehållet i forskningens inriktningar och fynden avgörs i M5, se [avsnitt 8](#8-kvar-att-avgöra-i-m5).

## 1. Epoken i korthet

- **Mål:** att göra den lilla bosättningen på Hemön till en ögrupp, tre öar förbundna med sjövägar, med en by som vet vad den vill bli.
- **Milstolpe:** *Vårdkasen tänds* på Hemöns krön (D-64).
  Den byggs i tre etapper, grunden, stommen och elden, och varje etapp kräver att ett nytt system har kommit i gång.
- **Längd:** 6 veckor med hög egenvård, 7 med normal och 8 med enbart manuell inmatning; 11 med låg egenvård och 11 utan träning (avsnitt 5).
- **Inget nollställs** (D-13, PRG-04).
  Vårdkasen står kvar och brinner, och epok 2 börjar nästa gryning (D-67).

Epoken motsvarar *bosättning* i PRD:ns förslag på epoker (6.4); vilka epoker som följer avgörs i M10 (O-03).

## 2. Milstolpen: Vårdkasen

En vårdkase är en signaleld på ett berg, tänd för att synas långt över vattnet.
Den passar gryningsmotivet och art direction (D-61): ett varmt ljus över ögruppen som också syns i nattvyn.

| Etapp | Förutsättning | Material | Arbete | Byggkraft | Med handkraft (D-65) |
|---|---|---|---|---|---|
| Grunden | Sjöväg till Nyön | 80 sten | 160 h | 500 | 21 spelardygn |
| Stommen | Sjöväg till Ytterön och två forskningssteg | 80 plank, 60 sten | 160 h | 500 | 21 spelardygn |
| Elden | Fyra forskningssteg, dimman lyft över innerhavet och 50 bybor | 60 vadmal, 40 plank | 160 h | 500 | 21 spelardygn |

- Varje förutsättning hör till en egen vana eller ett eget system: sjövägarna kräver Utforskning för expeditionerna, innerhavet kräver Utforskning för dimman, etapperna kräver Byggkraft och forskningen kräver varor och tid.
  Gryningskraften snabbar på allt med dagsverken och rustning.
- 500 Byggkraft per etapp ryms under sparandets fulla tak på 600 (D-35), så att spara ihop till en etapp blir ett eget beslut.
- Etapperna byggs i ordning, en åt gången, och syns på krönet så fort de är klara.
  Spelaren ser alltså framdrift varje vecka och inte bara i slutet.

### Vad spelaren ser när milstolpen nås

1. När elden är klar tänds Vårdkasen.
   Kameran glider upp till krönet, elden flammar och ljuset sprider sig över innerhavet.
   På Nyön och Ytterön tänds små eldar till svar.
2. *Vårdkasen brinner. Den syns över hela ögruppen.*
   Guiden står vid elden; inget poäng, inget betyg och inget *grattis*.
3. Runstenen får en ny rad som berättar epokens historia: när sjövägarna öppnades, vad forskningen gav och när kasen tändes (PRG-04).
4. En etapp med handkraft blir klar i gryningen; då tänds kasen i gryningsljuset och ögonblicket väntar till nästa gång spelaren öppnar spelet.

## 3. I vilken ordning systemen möter spelaren

Ett system introduceras när spelaren når dess förutsättning, inte på en fast dag.
Därför möter alla spelare samma ordning, men i olika takt.
Högst en introduktion visas per spelardygn; en introduktion som väntar visas nästa gång (PRG-02).

| # | System | Förutsättning | Normal | Manuell | Introduktion (tutorialelement) |
|---|---|---|---|---|---|
| 1 | Krafterna och planering | Den guidade turen | Dygn 1 | Dygn 1 | Turen, [resa 1](./ux-journeys.md#resa-1-första-sessionen): dagsverke, kartläggning, huggarkoja och brygga |
| 2 | Byggande och bemanning | Första gryningen efter turen | Dygn 2 | Dygn 2 | Byggkatalogen öppnas med *Värt att titta på*: sågverk och stenbrott på stenådern |
| 3 | Förädling och minimilager | Sågverket är klart | Vecka 1 | Vecka 1 | Guiden visar minimilagret på sågverket: *Så blir det alltid timmer kvar till byggen* (LOG-06) |
| 4 | Expedition | Bryggan är klar | Vecka 1 | Vecka 1 | Bryggan lyser; *Från bryggan kan en expedition segla mot det okända* (EXP-01) |
| 5 | Dimman bricka för bricka | Första kartläggningen är gjord | Vecka 1 | Vecka 1 | En bricka i dimkanten markeras; att lyfta den kostar 10 Utforskning (MAP-04) |
| 6 | Sjöväg | Nyön är hittad | Vecka 1–2 | Vecka 2 | Guiden visar sjövägens kapacitet när bryggan på Nyön är klar (LOG-04) |
| 7 | Vårdkasen | Sjöväg till Nyön | Vecka 2 | Vecka 2 | Krönet på Hemön lyser; *Här kan en vårdkase en dag synas över hela ögruppen* |
| 8 | Forskning | Runstenen är byggd, efter sjövägen | Vecka 2 | Vecka 2 | Runstenen visar tre inriktningar och det första steget (RES-01) |
| 9 | Andra expeditionen | Nyön är hittad | Vecka 2 | Vecka 2–3 | Rapporten föreslår en längre expedition; Ytterön ligger längre bort och tar tre spelardygn |
| 10 | Handkraft | En etapp går inte att betala med Byggkraft | När det behövs | När det behövs | Etappens knapp visar båda vägarna lika stora: *Bygg · 500* och *Bygg med handkraft · 21 dygn* |

Introduktionerna följer UX-resornas tonläge: ett kort ögonblick på kartan, ett förslag under *Värt att titta på* i den dagliga rapporten och aldrig en checklista, en bock eller en räknare.
De går att hoppa över och att se igen under *Hjälp*, även under viloperioden (RST-07).
Ingen introduktion visas under viloperioden; en som väntar kommer efter gryningen.

## 4. Var ekonomi v1 hamnar

| Del av ekonomi v1 | Hamnar i epoken | Roll |
|---|---|---|
| Hemön, startbyn, kartläggning av närområdet och kusten | Dygn 1 och vecka 1 | Turen och första veckans beslut (Närområdet eller Havet) |
| Sågverk, stenbrott, förråd, långhus och stugor | Vecka 1–2 | Grundkedjan som alla tre etapper bygger på |
| Brygga på Hemön och expeditionen till Nyön | Vecka 1 (normal) eller 2 (manuell) | Öppnar havet och förutsättningen för grunden |
| Nyön: brygga, fårgård, stenbrott och sjövägen med 40 enheter per spelardygn | Vecka 2 | Ull och vadmal för tillväxten och för elden; sten till grunden och stommen |
| Väveri och vadmal | Vecka 2 | Snabbare tillväxt mot 50 bybor och 60 vadmal till elden |
| Uppgraderingar till nivå 2 | Vecka 2–8 | Det Byggkraften räcker till utöver etapperna |

Nytt i epoken utöver ekonomi v1 [FÖRSLAG], med siffrorna i kalkylen:

| Nytt | Kostnad | Effekt |
|---|---|---|
| Ytterön | Andra expeditionen: 400 Utforskning, 48 aktiva timmar | Urskog, rikt fiskevatten och odlingsmark; egen sjöväg till Hemön |
| Lyfta dimman bricka för bricka | 10 Utforskning per bricka | Visar kartan och fynden (M5); 150 brickor över innerhavet krävs för elden |
| Runsten | 20 sten, 16 h, en bybo | Platsen för forskningen och världens historik |
| Forskningssteg | 20 plank och 10 sten, eller 20 plank och 10 vadmal; tre spelardygn | Inriktningarna avgörs i M5; dagsverke på runstenen snabbar på |
| Handkraft | Ingen kraft; brygga och väveri 7 spelardygn, etapp 21 spelardygn | Det milstolpen kräver kan byggas utan Byggkraft (D-65) |

Kalkylen bygger på ekonomi v1 med byggplatser på brickor ([ekonomi v1, avsnitt 3.1](./economy-v1.md#31-byggplatser), TASK-037).
Var Ytterön ligger avgör generatorn (TASK-031); kalkylen lägger en handgjord ö med sju landbrickor söder om Hemön.

## 5. Tempo

### Profilerna

| Profil | Rörelse | Träning | Sömn | Utforskning per vecka | Byggkraft per vecka | Gryningskraft per vecka |
|---|---|---|---|---|---|---|
| Hög | 15 000 steg | 6 pass | 8 h | 721 | 345 | 686 |
| Normal | 7 500 steg | 3 pass | 6,5 h | 602 | 270 | 644 |
| Enbart manuell | 40 aktiva minuter fem dagar i veckan (D-41) | 3 pass | 7 h fem nätter, två utan data | 300 | 270 | 560 |
| Låg | 3 000 steg | 1 pass | 6 h | 315 | 100 | 616 |
| Utan träning | 7 500 steg | vanan bortvald | 6,5 h | 602 | 0 | 644 |
| Ingen | | | | 0 | 0 | 280 |

Manuell inmatning ger exakt samma krafter som hälsokällan för samma egenvård (INV-F7).
Profilen *enbart manuell* är därför räknad som en spelare som bara skriver in sina promenader och inte vardagens steg, och som ibland låter sömnen vara.
Den får halva Utforskningen mot normal, vilket är det som gör den långsammare.
Byggkraften är räknad med den rättade regeln för den rullande veckan, se [avsnitt 7](#7-byggkraftens-rullande-vecka).
Kalkylens spelare bemannar förråd, bryggor och förädling före fler fiskebodar så länge maten räcker; bemanningen är gratis planering (D-23).

### Vecka då spelaren når varje steg

| Profil | Nyön | Sjöväg Nyön | Grunden | Ytterön | Sjöväg Ytterön | Forskning 4 | Innerhavet | 50 bybor | Stommen | Elden, kasen tänds | Bybor | Dagsproduktion |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Hög | 1 | 1 | 2 | 2 | 2 | 3 | 4 | 4 | 4 | **6** (dygn 36) | 72 | 460 |
| Normal | 1 | 1 | 3 | 2 | 2 | 3 | 4 | 4 | 5 | **7** (dygn 45) | 72 | 460 |
| Enbart manuell | 2 | 2 | 4 | 3 | 3 | 3 | 8 | 4 | 6 | **8** (dygn 53) | 72 | 551 |
| Låg | 2 | 2 | 5 | 3 | 4 | 3 | 8 | 4 | 8 | **11** (dygn 74) | 84 | 551 |
| Utan träning | 1 | 2 | 5 | 2 | 3 | 4 | 4 | 4 | 8 | **11** (dygn 77) | 68 | 460 |
| Ingen | – | – | – | – | – | – | – | 6 | – | – | 60 | 242 |

*Dagsproduktion* är samma viktade balansmått som i ekonomi v1 och visas aldrig för spelaren.

- **Enbart manuell inmatning når milstolpen vecka 8,** inom 6–10 veckor.
  Det som tar tid är dimman över innerhavet, eftersom promenaderna ger 300 Utforskning i veckan.
- **Normal och hög egenvård skiljer en vecka.**
  Hög egenvård når kasen nio dygn tidigare; mer än så blir det inte, eftersom plank och vadmal till etapperna också tar tid.
  Det är D-34 i praktiken: mer egenvård syns alltid, men den köper inte hela epoken.
- **Låg egenvård når milstolpen senare, utan straff.**
  Inget förfaller under någon körning; kalkylen kontrollerar varje timme att bybor, byggnader, nivåer, upptäckta platser, forskning och etapper aldrig minskar och att inga lager eller saldon blir negativa (HLT-06, INV-V1, INV-R1, INV-F1).
  Den låga profilen bygger alla tre etapperna med handkraft och lägger sin Byggkraft på bryggor, långhus och uppgraderingar.
  När kasen tänds har den en större by än de andra, eftersom byn hunnit växa längre.
- **Utan träning nås milstolpen vecka 11,** med handkraft för bryggor, väveri och alla tre etapperna (D-65).
  Utan handkraft skulle den spelaren aldrig få en sjöväg till Nyön, och den låga profilen skulle inte nå elden på 20 veckor.
- **Utan egenvård nås milstolpen inte,** men världen står still utan att förfalla (D-17).
  Byn växer ändå till 60 bybor med stugor, och dagsverket på grundnivån ger ett beslut varje morgon.

### Vecka för vecka

Beslut som kalkylens spelare fattar, med normal egenvård och med enbart manuell inmatning.
Varje vecka har minst ett beslut om var en kraft ska riktas och minst ett val mellan två rimliga alternativ.

| Vecka | Normal | Enbart manuell | Veckans tunga val |
|---|---|---|---|
| 1 | Turen, kartläggning av närområdet och kusten, sågverk, väveri, expedition till Nyön och brygga där | Turen, kartläggning, sågverk, väveri, långhus; Utforskningen sparas till expeditionen | Närområdet eller havet (ekonomi v1, beslut 1–2); var förråd och huggarkojor står |
| 2 | Expedition till Ytterön och brygga där, Runstenen, första forskningssteget | Expedition till Nyön, brygga på Nyön, Runstenen, första forskningssteget, första uppgraderingen | Byggkraft till sjöväg eller långhus; forskningens första inriktning |
| 3 | Grunden, långhus, forskningssteg, dimman | Expedition till Ytterön, brygga på Ytterön, forskningssteg | Spara till grunden eller bygga nu |
| 4 | Dimman, Byggkraft sparas till stommen | Grunden, forskningssteg, dimman | Vilken inriktning; vilka dimbrickor först |
| 5 | Stommen, forskningssteg | Dimman, Byggkraft sparas till stommen | Plank till stommen, forskning eller uppgraderingar |
| 6 | Dimman över innerhavet, Byggkraft sparas till elden | Stommen, forskningssteg | Spara till elden eller uppgradera; vilken flaskhals får dagsverket |
| 7 | **Elden: Vårdkasen tänds** | Dimman närmar sig innerhavet | Var dimman lyfts mot innerhavet; fynden avgörs i M5 |
| 8 | Epok 2 | **Elden: Vårdkasen tänds** | |

Dagsverke och rustning används varje vecka i alla profiler.
Vecka 5 och 7 för enbart manuell inmatning är de tunnaste: spelaren väntar in Utforskning till dimman.
Fynden i dimman (M5) ska ge just de veckorna egna beslut, se [avsnitt 8](#8-kvar-att-avgöra-i-m5).

## 6. Efter milstolpen

- **Epok 2 börjar nästa gryning** (D-67).
  Rapporten berättar att ett ljus syns långt ute vid horisonten, ett som ingen i ögruppen har tänt.
  Epokens första nya system introduceras först när spelaren själv öppnar det.
- **Inget nollställs** (D-13, PRG-04).
  Byggnader, bybor, lager, sparade krafter, upptäckter och forskning följer med.
  Vårdkasen fortsätter brinna och syns i nattvyn.
- **Epokens system finns kvar.**
  Dimman kan lyftas vidare, forskningens steg 5 och 6 finns kvar och expeditionerna går att skicka mot nya mål när epok 2 lägger till dem.
- **Epoker är innehåll, inte kod.**
  Epok 2 och senare läggs till som versionerat innehåll utan att befintliga världar ändras (PRG-05, NFR-09); hur innehåll versioneras avgörs i TASK-017.
  En värld som nått milstolpen innan epok 2 finns i appen fortsätter som vanligt, och epok 2 börjar vid första gryningen efter uppdateringen.
- **Vad epok 2 innehåller** avgörs i M10 (O-03).
  PRD:ns förslag är handel, vilket passar ljuset vid horisonten och gruppernas arkipelag (M8).

## 7. Byggkraftens rullande vecka

Tempot kräver att Byggkraften per vecka blir den som [forces.md](./forces.md) avser.
Regeln i forces.md v0.1, som också fanns i `ForceRules.BuildPower`, räknar avtagandet från passen under de sex föregående spelardygnen.
I den första veckan ger den rätt summa, men när passen fortsätter vecka efter vecka ger mer träning mindre Byggkraft:

| Pass i veckan | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Regel v0.1, vecka 10 | 100 | 180 | 240 | 200 | 125 | **0** | **0** |
| Rättad regel, vecka 10 | 100 | 190 | 270 | 320 | 345 | 345 | 345 |

Med regel v0.1 når hög egenvård milstolpen först vecka 11, efter normal egenvård vecka 7.
Produktägaren har valt att rätta regeln (D-63): ett spelardygn med pass ger kurvans värde för passen under de sju senaste spelardygnen, minus den Byggkraft som de sex föregående spelardygnen redan gav, dock aldrig under noll.
Varje rullande vecka ger då exakt kurvans värde, vilodagar kostar fortfarande inget och det finns ingen fast veckodag.
Den rättade regeln står i [forces.md](./forces.md#träningspass--byggkraft-rullande-sju-spelardygn) avsnitt 3 och i `ForceRules.BuildPower`.

## 8. Kvar att avgöra i M5

Prototypen i M1 behöver inget av detta: den bygger på ekonomi v1 och epokens första två veckor.

- **Forskningens inriktningar** (RES-01, RES-02): vad stegen i produktion, sjöfart och kartläggning eller kultur öppnar för strategier, och om spelaren kan byta inriktning.
  Kalkylen räknar forskningen utan effekt på ekonomin, så tempot ovan är försiktigt.
- **Fynd** (EXP-02, NAR-01): vad dimman och expeditionerna hittar, så att Utforskning har något att göra hela epoken.
  Utan fynd ligger Utforskningen på taket 800 från vecka 8 i profilen utan träning, samma lärdom som i ekonomi v1 (lärdom 1).
- **Gryningskraftens användning:** forskningen kostar ingen kraft (D-66), så Gryningskraften har fortfarande bara dagsverke och rustning.
  Dagsverke på runstenen ger den ett morgonval till; om det räcker prövas i prototypen (H1) innan fler morgondrag läggs till.
- **Ytteröns innehåll och kartgeneratorns placering** av krön, öar och innerhav (M3).
- **Exakta siffror** för etapperna, handkraften, forskningsstegen och dimman (O-01), med balansbotar och längre körningar i M4 och M5.
- **Guidens repliker** och introduktionernas utseende (M9).

## 9. Beslut

Produktägarens svar på de öppna frågorna i v0.1:

- **D-63:** Byggkraftens rullande vecka rättas så att varje rullande vecka ger kurvans värde för antalet pass; mer träning ger aldrig mindre Byggkraft.
- **D-64:** epok 1:s milstolpe är att Vårdkasen tänds, byggd i tre etapper med egna förutsättningar.
- **D-65:** det milstolpen kräver kan byggas med handkraft utan Byggkraft på en fast tid: 7 spelardygn för brygga och väveri, 21 för varje etapp av Vårdkasen (utvidgar D-43).
- **D-66:** forskning kostar ingen kraft, bara varor och tid.
- **D-67:** nästa epok börjar vid gryningen efter milstolpen, utan att något nollställs.
