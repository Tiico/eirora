# Balansrapport M4

> **Status:** [FÖRSLAG] v1.0, väntar på produktägarens beslut i TASK-055 (#138)  
> **Ärende:** TASK-054 (#137) · **Milstolpe:** M4 · **PRD:** O-01, D-34, D-35, D-43, D-65, AC-02, AC-06  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Krafter:** [forces.md](./forces.md) · **Ekonomi:** [economy-v1.md](./economy-v1.md), [economy-v2.md](./economy-v2.md) · **Epok 1:** [epoch-1.md](./epoch-1.md)  
> **Verktyg:** balansbotarna i `tools/Eirora.Balance` ([development.md](../development.md#balansbotar))

Balansbotarna har spelat ekonomin i en hel epok och ett år, med alla sex profilerna, på scenariernas kartor och på 260 genererade kartor.
Rapporten föreslår krafternas värden och kraftkostnader för M4 och prövar siffror för Havet, som produktägaren bad om 2026-10-26.
Inga regler ändras här; produktägaren beslutar i TASK-055 (#138), och de valda värdena förs in i reglerna i TASK-056 (#139).
Måtten är ekonomi v2:s (avsnitt 9): utvecklingsvärdet och värdet per använd kraft, som aldrig visas för spelaren.

## 1. Förslag i korthet

1. **Krafternas omvandling och tak behålls som i forces.md till M5.**
   D-34 håller efter fyra veckor på alla 260 kartor med normal egenvård, och ingen strategi dominerar i scenarierna.
   Botarna använder 3–12 % av sin Utforskning och omkring hälften av sin Styrka på tolv veckor, så andra kurvor skulle inte ändra världen förrän M5 ger krafterna mer att göra (avsnitt 3).
2. **Kraftkostnaderna behålls som i ekonomi v1 och v2.**
   Billigare *Arbeta extra* bryter D-34, och dyrare byggnader gör Bygden ännu vanligare som bästa strategi (avsnitt 3.4).
3. **Havet vinner oftare med ull först i importprioriteten, utan regeländring.**
   Den förvalda ordningen skeppar öns sten före ullen, och Havet väntar då på kläder.
   Balansboten *Havet, ull först* är bäst på 22 % av kartorna i stället för 3 %, och de fyra scenarierna behåller sina vinnare (avsnitt 4).
   Om produktägaren vill gå längre ger en sjöväg med 60 i stället för 40 enheter per dygn 25 %, men då vinner Havet också scenariot Rik hemö.
4. **Epok 1:s tempo håller ungefär.**
   Uppskattningen ger Vårdkasen en vecka tidigare än epoch-1.md för hög, normal, manuell och låg egenvård, och samma vecka utan träning (avsnitt 5).
5. **Ett år utan spelare och egenvård förfaller ingenting**, och världen når det som grundtakten ger (avsnitt 6).

## 2. Körningarna

| Svit | Kommando | Körningar | Innehåll |
|---|---|---|---|
| Epok | `--suite epoch` | 144 | Varje jämförd strategi med varje profil på de fyra scenariernas kartor i tolv veckor |
| År | `--suite year` | 8 | Ingen spelare utan egenvård och Bygden med normal egenvård på scenariernas kartor i 365 dygn |
| Scenarier | `--suite scenarios` | 24 | Ekonomi v2, avsnitt 15, fyra veckor |
| 260 kartor | `--maps generated --from 0 --seeds 260 --strategies near,sea,sea-wool,village,naive --profiles high,normal,manual,low --days 28` | 5 200 | Vilken strategi som är bäst, och D-34, efter fyra veckor |
| Alla profiler | `--maps generated --from 0 --seeds 260 --strategies near,sea,village,no-training,no-training-by-hand,naive --profiles high,normal,manual,low,no-training,none --days 28` | 9 360 | D-34 med varje profil och strategi |

Alla kommandon körs som `dotnet run -c Release --project tools/Eirora.Balance -- …`.
Botarna spelar den riktiga simuleringen med regelversion 15 och kontrollerar invarianterna varje timme: inget förfaller (INV-V1), inga lager eller saldon blir negativa eller går över sitt tak (INV-R1, INV-F1) och krafterna minskar aldrig utan ett kommando (INV-F6).
Ingen av körningarna bröt mot någon invariant.
Samma körningar ger samma rapport byte för byte; epoksviten kördes två gånger och gav identiska `balance.tsv` och `balance.md`, också när körningarna spelas parallellt.

Rapporten har fått tempokolumner: dygnet då världen fick sjöväg, bönder, hantverkare, sjöfart och 50 bybor, och en uppskattning av när Vårdkasen kan tändas (avsnitt 5).
Svepen i avsnitt 3.4 och 4 som ändrar en regel kördes med en lokal byggnad där bara den konstanten var ändrad, eftersom uppgiften inte ändrar reglerna; varje rad anger vilken konstant.
TASK-056 kör om botarna med de värden som produktägaren väljer.

## 3. Krafterna

### 3.1 Strategin hinner ikapp (D-34)

Produktägaren beslutade 2026-10-26 att D-34 räcker över fyra veckor, eftersom första veckan är guidad och avsiktligt lätt (H4).
Måttet är den genomtänkta strategi som ger minst värde per använd kraft, delat med den naiva strategins med samma profil och karta; forces.md kräver minst 1,3.

| Profil | Lägsta kvot, Närområdet, Havet och Bygden | 5:e percentilen | Median | Kartor under 1,3 | Lägsta med *Havet, ull först* |
|---|---|---|---|---|---|
| Hög | 1,30 | 1,42 | 1,72 | 1 | 1,62 |
| Normal | 1,37 | 1,47 | 1,78 | 0 | 1,67 |
| Enbart manuell | 1,28 | 1,46 | 1,91 | 2 | 1,65 |
| Låg | 1,51 | 1,65 | 1,94 | 0 | 1,52 |

- Kartorna under 1,3 är alla Havet: seed 109 med hög egenvård och seed 172 och 222 med enbart manuell inmatning, där Havet stannar på 58–60 bybor efter fyra veckor.
  Med ull först håller D-34 på alla 260 kartor med alla fyra profilerna.
- Efter tolv veckor ger varje genomtänkt strategi minst 2,3 gånger den naivas värde per använd kraft, eftersom den naiva stannar på 32 bybor.
- Strategin *Utan träning* stannar på 40 bybor på fyra kartor (seed 126, 170, 229 och 259) med alla profiler och ligger då under den naiva.
  Det är botens plan, inte reglerna: på samma kartor växer Bygden och *Utan träning, handkraft* med profilen utan träning till 60–73 bybor och når hantverkare (avsnitt 7).

### 3.2 Var krafterna tar vägen

Krafterna som botarna använder på tolv veckor, median över scenariernas fyra kartor, mot det som egenvården och turen gav:

| Profil | Utforskning använd av intjänad | Styrka använd av intjänad | Sparad Styrka vid slutet |
|---|---|---|---|
| Hög | 250–430 av 8 752 (3–5 %) | 1 745–1 955 av 4 240 (41–46 %) | 821–1 129 |
| Normal | 250–430 av 7 324 (3–6 %) | 1 745–1 955 av 3 340 (52–59 %) | 597–959 |
| Enbart manuell | 250–430 av 3 700 (7–12 %) | 1 810–1 955 av 3 340 (54–59 %) | 625–871 |
| Låg | 250–430 av 3 880 (6–11 %) | 1 285–1 295 av 1 300 (99–100 %) | 5–15 |

Intervallen spänner över Närområdet, Havet och Bygden.

- **Utforskningen har inget att göra efter kartläggningarna och expeditionen.**
  Den ligger på sparandets tak 800 redan efter fyra veckor i alla profiler med steg, som i ekonomi v1 (lärdom 1).
  Vårdkasen behöver 1 900 till: Ytterön för 400 och 150 dimrutor för 1 500 (epoch-1.md, avsnitt 4).
  Fynden i M5 är svaret; en lägre omvandling skulle bara göra profilerna mer lika, utan att ändra världen.
- **Styrkan räcker till mer än byggnaderna med normal och hög egenvård.**
  Hög, normal och enbart manuell använder lika mycket Styrka, och deras världar är nästan lika stora efter tolv veckor; Bygden på Rik hemö får 9 304 med både hög och normal egenvård.
  Mer egenvård syns alltså i tempot, inte i världens storlek, tills Vårdkasens etapper (3 × 500) tar Styrkan som blir över.
  Med normal egenvård blir 1 385–1 595 Styrka över på tolv veckor, ungefär etapperna.
- **Med låg egenvård bestämmer Styrkan.**
  Den används till sista enheten, och Bygden, som använder minst Styrka, är bäst på 98 % av kartorna (avsnitt 7).

### 3.3 Sparandets mjuka tak (D-35)

I alla körningar ligger sparad Utforskning på högst 800, Styrka på högst 1 200 och Ork på högst 600, som forces.md anger.
Under ett år ligger Bygden med normal egenvård på 800 Utforskning och 1 200 Styrka från de första veckorna; resten av det som egenvården ger går förlorat.
Det är avsiktligt (D-35) men gör att krafterna efter epok 1 behöver något att användas till, se avsnitt 8.

### 3.4 Svep över kraftkostnaderna

Närområdet, Havet, Bygden och Naiv med normal egenvård på 260 kartor i fyra veckor, med en kostnad ändrad åt gången:

| Ändring | Konstant | Bygden | Närområdet | Havet | Lägsta D-34 | Kartor under 1,3 |
|---|---|---|---|---|---|---|
| Som i dag | | 77 % | 20 % | 3 % | 1,37 | 0 |
| All Styrka för byggen och uppgraderingar × 0,5 | `buildPower` i `ContentCatalog` | 77 % | 20 % | 3 % | 1,43 | 0 |
| All Styrka för byggen och uppgraderingar × 1,5 | `buildPower` i `ContentCatalog` | 87 % | 10 % | 3 % | 1,22 | 1 |
| Arbeta extra 20 Ork i stället för 30 | `Workday.Cost` | 66 % | 32 % | 2 % | 1,03 | 74 |
| Arbeta extra 45 Ork | `Workday.Cost` | 82 % | 17 % | 2 % | 1,42 | 0 |
| Skynda på 10 Ork i stället för 20 | `Rush.Cost` | 76 % | 21 % | 3 % | 1,38 | 0 |
| Bryggan 30 Styrka i stället för 60 | `Jetty` i `ContentCatalog` | 77 % | 20 % | 3 % | 1,37 | 0 |
| Varvet 40 Styrka i stället för 80 | `Shipyard` i `ContentCatalog` | 77 % | 20 % | 3 % | 1,37 | 0 |
| Expeditionen 150 Utforskning i stället för 250 | `PrototypeIsland.ExpeditionCost` | 77 % | 20 % | 3 % | 1,37 | 0 |

- **Billigare Arbeta extra bryter D-34**, eftersom den naiva strategin lägger all sin Ork på extraarbete och då får lika mycket ut av den som den genomtänkta.
- **Dyrare byggnader gör Bygden ännu vanligare** och sänker D-34 under 1,3 på en karta; billigare ändrar ingenting med normal egenvård, där Styrkan inte bestämmer.
- **Utforskningens och bryggans kostnader bestämmer ingenting,** eftersom Utforskningen ligger på taket och Styrkan räcker.

Förslaget är därför att behålla kostnaderna.

## 4. Havet

Produktägaren: Havet vinner bara på 3 % av de genererade kartorna, och siffror för Havet ska prövas så att Havet vinner oftare.

### 4.1 Varför Havet förlorar

Med normal egenvård har Havet efter fyra veckor en median på 65 bybor och 0 hantverkare, mot Bygdens 66 och 16.
Havet bygger fårgården och stenbrottet på expeditionens ö, och allt därifrån går över sjövägen, som bär 40 enheter per dygn.
Den förvalda importprioriteten är resursernas ordning, där sten kommer före ull (D-126), så ullen till tyg väntar bakom stenen.
Utan tyg får byborna inga kläder, blir inte bönder och når inte hantverkarna.

### 4.2 Svepet

Närområdet, Havet, Bygden och Naiv med normal egenvård på 260 kartor i fyra veckor:

| Ändring | Konstant | Bygden | Närområdet | Havet | Havet mot bästa, median | Lägsta D-34 |
|---|---|---|---|---|---|---|
| Som i dag | | 77 % | 20 % | 3 % | 74 % | 1,37 |
| **Havet, ull först** (spelarens importprioritet) | ingen; strategin `sea-wool` | 65 % | 13 % | **22 %** | 94 % | 1,67 |
| Sjöväg 50 enheter per dygn | `SeaRoute.UnitsPerGameDay` | 78 % | 18 % | 4 % | 84 % | 1,43 |
| Sjöväg 60 | `SeaRoute.UnitsPerGameDay` | 74 % | 13 % | 13 % | 90 % | 1,67 |
| Sjöväg 80 | `SeaRoute.UnitsPerGameDay` | 64 % | 10 % | 26 % | 94 % | 1,67 |
| Ull först och sjöväg 50 | | 57 % | 10 % | 33 % | 96 % | 1,67 |
| Ull först och sjöväg 60 | | 64 % | 11 % | 25 % | 94 % | 1,67 |
| Två eller tre flyttar per gryning | `Settlement.Migrate` | 77 % | 20 % | 3 % | 73 % | 1,36 |
| Nybyggarnas bostäder 8 i stället för 4 | `IslandSettlement.SettlerHousing` | 77 % | 20 % | 3 % | 75 % | 1,37 |
| Bryggan 30 Styrka | `Jetty` i `ContentCatalog` | 77 % | 20 % | 3 % | 74 % | 1,37 |
| Expeditionen 150 Utforskning eller 16 timmar | `PrototypeIsland` | 77–78 % | 20 % | 2–3 % | 74 % | 1,37 |
| Varvet 40 Styrka eller knarr 20 per dygn | `Shipyard` i `ContentCatalog` | 77 % | 20 % | 3 % | 74 % | 1,37 |

Sjövägens kapacitet ändras på två ställen: `SeaRoute.UnitsPerGameDay` och `Settlement.SeaRoutePerTick` (40 × 15 625 miljondelar per kvart).

- **Ull först gör mest, och utan regeländring.**
  Det är ett beslut som spelaren redan kan fatta på bryggans blad (D-126).
  Med hög egenvård vinner *Havet, ull först* på 24 % av kartorna, med enbart manuell inmatning på 17 % och med låg på 7 %.
- **Sjövägens kapacitet är den enda siffra som flyttar Havet.**
  Flytten, nybyggarnas bostäder, bryggans och expeditionens kostnader och varvet ändrar ingenting på fyra veckor.
  Kapaciteten hjälper också Närområdet och Bygden något, eftersom planeraren anlägger en bosättning på expeditionens ö i alla strategier.
- **Ull först och sjöväg 50 ger mest Havet, 33 %**, men Havet vinner då också Rik hemö (tabellen nedan).

### 4.3 Scenarierna med förslagen

| Scenario | Som i dag | Havet, ull först | Sjöväg 60 | Ull först och sjöväg 60 |
|---|---|---|---|---|
| Rik hemö | Närområdet, 27 % | Närområdet, 6 % | Närområdet, 8 % | **Havet**, 1 % |
| Rik grannö | Havet, 8 % | Havet, 8 % | Havet, 7 % | Havet, 7 % |
| Prototypens karta | Bygden, 4 % | Bygden, 4 % | Bygden, 5 % | Bygden, 5 % |
| Utan träning | Handkraft, 14 % | Handkraft, 14 % | Handkraft, 14 % | Handkraft, 14 % |

Procenten är vinnarens försprång före tvåan.
Med *Havet, ull först* i Havets ställe behåller alla fyra scenarierna sin vinnare, men Rik hemö vinns med 6 % i stället för 27 %.
Med båda ändringarna vinner Havet två scenarier, och M4 behöver då ett nytt scenario där Närområdet är bäst (AC-06).

### 4.4 Efter tolv veckor

Efter tolv veckor med normal egenvård ligger Havet, också med ull först, 19–28 % under Närområdet och Bygden på alla fyra scenariernas kartor.
Planeraren från vecka 2 bygger hantverkare och sjöfart bara på hemön, och steg 3 kräver 45 bybor i en och samma bosättning; Havets bybor är delade på två öar, och på Rik grannö når Havet aldrig sjöfarten.
Det är planerarens gräns mer än reglernas, och Havets mål i epok 1, sjövägarna och Vårdkasen, finns inte i måttet (avsnitt 5).

## 5. Epok 1:s tempo

Vårdkasen, Ytterön, dimman över innerhavet och forskningen finns ännu inte i simuleringen (M5).
Rapporten uppskattar därför när kasen kan tändas genom att lägga milstolpen ovanpå varje körning:

- Utforskning och Styrka som egenvården gav men boten inte använde går till milstolpen i dess ordning: Ytterön (400 Utforskning, tre dygn, en brygga för 60 Styrka eller sju dygn med handkraft), dimman över innerhavet (1 500 Utforskning) och de tre etapperna.
- Varje etapp betalas med 500 Styrka när det kommer före dess 21 dygn med handkraft (D-65).
- Grunden kräver sjöväg till Nyön, stommen grunden och Ytterön, och elden stommen, innerhavet och 50 bybor.
- Etappernas material och arbete och forskningen räknas inte; forskningen kostar ingen kraft (D-66).
  Boten använder också den Styrka som en spelare skulle spara till etapperna, så uppskattningen blir sen snarare än tidig där Styrkan avgör.

Vecka då Vårdkasen tänds, på de fyra scenariernas kartor:

| Profil | epoch-1.md | Allt sparas till kasen | Närområdet | Havet | Bygden |
|---|---|---|---|---|---|
| Hög | 6 | 5 | 7–9 | 7 | 6 |
| Normal | 7 | 6 | 7–10 | 8–9 | 8–9 |
| Enbart manuell | 8 | 7 | 8–11 | 9–10 | 10–11 |
| Låg | 11 | 10 | efter 12 | 11 | 12 eller senare |
| Utan träning | 11 | 11 | – | – | – |
| Ingen | – | – | – | – | – |

*Allt sparas till kasen* är strategin *Utan träning, handkraft*, som inte använder någon Styrka utöver turens, så att allt annat går till milstolpen.
Utan träning når bara den sjövägen, i vecka 2, och tänder kasen med handkraft i vecka 11 på alla fyra kartorna, som epoch-1.md anger (D-43, D-65); `HandBuildTests` håller det.

- **Den som sparar till kasen når den en vecka före epoch-1.md** med hög, normal, manuell och låg egenvård.
  Byarna når 50 bybor redan vecka 3–4, och ekonomi v2 ger mer att bygga med; ekonomi v2, avsnitt 10, förutsåg det.
- **Den som bygger vidare som planeraren når den 0–4 veckor senare,** eftersom Styrkan går till byggnader först.
  Det är spelarens val mellan att växa och att tända kasen, och det valet ska finnas (epoch-1.md, avsnitt 5).
- **Enbart manuell inmatning begränsas av Utforskningen,** 300 i veckan mot 1 900 som Ytterön och innerhavet kräver.
- **De andra stegen:** sjöväg vecka 1–4, bönder vecka 1–2, hantverkare vecka 2–5 och sjöfart vecka 3–5 med normal, hög och manuell egenvård, men i tre av tolv körningar ingen sjöfart på tolv veckor; med låg egenvård sjöväg vecka 2–4.

## 6. Ett år

**Utan spelare och utan egenvård** (8 760 timmar per karta på de fyra kartorna):

- Byborna växer från 12 till 16 och stannar vid startbyns bostäder, som ekonomi v2, avsnitt 11, anger.
- Timret växer till lagrets tak, 150, och plankor och sten ligger kvar på startens 20.
- Utvecklingsvärdet når 659, från 390–409, och står sedan still.
- Orken stannar på sparandets tak 600; Utforskning och Styrka ligger kvar på turens 100.
- Inget förfaller och inget lager eller saldo blir negativt någon timme (INV-V1, INV-R1, INV-F1, INV-V4).

**Bygden med normal egenvård** når 148–301 bybor och ett utvecklingsvärde på 9 190–11 111 efter ett år, utan att något förfaller.
Den använder 2 070–3 200 av omkring 14 100 Styrka och 430 av omkring 31 400 Utforskning; resten stannar vid sparandets tak.

## 7. Botarnas begränsningar

Det här är fynd om botarna, inte om reglerna, men de påverkar hur säkra siffrorna ovan är.

1. **Utan träning stannar på 40 bybor på fyra kartor av 260.**
   Planeraren bygger tre jaktstugor men inget garveri, eftersom dess regel väntar på mer vilt än stugorna ger, och byborna blir aldrig bönder.
   Bygden och *Utan träning, handkraft* växer på samma kartor, så D-43 håller; planeraren bör rättas i en egen uppgift.
2. **Havet är svagare än en skicklig spelare.**
   Den förvalda importprioriteten och en planerare som bara utvecklar hemön håller Havet tillbaka (avsnitt 4).
3. **Med låg egenvård är Bygden nästan alltid bäst**, på 98 % av kartorna och 92 % med *Havet, ull först*.
   Bygden använder minst Styrka, och med 100 Styrka i veckan bestämmer Styrkan.
   I scenarierna är Närområdet ändå bäst med låg egenvård på Rik hemö och Havet på seed 31 (ekonomi v2, avsnitt 15).
4. **Mer Styrka kan ändra planerarens val** så att världen blir mindre, till exempel Bygden med låg egenvård på prototypens karta efter tolv veckor; det utreds i TASK-097 (#230).

## 8. Kvar till M5

- **Krafternas slutliga värden (O-01)** beslutas när botarna spelar Vårdkasen, Ytterön, dimman och forskningen (TASK-085, TASK-086); då ersätts uppskattningen i avsnitt 5 av riktiga körningar.
- **Utforskningen behöver något att göra** efter vecka 2–3: fynden i dimman och expeditionerna (EXP-02, NAR-01).
- **Styrkan behöver något att göra efter epok 1:** efter Vårdkasen ligger den på taket 1 200 med normal och hög egenvård.
- **Havet över hela epoken:** Ytterön, sjövägarna till Vårdkasen och forskningen inom sjöfart bör ge Havet något att vinna på också efter fyra veckor.

## 9. Frågor till produktägaren

1. **Krafternas omvandling och tak:** behålls startvärdena till M5 (förslaget), eller vill du pröva något annat nu?
2. **Kraftkostnaderna:** behålls de (förslaget)?
3. **Havet:**
   - a) Botarnas Havet skeppar ull först, utan regeländring (förslaget): Havet bäst på 22 %, alla scenarier oförändrade, men Rik hemö vinns med 6 % i stället för 27 %.
   - b) Som a, och den förvalda importprioriteten sätter ull före sten, så att en ny spelare får det utan att ändra något: samma siffror för botarna; en regeländring i TASK-056.
   - c) Som a, och sjövägen bär 60 enheter per dygn: Havet bäst på 25 %, men Havet vinner också Rik hemö, och M4 behöver ett nytt scenario för Närområdet.
4. **Tempot:** godtas att Vårdkasen kan tändas en vecka tidigare än epoch-1.md anger, för den som sparar till den, tills etapperna finns i simuleringen i M5?
5. **Låg egenvård:** godtas att Bygden är bäst på nästan alla genererade kartor med låg egenvård, så länge scenarierna visar andra vinnare?
