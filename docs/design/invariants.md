# Simuleringsinvarianter

> **Status:** [BESLUTAT] v1.0, godkänd av produktägaren vid grind A0 (D-82)  
> **Ärende:** TASK-002 · **Milstolpe:** M0 · **PRD:** NFR-02, CIV-04, CIV-06, RST-*, HLT-04–10, SOC-04, D-08, D-17, D-22, D-35–D-39  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md)

Det här är det som **alltid** är sant i simuleringen av en värld, oavsett innehåll, balans eller regelversion.
Varje invariant är skriven så att den kan bli ett property-based-test i det motoroberoende simuleringsbiblioteket (ADR-0002).
Siffror och innehåll (byggnader, recept, tak) hör hemma i TASK-003 och TASK-004; invarianterna ska hålla för alla rimliga val där.

## Notation

| Symbol | Betydelse |
|---|---|
| `S` | Ett simuleringstillstånd för en värld, **nåbart** från ett giltigt starttillstånd |
| `c` | Ett kommando (`GameCommand`) med idempotensnyckel |
| `apply(S, c)` | Tillämpa ett kommando; ger nytt tillstånd och ett utfall (godkänt eller nekat) |
| `advance(S, Δt)` | Låt simuleringstiden gå `Δt` utan kommandon |
| `run(S, L)` | Tillämpa en tidsstämplad lista `L` av kommandon, bidrag och tidssteg |
| `import(S, R)` | Importera hälsoposter `R` och omvandla dem till bidrag |
| `phase(S)` | Dygnsfas: `ACTIVE`, `RESTING` eller `DAWN` |
| `==` | Lika vid jämförelse av serialiserat tillstånd, byte för byte |

**Generatorer:** testerna skapar tillstånd genom slumpade kommandosekvenser från giltiga starttillstånd, aldrig genom att konstruera tillstånd direkt.
Tid slumpas över tidszoner, sommartidsövergångar och frånvaro upp till ett år.
Hälsoposter slumpas med dubbletter, överlapp mellan källor, sen ankomst och korrigeringar.

## 1. Determinism och kommandon

**INV-D1 Determinism.**
Samma starttillstånd, regelversion och indata ger alltid samma tillstånd.
*Test:* `run(S, L) == run(S, L)`, även i separata processer och, från M2, på klient och server (NFR-02).

**INV-D2 Ren kärna.**
Simuleringen läser aldrig väggklocka, operativsystemets slump, filsystem, nätverk eller Unity.
Tid, seeds och data kommer alltid in som indata.
*Test:* arkitekturtest som underkänner referenser till förbjudna API:er i simuleringsbiblioteket.

**INV-D3 Inga flyttal i tillståndet.**
Alla mängder i tillståndet är heltal eller fixed-point (PRD 7.3).
*Test:* schematest över alla serialiserade typer.

**INV-D4 Tiden går att dela upp.**
Att spola fram i ett steg ger samma resultat som att spola fram i många små.
*Test:* `advance(advance(S, a), b) == advance(S, a + b)` för alla `a, b ≥ 0`.
Det är den här egenskapen som gör att en värld kan simuleras ikapp efter sju dagars frånvaro (AC-02).

**INV-D5 Snapshot och replay.**
Ett tillstånd som återskapas från en snapshot plus efterföljande händelser är identiskt med det ursprungliga.
*Test:* för varje uppdelning `L = L1 ++ L2` gäller `run(restore(snapshot(run(S, L1))), L2) == run(S, L)`.

**INV-D6 Idempotens.**
Ett kommando med en redan använd idempotensnyckel ändrar ingenting.
*Test:* `apply(apply(S, c), c) == apply(S, c)`.

**INV-D7 Nekade kommandon saknar sidoeffekter.**
Ett nekat kommando lämnar tillståndet oförändrat och anger en förutsägbar orsak (AC-01).
*Test:* om `apply(S, c)` nekas gäller `state(apply(S, c)) == S`.

**INV-D8 Monoton simuleringstid.**
Simuleringstiden minskar aldrig.
Ett kommando tillämpas vid simuleringens aktuella tid.
*Test:* `time(run(S, L)) ≥ time(S)` för alla `L`.

## 2. Resurser

**INV-R1 Gränser.**
Varje lagrad mängd ligger mellan 0 och lagrets kapacitet.
*Test:* kontrolleras efter varje steg i varje genererad sekvens.

**INV-R2 Bevarande.**
Resurser skapas och förbrukas bara av definierade källor: recept, byggkostnader, basbehov, fynd och handel.
Transport inom världen flyttar resurser men ändrar inte summan.
*Test:* för varje resurs är förändringen i totalt lager lika med summan av händelsernas deltan, och transporthändelser summerar till noll.

**INV-R3 Inget förfall.**
Lagrade varor försvinner aldrig bara för att tiden går (D-17).
*Test:* under `advance` minskar ett lager endast genom förbrukning i recept eller basbehov, aldrig genom en tidsberoende förlust.

**INV-R4 Brister går alltid att återhämta.**
Varje resurs som behövs för basbehov kan produceras utan krafter, på minst två sätt eller av en källa som inte kan blockeras (PRD 6.2).
*Test:* innehållsvalidator i CI över all spelinnehållsdata, plus property: från varje nåbart tillstånd finns gratis planeringskommandon som ger positiv nettoproduktion av varje basbehov.

## 3. Bybor och världens beständighet

**INV-V1 Inget går förlorat med tiden.**
När tiden går minskar aldrig antalet bybor, byggnader, byggnadsnivåer, bosättningar, utforskade rutor, avslutad forskning, epokframsteg eller historik (D-08, D-17).
Bara ett uttryckligt rivningskommando från spelaren kan minska något av dem (D-38).
*Test:* för varje sådant attribut `M` gäller `M(advance(S, Δt)) ≥ M(S)`.

**INV-V2 Arbetsfördelning.**
Summan av bybor i arbetsgrupper är högst antalet bybor, och ingen arbetsgrupp har ett negativt antal.
Från TASK-050 gäller det i varje bosättning, eftersom byborna bara bemannar byggnader på sin egen ö (D-106).
*Test:* kontrolleras efter varje steg.

**INV-V3 Ouppfyllda basbehov kostar bara tillväxt.**
När ett basbehov inte uppfylls kan tillväxt stanna, men antalet bybor, byggnadsnivåer och grundproduktion minskar aldrig (D-39).
*Test:* följer av INV-V1 med generatorer som tömmer lagren av basbehov.

**INV-V4 Frånvaro är ofarlig.**
Hur länge spelaren än är borta utan egenvård och kommandon är världen efteråt minst lika utvecklad som före.
*Test:* sammansatt test av INV-R1, INV-R3 och INV-V1 för `Δt` upp till ett år med noll bidrag (AC-02).
Från TASK-049 också i utbyggda världar med byggkö, expedition till sjöss, sjöväg, fulla lager och långsamma processer, med och utan egenvård och med sena bidrag, tillsammans med INV-T5 och INV-D4.
Från TASK-044 också med pausade byggnader och spelarens bemanningsordning och byggkö: under frånvaron förblir det pausade pausat, ordningarna står kvar och aldrig färre bybor arbetar.
Från TASK-045 också i världar på en genererad karta med lager på tre öar och två sjövägar.
Från TASK-051 också med bärare, varor som väntar i byggnadernas utlager och råvara framme vid förädlingen, vägar och vägar i kön: bärarna flyttar bara varor mellan byggnaderna och lagren, och inget flyttas under vilan.
Från TASK-050 också med en bosättning per ö, byggnader som väntar på bybor som flyttar, byggmaterial som sjövägen ännu ska bära och varor från regelversion 7 som väntar vid förrådet på plats i lagret.

Framspolningen hoppar över kvartar där bara långsamma processer rör sig: vilotiden, och kvartarna efter en vaken kvart vars arbete inte ändrade något, fram till nästa gryning.
Det håller bara om inget tillstånd som kvartarna driver kan börja ändras utan att något av det som jämförs ändras först.
Den som lägger till sådant tillstånd, till exempel skepp med last på väg, reservationer, köer per bosättning eller vägnät (TASK-045, TASK-046, TASK-050, TASK-051), tar med det i `Settlement.RecordState` och låter `BuiltOutWorld` nå det.
Testet `FastForwardTests.EveryFieldThatATickCanChangeIsCompared` underkänner ett nytt fält som varken jämförs eller är fast eller härlett, och `FastForwardGivesTheSameWorldAsEveryQuarterHour` jämför med att läsa varje kvart.

## 4. Krafter

**INV-F1 Icke-negativa heltal.**
Varje krafts saldo är ett heltal ≥ 0.
*Test:* kontrolleras efter varje steg i varje genererad sekvens, och schematest över saldots typ.

**INV-F2 Endast egenvård skapar krafter, och bara sin egen.**
Steg ger bara Utforskning, träningspass bara Styrka och sömnlängd bara Ork (D-15).
Inget kommando växlar krafter mot varandra eller mot resurser, och ingenting kan köpas för pengar (MON-04).
*Test:* den enda händelsetyp som ökar ett kraftsaldo är omvandling av ett bidrag av motsvarande vana.

**INV-F3 Mer egenvård hjälper alltid, men allt mindre.**
För omvandlingen `f` av en vanas bidrag under en dag gäller att `f` är växande, konkav och begränsad (HLT-05).
Konkaviteten gäller den exakta kurvan före avrundning till hela krafter, se [krafternas regler](./forces.md).
*Test:* `f(x + d) ≥ f(x)`, `f(x + 2d) − f(x + d) ≤ f(x + d) − f(x)` och `f(x) ≤ taket` för alla `x, d ≥ 0`.

**INV-F4 Orken har ett golv.**
Ingen inloggad sömnlängd ger mindre Ork än saknad data, som ger neutral grundnivå (D-16, HLT-06).
*Test:* `f_sömn(x) ≥ f_sömn(saknas)` för alla `x ≥ 0`.

**INV-F5 Spendering är exakt.**
Ett kommando som kostar kraft godkänns bara om saldot räcker, och minskar då saldot med exakt kostnaden.
*Test:* `saldo(apply(S, c)) == saldo(S) − kostnad(c)` om godkänt; annars gäller INV-D7.

**INV-F6 Krafter förfaller inte.**
Ett kraftsaldo minskar aldrig bara för att tiden går.
Oanvända krafter sparas med ett mjukt tak som bara begränsar nya tillskott (D-35); taken står i [krafternas regler](./forces.md#5-sparande-d-35).
*Test:* `saldo(advance(S, Δt)) ≥ saldo(S)`.

**INV-F7 Manuell inmatning är fullvärdig i den egna världen.**
Ett egenrapporterat bidrag ger exakt samma kraft i den egna världen som ett verifierat bidrag av samma storlek (HLT-07, D-18).
*Test:* `f(verifierat(x)) == f(egenrapporterat(x))`.
Klassningen påverkar bara gruppbidrag, se INV-G3.

**INV-F8 Uppdelningen spelar ingen roll.**
Samma bidrag ger samma kraftsaldo oavsett i hur många delar och i vilken ordning de krediteras, så länge inga kommandon kommer emellan.
*Test:* för varje uppdelning av en dags bidrag i delmängder ger kreditering i följd samma saldo som kreditering i ett steg, även när sparandets tak nås.

## 5. Bidrag

**INV-B1 Högst en gång per intervall.**
Att importera samma hälsodata flera gånger, i valfri ordning och från överlappande källor, ger samma bidrag som en enda import (HLT-04, AC-03).
Det gäller även kraftsaldot när sparandets tak nås, eftersom taket räknas på kraftens läge (D-69).
*Test:* `import(S, R) == import(S, perm(R ++ R'))` där `R'` är dubbletter av poster i `R` och `perm` en godtycklig ordning.

**INV-B2 Varje bidrag är entydigt.**
Ett bidrag har exakt en vana, ett tidsintervall och en klassning: verifierat eller egenrapporterat (HLT-08).
*Test:* schematest över bidragstypen, plus property: varje importerat bidrag har exakt en vana, ett icke-tomt intervall och en klassning.

**INV-B3 Korrigeringar är kompensationshändelser.**
Sen eller ändrad källdata ger kompensationshändelser, så att summan av bidrag konvergerar till den korrigerade källan.
En korrigering nedåt klipps vid noll: den gör aldrig ett kraftsaldo negativt och tar aldrig tillbaka något som redan byggts eller upptäckts (D-36).
Den tar tillbaka kraft i samma takt som sparandets tak gav den (D-69).
Har kraften spenderats sedan bidraget krediterades tar den aldrig mer än bidraget faktiskt gav (D-62, D-74).
*Test:* efter varje sekvens av importer och korrigeringar är bidragssumman lika med källans slutliga värde, och INV-F1 och INV-V1 håller.

**INV-B4 Ingen rådata i tillståndet.**
Simuleringstillstånd, snapshots och händelser innehåller bara aggregerade bidrag, aldrig enskilda hälsoposter (HLT-10).
*Test:* schematest som underkänner rådatatyper i allt som serialiseras.

## 6. Tid, vilotid och långsamma processer

**INV-T1 Fasen bestäms av schema och tid, aldrig av sömn.**
`phase(S)` är en funktion av gällande viloschema, simuleringstid och tidszon, inget annat.
Upplåsning kräver aldrig bevisad sömn (RST-10).
*Test:* två tillstånd med samma schema och tid men olika bidrag har samma fas.

**INV-T2 Tillåtna övergångar.**
Fasen går bara `ACTIVE → RESTING → DAWN → ACTIVE`.
`DAWN` är ögonblicket då vilan slutar och nästa dag börjar; simuleringen rapporterar det som en händelse ([ADR-0005](../adr/0005-time-arrives-as-readings.md)).
*Test:* kontrolleras över varje genererad tidslinje: skymning och gryning växlar, och varje gryning ökar dagen med ett.

**INV-T3 Vilolåset.**
Under `RESTING` nekas varje spelpåverkande kommando enligt INV-D7.
Administrativa kommandon (konto, integritet, tillgänglighet, hjälp, radering, viloschema) tillåts (RST-07).
*Test:* för varje spelpåverkande kommandotyp `c` och varje tillstånd med `phase(S) = RESTING` nekas `apply(S, c)`.

**INV-T4 Byborna arbetar inte under vilan.**
Under `RESTING` sker ingen produktion, konsumtion eller transport som drivs av bybor (D-22, RST-04).
*Test:* under `advance` över en vilotid kommer alla förändringar från långsamma processer eller bidragsimport.

**INV-T5 Långsamma processer bryr sig inte om vilan.**
En långsam process gör lika stora framsteg under `Δt` oavsett fas (CIV-06).
*Test:* framsteg över samma `Δt` är identiskt för två tillstånd som bara skiljer sig i viloschema.

**INV-T6 Vilotidens längd.**
Ett viloschema har alltid en längd om 6–10 timmar i lokal tid (D-22, D-37).
Vid sommartidsövergång kan den verkliga längden därför bli 5–11 timmar.
*Test:* varje kommando som sätter en längd utanför intervallet nekas, och över genererade sommartidsövergångar slutar vilan alltid på schemats lokala klockslag.

**INV-T7 Schemaändringar väntar 24 timmar.**
En ändring av viloschemat påverkar inte fasen förrän tidigast 24 timmar senare, och en pågående vilotid kan aldrig kortas (RST-09, AC-05).
*Test:* efter en ändring vid tid `t` är fasen för varje `t' < t + 24 h` densamma som med det gamla schemat.

**INV-T8 Tidszonsbyte rör inte pågående vila.**
Ett byte av tidszon eller en sommartidsövergång ändrar varken start eller slut för en vilotid som redan pågår (AC-05).
*Test:* fasen över en pågående vilotid är identisk med och utan tidszonsbyte.

**INV-T9 Krafter från natten finns vid gryningen.**
Bidrag som importeras under vilan krediteras senast vid `DAWN` (RST-05).
*Test:* för varje bidrag som importeras under `RESTING` är kraftsaldot vid övergången till `DAWN` minst lika med saldot före plus bidragets kraft.

## 7. Grupper (från M8)

Gäller de delar av simuleringen som servern kör för arkipelagen; tas med här så att datamodellen inte stänger dörren.

**INV-G1 Endast positiv koppling.**
En medlems utfall är aldrig sämre för att en annan medlem är inaktiv än om den medlemmen inte funnits (SOC-04, AC-12).
*Test:* utfallet för medlem A är identiskt med inaktiv medlem B och utan medlem B.

**INV-G2 Mer bidrag från andra hjälper aldrig mindre.**
En medlems utfall är växande i de andra medlemmarnas bidrag.
*Test:* om en annan medlems bidrag ökar från `x` till `x + d` blir medlem A:s utfall aldrig sämre.

**INV-G3 Tak för egenrapporterade gruppbidrag.**
Det dagliga taket för egenrapporterade gruppbidrag är lägre än för verifierade (D-18, SOC-07).
*Test:* för varje vana och dag ger egenrapporterade gruppbidrag aldrig mer än sitt tak, och taket är strikt lägre än det verifierade.

## Beslut

Produktägarens svar på de öppna frågorna i v0.1:

- **D-37:** vilotidens 6–10 timmar räknas i lokal tid (INV-T6).
- **D-38:** rivning är ett uttryckligt kommando och det enda undantaget från INV-V1.
- **D-39:** ouppfyllda basbehov stoppar bara tillväxt (INV-V3); siffrorna sätts i TASK-004.
