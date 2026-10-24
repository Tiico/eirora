# Looks: så pusslar du ihop hus och natur

Den här guiden är för den som formger hur byggnader och natur ser ut och hur de växer när de uppgraderas.
Inget av det kräver kod.
Bakgrunden och valet står i [ADR-0009](../adr/0009-looks-are-a-modular-kit-as-data.md); stilen, paletten och polygonbudgeten står i [art-direction.md](./art-direction.md).

## Kom igång

1. Öppna `client/` i Unity och välj **Eirora → Look Gallery**.
2. Galleriet visar varje byggnad och varje sorts natur på en rad, med en kolumn per nivå eller steg, sett från spelets kamera.
   Byggnader står på sin egen mark och med naturen runt sig, så som i spelet: en skogshuggare står bland träd och en fiskebod på grunt vatten.
3. Klicka på en bild för att öppna looken i inspectorn.
   Ändra något, så ritas galleriet om direkt.
4. Välj ljus i verktygsfältet (Dawn, Day, Dusk, Night) för att se looken i dygnets olika stämningar; fönster och runglöd syns bara i skymning och natt.

Ändringarna syns också i spelet medan det körs i editorn.

## Var allt ligger

| Sökväg | Innehåll |
|---|---|
| `client/Assets/Eirora/Resources/Looks/Buildings/` | En `BuildingLook` per byggnad, med ett steg per nivå. |
| `client/Assets/Eirora/Resources/Looks/Nature/` | En `NatureLook` per sorts natur: skog, åker och berg. |
| `client/Assets/Eirora/Resources/Looks/Pieces/` | Byggsatsens bitar (`LookPiece`), till exempel ett träd, som looks använder. |
| `client/Assets/Eirora/Art/Models/` | Modeller gjorda för hand, till exempel i Blender. |

Nya looks och bitar skapas med **Create → Eirora** i projektfönstret.
Saknas en look för en byggnad visar galleriet knappen **Create**, och spelet ritar under tiden en magentafärgad kub.

## Steg och nivåer

En byggnads första steg är nivå 1, nästa nivå 2 och så vidare.
Ett steg bygger på steget före:

- det **behåller** alla delar från steget före,
- tar bort delarna vars namn står i **Removes**,
- lägger till sina egna delar, och en del med **samma namn (Id)** som en tidigare **ersätter** den.

Nivå 2 av ett sågverk kan alltså vara bara en skorsten och ett tillbygge, eller en ny del `mill` som ersätter det gamla huset med ett större.
Stäng av **Extends Previous** för att börja ett steg från noll.

Naturens steg har i stället **From**, en andel mellan 0 och 1: steget visas när naturen är minst så full.
Skogen har i dag `Sparse` från 0 och `Full` från 0,5.
I dag är en skog full utom där en byggnad står; när ekonomin visar skogens förråd och fältens mognad följer looken med av sig själv.

Galleriet markerar en nivå som saknas (spelet visar då den senaste som finns) och en nivå som ännu inte lägger till något.

## Delar

Mått och lägen är i hexenheter: en hex är knappt 1,8 bred, och marken är y = 0.
**Position** utgår från hexens mitt; **Yaw** vrider delen i grader.

| Form | Size | Övrigt | Paint | Trim |
|---|---|---|---|---|
| House | bredd, vägghöjd, djup | **Roof:** takets höjd | väggar | tak |
| Box | bredd, höjd, djup | står på sin botten | ovansida | sidor |
| Cone | radie (x), höjd (y) | **Sides:** antal sidor | allt | |
| Plate | bredd (x), djup (z) | ligger platt, lyft den med Position y | allt | |
| Windows | husets djup (z) | **Count:** antal fönster; placeras på husets framsida | fönsterljus | |
| Mesh | skala | **Mesh:** modellen | där modellen saknar färg | |
| Piece | skala | **Piece**, **Count**, **Spread**, **Growth** | | |

- **Paint och Trim** väljer en färg ur paletten och kan blandas mot en annan med **Mix With** och **Mix**, till exempel falurött mot svart för en skuggad takfot.
- **Glow** gör att delen lyser i skymning och natt i stället för att belysas, som fönster och runglöd.
- **Piece** placerar en bit ur byggsatsen.
  Med **Spread** över 0 sprids **Count** exemplar inom det avståndet, olika på varje hex men alltid på samma platser, så att träd nummer tre står kvar när skogen växer från tre till sex träd.
  **Growth** gör en del av exemplaren upp till så mycket högre.

## Modeller från Blender

1. Modellera i låg upplösning och färga med vertexfärger (Color Attributes) i palettens färger.
2. Exportera som FBX med vertexfärger i sRGB, och lägg filen i `client/Assets/Eirora/Art/Models/`.
   Unity importerar den läsbar och utan material.
3. Lägg till en del med formen **Mesh** i en look eller bit och välj modellen.
   Delen ärver Position, Yaw och Size (som skala) som andra delar.

Modellens ytor delas upp så att varje yta får sin egen platta skuggning, precis som de procedurella formerna.
En modell utan vertexfärger får färgen i **Paint**.

## Budget

Varje look har en **Triangle Budget** som gäller varje steg: hus 60–200 trianglar, specialbyggnad högst 600, träd 20–60 (art-direction.md, avsnitt 3).
Galleriet visar antalet för varje steg och markerar det som går över.
EditMode-testerna underkänner ett steg över budgeten, en byggnad som saknar en nivå den kan nå och en bit eller modell som saknas.

## Granska utan editorn

Varje steg kan sparas som PNG i dags- och skymningsljus, med knappen **Export PNGs…** i galleriet eller från kommandoraden (inte med `-nographics`, eftersom den renderar):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -quit -projectPath client -executeMethod Eirora.Editor.LookGallery.Export
```

Bilderna hamnar i `artifacts/looks/`, med namn som `Sawmill-2-dusk.png`.
Skärmbildsturen ([development.md](../development.md)) tar dessutom bilden `13-upgraded`: sågverket på nivå 2 bredvid ett förråd på nivå 1, så som spelaren ser det.
