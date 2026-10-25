# Ekonomi v1

> **Status:** [FÖRSLAG] v1.1, byggplatser på rutor (avsnitt 3.1), väntar på granskning av produktägaren; v1.0 godkändes vid grind A0 (D-82)  
> **Ärende:** TASK-004, TASK-037, TASK-044 · **Milstolpe:** M0, M3, M4 · **PRD:** CIV-01–09, LOG-01–04, LOG-06, MAP-02, MAP-03, D-12, D-17, D-34, D-39, D-43–D-45, D-79, D-88–D-93, D-95, D-105–D-107  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Krafter:** [forces.md](./forces.md) · **Invarianter:** [invariants.md](./invariants.md)  
> **Kalkyl:** [models/economy_v1.py](./models/economy_v1.py) (`python -I economy_v1.py`) · **Balansbotar:** [tools/Eirora.Balance](../../tools/Eirora.Balance) (TASK-048, avsnitt 7.1)

Den första ekonomin för prototypen: 12 byggnader, 7 resurser, recept, logistik och vad krafterna kostar.
Siffrorna är räknade timme för timme över sju dagar i kalkylen ovan, inte uppskattade.
Kalkylen är ett räkneark i kodform; spelets simulering skrivs i C# i M1.
Innehållsomfånget avgjordes i M4: Anno-bredd med gradvis upplåsning, en bosättning per ö och bärare på land (D-105–D-107, avsnitt 10).
Den ekonomin tas fram i ekonomi v2 (TASK-058); ekonomi v1 beskriver det som spelas i dag.

## 1. Grundidé

- **Grundtakten kräver inga krafter.**
  Mat, ved och sten kan alltid produceras utan egenvård.
  Mat har två oberoende källor (INV-R4), och ved och sten finns på flera platser.
  Utan egenvård står världen still men förfaller aldrig (D-17).
- **Krafterna tar de stora stegen.**
  Styrka för förädling, hamnar, långhus och uppgraderingar; Utforskning för att hitta nytt; Ork för att lösa dagens flaskhals.
- **Byborna är arbetskraften.**
  Varje byggnad behöver bemanning, och lediga bybor bygger.
  Den knappa resursen är ofta folk, inte material, så spelaren styr arbetet: pausar byggnader och ordnar bemanningen och byggkön (avsnitt 4.1).
- **Platsen avgör värdet.**
  Varje landruta är en möjlig byggplats (D-79).
  Avstånd till förråd, rika och fattiga rutor, skogen runt omkring och sjövägens kapacitet gör samma byggnad olika mycket värd.

## 2. Resurser

| Resurs | Roll | Källor |
|---|---|---|
| Fisk | Basbehov (mat) | Fiskebod |
| Säd | Basbehov (mat) | Åker (långsam process) |
| Timmer | Byggmaterial, råvara | Skogshuggare; skogen växer tillbaka (långsam process) |
| Plankor | Byggmaterial | Sågverk |
| Sten | Byggmaterial | Stenbrott |
| Ull | Råvara | Fårgård (långsam process) |
| Tyg | Komfort, ger snabbare tillväxt | Väveri |

Mat har två oberoende källor, så ingen enskild flaskhals kan svälta byborna (PRD 6.2).

## 3. Byggnader

| Byggnad | Kostnad | Arbete | Styrka | Bemanning | Effekt | Krav |
|---|---|---|---|---|---|---|
| Långhus | 10 timmer, 15 plankor, 10 sten | 40 h | 60 | 0 | +8 bostadsplatser | Land |
| Stuga | 15 timmer, 5 sten | 32 h | 0 | 0 | +4 bostadsplatser | Land |
| Förråd | 10 timmer, 5 sten | 16 h | 0 | 1 | Lager 150 per resurs; nav för transport | Land |
| Fiskebod | 8 timmer | 12 h | 0 | 2 | 1,25 fisk/h | Fiskevatten |
| Åker | 4 timmer | 8 h | 0 | 2 | 0,5 säd/h dygnet runt; skördas aktiva timmar | Åker |
| Skogshuggare | 6 timmer | 8 h | 0 | 2 | 0,5 timmer/h per skogsruta den fäller, högst två | Skog |
| Fårgård | 10 timmer, 5 plankor | 12 h | 0 | 1 | 0,4 ull/h dygnet runt; klipps aktiva timmar | Betesmark |
| Stenbrott | 10 timmer, 4 plankor | 16 h | 0 | 2 | 0,75 sten/h | Berg |
| Sågverk | 15 timmer, 5 sten | 24 h | 30 | 2 | 2 timmer → 1 planka per timme | Land |
| Väveri | 10 plankor, 5 sten | 24 h | 60 | 2 | 2 ull → 1 tyg per två timmar | Land |
| Brygga | 15 timmer, 10 plankor | 24 h | 60 | 1 | Sjöväg, start för expeditioner, räknas som förråd för avstånd | Hamnläge |
| Utkikstorn | 10 sten | 12 h | 40 | 0 | Kartläggning och expeditioner kostar 25 % mindre Utforskning | Land |
| *Uppgradering till nivå 2* | 10 plankor, 5 sten | 16 h | 50 | | +50 % produktion | |
| *Väg* | 2 sten per ruta | 4 h per ruta | 0 | | Höjer avståndsklassen ett steg | |

Rika rutor ger mer: stenådern dubblar stenbrottet, rikt fiskevatten ger 1,5 gånger, urskogen 1,25 gånger och betesmarken på Nyön dubblar fårgården.
Avståndet till förrådet (avsnitt 5), nivå 2 och extraarbete skalar all produktion, även åkerns och fårgårdens: de skalar hur fort säden och ullen växer dygnet runt, och skörden under de aktiva timmarna tömmer bara det som vuxit (D-103).
En åker 2–3 steg från förrådet ger alltså 75 % av en åker intill, och en åker på nivå 2 eller med extraarbete 150 %.
En skogsruta rymmer 40 timmer och växer tillbaka med 0,25 timmer i timmen.

Tabellens kostnader, krav, bostäder, nivåer och recept är innehåll i innehållskatalogen (`ContentCatalog`, TASK-042), som simuleringen och klientens byggvy läser.
Katalogens kanoniska text är incheckad i `tests/Eirora.Simulation.Tests/Economy/Golden/content-catalog.txt`, så varje ändring syns i granskningen och hör till en ny innehålls- eller regelversion (ADR-0006).
Skogen, lagret, förråden, sjövägen och utkikstornets rabatt är regler och står kvar i koden.

### 3.1 Byggplatser

Varje landruta är en möjlig byggplats, och vad den duger till följer av terräng, höjd och grannar (D-79, D-88–D-93, [ADR-0010](../adr/0010-world-map-format-and-generator-version.md)).
Det finns inga namngivna platser; kolumnen *Krav* ovan säger vad rutan ska duga till.

| Krav | Rutan | Byggnader |
|---|---|---|
| Land | Varje landruta utom berg och toppen | Långhus, stuga, förråd, sågverk, väveri, utkikstorn |
| Skog | En landruta utom berg och toppen med en upptäckt skogsruta att fälla på eller intill sig | Skogshuggare |
| Åker | Åkermark | Åker |
| Betesmark | Betesmark | Fårgård |
| Berg | Berg utom toppen | Stenbrott |
| Topp | Startöns enda högsta ruta | Vårdkase ([epok 1](./epoch-1.md)) |
| Fiskevatten | Grunt vatten intill land | Fiskebod, på pålar |
| Hamnläge | En landruta utom berg intill grunt vatten som når djuphavet | Brygga |

- En ruta bär en byggnad, och bara en upptäckt ruta går att bygga på.
- Skogshuggaren når sin egen ruta och de sex grannarna och fäller de två rikaste skogsrutorna som har timmer kvar.
  Timret räknas per skogsruta, så två skogshuggare som når samma skog delar på den.
- En skogsruta som en annan byggnad står på fälls inte.
  Att bygga i skogen är alltså ett val: rutan går att bygga på, men skogen går inte längre att hugga.
- Skogskravet räknar bara skog som skogshuggaren verkligen kan fälla: upptäckta skogsrutor som ingen annan byggnad står på (D-93).
  En skogshuggare kan alltså aldrig byggas där den inte ger något, och byggvyn avslöjar aldrig skog under dimman.
- Rikedomen hör till rutan: stenådern är ett rikt berg, och av urskogens två skogsrutor är den ena rik.
- Höjden avgör i v1 bara toppen, där Vårdkasen byggs ([epok 1](./epoch-1.md)).
  Toppen är startöns enda högsta ruta och bär bara Vårdkasen, även när det är ett berg (D-92).
  På `prototype-home-1` är det västra berget (-4,0) toppen, med höjd 6 mot stenåderns 5, så att stenådern förblir stenbrottets.
- Byggregeln prövas när bygget läggs i kön.
  En byggnad som redan står kvar står kvar även på en ruta som inte längre duger, så regeländringen flyttar ingenting (INV-V1).
- Byggplatserna på rutor kom med regelversion 4.
  Migrationen dit skriver om sparningens plats-id till rutornas koordinater, så att varje byggnad, väg och lyft dimma står kvar och saldona är oförändrade (ADR-0006).
  En skogsplats som prototypen räknade som flera skogsrutor lämnar sin andel timmer per ruta på sin egen ruta, och kartans övriga skog är full.
  Prototypens journaler från före regelversion 4 spelas inte längre upp, eftersom de skulle ge en annan värld än den spelaren byggde.
- Från regelversion 5 skalar avstånd, nivå 2 och extraarbete åkerns och fårgårdens tillväxt (D-103).
  Migrationen dit ändrar ingenting i sparningen: det som redan vuxit står kvar, och bara tiden efter växer med den nya regeln.
  Journaler från regelversion 4 spelas inte längre upp, av samma skäl som ovan.

Prototypens karta, `prototype-home` med samma land, vatten och terräng som klienten ritar (TASK-029), ger så här många byggplatser:

| Ö | Landrutor | Land | Skog | Åker | Betesmark | Berg | Topp | Fiskevatten | Hamnläge |
|---|---|---|---|---|---|---|---|---|---|
| Hemön | 46 | 43 | 37 | 2 | 1 | 2 | 1 | 35 | 28 |
| Nyön | 6 | 5 | 5 | 0 | 1 | 1 | 0 | 12 | 5 |

*Skog* räknar rutorna med skog på eller intill sig när hela ön är upptäckt och obebyggd; under spelet duger färre (D-93).

Det som i dag är kuliss blir riktig terräng, och det märks:

- Skogshuggaren i startbyn når två skogsrutor, inte en, och ger 1 timmer/h från början.
- Skogen öster om byn, som prototypen räknade som tre skogsrutor, är en enda skogsruta och ger 0,5 timmer/h.
- Nyöns mitt, där prototypen hade sitt hamnläge, är öns enda strand och når vattnet genom en vik mot Hemön.
  Varje landruta på Nyön utom berget är ett hamnläge, och kalkylens brygga står på skogsrutan intill betesmarken.
- Hemön har bara två åkerrutor och en betesruta, så maten kommer från fiskevattnet.
- Hemön har två berg att bryta sten i: stenådern och ett vanligt berg intill toppen, på det som prototypen ritade som strand (D-95).

## 4. Bybor, basbehov och vila

- Varje bybo äter 1 mat per dag vid gryningen, fisk först och sedan säd.
- Mätta bybor ger **+1 bybo** per dag om det finns bostadsplats.
  Bostad kommer från långhus, som kostar Styrka, eller stugor, som bara kostar material och mer arbete (D-43).
  Tillväxten blockeras alltså aldrig av att spelaren valt bort träning (HLT-01), men träning ger snabbare bostäder.
  Om det också finns 2 tyg förbrukas de som komfort och ger **+1 bybo till**.
- Brist på mat eller tyg stoppar bara tillväxten; ingen bybo, byggnad eller nivå går förlorad (D-39, INV-V3).
- Lediga bybor bygger med en arbetstimme per bybo och aktiv timme.
- Under vilotiden (8 h i kalkylen) sover byborna: ingen produktion, inget bygge, ingen transport (INV-T4).
  Åkern, fårgården och skogen fortsätter att växa och skördas när byborna vaknar (INV-T5).
- Start: 12 bybor, bostad för 16, ett förråd, en fiskebod, en åker och en skogshuggare vid en skogsdunge med två skogsrutor.
  På en genererad karta står samma fyra byggnader så nära startrutan som deras byggplatser tillåter, se [grundandet](#grundandet-på-en-genererad-karta).

### 4.1 Arbetsfördelning

Spelaren bestämmer vad byborna arbetar med (CIV-02, TASK-044).

- Byggnaderna som behöver bemanning står i en **bemanningsordning**, från början den ordning de byggdes i.
  En ny byggnad hamnar sist.
- Byborna fördelas uppifrån varje gång fördelningen kan ändras: vid grundandet, vid gryningen, när ett projekt blir klart och när spelaren pausar, återupptar eller ordnar om.
  En byggnad som inte är pausad bemannas om tillräckligt många bybor är kvar till den; annars hoppas den över och nästa som ryms bemannas.
  De som blir över är lediga och bygger.
- Byggnaderna arbetar också i bemanningsordningen, så en byggnad högre upp får först plats i ett nästan fullt lager och fäller först i en skog som två skogshuggare delar.
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

### 4.2 En bosättning per ö

Från regelversion 9 har varje ö en egen bosättning enligt [ekonomi v2, avsnitt 6](./economy-v2.md#6-bosättningar) (D-106, D-113, TASK-050).

- Hemöns bosättning finns från början; på en annan ö anläggs den av den första bryggan, med bostäder för 4 och utan bybor.
- Byborna bemannar bara byggnader på sin ö, i bemanningsordningen, och äter ur sin ös lager vid gryningen.
  En bosättning utan mat eller bostäder växer inte, men ingen bybo försvinner (D-39), och en bosättning utan bybor växer inte heller.
- Vid gryningen flyttar högst en ledig bybo i hela världen, över en sjöväg, till den bosättning som saknar flest bybor till sina byggnader och har en ledig bostad, från den förbundna bosättning som har flest lediga.
- Lediga bybor bygger på sin ö och på öar som sjövägen förbinder med deras; bryggan på en ö som ännu saknar bosättning bygger Hemöns.
  Byggarna seglar från en brygga på Hemön, så den kommer före allt på andra öar.
- Varje bygge i kön får arbete av de bybor som når det och som byggena före i kön inte har tagit, så ett bygge på en ö väntar inte på ett bygge på en annan.
  Ett bygge vars arbete redan är gjort, till exempel ett som spelaren har skyndat på och sedan flyttat bak i kön, står därför färdigt samma kvart, också när det inte är först i kön.
- Bygget betalas ur öns lager när det räcker, annars ur Hemöns, och Hemöns material tar plats på sjövägen före allt annat.
- Den första bryggans material betalas ur Hemöns lager, och sjövägen bär det först när den har öppnat, före allt annat, som allt material från Hemön till byggen på ön.
- Migrationen dit ger öarna de bybor som bemannar deras byggnader och låter resten bo kvar på Hemön (ADR-0006).
  En ö kan då ha fler bybor än bostäder; de bor kvar, och bosättningen växer först när det finns plats (D-39).
  Det som inte längre ryms i en ös lager skeppas hem om ön har en sjöväg, och väntar annars vid öns förråd eller brygga tills det finns plats.

## 5. Logistik

Från regelversion 8 bär förrådens och bryggornas bärare varorna på land, vägar läggs ruta för ruta, och avståndsklasserna nedan gäller inte längre ([ekonomi v2, avsnitt 7](./economy-v2.md#7-bärare-och-vägar); D-107, D-109–D-112).
Diagnostiken säger då om en vara inte når fram för att bärarna inte hinner, för att inget bemannat förråd når byggnaden eller för att lagret är fullt, och vad en väg, ett förråd eller en bärare till skulle ge.
Resten av avsnittet beskriver ekonomi v1 som den var före regelversion 8.

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
  En brygga rymmer 50 av varje resurs, också på Hemön, så att varorna har någonstans att vänta på skeppen (regelversion 9, [ekonomi v2](./economy-v2.md#6-bosättningar)).
- Varje ö med brygga får en egen sjöväg till Hemön när Hemön har en brygga.
  Varje sjöväg för 40 enheter per dag, delat mellan alla varor och båda hållen (D-106).
  Först går Hemöns material till byggen på ön, sedan mat ut för två dygn till öns bybor, sedan öns varor hem, där ön behåller lika mycket mat.
  Varorna går i sin fasta ordning tills spelaren får importprioritet (TASK-046), och sjövägarna lastar av i kartans ordning när Hemöns lager bara rymmer en del.

### Flaskhalsdiagnostik

Spelaren ser alltid varför en vara inte når fram (CIV-05, LOG-03, TASK-043).
Diagnostiken läser nästa aktiva kvart ur världen utan att ändra den (INV-D2) och ger varje producerande byggnad ett tillstånd: går för fullt, går långsammare eller står still.
Skälen är de som reglerna ovan ger, i den ordning de verkar:

| Skäl | Tillståndet | Vad bladet säger |
|---|---|---|
| Pausad av spelaren | Står still | Att den är pausad och vad pausen kostar |
| Bemanning saknas | Står still | Hur många bybor som saknas efter byggnaderna före den i bemanningen |
| Råvaran är under minimilagret | Står still eller går långsammare | Vilken råvara och hur mycket som saknas för en timmes arbete i full takt |
| Skogen är fälld | Går långsammare eller står still | Hur många av skogsrutorna som är fällda |
| Lagret är fullt | Står still eller går långsammare | Vilken vara och hur mycket lagret rymmer |
| Lång väg till förrådet | Går långsammare | Hur stor andel som når fram och vad en väg eller ett förråd intill skulle ge |

Bemanningen spelar bara roll för det en byggnad producerar.
Ett förråd eller en brygga utan sina bybor lagrar ändå och räknas ändå som förråd för avståndet, så de står aldrig still av det.

Vad en väg, ett förråd intill eller byggnadens bybor skulle ge räknas genom samma kvart som produktionen, med råvarorna, skogen och lagrets plats som de är.
Ett sågverk som ändå saknar timmer får alltså ingen vinst av en väg, och då är det timret som är skälet.

Byggkön ingår inte: en kvart då ett bygge blir klart kan ge en byggnad bybor, en nivå eller en väg innan den arbetar, och diagnostiken visar då vad byggnaderna gör som de står nu.

Varje sjöväg visar hur stor del av sin kapacitet den använder och vad som väntar på ön den går från; sjövägarna delar på platsen i Hemöns lager i kartans ordning.
Varje ö får sin största flaskhals, skälet som kostar mest just nu, och när det är en råvara som saknas också var extraarbete gör mest nytta (avsnitt 6, beslut 3).
En paus är spelarens eget val och blir aldrig öns flaskhals.
Kartan markerar byggnader som står still med en skylt med två staplar medan byborna är vakna, så att formen och inte färgen bär det (UX-05).
Senare regler, som bärarna i D-107, lägger till sina skäl utan att ändra hur diagnostiken läses.

### Logistiska avvägningar

1. **Rikt och långt bort, eller fattigt och nära.**
   Rikt fiskevatten tre steg bort ger 1,5 × 75 % = 1,13 gånger en vanlig fiskebod intill förrådet.
2. **Skogshuggaren i skogen eller intill förrådet.**
   I urskogen står skogshuggaren på en skogsruta och tar ingen mark nära förrådet, men den står två steg bort: av 1,13 timmer/h från den rika rutan och en vanlig når 0,84 fram.
   Vid skogskanten intill förrådet når den samma två rutor och allt når fram, men den tar en ruta som ett långhus eller ett sågverk hade behövt.
3. **Ett andra förråd eller vägar.**
   Ett förråd i ett produktionskluster lyfter alla byggnader runt omkring på en gång men kostar en bybo i bemanning.
   En väg kostar ingen bemanning men gäller bara en byggnad och kostar 2 sten per ruta.
4. **Nyöns rikedom eller Hemöns närhet.**
   Nyöns stenåder och betesmark är dubbelt så rika, men allt måste över sjövägen, som bara rymmer 40 enheter per dygn.
   Bryggan intill betesmarken når bara det ena av dem direkt; stenbrottet står två steg bort och får fram 75 % tills ett förråd byggs mitt på ön.
5. **Förädling eller byggmaterial.**
   Varje plankor kostar två timmer som annars kunde ha blivit ett bygge; minimilagret avgör balansen.

## 6. Vad krafterna kostar

| Kraft | Handling | Kostnad | Effekt |
|---|---|---|---|
| Utforskning | Kartlägg närområdet | 60 | Visar stenådern och urskogen på Hemön |
| Utforskning | Kartlägg kusten | 120 | Visar rikt fiskevatten och mer odlingsmark |
| Utforskning | Expedition | 250 | Hittar Nyön efter 32 aktiva timmar; kräver brygga |
| Styrka | Bygga eller uppgradera | Se tabellen i avsnitt 3 | |
| Ork | Arbeta extra | 30 | En byggnad producerar 50 % mer under dagen |
| Ork | Skynda på | 20 | +8 arbetstimmar på det första bygget i kön |

Planering, bemanning, minimilager och prioriteringar kostar aldrig kraft (D-23).
Startkrafter från den guidade turen: 100 Utforskning, 100 Styrka, 40 Ork (D-45).

### Grundandet på en genererad karta

Prototypens handgjorda karta behåller sin by, sina kartläggningar och Nyön, så att varje värld på den spelas upp som förut.
En genererad karta har inga utpekade platser, så grundandet följer av kartan med samma regel varje gång (TASK-033, `Founding`):

- Förrådet, fiskeboden, åkern och skogshuggaren står på närmaste ruta till startrutan där de kan arbeta.
  De väljer gräs eller strand när det går, så att skog, åker och betesmark finns kvar åt kartläggningarna.
- *Kartlägg närområdet* visar ett berg att bryta sten i och två skogsrutor, de som ligger längst från starten.
- *Kartlägg kusten* visar den åker och det fiskevatten vid startön som ligger längst från starten.
- Expeditionen hittar den närmaste ön 8–12 steg sjövägen som är värd resan: minst sex landrutor och någon rik mark.
  Kartvalideringen räknar samma ö som den första, eftersom den frågar `Founding` ([map-validation.md](./map-validation.md)).

Generator v1 lägger den rika marken ute på öarna (MAP-03), så kartläggningarna på en genererad startö visar vanlig mark.
Lika långt bort avgörs av kartans ordning, så samma karta alltid ger samma grundande.

Grundandet sparas inte, så en sparad värld på en genererad karta läser sina kartläggningar och sin expeditions ö ur regeln.
En ändrad regel skulle alltså ändra världar som redan spelas.
Därför kräver en ändring en ny version av regeln bredvid den gamla, vald efter kartans generator och version ([ADR-0010](../adr/0010-world-map-format-and-generator-version.md)).
De gyllene grundandena (`tests/Eirora.Simulation.Tests/WorldGeneration/Golden/foundings.txt`) fångar varje ändring.

### Beslut om var krafterna ska riktas

1. **Styrka: sågverk, brygga eller långhus först?**
   Sågverket ger plankor till allt annat, bryggan öppnar havet och långhuset ger fler bybor.
   Med måttlig egenvård räcker Styrkan till alla tre första veckan, men plankor och lediga bybor gör att de inte kan byggas samtidigt.
   Ordningen avgör vad som börjar ge avkastning först.
2. **Utforskning: närområdet eller en ny ö?**
   Två kartläggningar (180) ger rika rutor nära förrådet direkt.
   En expedition (250) ger en ny ö med dubbelt så rika rutor, men först efter några dygn och bakom en sjöväg med begränsad kapacitet.
3. **Ork: vilken flaskhals i dag?**
   Arbeta extra på stenbrottet, sågverket eller fårgården, eller skynda på ett långhus, beroende på vad som håller tillbaka världen just nu.
4. **Spara eller spendera.**
   Styrka kan sparas ihop till ett långhus eller en brygga (D-35), men byborna står still under tiden.

## 7. Sju dygn med två strategier

Båda körningarna har samma måttliga egenvård från [forces.md](./forces.md): 7 500 steg och 6,5 h sömn varje dygn, tre pass i veckan.

- **Närområdet:** kartlägger Hemön två gånger den första morgonen, bygger skogshuggare vid urskogens kant, stenbrott på stenådern, ett andra förråd i klustret, sågverk, långhus, fiskebod vid det rika vattnet och en till åker.
  Orken går till extraarbete i stenbrottet och sågverket.
- **Närområdet, ordnad:** samma, men med långhuset först i byggkön och stenbrottet pausat från 100 sten i lager tills lagret är nere under 80, så att dess bybor bygger i stället, tills sista dygnet (avsnitt 4.1).
- **Havet:** bygger brygga, skogshuggare vid skogen öster om byn och sågverk, skickar en expedition, bygger brygga, fårgård och stenbrott på Nyön, en stuga, väveri och långhus.
  Orken går till sågverket och fårgården.

Som jämförelse körs en **naiv** strategi med **hög** egenvård (15 000 steg, 8 h sömn, sex pass): den bygger det första som går där det går, uppgraderar det den redan har och läser inte kartan.

Från regelversion 8 bär förrådens och bryggornas bärare varorna på land, och avståndet kostar bärartid i stället för en del av produktionen ([ekonomi v2, avsnitt 7](./economy-v2.md#7-bärare-och-vägar); D-107, D-110).
Kalkylen räknar med bärarna, och strategierna ställer byggnaderna där balansbotarna ställer dem (avsnitt 7.1), intill ett förråd eller en brygga vars bärare hinner med.
Havet bygger därför Nyöns brygga, fårgård och stenbrott före den första bostaden, som blir en stuga, som i [ekonomi v2, avsnitt 9](./economy-v2.md#9-sju-dygn-med-tre-strategier).
Kalkylen utan bärare finns kvar för ekonomi v2:s migration och epok 1:s tempo, som räknar på ekonomi v1 som den var.

| Körning | Egenvård | Bybor | Byggnader | Öar | Sten i lager | Dagsproduktion | Utvecklingsvärde | Använt U/S/O | Sparat U/S/O | Värde per använd kraft |
|---|---|---|---|---|---|---|---|---|---|---|
| Närområdet | måttlig | 19 | 11 | 1 | 210 | 204 | 1 308 | 180/260/410 | 461/110/233 | 1,06 |
| Närområdet, ordnad | måttlig | 19 | 12 | 1 | 112 | 228 | 1 301 | 180/260/380 | 461/110/274 | 1,09 |
| Havet | måttlig | 20 | 12 | 2 | 29 | 246 | 1 242 | 250/210/340 | 426/160/270 | 1,04 |
| Naiv | hög | 19 | 10 | 1 | 54 | 211 | 1 169 | 180/360/630 | 520/85/96 | 0,65 |
| Utan träning | utan träning | 19 | 11 | 1 | 150 | 156 | 1 138 | 180/0/480 | 461/100/204 | 1,10 |
| Ingen spelare | ingen | 16 | 4 | 1 | 20 | 48 | 583 | 0/0/0 | 100/100/310 | – |
| Ingen spelare | måttlig | 16 | 4 | 1 | 20 | 48 | 583 | 0/0/0 | 551/370/492 | – |

*Dagsproduktion* är viktad produktionskapacitet per dag vid veckans slut, med den andel som bärarna hinner få fram.
*Utvecklingsvärde* är ett grovt balansmått (bybor × 15 + halva lagrets värde + tre dagars produktion) som bara används i balanstester och aldrig visas för spelaren.
Startvärdet är 409.
Kalkylen räknar med en bosättning per ö som i regelversion 9 (avsnitt 4.2).
Bärarna använder 31–74 % av sin tid under veckan, och startförrådets ensamma bärare i den orörda byn 37 %, som i [ekonomi v2, avsnitt 11](./economy-v2.md#11-utan-egenvård).

**Olika men jämbördiga.**
Närområdet slutar med ett starkt produktionskluster och ett stort stenlager på en ö.
Havet slutar med två öar, en sjöväg, en bybo till och högre dagsproduktion, men nästan tomma lager eftersom Nyön precis har kommit i gång.
Utvecklingsvärdena skiljer sig med 5 %.
Havet bör dra ifrån andra veckan medan Närområdet har material för fler långhus; det prövas med längre körningar och balansbotar i M4.

**Strategin hinner ikapp (D-34).**
Den naiva strategin med hög egenvård slutar under båda de genomtänkta strategierna med måttlig egenvård.
Den får ut 0,65 i värde per använd kraft mot 1,04–1,09, alltså 1,6 gånger mindre, vilket uppfyller kravet på minst 1,3 i [forces.md](./forces.md).
Den bygger sitt stenbrott på det vanliga berget intill toppen, fyra steg från förrådet, medan de genomtänkta strategierna kartlägger och hittar stenådern.
Med bärarna når allt fram från berget, men det kostar startförrådets bärare tid, och de tas från byggandet: bärarna använder 69 % av sin tid, mot 66 % i Närområdet med två förråd.
I ekonomi v1 utan bärare nådde hälften av stenen fram, och utan berget skulle stenådern vara Hemöns enda berg att bryta sten i, så att även den naiva strategin hittade dit och skillnaden blev bara 1,2 gånger (D-95).

**Arbetsfördelningen är ett val, inte en genväg (TASK-044).**
Med stenbrottet pausat medan stenen räcker och långhuset först i kön bygger Närområdet 12 byggnader i stället för 11 och hinner uppgradera sågverket, så att dagsproduktionen blir 228 mot 204.
Priset är 98 sten mindre i lager, så utvecklingsvärdet blir ändå något lägre.
Att flytta bybor från ett stenbrott till ett bygge byter alltså material mot byggen; ingen fördelning är bäst i alla lägen.
Att bara en bemannad åker växer håller pausen ärlig: annars kunde åkern pausas större delen av dygnet och skördas ikapp utan förlust.

**Utan träning (D-43).**
En spelare som valt bort träning men går och sover måttligt bygger stugor i stället för långhus och använder ingen Styrka.
Världen växer till lika många bybor, 19, men dagsproduktionen blir 156 mot 204–246 med träning, eftersom sågverk, brygga och väveri kräver Styrka.
Träning syns alltså i världen, men befolkningen står aldrig still på grund av en vana som spelaren valt bort.
Värdet per använd kraft blir högre, 1,10, eftersom stugorna inte räknas som kraftanvändning; måttet jämför bara strategier som använder alla krafter.

### 7.1 Balansbotarna i simuleringen

Balansbotarna (TASK-048) spelar samma sju dygn i spelets riktiga simulering, med samma kommandon och tidsavläsningar som klienten.
Egenvården registreras som dagliga summor vid varje gryning enligt profilerna i [epok 1](./epoch-1.md#profilerna), där *måttlig* heter *normal*.
Strategierna har inga fasta koordinater: de läser kartan och väljer byggplats efter vad rutan ger, avståndet till förrådet och vad som redan står där, så att samma strategier spelar genererade kartor.
Från regelversion 8 spelar de med bärarna, och dagsproduktionen räknar med den andel som bärarna hinner få fram.
Hur botarna körs står i [development.md](../development.md#balansbotar).

| Körning | Egenvård | Bybor | Byggnader | Öar | Sten i lager | Dagsproduktion | Utvecklingsvärde | Använt U/S/O | Sparat U/S/O | Värde per använd kraft |
|---|---|---|---|---|---|---|---|---|---|---|
| Närområdet | måttlig | 19 | 11 | 1 | 207 | 204 | 1 304 | 180/260/410 | 461/110/233 | 1,05 |
| Närområdet, ordnad | måttlig | 19 | 12 | 1 | 109 | 228 | 1 299 | 180/260/380 | 461/110/274 | 1,09 |
| Havet | måttlig | 21 | 12 | 2 | 29 | 246 | 1 256 | 250/210/340 | 426/160/270 | 1,06 |
| Naiv | hög | 20 | 10 | 1 | 53 | 211 | 1 177 | 180/360/630 | 520/85/96 | 0,66 |
| Utan träning | utan träning | 19 | 11 | 1 | 150 | 156 | 1 138 | 180/0/510 | 461/100/174 | 1,06 |
| Ingen spelare | ingen | 16 | 4 | 1 | 20 | 48 | 583 | 0/0/0 | 100/100/310 | – |
| Ingen spelare | måttlig | 16 | 4 | 1 | 20 | 48 | 583 | 0/0/0 | 551/370/492 | – |

Botarnas vecka ska stämma med kalkylens tabell ovan inom ungefär ett besluts skillnad, och testerna underkänner annars bygget:

| Mått | Tolerans |
|---|---|
| Bybor och byggnader | ±1 |
| Öar | exakt |
| Sten i lager | ±10 |
| Dagsproduktion | ±10 %, minst 24: sågverkets uppgradering |
| Utvecklingsvärde | ±5 %, minst 72: tre dygn av den |
| Använd och sparad kraft | ±60 per kraft, ett långhus Styrka |
| Värde per använd kraft | ±0,07, minst utvecklingsvärdets tolerans per använd kraft |

Testerna kräver också att botarna spelar exakt tabellen här, så att en ändring av balansen syns i dokumentet.
Kalkylen kartlägger båda områdena den första morgonen och ställer byggnaderna där botarna gör, så byborna, byggnaderna, öarna och lagren blir nästan desamma.
Skillnaderna som är kvar kommer av ordningen i kön:

- **Kvartarna.**
  Simuleringen räknar varje kvart och kalkylen varje timme, så botarna hinner ett bygge något tidigare.
  Havet hinner därför en bybo till.
- **Styrkan och Orken.**
  Med en annan takt i timmer, plankor och sten hinner ett annat mål först: utan träning hinner botarna arbeta extra en gång till, och den naiva strategin en bybo till.

Slutsatserna ovan håller.
De genomtänkta strategierna får 1,6 gånger den naiva strategins värde per använd kraft, och Närområdet och Havet skiljer sig med 4 % i utvecklingsvärde.

På 20 genererade kartor (`procedural-archipelago` version 1, seed 0–19) genomför båda strategierna sina planer med måttlig egenvård, Närområdet på 9–10 dygn och Havet på 7–10.
Där expeditionens ö saknar betesmark eller berg bygger Havet fårgården eller stenbrottet på hemön.
Efter fyra veckor har Havet högre utvecklingsvärde än Närområdet på 12 av kartorna, 2 164 mot 2 149 i snitt, men båda byarna står då sedan länge vid bostadstaket, eftersom planerna inte bygger fler bostäder än Närområdets tre långhus respektive Havets stuga och långhus.

## 8. Sju dygn utan egenvård

Utan spelare och utan egenvård (sista raderna i tabellen ovan):

- Byborna växer från 12 till 16 av egen kraft och stannar vid bostadstaket.
- Grundtakten (fisk, säd, timmer) fortsätter; långsamma processer fortgår.
- **Inget förfaller.**
  Kalkylen kontrollerar varje timme att antalet bybor, byggnader, nivåer och upptäckta rutor aldrig minskar och att inga lager eller saldon blir negativa (INV-V1, INV-R1, INV-F1).
- Orken växer till 310 på grundnivån, eftersom saknad sömndata ger 40 per dag.
- Med egenvård men utan beslut händer exakt samma sak i världen; krafterna sparas bara.
  Det är spegeln i praktiken: egenvården ger möjligheter, besluten gör dem till en värld (D-14).

## 9. Lärdomar inför prototypen

1. **Utforskning hopar sig.**
   Alla strategier slutar veckan med över 400 Utforskning, alltså över det mjuka taket.
   V1 behöver en löpande användning, till exempel att lyfta dimman ruta för ruta (MAP-04) och fler expeditionsmål.
2. **Orken har för få användningar.**
   Två handlingar räcker inte för ett verkligt morgonbeslut varje dag; fler morgondrag behövs, till exempel att flytta bybor mellan arbetsgrupper eller prioritera transporter.
   Att flytta bybor blev i stället planering utan kraft (D-23, avsnitt 4.1).
3. **Ved är grundflaskhalsen.**
   Utan minimilager stannar allt bygge, därför är LOG-06 flyttat från P1 till P0 (D-44).
4. **Tillväxttakten styr första veckan, inte bostäderna.**
   Alla strategier med beslut når 19–20 bybor, eftersom tillväxten är högst ett par bybor per dygn.
   Valet mellan långhus och stuga (D-43) märks först när bostadstaket nås, vilket prövas med längre körningar i M4.
5. **Byggplatser på rutor gör placeringen till ett beslut (TASK-037).**
   Skogshuggaren vid skogskanten, förrådet mitt i ett kluster och bryggan på rätt sida av Nyön är val som den naiva strategin missar.
   Skillnaden mellan genomtänkt och naiv strategi växer från 1,4 till 1,5 gånger, men bara för att startön har två berg att bryta sten i; med stenådern som enda berg blir den 1,2 (D-95).
   Startbyns skog ger dubbelt så mycket timmer som förut, eftersom den verkligen har två skogsrutor; ved är ändå flaskhalsen för sågverket.
   Prototypens karta har bara två åkerrutor; generatorn (TASK-031) bör ge varje startö mer odlingsmark, så att båda matkällorna går att bygga ut.

## 10. Beslut

Produktägarens svar på de öppna frågorna i v0.1:

- **D-43:** långhus kräver Styrka, men stugan ger bostad för bara material, så tillväxten blockeras aldrig av en bortvald vana.
- **D-44:** LOG-06 (minimilager och prioriteringar) flyttas från P1 till P0.
- **D-45:** startkrafterna från den guidade turen är 100 Utforskning, 100 Styrka och 40 Ork.

Senare beslut som berör ekonomin:

- **D-65** (från [epok 1](./epoch-1.md)): brygga och väveri kan byggas med handkraft utan Styrka på 7 dagar, så att den som valt bort träning också får sjövägar och tyg.
  Körningarna ovan räknar utan handkraft.
- **D-79** (från [ADR-0010](../adr/0010-world-map-format-and-generator-version.md)): varje landruta är en möjlig byggplats; avsnitt 3.1 beskriver hur.

Produktägarens svar på de öppna frågorna i v1.1 (TASK-037):

- **D-88:** fiskeboden står på pålar i det grunda vattnet, som startbyns fiskebod redan gör, så att ingen byggnad behöver flytta.
- **D-89:** ett hamnläge är varje landruta utom berg vid grunt vatten som når djuphavet, eftersom Nyön bara har en strandruta.
- **D-90:** skogshuggaren får stå bredvid skogen, så att närheten till förrådet blir ett verkligt val.
- **D-91:** långhus, förråd och andra byggnader får stå på varje landruta utom berg, även skog, åker och betesmark, som då inte längre går att hugga, odla eller beta.
- **D-92:** toppen är startöns enda högsta ruta och bär bara Vårdkasen, även när det är ett berg; en karta måste ha en enda sådan ruta, och på prototypens karta är det västra berget högst.
- **D-93:** skogskravet räknar bara upptäckta skogsrutor som ingen annan byggnad står på, så att byggvyn aldrig avslöjar dimman och ingen skogshuggare byggs där den inte ger något.

- **D-95:** prototypens karta får ett vanligt berg på (-3,-1) intill toppen, och kartvalideringen kräver minst två berg att bryta sten i utom toppen, så att stenådern blir ett val och kravet i D-34 nås ([map-validation.md](./map-validation.md)).

Produktägarens svar på avvikelsen som [grind A0](./gate-a0.md#3-avvikelser-mellan-dokument-och-kalkyl) hittade (TASK-038):

- **D-103:** avstånd, nivå 2 och extraarbete skalar hur fort åkern och fårgården växer, som avsnitt 3 och 5 redan sa; tidigare skalade de bara hur mycket som kunde skördas i timmen, ett tak som aldrig nåddes.

Produktägarens svar på besluten om innehåll, bosättningar och transport i M4 (TASK-039–041):

- **D-105:** innehållet får Anno-bredd: befolkningsnivåer med egna behov och fler än 20 byggnader redan i epok 1.
  Nivåerna och behoven låses upp gradvis under epoken, till exempel när en bosättning når en viss storlek, så att första veckan är lika lätt som i dag och den guidade turen förblir kort.
- **D-106:** en bosättning per ö.
  Det första förrådet eller den första bryggan på en ö grundar öns bosättning, med egna bybor, bostäder och lager.
  Byborna bemannar bara byggnader på sin ö, äter ur sin ös lager och flyttar över en sjöväg till en bosättning med lediga bostäder, högst en per dag.
- **D-107:** transporten på land sker med bärare med kapacitet i stället för bara avståndsklasser.
  Varje förråd har bärare som flyttar en begränsad mängd per timme, och vägar gör dem snabbare.
  Flaskhalsdiagnostiken ska alltid visa varför en vara inte når fram och vad en väg, ett förråd eller fler bärare skulle ge.

Nivåer, behov, byggnader, kedjor, upplåsning och bärarnas regler räknas fram i ekonomi v2 (TASK-058), som ersätter avsnitt 2–6 när den är godkänd.

Produktägarens svar på vad rivning ger tillbaka (TASK-059):

- **D-108:** en riven byggnad ger tillbaka hälften av materialet den kostade, avrundat nedåt per resurs, till öns lager; kraft återbetalas aldrig (INV-F5).
  Återbetalningen måste rymmas i öns lager (INV-R1), och bekräftelsen visar exakt vad som kommer tillbaka.
  Att flytta en byggnad genom att riva och bygga om kostar alltså halva materialet och hela kraften; balansbotarna och scenarierna prövar att det inte blir en dominerande strategi.
