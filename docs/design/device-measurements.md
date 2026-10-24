# Mätningar på enhet

> **Status:** [FÖRSLAG] v0.1, metoden är klar, mätningarna på produktägarens telefon återstår  
> **Ärende:** TASK-015 · **Milstolpe:** M1 · **PRD:** NFR-03, NFR-13, O-13, D-70  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md)

Det här dokumentet beskriver hur prototypen mäts på produktägarens telefon och samlar resultaten.
Mätningarna är underlag för målenheter och mätbudgetar i M2 (TASK-024, O-13).
Inga mätningar innehåller hälsodata: mätscenen spelar en påhittad vecka och läser aldrig hälsokällan.

## 1. Vad som mäts

| Mått | Hur | Enhet |
|---|---|---|
| APK-storlek | Filens storlek | MB |
| Kallstart | `am start -W -S`, median av fem starter; tiden till Androids första ritade bild | ms |
| Första bilden av världen | Från aktivitetens start till klientens första bild med världen, ur loggens tidsstämplar, median av fem starter | ms |
| Framspolning | Hur länge världen spolades fram till klockan innan första bilden, som klienten loggar | ms |
| Bildfrekvens och bildtid | Presentationstider för appens yta ur `dumpsys SurfaceFlinger --latency`, läst var 2:a sekund | fps, ms (median, 95:e percentil, max) |
| Hackighet | Andel bilder som tar mer än 50 ms | % |
| Minne | `TOTAL PSS` ur `dumpsys meminfo`, var 10:e sekund | MB (medel, max) |
| Uppvärmning | Batteriets och skalets temperatur före och efter passet, ur `dumpsys battery` och `dumpsys thermalservice` | °C |
| Batteri vid aktivt spel | Appens beräknade förbrukning ur `dumpsys batterystats` under passet | mAh och mAh per timme |
| Batteri under ett dygn | Appens andel av telefonens förbrukning under ungefär 24 timmar med vanlig användning | mAh och % |

Unity ritar i en egen yta som `dumpsys gfxinfo` inte ser, därför används SurfaceFlinger.
Kallstarten mäts till Androids första bild, som kommer innan Unity har laddat scenen.
Därför mäts också tiden till klientens första bild med världen, som innehåller Unitys laddning och framspolningen av världen till klockan.
Klienten loggar raden `Eirora measure: first frame` när den har ritat den bilden, och skriptet läser tiden ur loggens tidsstämplar.
Skalets temperatur saknas på en del telefoner och på emulatorn; då redovisas bara batteriets.

## 2. Mätscenen

Spelet är olika från dygn till dygn, så en mätning av den riktiga världen går inte att upprepa.
Klienten har därför en mätscen som startas med argumentet `-eirora-measure day` eller `-eirora-measure night`.
Den spelar samma vecka som skärmbildsturen på en fast klocka, utan att spara, och stannar sedan i dagsljus klockan 13 eller i vilotidens natt klockan 01.
Natten mäts för sig eftersom stjärnor, norrsken och upplysta fönster enligt [art direction](./art-direction.md) kostar annat än dagsljuset.
Med `-eirora-measure year` laddar scenen i stället en utbyggd värld med 36 byggnader och en expedition till sjöss och stannar klockan ett år efter att spelaren lämnade den, vid samma tid på dagen, så att världen spolas fram ett år när appen startar, som när spelaren kommer tillbaka efter ett års frånvaro (TASK-049).
Världen är en sparning i `client/Assets/Eirora/Resources/Measurement/AYearAway.bytes`, som .NET-testerna spolar fram på samma sätt.
Scenen körs tills den stoppas och rör inte spelarens sparade värld.

## 3. Så körs mätningen

Skriptet [`tools/device-measurements/measure.sh`](../../tools/device-measurements/measure.sh) kör allt över `adb` och fungerar i Git Bash, macOS och Linux.
Telefonen ska ha USB-felsökning påslagen, skärmen på och ljusstyrkan fast, eftersom skärmen dominerar batteriet.

Bygg APK:n (se [utvecklingsmiljön](../development.md)) och mät ett aktivt pass på fem minuter i dagsljus och i natt:

```bash
tools/device-measurements/measure.sh active --apk client/Builds/Android/Eirora.apk --light day --out measurements/day-1
```

```bash
tools/device-measurements/measure.sh active --light night --out measurements/night-1
```

Under passet räknar skriptet telefonen som urkopplad (`dumpsys battery unplug`), så att batteristatistiken räknas även med kabeln i; det återställs efteråt.

### Kallstart efter ett års frånvaro

Kommandot `cold-start` mäter bara kallstarterna, fem som standard, utan passet efteråt.
Mät kallstarten efter ett år och en vanlig kallstart i dagsljus med samma bygge, så att skillnaden visar vad framspolningen kostar:

```bash
tools/device-measurements/measure.sh cold-start --apk client/Builds/Android/Eirora.apk --light year --out measurements/year-1
```

```bash
tools/device-measurements/measure.sh cold-start --light day --out measurements/day-cold-1
```

Varje start skrivs till `cold-start-ms.txt` och `first-frame-ms.txt`, och `results.tsv` får medianerna `cold_start_median_ms`, `first_frame_median_ms` och `catch_up_median_ms`.
Pröva resultatet mot telefonens nivå, till exempel referensnivån för en Pixel 7:

```bash
tools/device-measurements/measure.sh budget measurements/year-1/results.tsv --tier reference
```

Mätningen `FastForwardBenchmarks` i `benchmarks/` spolar fram samma värld headless med samma avläsning, mot en gräns som är räknad från golvet, se [målenheter och mätbudgetar](./performance-budgets.md#2-budgetar).
Den 23 oktober 2026 tog det 34 ms processortid på en M1 Pro (.NET 10, Release).
Kvoten mellan `catch_up_median_ms` på telefonen och den siffran visar alltså hur mycket långsammare telefonen och IL2CPP är, och hur gränsen står sig.

Batteriet under ett dygn mäts med telefonen urkopplad och vanlig användning, med prototypen öppnad några gånger som i dagligt spel:

```bash
tools/device-measurements/measure.sh background-start --out measurements/dygn-1
```

```bash
tools/device-measurements/measure.sh background-end --out measurements/dygn-1
```

Prototypen läser hälsodata bara när den öppnas och gör ingenting i bakgrunden (D-70), så dess andel av dygnets förbrukning bör vara nästan bara de aktiva passen.

## 4. Felmarginal och upprepbarhet

Varje mätning körs två gånger på samma bygge och jämförs:

```bash
tools/device-measurements/measure.sh compare measurements/day-1/results.tsv measurements/day-2/results.tsv --margin 10
```

Två körningar räknas som samma resultat om varje mått skiljer högst 10 % från medelvärdet av de två.
Kallstarten och batteriet varierar mest; om de hamnar utanför marginalen körs en tredje mätning och medianen redovisas.
Temperaturen före passet styr uppvärmningen, så telefonen ska ha legat stilla i minst tio minuter innan ett pass.

## 5. Resultat

Mätningarna på produktägarens telefon återstår.
Skriptets tolkning av `dumpsys` är prövad mot exempelutdata i Android 14:s format, men inte mot en riktig telefon.

| Mått | Dagsljus | Natt |
|---|---|---|
| Telefon och Android | | |
| APK-storlek | | |
| Kallstart, median | | |
| Bildfrekvens | | |
| Bildtid, median / 95:e percentil / max | | |
| Bilder över 50 ms | | |
| Minne, medel / max | | |
| Uppvärmning under fem minuter | | |
| Batteri, mAh per timme aktivt spel | | |
| Batteri under ett dygn, appens andel | | |

## 6. Första budgetförslag

Förslagen gäller prototypen på produktägarens telefon och prövas mot mätningarna ovan.
Målenheterna och budgetarna per nivå föreslås i [målenheter och mätbudgetar](./performance-budgets.md) (TASK-024), och `measure.sh budget` avgör om ett pass håller dem.

| Mått | Förslag | Varför |
|---|---|---|
| Bildfrekvens | 30 fps, som klienten är låst till | Ett lugnt spel utan snabb rörelse; halverad frekvens sparar batteri |
| Bildtid | 95:e percentilen högst 40 ms, högst 1 % av bilderna över 50 ms | Kameran ska kännas jämn när spelaren panorerar |
| Kallstart | Högst 3 s till första bild, högst 5 s till spelbar ö | Spelaren öppnar spelet för en kort stund flera gånger om dagen |
| Minne | Högst 400 MB PSS | Lämnar utrymme på telefoner med 4 GB minne |
| APK-storlek | Högst 100 MB | Under Google Plays gräns för basmodulen med god marginal |
| Uppvärmning | Skalet högst 5 °C varmare efter tio minuters spel, ingen strypning | Telefonen ska inte kännas varm i handen |
| Batteri, aktivt spel | Högst 400 mAh per timme, ungefär 1 % av batteriet för ett pass på fem minuter | Ett dagligt pass ska inte märkas på batteriet |
| Batteri, dygn | Under 2 % av dygnets förbrukning, utöver de aktiva passen nästan noll | Ingen bakgrundskörning (D-70) |

Budgetarna håller skönheten i art direction (NFR-13): om ett mått går över budgeten sänks först kostnader som inte syns, som skuggupplösning och antal ljuskällor, innan ljusstämningarna ändras.

## 7. Öppna frågor

- Ska batteriet mätas med fast ljusstyrka på en nivå som produktägaren brukar använda, eller med automatisk ljusstyrka som i vardagen?
  Avgjord: fast ljusstyrka under det aktiva passet, med nivån i rapporten, och automatisk ljusstyrka över en hel dag (D-83).
- Behövs en andra, äldre Android-telefon redan i M1, eller räcker produktägarens telefon tills målenheterna bestäms i TASK-024?
  Avgjord: produktägarens telefon räcker i M1; målenheterna bestäms i TASK-024.
