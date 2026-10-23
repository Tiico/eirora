# Ekonomi v1

> **Status:** [FÖRSLAG] v1.1, byggplatser på brickor (avsnitt 3.1), väntar på granskning av produktägaren; v1.0 godkändes vid grind A0 (D-82)  
> **Ärende:** TASK-004, TASK-037, TASK-044 · **Milstolpe:** M0, M3, M4 · **PRD:** CIV-01–09, LOG-01–04, LOG-06, MAP-02, MAP-03, D-12, D-17, D-34, D-39, D-43–D-45, D-79, D-88–D-93, D-95, D-105–D-107  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Krafter:** [forces.md](./forces.md) · **Invarianter:** [invariants.md](./invariants.md)  
> **Kalkyl:** [models/economy_v1.py](./models/economy_v1.py) (`python -I economy_v1.py`) · **Balansbotar:** [tools/Eirora.Balance](../../tools/Eirora.Balance) (TASK-048, avsnitt 7.1)

Den första ekonomin för prototypen: 12 byggnader, 7 resurser, recept, logistik och vad krafterna kostar.
Siffrorna är räknade timme för timme över sju spelardygn i kalkylen ovan, inte uppskattade.
Kalkylen är ett räkneark i kodform; spelets simulering skrivs i C# i M1.
Innehållsomfånget avgjordes i M4: Anno-bredd med gradvis upplåsning, en bosättning per ö och bärare på land (D-105–D-107, avsnitt 10).
Den ekonomin tas fram i ekonomi v2 (TASK-058); ekonomi v1 beskriver det som spelas i dag.

## 1. Grundidé

- **Grundtakten kräver inga krafter.**
  Mat, ved och sten kan alltid produceras utan egenvård.
  Mat har två oberoende källor (INV-R4), och ved och sten finns på flera platser.
  Utan egenvård står världen still men förfaller aldrig (D-17).
- **Krafterna tar de stora stegen.**
  Byggkraft för förädling, hamnar, långhus och uppgraderingar; Utforskning för att hitta nytt; Gryningskraft för att lösa dagens flaskhals.
- **Byborna är arbetskraften.**
  Varje byggnad behöver bemanning, och lediga bybor bygger.
  Den knappa resursen är ofta folk, inte material, så spelaren styr arbetet: pausar byggnader och ordnar bemanningen och byggkön (avsnitt 4.1).
- **Platsen avgör värdet.**
  Varje landbricka är en möjlig byggplats (D-79).
  Avstånd till förråd, rika och fattiga brickor, skogen runt omkring och sjövägens kapacitet gör samma byggnad olika mycket värd.

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

| Byggnad | Kostnad | Arbete | Byggkraft | Bemanning | Effekt | Krav |
|---|---|---|---|---|---|---|
| Långhus | 10 timmer, 15 plank, 10 sten | 40 h | 60 | 0 | +8 bostadsplatser | Land |
| Stuga | 15 timmer, 5 sten | 32 h | 0 | 0 | +4 bostadsplatser | Land |
| Förråd | 10 timmer, 5 sten | 16 h | 0 | 1 | Lager 150 per resurs; nav för transport | Land |
| Fiskebod | 8 timmer | 12 h | 0 | 2 | 1,25 fisk/h | Fiskevatten |
| Åker | 4 timmer | 8 h | 0 | 2 | 0,5 säd/h dygnet runt; skördas aktiva timmar | Åker |
| Huggarkoja | 6 timmer | 8 h | 0 | 2 | 0,5 timmer/h per skogsbricka den fäller, högst två | Skog |
| Fårgård | 10 timmer, 5 plank | 12 h | 0 | 1 | 0,4 ull/h dygnet runt; klipps aktiva timmar | Bete |
| Stenbrott | 10 timmer, 4 plank | 16 h | 0 | 2 | 0,75 sten/h | Berg |
| Sågverk | 15 timmer, 5 sten | 24 h | 30 | 2 | 2 timmer → 1 plank per timme | Land |
| Väveri | 10 plank, 5 sten | 24 h | 60 | 2 | 2 ull → 1 vadmal per två timmar | Land |
| Brygga | 15 timmer, 10 plank | 24 h | 60 | 1 | Sjöväg, start för expeditioner, räknas som förråd för avstånd | Hamnläge |
| Utkiksvarde | 10 sten | 12 h | 40 | 0 | Kartläggning och expeditioner kostar 25 % mindre Utforskning | Land |
| *Uppgradering till nivå 2* | 10 plank, 5 sten | 16 h | 50 | | +50 % produktion | |
| *Väg* | 2 sten per bricka | 4 h per bricka | 0 | | Höjer avståndsklassen ett steg | |

Rika brickor ger mer: stenådern dubblar stenbrottet, rikt fiskevatten ger 1,5 gånger, urskogen 1,25 gånger och betet på Nyön dubblar fårgården.
Avståndet till förrådet (avsnitt 5), nivå 2 och dagsverke skalar all produktion, även åkerns och fårgårdens: de skalar hur fort säden och ullen växer dygnet runt, och skörden under de aktiva timmarna tömmer bara det som vuxit (D-103).
En åker 2–3 steg från förrådet ger alltså 75 % av en åker intill, och en åker på nivå 2 eller med dagsverke 150 %.
En skogsbricka rymmer 40 timmer och växer tillbaka med 0,25 timmer i timmen.

Tabellens kostnader, krav, bostäder, nivåer och recept är innehåll i innehållskatalogen (`ContentCatalog`, TASK-042), som simuleringen och klientens byggvy läser.
Katalogens kanoniska text är incheckad i `tests/Eirora.Simulation.Tests/Economy/Golden/content-catalog.txt`, så varje ändring syns i granskningen och hör till en ny innehålls- eller regelversion (ADR-0006).
Skogen, lagret, förråden, sjövägen och utkiksvardens rabatt är regler och står kvar i koden.

### 3.1 Byggplatser

Varje landbricka är en möjlig byggplats, och vad den duger till följer av terräng, höjd och grannar (D-79, D-88–D-93, [ADR-0010](../adr/0010-world-map-format-and-generator-version.md)).
Det finns inga namngivna platser; kolumnen *Krav* ovan säger vad brickan ska duga till.

| Krav | Brickan | Byggnader |
|---|---|---|
| Land | Varje landbricka utom berg och krönet | Långhus, stuga, förråd, sågverk, väveri, utkiksvarde |
| Skog | En landbricka utom berg och krönet med en upptäckt skogsbricka att fälla på eller intill sig | Huggarkoja |
| Åker | Åkermark | Åker |
| Bete | Betesmark | Fårgård |
| Berg | Berg utom krönet | Stenbrott |
| Krön | Startöns enda högsta bricka | Vårdkase ([epok 1](./epoch-1.md)) |
| Fiskevatten | Grunt vatten intill land | Fiskebod, på pålar |
| Hamnläge | En landbricka utom berg intill grunt vatten som når djuphavet | Brygga |

- En bricka bär en byggnad, och bara en upptäckt bricka går att bygga på.
- Huggarkojan når sin egen bricka och de sex grannarna och fäller de två rikaste skogsbrickorna som har timmer kvar.
  Timret räknas per skogsbricka, så två huggarkojor som når samma skog delar på den.
- En skogsbricka som en annan byggnad står på fälls inte.
  Att bygga i skogen är alltså ett val: brickan går att bygga på, men skogen går inte längre att hugga.
- Skogskravet räknar bara skog som huggarkojan verkligen kan fälla: upptäckta skogsbrickor som ingen annan byggnad står på (D-93).
  En huggarkoja kan alltså aldrig byggas där den inte ger något, och byggvyn avslöjar aldrig skog under dimman.
- Rikedomen hör till brickan: stenådern är ett rikt berg, och av urskogens två skogsbrickor är den ena rik.
- Höjden avgör i v1 bara krönet, där Vårdkasen byggs ([epok 1](./epoch-1.md)).
  Krönet är startöns enda högsta bricka och bär bara Vårdkasen, även när det är ett berg (D-92).
  På `prototype-home-1` är det västra berget (-4,0) krönet, med höjd 6 mot stenåderns 5, så att stenådern förblir stenbrottets.
- Byggregeln prövas när bygget läggs i kön.
  En byggnad som redan står kvar står kvar även på en bricka som inte längre duger, så regeländringen flyttar ingenting (INV-V1).
- Byggplatserna på brickor kom med regelversion 4.
  Migrationen dit skriver om sparningens plats-id till brickornas koordinater, så att varje byggnad, väg och lyft dimma står kvar och saldona är oförändrade (ADR-0006).
  En skogsplats som prototypen räknade som flera skogsbrickor lämnar sin andel timmer per bricka på sin egen bricka, och kartans övriga skog är full.
  Prototypens journaler från före regelversion 4 spelas inte längre upp, eftersom de skulle ge en annan värld än den spelaren byggde.
- Från regelversion 5 skalar avstånd, nivå 2 och dagsverke åkerns och fårgårdens tillväxt (D-103).
  Migrationen dit ändrar ingenting i sparningen: det som redan vuxit står kvar, och bara tiden efter växer med den nya regeln.
  Journaler från regelversion 4 spelas inte längre upp, av samma skäl som ovan.

Prototypens karta, `prototype-home` med samma land, vatten och terräng som klienten ritar (TASK-029), ger så här många byggplatser:

| Ö | Landbrickor | Land | Skog | Åker | Bete | Berg | Krön | Fiskevatten | Hamnläge |
|---|---|---|---|---|---|---|---|---|---|
| Hemön | 46 | 43 | 37 | 2 | 1 | 2 | 1 | 35 | 28 |
| Nyön | 6 | 5 | 5 | 0 | 1 | 1 | 0 | 12 | 5 |

*Skog* räknar brickorna med skog på eller intill sig när hela ön är upptäckt och obebyggd; under spelet duger färre (D-93).

Det som i dag är kuliss blir riktig terräng, och det märks:

- Huggarkojan i startbyn når två skogsbrickor, inte en, och ger 1 timmer/h från början.
- Skogen öster om byn, som prototypen räknade som tre skogsbrickor, är en enda skogsbricka och ger 0,5 timmer/h.
- Nyöns mitt, där prototypen hade sitt hamnläge, är öns enda strand och når vattnet genom en vik mot Hemön.
  Varje landbricka på Nyön utom berget är ett hamnläge, och kalkylens brygga står på skogsbrickan intill betet.
- Hemön har bara två åkerbrickor och en betesbricka, så maten kommer från fiskevattnet.
- Hemön har två berg att bryta sten i: stenådern och ett vanligt berg intill krönet, på det som prototypen ritade som strand (D-95).

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
- Start: 12 bybor, bostad för 16, ett förråd, en fiskebod, en åker och en huggarkoja vid en skogsdunge med två skogsbrickor.
  På en genererad karta står samma fyra byggnader så nära startbrickan som deras byggplatser tillåter, se [grundandet](#grundandet-på-en-genererad-karta).

### 4.1 Arbetsfördelning

Spelaren bestämmer vad byborna arbetar med (CIV-02, TASK-044).

- Byggnaderna som behöver bemanning står i en **bemanningsordning**, från början den ordning de byggdes i.
  En ny byggnad hamnar sist.
- Byborna fördelas uppifrån varje gång fördelningen kan ändras: vid grundandet, vid gryningen, när ett projekt blir klart och när spelaren pausar, återupptar eller ordnar om.
  En byggnad som inte är pausad bemannas om tillräckligt många bybor är kvar till den; annars hoppas den över och nästa som ryms bemannas.
  De som blir över är lediga och bygger.
- Byggnaderna arbetar också i bemanningsordningen, så en byggnad högre upp får först plats i ett nästan fullt lager och fäller först i en skog som två huggarkojor delar.
- **Pausa** en byggnad som producerar: den bemannas inte, och dess bybor blir lediga och bygger tills den återupptas.
  Den tappar ingen nivå och inget lager (D-39), och det som redan mognat står kvar.
  Förråd och bryggor producerar inget och kan inte pausas.
- Bara en bemannad åker eller fårgård sköts och växer, så att en paus kostar det byggnaden ger.
  Det gäller också en åker eller fårgård som står obemannad för att byborna inte räcker.
- **Ändra bemanningsordningen:** flytta en byggnad till en annan plats i ordningen.
  Byggnaderna bemannas i den nya ordningen från nästa kvart.
- **Flytta ett projekt först i byggkön:** de lediga byborna arbetar alltid på det första projektet, och det arbete ett projekt redan fått följer med det.
  Att avbryta ett projekt ingår inte.
- Alla tre är planering och kostar ingen kraft (D-23), men de ändrar världen och nekas därför under vilan (INV-T3).
- Arbetsfördelningen kom med regelversion 6.
  Förut var en bemannad byggnad bemannad för alltid, åkern och fårgården växte även obemannade, och byggkön gick i den ordning projekten lades.
  Migrationen dit låter alla byggnader vara igång i byggordning och ändrar inget annat; vem som är bemannad står kvar tills byborna fördelas nästa gång (ADR-0006).
  Journaler från regelversion 5 spelas inte längre upp, eftersom de skulle ge en annan värld.

## 5. Logistik

| Avstånd till närmaste förråd eller brygga | 0–1 steg | 2–3 steg | 4 eller fler |
|---|---|---|---|
| Andel av produktionen som når fram | 100 % | 75 % | 50 % |

- Avståndet räknas i steg över land till ett förråd eller en brygga på samma ö, med sökvägen från TASK-028.
  En vik eller ett sund gör vägen längre än fågelvägen, och det gäller på alla öar, även Nyön.
  En fiskebod på pålar tar sitt första steg i land.
- En väg höjer avståndsklassen ett steg.
- Ett förråd rymmer 150 av varje resurs.
  Fullt lager stoppar produktionen; inget går förlorat.
- Förädling lämnar ett **minimilager** på 20 av sin råvara (LOG-06, P0 enligt D-44), annars äter sågverket upp all ved som behövs till byggen.
- Varje ö där spelaren har byggt har eget lager; öarna är kartans (TASK-045).
  En brygga utanför Hemön rymmer också 150 av varje resurs, så att varorna har någonstans att vänta på skeppen.
- Varje ö med brygga får en egen sjöväg till Hemön när Hemön har en brygga.
  Varje sjöväg för 40 enheter per spelardygn, delat mellan alla varor, i varornas fasta ordning tills spelaren får importprioritet (TASK-046).
  Sjövägarna lastar av i kartans ordning när Hemöns lager bara rymmer en del.

### Logistiska avvägningar

1. **Rikt och långt bort, eller fattigt och nära.**
   Rikt fiskevatten tre steg bort ger 1,5 × 75 % = 1,13 gånger en vanlig fiskebod intill förrådet.
2. **Huggarkojan i skogen eller intill förrådet.**
   I urskogen står huggarkojan på en skogsbricka och tar ingen mark nära förrådet, men den står två steg bort: av 1,13 timmer/h från den rika brickan och en vanlig når 0,84 fram.
   Vid skogskanten intill förrådet når den samma två brickor och allt når fram, men den tar en bricka som ett långhus eller ett sågverk hade behövt.
3. **Ett andra förråd eller vägar.**
   Ett förråd i ett produktionskluster lyfter alla byggnader runt omkring på en gång men kostar en bybo i bemanning.
   En väg kostar ingen bemanning men gäller bara en byggnad och kostar 2 sten per bricka.
4. **Nyöns rikedom eller Hemöns närhet.**
   Nyöns stenåder och bete är dubbelt så rika, men allt måste över sjövägen, som bara rymmer 40 enheter per dygn.
   Bryggan intill betet når bara det ena av dem direkt; stenbrottet står två steg bort och får fram 75 % tills ett förråd byggs mitt på ön.
5. **Förädling eller byggmaterial.**
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

### Grundandet på en genererad karta

Prototypens handgjorda karta behåller sin by, sina kartläggningar och Nyön, så att varje värld på den spelas upp som förut.
En genererad karta har inga utpekade platser, så grundandet följer av kartan med samma regel varje gång (TASK-033, `Founding`):

- Förrådet, fiskeboden, åkern och huggarkojan står på närmaste bricka till startbrickan där de kan arbeta.
  De väljer gräs eller strand när det går, så att skog, åker och bete finns kvar åt kartläggningarna.
- *Kartlägg närområdet* visar ett berg att bryta sten i och två skogsbrickor, de som ligger längst från starten.
- *Kartlägg kusten* visar den åker och det fiskevatten vid startön som ligger längst från starten.
- Expeditionen hittar den närmaste ön 8–12 steg sjövägen som är värd resan: minst sex landbrickor och någon rik mark.
  Kartvalideringen räknar samma ö som den första, eftersom den frågar `Founding` ([map-validation.md](./map-validation.md)).

Generator v1 lägger den rika marken ute på öarna (MAP-03), så kartläggningarna på en genererad startö visar vanlig mark.
Lika långt bort avgörs av kartans ordning, så samma karta alltid ger samma grundande.

Grundandet sparas inte, så en sparad värld på en genererad karta läser sina kartläggningar och sin expeditions ö ur regeln.
En ändrad regel skulle alltså ändra världar som redan spelas.
Därför kräver en ändring en ny version av regeln bredvid den gamla, vald efter kartans generator och version ([ADR-0010](../adr/0010-world-map-format-and-generator-version.md)).
De gyllene grundandena (`tests/Eirora.Simulation.Tests/WorldGeneration/Golden/foundings.txt`) fångar varje ändring.

### Beslut om var krafterna ska riktas

1. **Byggkraft: sågverk, brygga eller långhus först?**
   Sågverket ger plank till allt annat, bryggan öppnar havet och långhuset ger fler bybor.
   Med måttlig egenvård räcker Byggkraften till alla tre första veckan, men plank och lediga bybor gör att de inte kan byggas samtidigt.
   Ordningen avgör vad som börjar ge avkastning först.
2. **Utforskning: närområdet eller en ny ö?**
   Två kartläggningar (180) ger rika brickor nära förrådet direkt.
   En expedition (250) ger en ny ö med dubbelt så rika brickor, men först efter några dygn och bakom en sjöväg med begränsad kapacitet.
3. **Gryningskraft: vilken flaskhals i dag?**
   Dagsverke på stenbrottet, sågverket eller fårgården, eller rusta ett långhus, beroende på vad som håller tillbaka världen just nu.
4. **Spara eller spendera.**
   Byggkraft kan sparas ihop till ett långhus eller en brygga (D-35), men byborna står still under tiden.

## 7. Sju dygn med två strategier

Båda körningarna har samma måttliga egenvård från [forces.md](./forces.md): 7 500 steg och 6,5 h sömn varje dygn, tre pass i veckan.

- **Närområdet:** kartlägger Hemön två gånger, bygger huggarkoja i urskogen, stenbrott på stenådern, ett andra förråd i klustret, sågverk, långhus, fiskebod vid det rika vattnet och en till åker.
  Gryningskraften går till dagsverken i stenbrottet och sågverket.
- **Närområdet, ordnad:** samma, men med långhuset först i byggkön och stenbrottet pausat när 100 sten ligger i lager, så att dess bybor bygger i stället, tills sista dygnet (avsnitt 4.1).
- **Havet:** bygger brygga, huggarkoja vid skogen öster om byn och sågverk, skickar en expedition, bygger brygga, fårgård och stenbrott på Nyön, väveri och långhus.
  Gryningskraften går till sågverket och fårgården.

Som jämförelse körs en **naiv** strategi med **hög** egenvård (15 000 steg, 8 h sömn, sex pass): den bygger det första som går där det går, uppgraderar det den redan har och läser inte kartan.

| Körning | Egenvård | Bybor | Byggnader | Öar | Sten i lager | Dagsproduktion | Utvecklingsvärde | Använt U/B/G | Sparat U/B/G | Värde per använd kraft |
|---|---|---|---|---|---|---|---|---|---|---|
| Närområdet | måttlig | 19 | 11 | 1 | 208 | 213 | 1 316 | 180/260/440 | 461/110/229 | 1,03 |
| Närområdet, ordnad | måttlig | 19 | 13 | 1 | 150 | 213 | 1 254 | 180/260/440 | 461/110/244 | 0,96 |
| Havet | måttlig | 20 | 12 | 2 | 36 | 230 | 1 207 | 250/270/360 | 426/100/260 | 0,91 |
| Naiv | hög | 20 | 10 | 1 | 19 | 188 | 1 089 | 180/310/630 | 520/135/96 | 0,61 |
| Utan träning | utan träning | 19 | 11 | 1 | 150 | 129 | 1 055 | 180/0/460 | 461/100/224 | 1,01 |
| Ingen spelare | ingen | 16 | 4 | 1 | 20 | 48 | 583 | 0/0/0 | 100/100/310 | – |
| Ingen spelare | måttlig | 16 | 4 | 1 | 20 | 48 | 583 | 0/0/0 | 551/370/492 | – |

*Dagsproduktion* är viktad produktionskapacitet per spelardygn vid veckans slut.
*Utvecklingsvärde* är ett grovt balansmått (bybor × 15 + halva lagrets värde + tre dagars produktion) som bara används i balanstester och aldrig visas för spelaren.
Startvärdet är 409.

**Olika men jämbördiga.**
Närområdet slutar med ett starkt produktionskluster och ett stort stenlager på en ö.
Havet slutar med två öar, en sjöväg, en bybo till och högre dagsproduktion, men nästan tomma lager eftersom Nyön precis har kommit i gång.
Utvecklingsvärdena skiljer sig med 9 %.
Havet bör dra ifrån andra veckan medan Närområdet har material för fler långhus; det prövas med längre körningar och balansbotar i M4.

**Strategin hinner ikapp (D-34).**
Den naiva strategin med hög egenvård slutar under båda de genomtänkta strategierna med måttlig egenvård.
Den får ut 0,61 i värde per använd kraft mot 0,91–1,03, alltså 1,5 gånger mindre, vilket uppfyller kravet på minst 1,3 i [forces.md](./forces.md).
Den bygger sitt stenbrott på det vanliga berget intill krönet, fyra steg från förrådet, medan de genomtänkta strategierna kartlägger och hittar stenådern.
Utan det berget skulle stenådern vara Hemöns enda berg att bryta sten i, och då hittar även den naiva strategin dit och skillnaden blir bara 1,2 gånger (D-95).

**Arbetsfördelningen är ett val, inte en genväg (TASK-044).**
Med stenbrottet pausat medan stenen räcker och långhuset först i kön bygger Närområdet 13 byggnader i stället för 11, med samma dagsproduktion.
Priset är 58 sten mindre i lager, så utvecklingsvärdet blir något lägre.
Att flytta bybor från ett stenbrott till ett bygge byter alltså material mot byggen; ingen fördelning är bäst i alla lägen.
Att bara en bemannad åker växer håller pausen ärlig: annars kunde åkern pausas större delen av dygnet och skördas ikapp utan förlust.

**Utan träning (D-43).**
En spelare som valt bort träning men går och sover måttligt bygger stugor i stället för långhus och använder ingen Byggkraft.
Världen växer till lika många bybor, 19, men dagsproduktionen blir 129 mot 213–230 med träning, eftersom sågverk, brygga och väveri kräver Byggkraft.
Träning syns alltså i världen, men befolkningen står aldrig still på grund av en vana som spelaren valt bort.
Värdet per använd kraft blir högre, 1,01, eftersom stugorna inte räknas som kraftanvändning; måttet jämför bara strategier som använder alla krafter.

### 7.1 Balansbotarna i simuleringen

Balansbotarna (TASK-048) spelar samma sju dygn i spelets riktiga simulering, med samma kommandon och tidsavläsningar som klienten.
Egenvården registreras som dagliga summor vid varje gryning enligt profilerna i [epok 1](./epoch-1.md#profilerna), där *måttlig* heter *normal*.
Strategierna har inga fasta koordinater: de läser kartan och väljer byggplats efter vad brickan ger, avståndet till förrådet och vad som redan står där, så att samma strategier spelar genererade kartor.
Hur botarna körs står i [development.md](../development.md#balansbotar).

| Körning | Egenvård | Bybor | Byggnader | Öar | Sten i lager | Dagsproduktion | Utvecklingsvärde | Använt U/B/G | Sparat U/B/G | Värde per använd kraft |
|---|---|---|---|---|---|---|---|---|---|---|
| Närområdet | måttlig | 19 | 12 | 1 | 204 | 194 | 1 267 | 180/260/410 | 461/110/233 | 1,01 |
| Närområdet, ordnad | måttlig | 19 | 12 | 1 | 148 | 218 | 1 281 | 180/260/410 | 461/110/259 | 1,03 |
| Havet | måttlig | 20 | 12 | 2 | 37 | 246 | 1 221 | 250/330/380 | 426/40/240 | 0,85 |
| Naiv | hög | 20 | 10 | 1 | 14 | 194 | 1 091 | 180/360/630 | 520/85/96 | 0,58 |
| Utan träning | utan träning | 19 | 11 | 1 | 150 | 134 | 1 069 | 180/0/490 | 461/100/194 | 0,98 |
| Ingen spelare | ingen | 16 | 4 | 1 | 20 | 48 | 583 | 0/0/0 | 100/100/310 | – |
| Ingen spelare | måttlig | 16 | 4 | 1 | 20 | 48 | 583 | 0/0/0 | 551/370/492 | – |

Botarnas vecka ska stämma med kalkylens tabell ovan inom ungefär ett besluts skillnad, och testerna underkänner annars bygget:

| Mått | Tolerans |
|---|---|
| Bybor och byggnader | ±1 |
| Öar | exakt |
| Sten i lager | ±10 |
| Dagsproduktion | ±10 % |
| Utvecklingsvärde | ±5 % |
| Använd och sparad kraft | ±60 per kraft, ett långhus Byggkraft |
| Värde per använd kraft | ±0,07 |

Testerna kräver också att botarna spelar exakt tabellen här, så att en ändring av balansen syns i dokumentet.
Byborna och öarna blir desamma som i kalkylen, och skillnaderna i övrigt har tre orsaker:

- **Kartläggningarna.**
  Botarna kartlägger båda områdena den första morgonen, så snart Utforskningen räcker, medan kalkylen kartlägger ett om dagen.
  Det andra området, med det rika fiskevattnet och åkern, kan därför bebyggas ett dygn tidigare.
- **Byggplatserna.**
  Närområdet och Utan träning ställer huggarkojan där mest timmer når förrådet: vid urskogens kant intill förrådet, där den når den rika skogsbrickan och en vanlig, i stället för på urskogen två steg bort (avvägning 2 i avsnitt 5).
  Havets brygga på Nyön står på stranden intill både betet och berget, så stenbrottet får fram allt i stället för 75 %.
- **Ordningen i kön.**
  Med en annan takt i timmer, plank och sten hinner ett annat mål först.
  Närområdet betalar det andra långhuset före sågverkets uppgradering, som därför inte är färdig när veckan slutar och saknas med 24 i dagsproduktion.
  Naiv uppgraderar också åkern, och Havet hinner betala ett andra långhus, så båda använder mer Byggkraft och får lägre värde per använd kraft.

Slutsatserna ovan håller.
De genomtänkta strategierna får 1,5–1,7 gånger den naiva strategins värde per använd kraft, och Närområdet och Havet skiljer sig med 4 % i utvecklingsvärde.

På 20 genererade kartor (`procedural-archipelago` version 1, seed 0–19) genomför båda strategierna sina planer med måttlig egenvård, Närområdet på 9–10 dygn och Havet på 7–15.
Där expeditionens ö saknar bete eller berg bygger Havet fårgården eller stenbrottet på hemön.
Efter fyra veckor har Havet högre utvecklingsvärde än Närområdet på 17 av kartorna, 2 154 mot 2 033 i snitt, men båda byarna står då sedan länge vid bostadstaket, eftersom planerna inte bygger fler bostäder än de tre respektive två långhusen.

## 8. Sju dygn utan egenvård

Utan spelare och utan egenvård (sista raderna i tabellen ovan):

- Byborna växer från 12 till 16 av egen kraft och stannar vid bostadstaket.
- Grundtakten (fisk, säd, timmer) fortsätter; långsamma processer fortgår.
- **Inget förfaller.**
  Kalkylen kontrollerar varje timme att antalet bybor, byggnader, nivåer och upptäckta brickor aldrig minskar och att inga lager eller saldon blir negativa (INV-V1, INV-R1, INV-F1).
- Gryningskraften växer till 310 på grundnivån, eftersom saknad sömndata ger 40 per spelardygn.
- Med egenvård men utan beslut händer exakt samma sak i världen; krafterna sparas bara.
  Det är spegeln i praktiken: egenvården ger möjligheter, besluten gör dem till en värld (D-14).

## 9. Lärdomar inför prototypen

1. **Utforskning hopar sig.**
   Alla strategier slutar veckan med över 400 Utforskning, alltså över det mjuka taket.
   V1 behöver en löpande användning, till exempel att lyfta dimman bricka för bricka (MAP-04) och fler expeditionsmål.
2. **Gryningskraften har för få användningar.**
   Två handlingar räcker inte för ett verkligt morgonbeslut varje dag; fler morgondrag behövs, till exempel att flytta bybor mellan arbetsgrupper eller prioritera transporter.
   Att flytta bybor blev i stället planering utan kraft (D-23, avsnitt 4.1).
3. **Ved är grundflaskhalsen.**
   Utan minimilager stannar allt bygge, därför är LOG-06 flyttat från P1 till P0 (D-44).
4. **Tillväxttakten styr första veckan, inte bostäderna.**
   Alla strategier med beslut når 19–20 bybor, eftersom tillväxten är högst ett par bybor per dygn.
   Valet mellan långhus och stuga (D-43) märks först när bostadstaket nås, vilket prövas med längre körningar i M4.
5. **Byggplatser på brickor gör placeringen till ett beslut (TASK-037).**
   Huggarkojan vid skogskanten, förrådet mitt i ett kluster och bryggan på rätt sida av Nyön är val som den naiva strategin missar.
   Skillnaden mellan genomtänkt och naiv strategi växer från 1,4 till 1,5 gånger, men bara för att startön har två berg att bryta sten i; med stenådern som enda berg blir den 1,2 (D-95).
   Startbyns skog ger dubbelt så mycket timmer som förut, eftersom den verkligen har två skogsbrickor; ved är ändå flaskhalsen för sågverket.
   Prototypens karta har bara två åkerbrickor; generatorn (TASK-031) bör ge varje startö mer odlingsmark, så att båda matkällorna går att bygga ut.

## 10. Beslut

Produktägarens svar på de öppna frågorna i v0.1:

- **D-43:** långhus kräver Byggkraft, men stugan ger bostad för bara material, så tillväxten blockeras aldrig av en bortvald vana.
- **D-44:** LOG-06 (minimilager och prioriteringar) flyttas från P1 till P0.
- **D-45:** startkrafterna från den guidade turen är 100 Utforskning, 100 Byggkraft och 40 Gryningskraft.

Senare beslut som berör ekonomin:

- **D-65** (från [epok 1](./epoch-1.md)): brygga och väveri kan byggas med handkraft utan Byggkraft på 7 spelardygn, så att den som valt bort träning också får sjövägar och vadmal.
  Körningarna ovan räknar utan handkraft.
- **D-79** (från [ADR-0010](../adr/0010-world-map-format-and-generator-version.md)): varje landbricka är en möjlig byggplats; avsnitt 3.1 beskriver hur.

Produktägarens svar på de öppna frågorna i v1.1 (TASK-037):

- **D-88:** fiskeboden står på pålar i det grunda vattnet, som startbyns fiskebod redan gör, så att ingen byggnad behöver flytta.
- **D-89:** ett hamnläge är varje landbricka utom berg vid grunt vatten som når djuphavet, eftersom Nyön bara har en strandbricka.
- **D-90:** huggarkojan får stå bredvid skogen, så att närheten till förrådet blir ett verkligt val.
- **D-91:** långhus, förråd och andra byggnader får stå på varje landbricka utom berg, även skog, åker och bete, som då inte längre går att hugga, odla eller beta.
- **D-92:** krönet är startöns enda högsta bricka och bär bara Vårdkasen, även när det är ett berg; en karta måste ha en enda sådan bricka, och på prototypens karta är det västra berget högst.
- **D-93:** skogskravet räknar bara upptäckta skogsbrickor som ingen annan byggnad står på, så att byggvyn aldrig avslöjar dimman och ingen huggarkoja byggs där den inte ger något.

- **D-95:** prototypens karta får ett vanligt berg på (-3,-1) intill krönet, och kartvalideringen kräver minst två berg att bryta sten i utom krönet, så att stenådern blir ett val och kravet i D-34 nås ([map-validation.md](./map-validation.md)).

Produktägarens svar på avvikelsen som [grind A0](./gate-a0.md#3-avvikelser-mellan-dokument-och-kalkyl) hittade (TASK-038):

- **D-103:** avstånd, nivå 2 och dagsverke skalar hur fort åkern och fårgården växer, som avsnitt 3 och 5 redan sa; tidigare skalade de bara hur mycket som kunde skördas i timmen, ett tak som aldrig nåddes.

Produktägarens svar på besluten om innehåll, bosättningar och transport i M4 (TASK-039–041):

- **D-105:** innehållet får Anno-bredd: befolkningsnivåer med egna behov och fler än 20 byggnader redan i epok 1.
  Nivåerna och behoven låses upp gradvis under epoken, till exempel när en bosättning når en viss storlek, så att första veckan är lika lätt som i dag och den guidade turen förblir kort.
- **D-106:** en bosättning per ö.
  Det första förrådet eller den första bryggan på en ö grundar öns bosättning, med egna bybor, bostäder och lager.
  Byborna bemannar bara byggnader på sin ö, äter ur sin ös lager och flyttar över en sjöväg till en bosättning med lediga bostäder, högst en per spelardygn.
- **D-107:** transporten på land sker med bärare med kapacitet i stället för bara avståndsklasser.
  Varje förråd har bärare som flyttar en begränsad mängd per timme, och vägar gör dem snabbare.
  Flaskhalsdiagnostiken ska alltid visa varför en vara inte når fram och vad en väg, ett förråd eller fler bärare skulle ge.

Nivåer, behov, byggnader, kedjor, upplåsning och bärarnas regler räknas fram i ekonomi v2 (TASK-058), som ersätter avsnitt 2–6 när den är godkänd.

Produktägarens svar på vad rivning ger tillbaka (TASK-059):

- **D-108:** en riven byggnad ger tillbaka hälften av materialet den kostade, avrundat nedåt per resurs, till öns lager; kraft återbetalas aldrig (INV-F5).
  Återbetalningen måste rymmas i öns lager (INV-R1), och bekräftelsen visar exakt vad som kommer tillbaka.
  Att flytta en byggnad genom att riva och bygga om kostar alltså halva materialet och hela kraften; balansbotarna och scenarierna prövar att det inte blir en dominerande strategi.
