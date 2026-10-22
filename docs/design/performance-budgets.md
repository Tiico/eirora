# Målenheter och mätbudgetar

> **Status:** [FÖRSLAG] v0.1, väntar på produktägarens beslut och på mätningarna i TASK-015  
> **Ärende:** TASK-024 · **Milstolpe:** M2 · **PRD:** NFR-03, NFR-07, NFR-13, O-13  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md)

Det här dokumentet föreslår svaret på O-13: vilka telefoner Eirora ska vara vackert och spelbart på, och vilka gränser ett bygge ska hålla där.
Budgetarna utgår från förslagen i [mätningar på enhet](./device-measurements.md#6-första-budgetförslag) och prövas mot mätningarna på produktägarens telefon när de finns.
Skriptet `tools/device-measurements/measure.sh budget` avgör om ett uppmätt bygge håller budgeten.

## 1. Målenheter

Spelet släpps globalt på engelska och svenska för vuxna (D-30, D-31), och Health Connect och HealthKit kräver en ny nog telefon.
Målenheterna delas därför i tre nivåer, med en Android-telefon och en iPhone där det går (NFR-07).

| Nivå | Android | iPhone | Vad som gäller |
|---|---|---|---|
| Golv | Samsung Galaxy A06 (Helio G85, Mali-G52 MC2, 4 GB) | – | Spelbart i 30 fps med sänkt kvalitet, utan att någon funktion saknas |
| Bas | Samsung Galaxy A16 5G (Exynos 1330, Mali-G68 MP2, 4 GB) | iPhone 11 (A13, 4 GB) | Hela art direction i 30 fps; skönheten hålls här (NFR-13) |
| Referens | Google Pixel 7 (Tensor G2, 8 GB) | iPhone 13 (A15, 4 GB) | Telefonerna mätningarna jämförs över tid på |

Motivering:

- **Galaxy A16 5G** var den mest sålda Android-telefonen i världen 2025 och **Galaxy A06** den sjätte mest sålda telefonen totalt, enligt Counterpoint ([MacRumors](https://www.macrumors.com/2026/01/29/iphone-16-worlds-best-selling-phone-2025/)).
  Galaxy A06 har den svagaste grafikkretsen av de mest sålda telefonerna och är därför golvet ([GSMArena](https://m.gsmarena.com/samsung_galaxy_a06-review-2743p4.php), [GSMArena](https://m.gsmarena.com/samsung_galaxy_a16_5g-review-2758p4.php)).
- **iPhone 11** är den äldsta iPhone som får iOS 26 ([TechRadar](https://www.techradar.com/phones/ios/ios-26-compatibility-does-your-iphone-support-it-heres-the-full-list-of-supported-devices)), och därmed den äldsta som HealthKit-integrationen i M6 behöver stödja under spelets första år.
  Dess A13 är kraftfullare än Galaxy A16, men den hamnar i basnivån eftersom ingen äldre iPhone behöver stödjas.
- **iPhone 13** var den mest använda iPhone-modellen under 2025 ([TelemetryDeck](https://telemetrydeck.com/survey/apple/iPhone/models)).
- **Pixel 7** är produktägarens telefon, som all mätning hittills görs på.
- I Sverige har iOS omkring 59 % av mobilerna ([StatCounter](https://gs.statcounter.com/os-market-share/mobile/sweden)), så iPhone är minst lika viktig som Android i den första målgruppen.

Projektet kräver Android 8 (API 26) och iOS 15, men en telefon som inte finns i tabellen lovas inget.
Flaggskepp ingår inte: det som håller på basnivån håller på dem.

## 2. Budgetar

Budgetarna gäller ett aktivt pass i mätscenen, i dagsljus och i natt, enligt [mätmetoden](./device-measurements.md#3-så-körs-mätningen).
Måtten heter som i skriptets `results.tsv`, och gränserna står i [`budgets.tsv`](../../tools/device-measurements/budgets.tsv).

| Mått | Golv | Bas | Referens | Varför |
|---|---|---|---|---|
| Bildfrekvens (`fps`) | minst 29 | minst 29 | minst 29 | Klienten är låst till 30 fps; ett lugnt spel behöver inte mer och sparar batteri |
| Bildtid, 95:e percentilen (`frame_p95_ms`) | högst 50 ms | högst 40 ms | högst 36 ms | Kameran ska kännas jämn när spelaren panorerar |
| Bilder över 50 ms (`frames_over_50ms_pct`) | högst 3 % | högst 1 % | högst 1 % | Enstaka hack märks mer än en jämnt lägre frekvens |
| Kallstart till första bild (`cold_start_median_ms`) | högst 5 s | högst 3 s | högst 2 s | Spelaren öppnar spelet för en kort stund flera gånger om dagen |
| Minne (`pss_max_mb`) | högst 400 MB | högst 400 MB | högst 400 MB | Golvet och basen har 4 GB; Android stänger appar som tar mer i bakgrunden |
| Paketets storlek (`apk_mb`) | högst 100 MB | högst 100 MB | högst 100 MB | Långt under Google Plays gräns, och en snabb nedladdning på mobilnät |
| Uppvärmning under passet (`skin_warming_c`, `battery_warming_c`) | högst 5 °C | högst 5 °C | högst 5 °C | Telefonen ska inte kännas varm i handen |
| Batteri vid aktivt spel (`app_battery_mah_per_hour`) | högst 400 mAh/h | högst 400 mAh/h | högst 400 mAh/h | Ett pass på fem minuter ska kosta omkring 1 % av batteriet |
| Batteri under ett dygn (`app_share_of_drain_pct`) | högst 2 % | högst 2 % | högst 2 % | Ingen bakgrundskörning (D-70) |

Uppvärmningen mäts över ett pass på tio minuter (`--seconds 600`), eftersom fem minuter är för kort för att telefonen ska bli varm.
Ett lokalt bygge av APK:n var 34 MB den 22 oktober 2026, alltså en tredjedel av storleksbudgeten.

Kartans rendering har en egen budget som inte behöver en telefon: hela kartan ritas med högst 75 000 trianglar ([ADR-0010](../adr/0010-world-map-format-and-generator-version.md#storlek-och-prestanda)), och ett EditMode-test prövar den största av femtio genererade kartor mot den (TASK-033).

## 3. Så avgörs om ett bygge håller budgeten

Ett mätpass på en telefon i en nivå jämförs med nivåns budget:

```bash
tools/device-measurements/measure.sh budget measurements/day-1/results.tsv measurements/dygn-1/results.tsv --tier reference
```

Skriptet skriver varje mått med uppmätt värde, gräns och `ok` eller `OVER BUDGET`, och avslutar med fel om något mått går över.
Ett mått som inte mättes skrivs som `not measured` och underkänner inte, så ett pass utan dygnsmätning går att pröva.
Testet `tools/device-measurements/test-budget.sh` prövar kommandot med påhittade resultat.

## 4. Vad CI mäter

CI kan bara mäta det som inte kräver en riktig telefon: emulatorns programvarurenderare säger ingenting om bildtiden på en Mali-krets.

| Budget | Var | Underkänner bygget |
|---|---|---|
| Paketets storlek | Android-bygget i Unity-workflowen, vid releaser och manuella körningar | Ja |
| Trianglar för hela kartan | Unity EditMode-testerna, på varje pull request som rör klienten | Ja |
| Bildtid, minne, kallstart, uppvärmning, batteri | `measure.sh` på telefoner i varje nivå | Nej, mäts för hand före varje grind och release |

Att mäta golvet och basen kräver telefoner som projektet inte har.
Förslaget är att köra mätscenen som ett *game loop*-test i Firebase Test Lab, som har Galaxy A-telefoner och rapporterar bildfrekvens och minne, när ett Firebase-projekt finns.
Tills dess mäts referensnivån på produktägarens telefon, och golvet och basen bedöms med samma marginal som Pixel 7 har till sin budget.

## 5. Skönhet mot budget

Art direction är utgångspunkten och budgeten är gränsen (NFR-13).
När ett mått går över sänks kostnader i den här ordningen, och bara på de nivåer som går över:

1. Det som inte syns i en skärmbild: skuggornas upplösning och avstånd, MSAA och antalet ljuskällor som räknas per bild.
2. Mängden detaljer: färre träd per skogsbricka och färre bybor i rörelse.
3. Effekter: partiklar, rörligt vatten och norrsken förenklas.
4. Ljusstämningarna ändras aldrig: gryning, dag, skymning och natt ser likadana ut på alla nivåer.

Golvet får sänkt kvalitet från steg 1 och 2 som standard; basen och referensen har full kvalitet.

## 6. Öppna frågor för produktägaren

- Ska golvet vara Galaxy A06, eller räcker det att spelet håller på basnivån och att svagare telefoner bara får fungera?
  Ett golv på A06 gör spelet tillgängligt för fler men binder grafiken hårdare.
- Ska ett Firebase-projekt skapas för Test Lab, så att golvet och basen mäts automatiskt på riktiga telefoner?
- Budgetarna prövas mot mätningarna i TASK-015 (#30) och justeras om Pixel 7 inte håller dem med god marginal.
