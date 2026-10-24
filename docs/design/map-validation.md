# Kartvalidering

> **Ärende:** [TASK-030](https://github.com/Tiico/eirora/issues/64) · **Milstolpe:** M3 · **PRD:** MAP-01, MAP-03, MAP-09, D-79, D-80 · **ADR:** [ADR-0010](../adr/0010-world-map-format-and-generator-version.md)

Validatorn avgör om en karta ger en bra start och goda vägar att växa.
Den kommer före generatorn, så att generatorn byggs mot ett mätbart mål och väljer om en ny karta tills validatorn godkänner den (TASK-031).
Varje regel ger ett mätvärde och en gräns, så att en karta får en rapport och inte bara godkänd eller underkänd.

Gränserna är utgångsvärden ur epok 1:s modell (`docs/design/models/epoch_1.py`) och dagens Hemön.
Produktägaren valde att alla regler underkänner, och att siffrorna justeras när bulkkörningarna visar hur genererade kartor fördelar sig.
Alla gränser står i `ValidationLimits` i `src/Eirora.WorldGeneration/Validation/`.

## Vad som räknas fram ur kartan

Kartan sparar bara terräng, höjd och rikedom; resten följer av rutorna (`MapFeatures`, ADR-0010).

- **Fiskevatten** är grunt hav intill land.
- **Ett hamnläge** är här en strand intill grunt hav som når djupt hav.
  I ekonomin är varje landruta utom berg vid sådant vatten ett hamnläge (D-89), så en strand som valideringen godtar är alltid ett hamnläge där; regeln står kvar som den var, eftersom generator v1 är låst till den.
- **Toppen** är startöns enda högsta ruta; delar två rutor toppen finns ingen topp.
- **Avståndet till en ö** är antalet steg till sjöss från startöns närmaste hamnläge till öns närmaste hamnläge.
  Ett skepp lämnar en kust, seglar bara över hav och lägger till vid en kust.

## Reglerna

| Regel | Mätvärde | Gräns | Varför |
|---|---|---|---|
| `radius` | Kartans radie | = 24 | Alla kartor är lika stora (D-80). |
| `deep-sea-edge` | Rutor i de två yttersta ringarna som inte är djupt hav | = 0 | Världar som ligger kant i kant i en arkipelag har hav emellan (ADR-0010). |
| `islands-have-harbours` | Öar utan hamnläge | = 0 | Varje ö ska gå att nå med skepp. |
| `fairway-to-edge` | Steg till sjöss från startöns hamnlägen ut till kanten | ≥ 0 (finns) | Sjövägar till andra världar börjar vid kanten (ADR-0010). |
| `islands-reachable` | Öar vars hamnlägen inget skepp från startön når | = 0 | Inga platser som inte går att nå. |
| `start-land` | Startöns landrutor | 40–90 | Hemön har 46; plats att växa utan att startön blir hela kartan. |
| `start-grassland` | Gräsmark på startön | ≥ 8 | Långhus, stugor, förråd, sågverk, väveri och utkikstorn. |
| `start-forest` | Skog på startön | ≥ 3 | Skogshuggaren behöver minst två skogsrutor för full takt (ekonomi v1). |
| `start-farmland` | Åker på startön | ≥ 2 | Åkern. |
| `start-pasture` | Betesmark på startön | ≥ 1 | Fårgården. |
| `start-fishing-water` | Fiskevatten vid startön | ≥ 2 | Fiskeboden. |
| `start-harbours` | Hamnlägen på startön | ≥ 1 | Bryggan, där expeditionerna börjar. |
| `crest` | Toppens höjd | ≥ 4 | Vårdkasen står på Hemöns topp (epok 1), och toppen ska vara en höjd. |
| `start-quarries` | Berg på startön utom toppen | ≥ 2 | Stenbrotten: toppen bär bara Vårdkasen (D-92), och det rikaste berget ska vara ett val, inte det enda (D-95). |
| `first-island` | Steg till sjöss till den första expeditionens ö | 8–12 | Nyön ligger 10 steg bort i epok 1:s modell. En expeditions ö har minst 6 landrutor och någon rik mark, så ett skär räknas aldrig. |
| `second-island` | Steg till sjöss till en annan ö för den andra expeditionen | 11–16 | Ytterön ligger 12 steg bort i modellen och tar tre dagar. |
| `inner-sea` | Havsrutor inom öarnas räckvidd från starten, utom kusten närmast startön | ≥ 150 | Vårdkasens eld kräver att dimman lyfts över innerhavet (epok 1). |
| `trade-offs` | Sorter av mark (skog, sten, fisk, ull, säd) där rikare mark ligger längre bort än vanlig | ≥ 3 | Rikedom mot avstånd är en verklig avvägning (MAP-03). |

## Hemön i dag

Prototypens handgjorda karta klarar alla regler utom `second-island`: Ytterön, den andra expeditionens ö, kommer när havet runt Hemön fylls en gång (D-81).

Nyön ligger 9 steg till sjöss från Hemöns hamnlägen, och toppen är det västra berget (M1) med höjd 6.
Intill toppen står ett vanligt berg på (-3,-1), så att Hemön har stenådern och ett berg till att bryta sten i (D-95).

Validatorn visade två fel i den första versionen av kartan, som rättades innan någon värld sparades med den:
Nyöns hamnläge låg mitt på ön, utan vatten intill, och Hemöns två berg var lika höga.
Nu har Nyön en vik väster om hamnläget, mot Hemön, och det västra berget är ett steg högre än stenådern.

## Bulkkörning

`BulkValidation` validerar kartorna en generator gör av en följd av seeds och sammanfattar per regel hur många som godkänns och hur mätvärdet fördelar sig (minsta, median, största).
Samma seeds ger alltid samma rapport, byte för byte.
CI kör ett litet urval för varje generator i `WorldGenerators.All`; lokalt körs tiotusentals med `tools/Eirora.MapCheck`, se [development.md](../development.md#kartor-och-gyllene-kartor).
Seedverktyget i samma verktyg ritar kartorna med det validatorn mätte (`MapAnalysis`) och listar per regel de kartor som ligger närmast gränsen (TASK-034).

Generator v1 (`procedural-archipelago`, TASK-031) väljer om tills validatorn godkänner kartan, och bulkrapportens rad `attempts` visar hur många försök det krävde.
Över seeds 0–9 999 godkändes alla 10 000 kartor, oftast på första försöket och aldrig efter fler än fyra.
