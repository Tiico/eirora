# Hus och natur ser ut som de gör genom en modulbyggsats i data, inte genom kod

Varje byggnad och varje sorts natur har en Look-asset i `client/Assets/Eirora/Resources/Looks/` som beskriver hur den ser ut steg för steg: en byggnad har ett steg per nivå och naturen ett steg per grad av fullhet, till exempel hur mycket skog som står kvar.
Ett steg är en lista av delar.
En del är en procedurell form med mått (hus med sadeltak, block, kon, platta, fönsterrad), en modell gjord för hand i Blender eller en återanvändbar bit ur byggsatsen, till exempel ett träd.
Ett steg bygger på steget före: det behåller dess delar, tar bort de som anges och lägger till sina egna, och en del med samma namn som en tidigare ersätter den.
En designer kan därför pussla ihop nivå 2 som "nivå 1 plus skorsten och tillbygge" utan att röra kod, och bitarna i byggsatsen ändras på ett ställe för alla som använder dem.
Vi valde detta framför hela prefabs per nivå därför att progressionen är själva poängen: varje nivå ska läsas som en utveckling av den förra, och en byggsats håller ihop stilen när både Claude och en människa formger.

## Så används det

- **Galleriet** (`Eirora → Look Gallery` i Unity) visar alla byggnader och all natur sida vid sida, en kolumn per nivå eller steg, i spelets kameravinkel och ljus.
  Det ritas om så fort en look eller bit ändras, visar antal trianglar mot budgeten och markerar nivåer som saknas eller ännu inte skiljer sig från nivån före.
- **I spelet** ritar ön direkt från samma assets, så en ändring syns också medan spelet körs i editorn.
- **Modeller** läggs i `client/Assets/Eirora/Art/Models/` och importeras läsbara, utan material och med vertexfärger i sRGB; ön bakar in dem i samma mesh som allt annat.
- **Färger** anges som palettens namn (`Swatch`), eventuellt blandade mot en annan, så att art direction (D-61) gäller och en ändring av paletten når alla looks.
- **Granskning utan editor:** `Eirora.Editor.LookGallery.Export` sparar varje steg i dags- och skymningsljus som PNG, och skärmbildsturen visar ett uppgraderat sågverk bredvid ett förråd på nivå 1.

Arbetsflödet för designer står i [looks.md](../design/looks.md).

## Considered Options

- **En hel prefab per byggnad och nivå**, modellerad i Blender eller Unity.
  Det är det vanligaste flödet för en 3D-artist, men varje nivå blir en egen modell utan koppling till den förra, stilen glider isär och varje ändring av grunden måste göras om i alla nivåer.
  Renderingen hade också behövt byggas om till ett GameObject per byggnad.
  Modulbyggsatsen tar ändå emot handgjorda modeller, så den här vägen står öppen del för del.
- **JSON-recept i det delade biblioteket**, så att samma byggsats kan testas utan Unity.
  Utseende är bara klientens sak (ADR-0002), och en designer behöver inspector, ångra och direkt förhandsvisning mer än textfiler.
- **Fortsatt procedurell kod** (en `switch` per byggnad).
  Varje ändring kräver en programmerare, nivåer saknade helt egen form och det fanns inget sätt att se alla nivåer samtidigt.

## Consequences

- Flytten från koden gav samma geometri för alla byggnader, berg och åkrar.
  Skogens träd placeras nu med samma frö men utan den negativa modulo som gjorde vissa träd förkrympta.
- Simuleringen anger högsta nivå per byggnad (`BuildingSpec.MaxLevel`), och ett EditMode-test kräver ett steg för varje nivå som går att nå, att varje steg håller sin triangelbudget och att varje bit och modell finns.
  En ny nivå i ekonomin kan därför inte släppas utan att ha ett utseende.
  Nivå 2 är i dag samma som nivå 1, och galleriet visar det.
- Saknas en look ritar spelet en magentafärgad kub, så att den syns direkt.
- Unity sparar val i listor (form, färg, byggnad) som tal, så nya former, färger och naturtyper läggs alltid sist och tas aldrig bort.
- Ön ritas fortfarande som en enda sammanslagen mesh som byggs om när världen ändras; GPU-instansiering (art-direction.md, avsnitt 7) blir ett senare steg om mätningar kräver det.
- Naturens fullhet kommer i dag bara från om hexen är bebyggd.
  När ekonomin visar skogens förråd och fältens mognad skickas det värdet in i stället, utan att någon look behöver ändras.
- Paletten själv är fortfarande kod (`Palette.cs`); att lägga den som data (art-direction.md, avsnitt 9) görs utan att looks påverkas.
