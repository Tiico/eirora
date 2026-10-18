# Kartvalidering

> **Ärende:** [TASK-030](https://github.com/Tiico/eirora/issues/64) · **Milstolpe:** M3 · **PRD:** MAP-01, MAP-03, MAP-09, D-79, D-80 · **ADR:** [ADR-0010](../adr/0010-world-map-format-and-generator-version.md)

Validatorn avgör om en karta ger en bra start och goda vägar att växa.
Den kommer före generatorn, så att generatorn byggs mot ett mätbart mål och väljer om en ny karta tills validatorn godkänner den (TASK-031).
Varje regel ger ett mätvärde och en gräns, så att en karta får en rapport och inte bara godkänd eller underkänd.

Gränserna är utgångsvärden ur epok 1:s modell (`docs/design/models/epoch_1.py`) och dagens Hemön.
Produktägaren valde att alla regler underkänner, och att siffrorna justeras när bulkkörningarna visar hur genererade kartor fördelar sig.
Alla gränser står i `ValidationLimits` i `src/Eirora.WorldGeneration/Validation/`.

## Vad som räknas fram ur kartan

Kartan sparar bara terräng, höjd och rikedom; resten följer av brickorna (`MapFeatures`, ADR-0010).

- **Fiskevatten** är grunt hav intill land.
- **Ett hamnläge** är en strand intill grunt hav som når djupt hav.
- **Krönet** är startöns enda högsta bricka; delar två brickor toppen finns inget krön.
- **Avståndet till en ö** är antalet steg till sjöss från startöns närmaste hamnläge till öns närmaste hamnläge.
  Ett skepp lämnar en kust, seglar bara över hav och lägger till vid en kust.

## Reglerna

| Regel | Mätvärde | Gräns | Varför |
|---|---|---|---|
| `radius` | Kartans radie | = 24 | Alla kartor är lika stora (D-80). |
| `deep-sea-edge` | Brickor i de två yttersta ringarna som inte är djupt hav | = 0 | Världar som ligger kant i kant i en arkipelag har hav emellan (ADR-0010). |
| `islands-have-harbours` | Öar utan hamnläge | = 0 | Varje ö ska gå att nå med skepp. |
| `fairway-to-edge` | Steg till sjöss från startöns hamnlägen ut till kanten | ≥ 0 (finns) | Sjövägar till andra världar börjar vid kanten (ADR-0010). |
| `islands-reachable` | Öar vars hamnlägen inget skepp från startön når | = 0 | Inga platser som inte går att nå. |
| `start-land` | Startöns landbrickor | 40–90 | Hemön har 46; plats att växa utan att startön blir hela kartan. |
| `start-grassland` | Gräsmark på startön | ≥ 8 | Långhus, stugor, förråd, sågverk, väveri och utkiksvarde. |
| `start-forest` | Skog på startön | ≥ 3 | Huggarkojan behöver minst två skogsbrickor för full takt (ekonomi v1). |
| `start-farmland` | Åker på startön | ≥ 2 | Åkern. |
| `start-pasture` | Betesmark på startön | ≥ 1 | Fårgården. |
| `start-mountain` | Berg på startön | ≥ 1 | Stenbrottet. |
| `start-fishing-water` | Fiskevatten vid startön | ≥ 2 | Fiskeboden. |
| `start-harbours` | Hamnlägen på startön | ≥ 1 | Bryggan, där expeditionerna börjar. |
| `crest` | Krönets höjd | ≥ 4 | Vårdkasen står på Hemöns krön (epok 1), och krönet ska vara en höjd. |
| `first-island` | Steg till sjöss till den första expeditionens ö | 8–12 | Nyön ligger 10 steg bort i epok 1:s modell. |
| `second-island` | Steg till sjöss till en annan ö för den andra expeditionen | 11–16 | Ytterön ligger 12 steg bort i modellen och tar tre spelardygn. |
| `inner-sea` | Havsbrickor inom öarnas räckvidd från starten, utom kusten närmast startön | ≥ 150 | Vårdkasens eld kräver att dimman lyfts över innerhavet (epok 1). |
| `trade-offs` | Sorter av mark (skog, sten, fisk, ull, säd) där rikare mark ligger längre bort än vanlig | ≥ 3 | Rikedom mot avstånd är en verklig avvägning (MAP-03). |

## Hemön i dag

Prototypens handgjorda karta godkänns inte, och rapporten säger varför:

- Nyöns hamnläge ligger mitt på ön, utan vatten intill, så inget skepp kan lägga till där (`islands-have-harbours`, `islands-reachable`).
- Hemöns två berg är lika höga, så krönet är inte entydigt (`crest`).
- Utan ett hamnläge på Nyön och utan Ytterön finns ingen ö för expeditionerna (`first-island`, `second-island`).

Det spelar ingen roll för prototypens ekonomi, som har egna platser tills TASK-037, men generatorn som fyller havet runt Hemön (TASK-031) måste ta hänsyn till det.

## Bulkkörning

`BulkValidation` validerar kartorna en generator gör av en följd av seeds och sammanfattar per regel hur många som godkänns och hur mätvärdet fördelar sig (minsta, median, största).
Samma seeds ger alltid samma rapport, byte för byte.
CI kör ett litet urval för varje generator i `WorldGenerators.All`; lokalt körs tiotusentals med `tools/Eirora.MapCheck`, se [development.md](../development.md#kartor-och-gyllene-kartor).
