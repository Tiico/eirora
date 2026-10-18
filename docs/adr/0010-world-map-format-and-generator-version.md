# Kartan är en oföränderlig hexskiva som sparas hel; generatorn körs en gång per värld och hålls fast vid sin version

> **Beslutat 2026-10-18** av produktägaren i [TASK-027](https://github.com/Tiico/eirora/issues/61): varje landbricka är en byggplats (D-79), kartan har radie 24 (D-80) och prototypens värld behålls med havet runt den fyllt en gång (D-81).

En världs karta (`WorldMap`) är en hexskiva med radie 24 runt origo, 1 801 brickor, där varje bricka har terräng, höjd och rikedom.
Kartan skapas en gång när världen skapas, av en generator med en fast version, och ändras aldrig därefter: allt som förändras på kartan under spelet, som dimma, avverkad skog, vägar och byggnader, är världens tillstånd och ligger i snapshoten.
Kartan sparas hel i sparningen, aldrig bara som seed (MAP-06), och snapshoten pekar ut den med en hash, så en ny generatorversion kan aldrig ändra en befintlig värld.
Generatorerna är rena heltalsfunktioner med en egen, specificerad slumpgenerator, så samma seed och generatorversion ger samma byte på .NET, Mono och IL2CPP (NFR-02, INV-D3).
Vi valde en oföränderlig karta framför en karta som är en del av det föränderliga tillståndet därför att generatorns utdata då kan kontrolleras mot gyllene kartor en gång för alla, sparas en gång per värld och delas av klient och server utan att varje snapshot bär den.

## Vad kartan innehåller

Kartformat 1 har följande innehåll.

**Huvud.**
- `eirora-map 1`: magi och kartformatsversion.
- `radius 24`: kartans storlek.
- `source procedural|handmade|geographic`: källtypen (`sourceType`, D-05, MAP-10).
- `generator <id> <version>` och `seed <16 hex>` för genererade kartor.
- `provenance <dataset> <version> <licens>` och utsnittet i heltal (mikrograder) för geografiska kartor.
- `start <q> <r>`: brickan där världen grundas.

**Brickor.**
- En rad per bricka inom radien, i ordning efter `r` och sedan `q`.
- Varje rad har axiala koordinater, terräng, höjd 0–7 och rikedom i procent (100 är normalt).
- Terrängen är en av `DeepSea`, `ShallowSea`, `Beach`, `Grassland`, `Forest`, `Farmland`, `Pasture` och `Mountain`.
- Höjden är de tydliga höjdsteg som art direction bygger på (D-61); snö på topparna är utseende och följer av höjden.
- Rikedomen gäller det brickan ger, och det följer av terrängen: fisk i grunt vatten, sten i berg och bete på betesmark.

**Öar.**
- `island <id> <q> <r>`: ett stabilt id och en ankarbricka.
- Ön är den sammanhängande landmassa som innehåller ankaret.
- Varje landbricka hör till exakt en ö.
- Namnen på öarna är innehåll, inte karta.

Det som följer av brickorna sparas inte, eftersom en snapshot aldrig innehåller härledda cachar (ADR-0006):
- gångbarhet och byggbarhet följer av terräng, höjd och grannar enligt reglerna;
- fiskevatten är grunt vatten intill land;
- ett hamnläge är en landbricka utom berg intill grunt vatten som når djupt hav (D-83);
- farleder och flaskhalsar följer av havsbrickornas sammanhang;
- krönet är startöns högsta bricka.

Biom per bricka ingår inte i kartformat 1, eftersom V1 har ett enda nordiskt biom.
Ett biom läggs till med kartformat 2 och en migration som ger alla befintliga brickor det nordiska biomet.

### Byggplatser är brickor

Varje landbricka är en möjlig byggplats, och vad den duger till följer av dess terräng, höjd och grannar (D-79).
Dagens 20 namngivna platser (`Site`, `SiteKind`) försvinner som begrepp: tillståndet refererar till brickor med koordinater i stället för plats-id, och de dolda platserna ersätts av dimman.
Avverkad skog, vägar, byggnader och lyft dimma är tillstånd per bricka i snapshoten.

## Versioner

Fyra versioner hålls isär.

| Version | Gäller | Ändras när |
|---|---|---|
| Formatversion | Sparningens behållare (ADR-0006, TASK-020) | Huvud, sektioner, kodning eller komprimering ändras. |
| Regelversion | Tillståndets form och reglerna | En regel eller tillståndets form ändras (ADR-0006). |
| Kartformatsversion | Kartans textform | Kartans representation ändras, till exempel ett nytt fält. |
| Generatorversion | En generators utdata | Generatorn ger andra kartor för samma seed. |

- En kartformatsmigration är en ren funktion från kartans text i version n till n+1, som tillståndsmigrationerna i ADR-0006.
  Den får bara ändra representationen.
  Ändrar en uppdatering vad en befintlig bricka betyder för reglerna är det en regeländring, med regelversion och migration.
- Generatorversionen är härkomst, inte något som körs igen: världen laddas från sin sparade karta, och generatorn för dess version behövs aldrig mer för den världen.
  Den behålls för att reproducera och felsöka kartor och för de gyllene kartorna i CI.
- En app öppnar aldrig en karta med högre kartformatsversion än den känner till, och skriver den aldrig över (ADR-0006, punkt 5).

### Kartan i sparningen

- Sparningen får en sektion `map` med kartans text (TASK-020).
- Varje snapshot får raden `map-sha256 <hex>` i huvudet, och kartans hash ingår i fingeravtrycket.
- En snapshot kan därmed inte återställas mot fel karta.
- Kartan lagras en gång per sparning och en gång per värld på servern, oavsett hur många snapshots som finns.

### Generatorversionen hålls fast

- Varje generatorversion är egen kod, till exempel `ProceduralArchipelagoV1`, som aldrig ändras när versionen är låst.
- En ny generator är en ny klass med nästa version och används bara för nya världar.
- CI har gyllene kartor per låst generatorversion: en lista med seeds och SHA-256 av varje karta.
  Ett test i .NET och samma test i Unity EditMode (Mono) misslyckas om en enda byte ändras; att IL2CPP ger samma byte prövas på samma sätt som simuleringens determinism i TASK-021.
- En generatorversion låses när produktägaren har valt bland seeds i seedverktyget (TASK-031, TASK-034).

## Determinism

- Generatorerna och kartan ligger i delade bibliotek utan Unity-beroende som byggs från samma källkod av .NET och Unity (ADR-0004).
  `WorldMap`, brickor och hexgeometrin ligger i `Eirora.Simulation`, eftersom reglerna läser dem (TASK-028).
  Generatorerna ligger i ett eget bibliotek, `Eirora.WorldGeneration`, som klienten använder när en värld skapas offline (ADR-0001) och servern när kartor valideras i bulk.
  Det omfattas av samma renhetsskanning som simuleringen (MAP-05, INV-D2).
- Slumpen kommer från en egen implementation av PCG32 (XSH-RR, 64 bitars tillstånd) med publicerade testvektorer.
  Den använder bara heltalsaritmetik med uttryckligt `unchecked`.
  Varje steg i generatorn får en egen ström, härledd från världens seed och stegets namn med FNV-1a över UTF-8-byte, aldrig med `string.GetHashCode`.
- Brus och höjdfält räknas i heltal och fixed-point (Q16.16) med heltalsinterpolation, aldrig med flyttal (INV-D3).
  Renhetsskanningen underkänner flyttal, `System.Math` och `System.Random`.
- Ingen generator beror på iterationsordningen i `Dictionary` eller `HashSet`.
  Sorteringar har en fullständig ordning med koordinaterna som sista nyckel, eftersom `Array.Sort` inte är stabil.
- Varför samma seed och generatorversion ger samma karta på klient och server: koden är densamma (ADR-0004), indata är bara seed och version, och varje operation är heltalsaritmetik vars resultat C# specificerar exakt.
  Division med negativa tal, rest, överspill och skift beter sig likadant i .NET, Mono och IL2CPP.
  Testvektorerna för PCG32 och de gyllene kartorna körs i .NET och Unity och visar det; IL2CPP prövas i TASK-021.

## Storlek och prestanda

- Radie 24 ger 1 801 brickor (D-80).
  Det rymmer startön, innerhavet och öarna i epok 1 (en ögrupp med tre öar och minst 150 brickor dimma över innerhavet) och fler epoker längre ut.
  En karta kan aldrig växa i efterhand, eftersom den är oföränderlig.
- Kartans text är ungefär 30 kB (en rad på cirka 16 tecken per bricka).
  Sparningen bär den en gång, så en sparning går från omkring 15 kB till omkring 45 kB.
- I minnet är en bricka några byte, och hela kartan under 10 kB.
  Sökväg med A* över 1 801 brickor är liten.
  Budgetarna för sökväg och rendering sätts i TASK-024 och TASK-028.
- Rendering är högst 1 801 brickor med 12–40 trianglar var (art direction), alltså under 75 000 trianglar för hela kartan.
  Hur den delas upp och döljs bakom dimman avgörs i TASK-033.

## Arkipelagen

O-04 avgörs inte här.
Kartan klarar ändå de krav som varje komposition behöver (MAP-07, TASK-035):

- Kartans koordinater är lokala med origo i mitten.
  Arkipelagen placerar en värld med en förskjutning, och vid behov en rotation i steg om 60°, som är exakt i axiala heltal.
- Alla världar har samma radie, så servern kan behandla dem som lika stora skivor.
- De två yttersta ringarna är djupt hav, så att två världar som ligger kant i kant har hav emellan.
- Varje karta har minst ett hamnläge med en farled av djupt hav ut till kanten, där sjövägar till andra världar kan börja.
- Validatorn (TASK-030) underkänner kartor som bryter mot detta.

## Källtyper

Alla källor ger samma `WorldMap` och går genom samma validator.

- `procedural`: genererad av `ProceduralArchipelagoV1` och senare versioner från en seed (D-04, MAP-01).
- `handmade`: ritad för hand, som prototypens Hemön och Nyön, utan seed.
- `geographic`: härledd från verklig geodata i V2 (D-05, MAP-10).
  Härkomsten anger dataset, version, licens och utsnitt i heltal.
  Geodata importeras inte i V1; TASK-035 lägger bara till kontraktstestet.

## Prototypens värld

Produktägarens värld från M1 lever vidare utan att något den sett eller byggt ändras (D-81).

1. TASK-029 gör Hemön och Nyön till den handgjorda kartan `prototype-home`, version 1, med samma land, vatten och terräng som klienten ritar i dag.
   Det som i dag är kuliss, som skogsbrickor och stränder, blir riktig terräng.
   Ytan utanför det klienten ritar är djupt hav.
2. En regelmigration (ADR-0006) ger den sparade världen kartan.
   Den skriver om tillståndets plats-id till brickornas koordinater, till exempel `building Longhouse S` till `building Longhouse 0 0`.
   Ekonomins fingeravtryck ändras inte i den migrationen.
3. När generator v1 finns (TASK-031) fyller en regelmigration en gång den del av kartan som prototypen aldrig visat med generatorns hav och öar.
   Seeden härleds från världens första snapshot, så att klient och server får samma karta.
   Innerhavet och Ytterön hamnar bakom dimman.
   Generatorn tar därför emot en fast kärna som den bygger runt, och validatorn kontrollerar att kärnan är orörd.
4. Därefter är kartan oföränderlig som alla andras.

Byggande på varje landbricka (D-79) är en senare regeländring med egna siffror i ekonomimodellen.
Den kommer efter TASK-029, så att flytten till kartan och ändringen av reglerna kan prövas var för sig.

## Considered Options

**Kartans plats i sparningen.**
- **En egen sektion med hash i snapshoten (vald).**
  Kartan lagras en gång, snapshoten förblir liten, och hashen gör det omöjligt att återställa mot fel karta.
- **Kartan i varje snapshot.**
  Snapshoten blir självständig, men en sparning bär två till fyra kopior och servern lagrar kartan med varje snapshot.
- **Bara seed och generatorversion.**
  Minst, men en ändrad generator ändrar världen, och det förbjuder MAP-06 och AC-07.

**Föränderlig eller oföränderlig karta.**
- **Oföränderlig karta och förändringar i tillståndet (vald).**
  Generatorns utdata kan kontrolleras mot gyllene kartor, och det som förändras är det reglerna äger.
- **Kartan som en del av tillståndet.**
  Allt på ett ställe, men varje snapshot måste bära kartan, och generatorns utdata går inte att skilja från spelets förändringar.

**Slumpgenerator.**
- **PCG32 (vald).**
  Liten, väl specificerad, publicerade testvektorer och strömmar per steg.
- **xoshiro256\*\*.**
  Snabb och välkänd, men 256 bitars tillstånd och fler varianter att hålla isär.
- **SplitMix64.**
  Enklast, men tänkt för att seeda andra generatorer snarare än som huvudgenerator.

**Var generatorerna ligger.**
- **Eget bibliotek bredvid simuleringen (valt).**
  Frysta generatorversioner växer i ett eget bibliotek, och simuleringen ser bara kartan (MAP-05).
- **I simuleringsbiblioteket.**
  Ett bibliotek färre, men varje låst generator följer med reglerna för alltid och blandar ihop karta och regler.

**Byggplatser (D-79).**
- **Varje landbricka (vald).**
  Placering och närhet blir ett verkligt val på hela kartan och skalar till genererade världar.
- **Namngivna platser som i dag.**
  Minst ändring i ekonomin, men generatorn måste avgöra var det är värt att bygga, och placeringen blir mindre fri.
- **Brickor plus namngivna platser.**
  Mest uttrycksfullt, men två system att hålla i takt.

**Kartans storlek (D-80).**
- **Radie 24 (vald):** plats för flera epoker till cirka 30 kB.
- **Radie 16:** 817 brickor, räcker för epok 1 men kan inte växa senare.
- **Radie 32:** 3 169 brickor, dubbelt så stor sparning och mer att fylla med innehåll.

**Prototypens värld (D-81).**
- **Behålls och havet fylls runt den en gång (vald):** världen kan fortsätta in i epok 1.
- **Handgjord karta för alltid:** enklast, men världen når aldrig Ytterön eller innerhavet.
- **Ny värld med generatorn:** renast, men dagboksvärlden tar slut.

## Consequences

- TASK-028 lägger hexgeometrin i `Eirora.Simulation` och ersätter `Site.DistanceTo` och klientens heltalsdelar av `Hex`.
- TASK-029 bygger kartformat 1 enligt den här ADR:n, med följande delar:
  - sparningens `map`-sektion och `map-sha256`;
  - PCG32 med testvektorer;
  - `prototype-home`;
  - migrationen från plats-id till koordinater;
  - ekonomin läser kartan i stället för `PrototypeIsland`.
- TASK-030 validerar kartans radie, öar, kant av djupt hav och hamnläge mot kanten.
- TASK-031 skriver `ProceduralArchipelagoV1` med en fast kärna för prototypens värld.
- Varje låst generatorversion lever kvar i koden och i CI för alltid, även när inga nya världar skapas med den.
- En ny regel som läser kartan annorlunda kräver regelversion och migration, men aldrig en ny karta.
- Byggande på varje landbricka kräver en egen regeländring med nya siffror i ekonomimodellen.
