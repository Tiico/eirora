# Balansrapport M4

> **Status:** [BESLUTAT] v1.2, produktägarens beslut i TASK-055 (#138) 2026-10-27 som D-133–D-137 (avsnitt 9); den förvalda importprioriteten ändrades med regelversion 16 i TASK-056 (#139), och körningarna med den står i avsnitt 10  
> **Ärende:** TASK-054 (#137), TASK-055 (#138), TASK-056 (#139) · **Milstolpe:** M4 · **PRD:** O-01, D-34, D-35, D-43, D-65, D-126, D-133–D-137, AC-02, AC-06  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Krafter:** [forces.md](./forces.md) · **Ekonomi:** [economy-v1.md](./economy-v1.md), [economy-v2.md](./economy-v2.md) · **Epok 1:** [epoch-1.md](./epoch-1.md)  
> **Verktyg:** balansbotarna i `tools/Eirora.Balance` ([development.md](../development.md#balansbotar))

Balansbotarna har spelat ekonomin i en hel epok och ett år, med alla sex profilerna, på scenariernas kartor och på 260 genererade kartor.
Rapporten föreslår krafternas värden och kraftkostnader för M4 och prövar siffror för Havet, som produktägaren bad om 2026-10-26.
Avsnitt 1–9 räknar med regelversion 15, som produktägaren beslutade på i TASK-055 (#138).
Regeländringen fördes in i TASK-056 (#139) som regelversion 16, och avsnitt 10 visar att körningarna med den ger det utfall som avsnitt 4 förutsåg.
Måtten är ekonomi v2:s (avsnitt 9): utvecklingsvärdet och värdet per använd kraft, som aldrig visas för spelaren.
Alla körningar är gjorda med planeraren efter TASK-097 (#230, PR #231).

## 1. Förslag i korthet

1. **Krafternas omvandling och tak behålls som i forces.md till M5.**
   D-34 håller efter fyra veckor på alla 260 kartor med normal egenvård, och ingen strategi dominerar i scenarierna.
   Botarna använder 3–12 % av sin Utforskning och omkring hälften av sin Styrka på tolv veckor, så andra kurvor skulle inte ändra världen förrän M5 ger krafterna mer att göra (avsnitt 3).
2. **Kraftkostnaderna behålls som i ekonomi v1 och v2.**
   Billigare *Arbeta extra* bryter D-34, och dyrare byggnader gör Bygden bäst på fyra av fem kartor (avsnitt 3.4).
3. **Havet vinner oftare med ull först i importprioriteten.**
   Den förvalda ordningen skeppar öns sten före ullen, och Havet väntar då på kläder.
   Balansboten *Havet, ull först* är bäst på 28 % av kartorna i stället för 7 %, och de fyra scenarierna behåller sina vinnare (avsnitt 4).
   En sjöväg med 60 i stället för 40 enheter per dygn ger Havet 30 % utan ull först och 36 % med.
4. **Epok 1:s tempo håller ungefär.**
   Den som sparar till Vårdkasen tänder den en vecka tidigare än epoch-1.md anger med hög, normal, manuell och låg egenvård, och samma vecka utan träning (avsnitt 5).
5. **Ett år utan spelare och egenvård förfaller ingenting**, och världen når det som grundtakten ger (avsnitt 6).

## 2. Körningarna

| Svit | Kommando | Körningar | Innehåll |
|---|---|---|---|
| Epok | `--suite epoch` | 144 | Varje jämförd strategi med varje profil på de fyra scenariernas kartor i tolv veckor |
| År | `--suite year` | 8 | Ingen spelare utan egenvård och Bygden med normal egenvård på scenariernas kartor i 365 dygn |
| Scenarier | `--suite scenarios` | 24 | Ekonomi v2, avsnitt 15, fyra veckor |
| 260 kartor | `--maps generated --from 0 --seeds 260 --strategies near,sea,sea-wool,village,naive --profiles high,normal,manual,low --days 28`; från TASK-056 är `sea` Havet med ull först, och `sea-wool` finns inte längre | 5 200 | Vilken strategi som är bäst, och D-34, efter fyra veckor |
| Alla profiler | `--maps generated --from 0 --seeds 260 --strategies near,sea,village,no-training,no-training-by-hand,naive --profiles high,normal,manual,low,no-training,none --days 28` | 9 360 | D-34 med varje profil och strategi |

Alla kommandon körs som `dotnet run -c Release --project tools/Eirora.Balance -- …`.
Botarna spelar den riktiga simuleringen med regelversion 15 och kontrollerar invarianterna varje timme: inget förfaller (INV-V1), inga lager eller saldon blir negativa eller går över sitt tak (INV-R1, INV-F1) och krafterna minskar aldrig utan ett kommando (INV-F6).
Ingen av körningarna bröt mot någon invariant.
Samma körningar ger samma rapport byte för byte; epoksviten kördes två gånger och gav identiska `balance.tsv` och `balance.md`, också när körningarna spelas parallellt.

Rapporten har fått tempokolumner: dygnet då världen fick sjöväg, bönder, hantverkare, sjöfart och 50 bybor, och en uppskattning av när Vårdkasen kan tändas (avsnitt 5).
Svepen i avsnitt 3.4 och 4 som ändrar en regel kördes med en lokal byggnad där bara den konstanten var ändrad, eftersom uppgiften inte ändrar reglerna; varje rad anger vilken konstant.
Planeraren väljer olika när en siffra ändras, så andelarna rör sig med några procentenheter också för ändringar som inte gör något; skillnader under 3–4 procentenheter är brus.

## 3. Krafterna

### 3.1 Strategin hinner ikapp (D-34)

Produktägaren beslutade 2026-10-26 att D-34 räcker över fyra veckor, eftersom första veckan är guidad och avsiktligt lätt (H4).
Måttet är den genomtänkta strategi som ger minst värde per använd kraft, delat med den naiva strategins med samma profil och karta; forces.md kräver minst 1,3.

| Profil | Lägsta kvot, Närområdet, Havet och Bygden | 5:e percentilen | Median | Kartor under 1,3 | Lägsta med *Havet, ull först* |
|---|---|---|---|---|---|
| Hög | 1,26 | 1,69 | 2,00 | 1 | 1,36 |
| Normal | 1,32 | 1,75 | 2,11 | 0 | 1,42 |
| Enbart manuell | 1,31 | 1,72 | 2,09 | 0 | 1,44 |
| Låg | 1,44 | 1,58 | 2,04 | 0 | 1,44 |

- Kartan under 1,3 är seed 247 med hög egenvård, där Havet stannar på 62 bybor efter fyra veckor.
  Med ull först håller D-34 på alla 260 kartor med alla fyra profilerna.
- Efter tolv veckor ger varje genomtänkt strategi minst 2,4 gånger den naivas värde per använd kraft, eftersom den naiva stannar på 32 bybor.
- Strategin *Utan träning* stannar på 40 bybor på fyra kartor (seed 126, 170, 229 och 259) med alla profiler och ligger då under den naiva.
  Det är botens plan, inte reglerna: på samma kartor växer Bygden och *Utan träning, handkraft* med profilen utan träning och når hantverkare (avsnitt 7).

### 3.2 Var krafterna tar vägen

Krafterna som botarna använder på tolv veckor, över scenariernas fyra kartor och Närområdet, Havet och Bygden, mot det som egenvården och turen gav:

| Profil | Utforskning använd av intjänad | Styrka använd av intjänad | Sparad Styrka vid slutet |
|---|---|---|---|
| Hög | 250–430 av 8 752 (3–5 %) | 1 610–2 490 av 4 240 (38–59 %) | 767–1 200 |
| Normal | 250–430 av 7 324 (3–6 %) | 1 500–2 500 av 3 340 (45–75 %) | 691–1 200 |
| Enbart manuell | 250–430 av 3 700 (7–12 %) | 1 550–2 100 av 3 340 (46–63 %) | 689–1 105 |
| Låg | 250–430 av 3 880 (6–11 %) | 1 260–1 300 av 1 300 (97–100 %) | 0–40 |

- **Utforskningen har inget att göra efter kartläggningarna och expeditionen.**
  Den ligger på sparandets tak 800 redan efter fyra veckor i alla profiler med steg, som i ekonomi v1 (lärdom 1).
  Vårdkasen behöver 1 900 till: Ytterön för 400 och 150 dimrutor för 1 500 (epoch-1.md, avsnitt 4).
  Fynden i M5 är svaret; en lägre omvandling skulle bara göra profilerna mer lika, utan att ändra världen.
- **Styrkan räcker till mer än byggnaderna med normal och hög egenvård.**
  Hög, normal och enbart manuell använder ungefär lika mycket Styrka, och deras världar är nästan lika stora efter tolv veckor; Bygden på Rik hemö får 9 082 med både hög och normal egenvård.
  Mer egenvård syns alltså i tempot, inte i världens storlek, tills Vårdkasens etapper (3 × 500) tar Styrkan som blir över.
  Med normal egenvård blir 840–1 840 Styrka över på tolv veckor, ungefär etapperna.
- **Med låg egenvård bestämmer Styrkan.**
  Den används nästan till sista enheten, och Bygden, som använder minst Styrka, är bäst på 81 % av kartorna (avsnitt 7).

### 3.3 Sparandets mjuka tak (D-35)

I alla körningar ligger sparad Utforskning på högst 800, Styrka på högst 1 200 och Ork på högst 600, som forces.md anger.
Under ett år ligger Bygden med normal egenvård på 800 Utforskning och 1 200 Styrka vid slutet; resten av det som egenvården ger går förlorat.
Det är avsiktligt (D-35) men gör att krafterna efter epok 1 behöver något att användas till, se avsnitt 8.

### 3.4 Svep över kraftkostnaderna

Närområdet, Havet, Bygden och Naiv med normal egenvård på 260 kartor i fyra veckor, med en kostnad ändrad åt gången:

| Ändring | Konstant | Närområdet | Bygden | Havet | Lägsta D-34 | Kartor under 1,3 |
|---|---|---|---|---|---|---|
| Som i dag | | 49 % | 44 % | 7 % | 1,32 | 0 |
| All Styrka för byggen och uppgraderingar × 0,5 | `buildPower` i `ContentCatalog` | 50 % | 43 % | 7 % | 1,30 | 0 |
| All Styrka för byggen och uppgraderingar × 1,5 | `buildPower` i `ContentCatalog` | 7 % | 80 % | 13 % | 1,32 | 0 |
| Arbeta extra 20 Ork i stället för 30 | `Workday.Cost` | 55 % | 38 % | 6 % | 1,04 | 16 |
| Arbeta extra 45 Ork | `Workday.Cost` | 45 % | 48 % | 8 % | 1,31 | 0 |
| Skynda på 10 Ork i stället för 20 | `Rush.Cost` | 48 % | 45 % | 7 % | 1,34 | 0 |
| Bryggan 30 Styrka i stället för 60 | `Jetty` i `ContentCatalog` | 48 % | 45 % | 7 % | 1,34 | 0 |
| Varvet 40 Styrka i stället för 80 | `Shipyard` i `ContentCatalog` | 47 % | 46 % | 7 % | 1,32 | 0 |
| Expeditionen 150 Utforskning i stället för 250 | `PrototypeIsland.ExpeditionCost` | 49 % | 44 % | 7 % | 1,36 | 0 |

- **Billigare Arbeta extra bryter D-34**, eftersom den naiva strategin lägger all sin Ork på extraarbete och då får nästan lika mycket ut av den som den genomtänkta.
- **Dyrare byggnader gör Bygden bäst på fyra av fem kartor**, eftersom den använder minst Styrka; billigare ändrar ingenting med normal egenvård, där Styrkan inte bestämmer.
- **Utforskningens och bryggans kostnader bestämmer ingenting,** eftersom Utforskningen ligger på taket och Styrkan räcker.

Förslaget är därför att behålla kostnaderna.

## 4. Havet

Produktägaren: Havet vinner bara på 3 % av de genererade kartorna, och siffror för Havet ska prövas så att Havet vinner oftare.
Med planeraren efter TASK-097 är Havet bäst på 7 %.

### 4.1 Varför Havet förlorar

Havet bygger fårgården och stenbrottet på expeditionens ö, och allt därifrån går över sjövägen, som bär 40 enheter per dygn.
Med regelversion 15 var den förvalda importprioriteten resursernas ordning, där sten kommer före ull (D-126), så ullen till tyg väntade bakom stenen.
Utan tyg får byborna inga kläder, blir inte bönder och når hantverkarna senare.

### 4.2 Svepet

Närområdet, Havet, Bygden och Naiv med normal egenvård på 260 kartor i fyra veckor, med Havet som i dag och med *Havet, ull först*:

| Ändring | Konstant | Havet bäst | Havet, ull först, bäst | Havet mot bästa, median, med ull först | Lägsta D-34 med ull först |
|---|---|---|---|---|---|
| Som i dag | | 7 % | **28 %** | 96 % | 1,42 |
| Sjöväg 50 enheter per dygn | `SeaRoute.UnitsPerGameDay` | 18 % | 38 % | 98 % | 1,42 |
| Sjöväg 60 | `SeaRoute.UnitsPerGameDay` | 30 % | 36 % | 97 % | 1,42 |
| Sjöväg 80 | `SeaRoute.UnitsPerGameDay` | 38 % | 38 % | 98 % | 1,42 |
| Två flyttar per gryning | `Settlement.Migrate` | 6 % | 33 % | 97 % | 1,42 |
| Nybyggarnas bostäder 8 i stället för 4 | `IslandSettlement.SettlerHousing` | 10 % | 28 % | 96 % | 1,42 |
| Bryggan 30 Styrka | `Jetty` i `ContentCatalog` | 7 % | 26 % | 95 % | 1,42 |
| Expeditionen 150 Utforskning | `PrototypeIsland.ExpeditionCost` | 7 % | 28 % | 96 % | 1,42 |
| Expeditionen 16 timmar | `PrototypeIsland.ExpeditionHours` | 8 % | 25 % | 96 % | 1,42 |
| Varvet 40 Styrka eller knarr 20 per dygn | `Shipyard` i `ContentCatalog` | 7 % | 28 % | 96 % | 1,42 |

Sjövägens kapacitet ändras på två ställen: `SeaRoute.UnitsPerGameDay` och `Settlement.SeaRoutePerTick` (40 × 15 625 miljondelar per kvart).

- **Ull först gör mest, och utan att ändra en siffra.**
  Det är ett beslut som spelaren redan kan fatta på bryggans blad (D-126).
  Med hög egenvård vinner *Havet, ull först* på 27 % av kartorna, med enbart manuell inmatning på 25 % och med låg på 28 %.
- **Sjövägens kapacitet är den enda siffra som flyttar Havet mycket.**
  Med ull först lägger 50–80 enheter per dygn till omkring tio procentenheter, och utan ull först gör kapaciteten nästan samma sak som ull först.
  Flytten, nybyggarnas bostäder, bryggans och expeditionens kostnader och varvet ändrar inget utöver bruset på fyra veckor.

### 4.3 Scenarierna med förslagen

| Scenario | Som i dag | Havet, ull först | Ull först och sjöväg 60 |
|---|---|---|---|
| Rik hemö | Närområdet, 9 % | Närområdet, 3 % | Närområdet, 3 % |
| Rik grannö | Havet, 4 % | Havet, 4 % | Havet, 1 % |
| Prototypens karta | Bygden, 19 % | Bygden, 12 % | Bygden, 20 % |
| Utan träning | Handkraft, 12 % | Handkraft, 12 % | Handkraft, 12 % |

Procenten är vinnarens försprång före tvåan, med *Havet, ull först* i Havets ställe.
Alla fyra scenarierna behåller sin vinnare, men på Rik hemö är Havet nu bara 3 % efter Närområdet, så det scenariot blir det känsligaste.
Utan träning bygger Havet ingen sjöväg, så ull först ändrar ingenting där.

### 4.4 Efter tolv veckor

Efter tolv veckor med normal egenvård ligger *Havet, ull först* 9–31 % under det bästa på scenariernas kartor.
Planeraren från vecka 2 bygger hantverkare bara på hemön, och Havets bybor är delade på två öar.
Havet når ändå sjöfarten i vecka 3, bland de första, och tänder Vårdkasen tidigast av de strategier som bygger vidare (avsnitt 5), så Havets mål i epok 1, sjövägarna och Vårdkasen, syns i tempot men inte i utvecklingsvärdet.

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
| Hög | 6 | 5 | 8–10 | 7–8 | 8–9 |
| Normal | 7 | 6 | 10–11 | 7–8 | 9–11 |
| Enbart manuell | 8 | 7 | 9–11 | 7–10 | 9–11 |
| Låg | 11 | 10 | efter 12 | 11 | 12 eller senare |
| Utan träning | 11 | 11 | – | – | – |
| Ingen | – | – | – | – | – |

*Allt sparas till kasen* är strategin *Utan träning, handkraft*, som inte använder någon Styrka utöver turens och bryggornas, så att allt annat går till milstolpen.
Utan träning når bara den sjövägen, i vecka 2, och tänder kasen med handkraft i vecka 11 på alla fyra kartorna, som epoch-1.md anger (D-43, D-65); `HandBuildTests` håller det.

- **Den som sparar till kasen når den en vecka före epoch-1.md** med hög, normal, manuell och låg egenvård.
  Byarna når 50 bybor redan vecka 3–4, och ekonomi v2 ger mer att bygga med; ekonomi v2, avsnitt 10, förutsåg det.
- **Den som bygger vidare som planeraren når den 0–5 veckor senare,** eftersom Styrkan går till byggnader och uppgraderingar först.
  Det är spelarens val mellan att växa och att tända kasen, och det valet ska finnas (epoch-1.md, avsnitt 5).
- **Enbart manuell inmatning begränsas av Utforskningen,** 300 i veckan mot 1 900 som Ytterön och innerhavet kräver.
- **De andra stegen:** sjöväg vecka 1–4, bönder vecka 1–2, hantverkare vecka 2–3 och sjöfart vecka 3–4 med normal och hög egenvård; med låg egenvård sjöväg vecka 2–6 och sjöfart vecka 4–6.

## 6. Ett år

**Utan spelare och utan egenvård** (8 760 timmar per karta på de fyra kartorna):

- Byborna växer från 12 till 16 och stannar vid startbyns bostäder, som ekonomi v2, avsnitt 11, anger.
- Timret växer till lagrets tak, 150, och plankor och sten ligger kvar på startens 20.
- Utvecklingsvärdet når 659, från 390–409, och står sedan still.
- Orken stannar på sparandets tak 600; Utforskning och Styrka ligger kvar på turens 100.
- Inget förfaller och inget lager eller saldo blir negativt någon timme (INV-V1, INV-R1, INV-F1, INV-V4).

**Bygden med normal egenvård** når 121–183 bybor och ett utvecklingsvärde på 8 188–10 060 efter ett år, utan att något förfaller.
Den använder 1 680–2 350 av omkring 14 100 Styrka och 430 av omkring 31 400 Utforskning; resten stannar vid sparandets tak.

## 7. Botarnas begränsningar

Det här är fynd om botarna, inte om reglerna, men de påverkar hur säkra siffrorna ovan är.

1. **Utan träning stannar på 40 bybor på fyra kartor av 260.**
   Planeraren bygger tre jaktstugor men inget garveri, eftersom dess regel väntar på mer vilt än stugorna ger, och byborna blir aldrig bönder.
   Bygden och *Utan träning, handkraft* växer på samma kartor, så D-43 håller; planeraren rättas i en egen uppgift.
2. **Havet är svagare än en skicklig spelare.**
   Den förvalda importprioriteten med regelversion 15 och en planerare som bara bygger hantverkare på hemön håller Havet tillbaka (avsnitt 4); importprioriteten är ändrad med regelversion 16 (avsnitt 10).
3. **Med låg egenvård är Bygden oftast bäst**, på 81 % av kartorna och 71 % med *Havet, ull först*.
   Bygden använder minst Styrka, och med 100 Styrka i veckan bestämmer Styrkan.
   I scenarierna är Havet ändå bäst med låg egenvård på Rik hemö och handkraften på seed 31 (ekonomi v2, avsnitt 15).
4. **Planerarens rättelser i TASK-097 ändrade siffrorna mycket:** Havet gick från 3 % till 7 % och Närområdet från 20 % till 49 %, utan någon regeländring.
   Andelarna säger alltså lika mycket om botarnas skicklighet som om reglerna.

## 8. Kvar till M5

- **Krafternas slutliga värden (O-01)** beslutas när botarna spelar Vårdkasen, Ytterön, dimman och forskningen (TASK-085, TASK-086); då ersätts uppskattningen i avsnitt 5 av riktiga körningar.
- **Utforskningen behöver något att göra** efter de första veckorna: fynden i dimman och expeditionerna (EXP-02, NAR-01).
- **Styrkan behöver något att göra efter epok 1:** efter Vårdkasen ligger den på taket 1 200 med normal och hög egenvård.
- **Havet över hela epoken:** Ytterön, sjövägarna till Vårdkasen och forskningen inom sjöfart bör ge Havet något att vinna på också efter fyra veckor.

## 9. Beslut

Produktägarens beslut i TASK-055 (#138), 2026-10-27, loggade i PRD:n:

- **D-133:** krafternas omvandling och tak behålls som i forces.md till M5.
- **D-134:** kraftkostnaderna behålls som i ekonomi v1 och v2.
- **D-135:** Havet får ull först.
  Balansbotarnas Havet skeppar ull först, och den förvalda importprioriteten sätter ull före sten, vilket ändrar standarden i D-126.
  Regeländringen görs i TASK-056 (#139), som också byter Havet mot *Havet, ull först* i scenarierna och kör om dem; Rik hemö vinns då med 3 % i stället för 9 % (avsnitt 4.3).
- **D-136:** att Vårdkasen kan tändas en vecka tidigare än epoch-1.md anger, för den som sparar till den, godtas tills etapperna finns i simuleringen i M5.
- **D-137:** att Bygden är bäst på de flesta genererade kartor med låg egenvård godtas, så länge scenarierna visar andra vinnare.
  Produktägaren godtog 98 % från v1.0 av rapporten; med planeraren efter TASK-097 är det 81 %.

Alternativen bakom D-135: ull först bara för botarna, utan regeländring; ull först och en sjöväg med 60 enheter per dygn, som ger Havet 36 % och behåller scenariernas vinnare med 1–3 % på Rik hemö och Rik grannö.

## 10. Regelversion 16

TASK-056 (#139) förde in D-133–D-135 i reglerna.
Krafternas omvandling, tak och kostnader är oförändrade, så den enda regeländringen är importprioriteten.

- **Den förvalda importprioriteten** är varornas fasta ordning med ull direkt före sten: fisk, säd, timmer, plankor, ull, sten, tyg och sedan de upplåsta stegens varor (`StockRules.DefaultImportOrder`).
- **Migrationen** från regelversion 15 behåller saldon, lager, byggnader och bybor.
  En ö utan spelarens egen ordning får den nya ordningen från migrationen, och en ordning som spelaren själv har satt behålls.
  Journaler från regelversion 15 spelas inte upp, eftersom en värld med sjöväg skulle ta hem varorna i en annan ordning.
- **Balansbotarnas Havet** sätter öns ull först, som *Havet, ull först* gjorde i avsnitt 4; den strategin är borttagen.

Alla körningar i avsnitt 2 är gjorda om med regelversion 16, och ingen bröt mot någon invariant.

### 10.1 Havet vinner oftare

Närområdet, Havet, Bygden och Naiv på 260 kartor i fyra veckor:

| Profil | Havet, regelversion 15 | Havet med bara den nya ordningen | Havet med ull först | Närområdet | Bygden | Lägsta D-34 | Kartor under 1,3 |
|---|---|---|---|---|---|---|---|
| Hög | 8 % | 16 % | **27 %** | 37 % | 37 % | 1,36 | 0 |
| Normal | 7 % | 18 % | **27 %** | 34 % | 38 % | 1,42 | 0 |
| Enbart manuell | 10 % | 16 % | **24 %** | 36 % | 40 % | 1,44 | 0 |
| Låg | 18 % | 24 % | **28 %** | 1 % | 71 % | 1,44 | 0 |

Närområdet och Bygden avser körningarna med ull först.

- **Havet vinner nästan fyra gånger så ofta** med normal egenvård, 27 % mot 7 %, som avsnitt 4.2 förutsåg (28 %).
  Ingen strategi är bäst på mer än 40 % av kartorna med hög, normal eller manuell egenvård.
- **Den nya ordningen ensam gör ungefär hälften.**
  Med ullen direkt före stenen men efter fisk, säd, timmer och plankor är Havet bäst på 18 %; resten kommer av att boten sätter ullen först.
- **D-34 håller på alla kartor med alla fyra profilerna**, också seed 247 med hög egenvård, som låg på 1,26 med regelversion 15.

### 10.2 Scenarierna

| Scenario | Bäst, före tvåan, regelversion 15 | Regelversion 16 |
|---|---|---|
| Rik hemö | Närområdet, 9 % | Närområdet, 3 % |
| Rik grannö | Havet, 4 % | Havet, 4 % |
| Prototypens karta | Bygden, 19 % | Bygden, 12 % |
| Utan träning | Handkraft, 12 % | Handkraft, 12 % |

Alla fyra scenarierna behåller sin vinnare, med de försprång som avsnitt 4.3 förutsåg.
På Rik hemö är Havet nu 3 % efter Närområdet, och på prototypens karta är Havet tvåa efter Bygden.
Med varje profil på varje scenarios karta är vinnarna desamma som i [ekonomi v2, avsnitt 15](./economy-v2.md#alla-profiler), och den lägsta kvoten mot den naiva strategin är 1,61–1,68.

### 10.3 Epoken och året

- **Efter tolv veckor** med normal egenvård ligger Havet 9–31 % under det bästa på scenariernas kartor, som *Havet, ull först* i avsnitt 4.4.
- **Tempot:** Havet når hantverkarna vecka 2–3 och sjöfarten vecka 3 med normal och hög egenvård, och tänder Vårdkasen vecka 7–9; de andra strategierna och *Allt sparas till kasen* är oförändrade (avsnitt 5).
- **Krafterna** används som i avsnitt 3.2: 3–12 % av Utforskningen och omkring hälften av Styrkan.
- **Ett år:** utan spelare är allt som i avsnitt 6, och Bygden når 121–183 bybor och 8 188–10 060 i utvecklingsvärde; bara Rik hemö ändras, från 9 791 till 9 840.
- **Kalkylen** i ekonomi v2 skeppar i samma ordning: Havet får 6 282 vecka 6 i stället för 5 942 och når hantverkarna vecka 2 i stället för 4 ([ekonomi v2, avsnitt 10](./economy-v2.md#10-sex-veckor)).
  Botarnas Havet på prototypens karta får 6 846 efter sex veckor i stället för 5 821.

### 10.4 Kvar

- Strategin *Utan träning* stannar fortfarande på 40 bybor på seed 126, 170, 229 och 259 (avsnitt 7).
- Med profilen utan träning stannar också Havet på 40 bybor på seed 247, med 1,15 gånger den naiva strategins värde per använd kraft; det var likadant med regelversion 15, eftersom bryggan på expeditionens ö väntar på Styrka som profilen inte ger, och Havet då aldrig skeppar något.
