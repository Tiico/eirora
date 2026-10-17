# Ekonomi v1

> **Status:** [FÖRSLAG] v0.1, väntar på granskning av produktägaren  
> **Ärende:** TASK-004 · **Milstolpe:** M0 · **PRD:** CIV-01–09, LOG-01–04, LOG-06, D-12, D-17, D-34, D-39, D-43–D-45, O-02  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Krafter:** [forces.md](./forces.md) · **Invarianter:** [invariants.md](./invariants.md)  
> **Kalkyl:** [models/economy_v1.py](./models/economy_v1.py) (`python -I economy_v1.py`)

Den första ekonomin för prototypen: 12 byggnader, 7 resurser, recept, logistik och vad krafterna kostar.
Siffrorna är räknade timme för timme över sju spelardygn i kalkylen ovan, inte uppskattade.
Kalkylen är ett räkneark i kodform; spelets simulering skrivs i C# i M1.
Innehållsomfånget (O-02, CIV-09) prövas i prototypen.

## 1. Grundidé

- **Grundtakten kräver inga krafter.**
  Mat, ved och sten kan alltid produceras utan egenvård.
  Mat har två oberoende källor (INV-R4), och ved och sten finns på flera platser.
  Utan egenvård står världen still men förfaller aldrig (D-17).
- **Krafterna tar de stora stegen.**
  Byggkraft för förädling, hamnar, långhus och uppgraderingar; Utforskning för att hitta nytt; Gryningskraft för att lösa dagens flaskhals.
- **Byborna är arbetskraften.**
  Varje byggnad behöver bemanning, och lediga bybor bygger.
  Den knappa resursen är ofta folk, inte material.
- **Platsen avgör värdet.**
  Avstånd till förråd, rika och fattiga platser och sjövägens kapacitet gör samma byggnad olika mycket värd.

## 2. Resurser

| Resurs | Roll | Källor |
|---|---|---|
| Fisk | Basbehov (mat) | Fiskebod |
| Säd | Basbehov (mat) | Åker (långsam process) |
| Timmer | Byggmaterial, råvara | Huggarkoja; skogen växer tillbaka (långsam process) |
| Plank | Byggmaterial | Sågverk |
| Sten | Byggmaterial | Stenbrott |
| Ull | Råvara | Fårgård (långsam process) |
| Vadmal | Komfort, ger snabbare tillväxt | Väveri |

Mat har två oberoende källor, så ingen enskild flaskhals kan svälta byborna (PRD 6.2).

## 3. Byggnader

| Byggnad | Kostnad | Arbete | Byggkraft | Bemanning | Effekt | Plats |
|---|---|---|---|---|---|---|
| Långhus | 10 timmer, 15 plank, 10 sten | 40 h | 60 | 0 | +8 bostadsplatser | Land |
| Stuga | 15 timmer, 5 sten | 32 h | 0 | 0 | +4 bostadsplatser | Land |
| Förråd | 10 timmer, 5 sten | 16 h | 0 | 1 | Lager 150 per resurs; nav för transport | Land |
| Fiskebod | 8 timmer | 12 h | 0 | 2 | 1,25 fisk/h | Fiskevatten |
| Åker | 4 timmer | 8 h | 0 | 2 | 0,5 säd/h dygnet runt; skördas aktiva timmar | Odlingsmark |
| Huggarkoja | 6 timmer | 8 h | 0 | 2 | 1 timmer/h vid minst två skogsbrickor, annars 0,5 | Skog |
| Fårgård | 10 timmer, 5 plank | 12 h | 0 | 1 | 0,4 ull/h dygnet runt; klipps aktiva timmar | Bete |
| Stenbrott | 10 timmer, 4 plank | 16 h | 0 | 2 | 0,75 sten/h | Berg |
| Sågverk | 15 timmer, 5 sten | 24 h | 30 | 2 | 2 timmer → 1 plank per timme | Land |
| Väveri | 10 plank, 5 sten | 24 h | 60 | 2 | 2 ull → 1 vadmal per två timmar | Land |
| Brygga | 15 timmer, 10 plank | 24 h | 60 | 1 | Sjöväg, start för expeditioner, räknas som förråd för avstånd | Hamnläge |
| Utkiksvarde | 10 sten | 12 h | 40 | 0 | Kartläggning och expeditioner kostar 25 % mindre Utforskning | Land |
| *Uppgradering till nivå 2* | 10 plank, 5 sten | 16 h | 50 | | +50 % produktion | |
| *Väg* | 2 sten per bricka | 4 h per bricka | 0 | | Höjer avståndsklassen ett steg | |

Rika platser ger mer: en stenåder dubblar stenbrottet, rikt fiskevatten ger 1,5 gånger och betet på Nyön dubblar fårgården.
En skogsbricka rymmer 40 timmer och växer tillbaka med 0,25 timmer i timmen.

## 4. Bybor, basbehov och vila

- Varje bybo äter 1 mat per spelardygn vid gryningen, fisk först och sedan säd.
- Mätta bybor ger **+1 bybo** per spelardygn om det finns bostadsplats.
  Bostad kommer från långhus, som kostar Byggkraft, eller stugor, som bara kostar material och mer arbete (D-43).
  Tillväxten blockeras alltså aldrig av att spelaren valt bort träning (HLT-01), men träning ger snabbare bostäder.
  Om det också finns 2 vadmal förbrukas de som komfort och ger **+1 bybo till**.
- Brist på mat eller vadmal stoppar bara tillväxten; ingen bybo, byggnad eller nivå går förlorad (D-39, INV-V3).
- Lediga bybor bygger med en arbetstimme per bybo och aktiv timme.
- Under viloperioden (8 h i kalkylen) sover byborna: ingen produktion, inget bygge, ingen transport (INV-T4).
  Åkern, fårgården och skogen fortsätter att växa och skördas när byborna vaknar (INV-T5).
- Start: 12 bybor, bostad för 16, ett förråd, en fiskebod, en åker och en huggarkoja vid en fattig skog.

## 5. Logistik

| Avstånd till närmaste förråd eller brygga | 0–1 bricka | 2–3 brickor | 4 eller fler |
|---|---|---|---|
| Andel av produktionen som når fram | 100 % | 75 % | 50 % |

- En väg höjer avståndsklassen ett steg.
- Ett förråd rymmer 150 av varje resurs.
  Fullt lager stoppar produktionen; inget går förlorat.
- Förädling lämnar ett **minimilager** på 20 av sin råvara (LOG-06, P0 enligt D-44), annars äter sågverket upp all ved som behövs till byggen.
- Sjövägen mellan Hemön och Nyön för 40 enheter per spelardygn, delat mellan alla varor.

### Logistiska avvägningar

1. **Rikt och långt bort, eller fattigt och nära.**
   Rikt fiskevatten tre brickor bort ger 1,5 × 75 % = 1,13 gånger en vanlig fiskebod intill förrådet.
   Den rika skogen tre brickor bort ger 75 % av 1 timmer/h; den fattiga skogen intill ger 0,5 timmer/h men tar slut snabbare.
2. **Ett andra förråd eller vägar.**
   Ett förråd i ett produktionskluster lyfter alla byggnader runt omkring på en gång men kostar en bybo i bemanning.
   En väg kostar ingen bemanning men gäller bara en byggnad och kostar 2 sten per bricka.
3. **Nyöns rikedom eller Hemöns närhet.**
   Nyöns stenåder och bete är dubbelt så rika, men allt måste över sjövägen, som bara rymmer 40 enheter per dygn.
4. **Förädling eller byggmaterial.**
   Varje plank kostar två timmer som annars kunde ha blivit ett bygge; minimilagret avgör balansen.

## 6. Vad krafterna kostar

| Kraft | Handling | Kostnad | Effekt |
|---|---|---|---|
| Utforskning | Kartlägg närområdet | 60 | Visar stenådern och urskogen på Hemön |
| Utforskning | Kartlägg kusten | 120 | Visar rikt fiskevatten och mer odlingsmark |
| Utforskning | Expedition | 250 | Hittar Nyön efter 32 aktiva timmar; kräver brygga |
| Byggkraft | Bygga eller uppgradera | Se tabellen i avsnitt 3 | |
| Gryningskraft | Dagsverke | 30 | En byggnad producerar 50 % mer under spelardygnet |
| Gryningskraft | Rusta | 20 | +8 arbetstimmar på det första bygget i kön |

Planering, bemanning, minimilager och prioriteringar kostar aldrig kraft (D-23).
Startkrafter från den guidade turen: 100 Utforskning, 100 Byggkraft, 40 Gryningskraft (D-45).

### Beslut om var krafterna ska riktas

1. **Byggkraft: sågverk, brygga eller långhus först?**
   Sågverket ger plank till allt annat, bryggan öppnar havet och långhuset ger fler bybor.
   Med måttlig egenvård räcker Byggkraften till alla tre första veckan, men plank och lediga bybor gör att de inte kan byggas samtidigt.
   Ordningen avgör vad som börjar ge avkastning först.
2. **Utforskning: närområdet eller en ny ö?**
   Två kartläggningar (180) ger rika platser nära förrådet direkt.
   En expedition (250) ger en ny ö med dubbelt så rika platser, men först efter några dygn och bakom en sjöväg med begränsad kapacitet.
3. **Gryningskraft: vilken flaskhals i dag?**
   Dagsverke på stenbrottet, sågverket eller fårgården, eller rusta ett långhus, beroende på vad som håller tillbaka världen just nu.
4. **Spara eller spendera.**
   Byggkraft kan sparas ihop till ett långhus eller en brygga (D-35), men byborna står still under tiden.

## 7. Sju dygn med två strategier

Båda körningarna har samma måttliga egenvård från [forces.md](./forces.md): 7 500 steg och 6,5 h sömn varje dygn, tre pass i veckan.

- **Närområdet:** kartlägger Hemön två gånger, bygger huggarkoja i urskogen, stenbrott på stenådern, ett andra förråd i klustret, sågverk, långhus, fiskebod vid det rika vattnet och en till åker.
  Gryningskraften går till dagsverken i stenbrottet och sågverket.
- **Havet:** bygger brygga, huggarkoja i den rika skogen och sågverk, skickar en expedition, bygger brygga, fårgård och stenbrott på Nyön, väveri och långhus.
  Gryningskraften går till sågverket och fårgården.

Som jämförelse körs en **naiv** strategi med **hög** egenvård (15 000 steg, 8 h sömn, sex pass): den bygger det första som går där det går, uppgraderar det den redan har och läser inte kartan.

| Körning | Egenvård | Bybor | Byggnader | Öar | Sten i lager | Dagsproduktion | Utvecklingsvärde | Använt U/B/G | Sparat U/B/G | Värde per använd kraft |
|---|---|---|---|---|---|---|---|---|---|---|
| Närområdet | måttlig | 19 | 12 | 1 | 208 | 182 | 1 188 | 180/260/410 | 461/110/233 | 0,94 |
| Havet | måttlig | 19 | 12 | 2 | 15 | 234 | 1 104 | 250/270/280 | 426/100/298 | 0,90 |
| Naiv | hög | 20 | 9 | 1 | 39 | 162 | 1 014 | 180/150/630 | 520/295/96 | 0,65 |
| Utan träning | utan träning | 19 | 11 | 1 | 150 | 122 | 1 050 | 180/0/460 | 461/100/224 | 1,04 |
| Ingen spelare | ingen | 16 | 4 | 1 | 20 | 40 | 532 | 0/0/0 | 100/100/310 | – |
| Ingen spelare | måttlig | 16 | 4 | 1 | 20 | 40 | 532 | 0/0/0 | 551/370/492 | – |

*Dagsproduktion* är viktad produktionskapacitet per spelardygn vid veckans slut.
*Utvecklingsvärde* är ett grovt balansmått (bybor × 15 + halva lagrets värde + tre dagars produktion) som bara används i balanstester och aldrig visas för spelaren.
Startvärdet är 385.

**Olika men jämbördiga.**
Närområdet slutar med ett starkt produktionskluster och ett stort stenlager på en ö.
Havet slutar med två öar, en sjöväg och högre dagsproduktion, men ännu tomma lager eftersom Nyön precis har kommit i gång.
Utvecklingsvärdena skiljer sig med knappt 8 %.
Havet bör dra ifrån andra veckan medan Närområdet har material för fler långhus; det prövas med längre körningar och balansbotar i M4.

**Strategin hinner ikapp (D-34).**
Den naiva strategin med hög egenvård slutar under båda de genomtänkta strategierna med måttlig egenvård.
Den får ut 0,65 i värde per använd kraft mot 0,90–0,94, alltså 1,4 gånger mindre, vilket uppfyller kravet på minst 1,3 i [forces.md](./forces.md).

**Utan träning (D-43).**
En spelare som valt bort träning men går och sover måttligt bygger stugor i stället för långhus och använder ingen Byggkraft.
Världen växer till lika många bybor, 19, men dagsproduktionen blir 122 mot 182–234 med träning, eftersom sågverk, brygga och väveri kräver Byggkraft.
Träning syns alltså i världen, men befolkningen står aldrig still på grund av en vana som spelaren valt bort.
Värdet per använd kraft blir högre, 1,04, eftersom stugorna inte räknas som kraftanvändning; måttet jämför bara strategier som använder alla krafter.

## 8. Sju dygn utan egenvård

Utan spelare och utan egenvård (sista raderna i tabellen ovan):

- Byborna växer från 12 till 16 av egen kraft och stannar vid bostadstaket.
- Grundtakten (fisk, säd, timmer) fortsätter; långsamma processer fortgår.
- **Inget förfaller.**
  Kalkylen kontrollerar varje timme att antalet bybor, byggnader, nivåer och upptäckta platser aldrig minskar och att inga lager eller saldon blir negativa (INV-V1, INV-R1, INV-F1).
- Gryningskraften växer till 310 på grundnivån, eftersom saknad sömndata ger 40 per spelardygn.
- Med egenvård men utan beslut händer exakt samma sak i världen; krafterna sparas bara.
  Det är spegeln i praktiken: egenvården ger möjligheter, besluten gör dem till en värld (D-14).

## 9. Lärdomar inför prototypen

1. **Utforskning hopar sig.**
   Alla strategier slutar veckan med över 400 Utforskning, alltså över det mjuka taket.
   V1 behöver en löpande användning, till exempel att lyfta dimman bricka för bricka (MAP-04) och fler expeditionsmål.
2. **Gryningskraften har för få användningar.**
   Två handlingar räcker inte för ett verkligt morgonbeslut varje dag; fler morgondrag behövs, till exempel att flytta bybor mellan arbetsgrupper eller prioritera transporter.
3. **Ved är grundflaskhalsen.**
   Utan minimilager stannar allt bygge, därför är LOG-06 flyttat från P1 till P0 (D-44).
4. **Tillväxttakten styr första veckan, inte bostäderna.**
   Alla strategier med beslut når 19–20 bybor, eftersom tillväxten är högst ett par bybor per dygn.
   Valet mellan långhus och stuga (D-43) märks först när bostadstaket nås, vilket prövas med längre körningar i M4.

## 10. Beslut

Produktägarens svar på de öppna frågorna i v0.1:

- **D-43:** långhus kräver Byggkraft, men stugan ger bostad för bara material, så tillväxten blockeras aldrig av en bortvald vana.
- **D-44:** LOG-06 (minimilager och prioriteringar) flyttas från P1 till P0.
- **D-45:** startkrafterna från den guidade turen är 100 Utforskning, 100 Byggkraft och 40 Gryningskraft.

Senare beslut som berör ekonomin:

- **D-65** (från [epok 1](./epoch-1.md)): brygga och väveri kan byggas med handkraft utan Byggkraft på 7 spelardygn, så att den som valt bort träning också får sjövägar och vadmal.
  Körningarna ovan räknar utan handkraft.
