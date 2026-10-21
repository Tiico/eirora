# Sparningen är en snapshot som migreras framåt; gamla regler finns bara som frysta byggen

En sparning består av världens senaste snapshot och journalen sedan världen fick sin nuvarande regelversion, och varje sparning skrivs atomiskt med en ny snapshot.
När appen får en ny regelversion migreras snapshoten steg för steg från version n till n+1, och de nya reglerna gäller för all simuleringstid efter snapshoten.
Historiken tolkas därmed aldrig om, och klienten behöver aldrig köra gamla regler: källkoden innehåller bara aktuell regelversion och migrationskedjan, medan varje tidigare regelversion bevaras som ett oföränderligt bygge av simuleringsbiblioteket som servern och CI laddar.
Innehåll (byggnader, recept, resurser, fynd, epoker) har en egen innehållsversion och läggs bara till, så att en ny epok inte påverkar en värld förrän världen når den (PRG-05).
Vi valde detta framför att låta klienten bära alla regelversioner därför att en migration av ett tillstånd är en liten, ren och testbar funktion, medan gamla regler i klienten växer med varje regeländring och följer med i varje app för alltid.

## Vad som händer med en värld vid en ny regelversion

1. Appen med regelversion n+1 öppnar en sparning vars snapshot har regelversion n.
   Journalen efter snapshoten är tom, eftersom varje sparning slutar i en snapshot.
2. Migrationerna körs i ordning, n → n+1 och vidare till aktuell version.
   En migration är en ren funktion i simuleringsbiblioteket från ett tillstånd till nästa versions tillstånd (INV-D2); en regeländring som inte ändrar tillståndets form har en identitetsmigration.
3. Den migrerade snapshoten får den nya regelversionen och behåller sin simuleringstid.
   Tiden efter den, även en lång frånvaro, simuleras med de nya reglerna (INV-D4).
4. Den nya sparningen skrivs atomiskt och kontrolleras genom att laddas om och jämföras med fingeravtrycket.
   Det föregående segmentet, snapshoten från när världen fick version n plus journalen under version n, behålls till nästa regelbyte.
   En felaktig migration kan därför rättas utan ny regelversion: appen migrerar om från segmentets sista snapshot och spelar upp journalen för den aktuella versionen med aktuella regler.
5. En sparning med högre regel-, innehålls- eller formatversion än appen skrivs aldrig över; spelaren får besked om att uppdatera och sparningen behålls (TASK-020).

En värld kan alltså aldrig förlora något på grund av en ny regelversion: allt som hänt står kvar som det hände, och bara framtiden räknas med de nya reglerna (INV-V1, NFR-01).

## Snapshoten

- Den innehåller hela det tillstånd som fingeravtrycket täcker: krafter, bidrag per spelardygn, väntande avstämningar, viloschema och väntande schemaändring, ekonomi, historik och alla använda idempotensnycklar med sina utfall (INV-D6).
  Den innehåller aldrig härledda cachar, rådata (INV-B4) eller flyttal (INV-D3).
- Ett huvud anger formatversion, regelversion, innehållsversion, simuleringstid och en hash av fingeravtrycket.
- Samma tillstånd serialiseras alltid till samma byte, och innehåll refereras med stabila id:n, aldrig med en position i en lista.
- Formatversionen gäller behållaren (huvud, kodning, komprimering); tillståndets form följer regelversionen, så migrationer av tillståndet räknas per regelversion.
- En snapshot tas vid varje sparning, det vill säga efter varje godkänt kommando och varje tidssteg som appen sparar.
  Världen är några kilobyte, så skrivningen kostar lite, och föregående sparning behålls tills den nya är skriven.

## Innehållet

- Varje innehållspost har ett stabilt id som aldrig återanvänds eller byter betydelse.
  Innehållsversionen ökar med varje tillägg.
- Nytt innehåll kan bara nås när världen når den epok som för in det, så en uppdatering med en ny epok ändrar inget i en värld som inte nått den.
  Snapshoten anger innehållsversionen, och en app med äldre innehåll än sparningen öppnar den inte.
- Att ändra hur befintligt innehåll verkar (kostnad, recept, tak) eller att ta bort det är en regeländring: regelversionen höjs och en migration skrivs vid behov.
- Innehållet levereras med appen.
  Nedladdat innehåll utan appsläpp kan läggas ovanpå senare med samma id:n och innehållsversion.

## Servern

- Servern har ett manifest över de regelversioner den stödjer: den aktuella och de äldre vars efterföljare ännu inte är helt utrullad i båda butikerna, alltid minst den föregående.
- Varje stödd version är ett fryst bygge av simuleringsbiblioteket som servern laddar sida vid sida, och klienten anger sin regelversion i varje delad åtgärd (NFR-02).
- `ServerInfo` listar de stödda versionerna i stället för en enda.
- En klient utanför fönstret spelar sin egen värld som vanligt (ADR-0001); dess gruppåtgärder köas med beskedet att appen behöver uppdateras.

## Gyllene sparningar i CI

- För varje regel- och formatversion skapar ett headless-scenario med syntetiska bidrag en gyllene sparning som checkas in under `tests/` med det förväntade fingeravtrycket efter laddning med aktuell kod.
- CI laddar varje gyllene sparning med aktuell kod, jämför fingeravtrycket och fortsätter scenariot en bit med aktuella regler.
- Varje fryst bygge spelar upp sin egen gyllene journal bit för bit, så att ett bygge som inte längre ger samma tillstånd upptäcks.
- Förväntade fingeravtryck ändras bara i den PR som höjer regelversionen eller ändrar en migration, så att granskningen ser ändringen.
- Riktiga spelares sparningar checkas aldrig in, eftersom de innehåller bidrag per spelardygn.

## Considered Options

- **Spela upp journalen efter snapshoten med dess gamla regler och migrera sedan:** exakt och tillåter glesa snapshots, men klienten måste bära varje regelversion som någonsin funnits, eftersom en spelare kan komma tillbaka efter ett år.
- **Spela upp hela journalen med de nya reglerna:** inga migrationer och inga gamla regler, men historiken tolkas om, så byggnader och framsteg kan försvinna i efterhand (bryter INV-V1, PRG-04 och NFR-01).
- **Frysta regelmoduler i källkoden:** varje version är en uppsättning regelobjekt och ändrade delar kopieras till nästa version; fungerar även i klienten under IL2CPP, men koden växer med varje regeländring.
- **Versionsvillkor i reglerna (`if (rules >= 3)`):** billigast i början, men varje regel bär hela sin historik och ett misstag ändrar gamla världar tyst.
- **Snapshot vid gryning eller var N:e journalpost:** färre skrivningar, men kräver gamla regler i klienten för journalen efter snapshoten.
- **Innehållet ingår i regelversionen:** ett nummer att hålla reda på, men varje ny epok blir en regeluppgradering med migration och serverfönster, även när inget befintligt ändras.
- **Servern stödjer alla versioner för alltid, eller bara den senaste:** det första ger en testmatris som växer för evigt; det andra stoppar gruppåtgärder under stegvis utrullning och App Review.
- **Gyllene sparningar som genereras från gamla taggar i CI, eller produktägarens riktiga världar:** det första ruttnar när gamla verktygskedjor inte längre bygger; det andra lägger bidrag som härrör från hälsodata i repot.

## Consequences

- Varje höjning av regelversionen kräver en migration (även identitet), en gyllene sparning och ett fryst bygge som CI publicerar; ett test underkänner en regelversion som saknar gyllene sparning.
- `RulesVersion` höjs också när befintligt innehåll ändrar verkan, inte bara när koden för reglerna ändras.
- TASK-019 bygger snapshoten enligt ovan, TASK-020 sparningen med atomisk skrivning, segment och migrationskedja, och TASK-021 serverns fönster av frysta byggen.
- Prototypens journaler från M1 saknar snapshot.
  Den första versionen med sparformatet har därför samma regelversion som den sista prototypen och spelar upp journalen en gång innan den skriver sin första snapshot.
  Höjs regelversionen i prototypen innan dess kan en äldre journal inte laddas, och prototypen säger det i stället för att visa en annan värld (TASK-014).
- Att journalen sparas trots att laddningen utgår från snapshoten gör att fel kan återskapas, att en migration kan göras om och att konflikter mellan enheter kan lösas (TASK-023).
- Från sparformat 3 består sparningen av en världsdel och en bidragsdel ([ADR-0008](./0008-backend-hosting-accounts-and-database.md), D-99).
  På telefonen ligger båda i samma fil; i molnsparningen krypteras bidragsdelen, och utan den laddas världen från världsdelen i ett nytt segment (D-100).
- Möjliggörs av [ADR-0001](./0001-local-world-server-for-shared-state.md), eftersom servern bara behöver gamla regelversioner för det delade.
- Kompletterar [ADR-0004](./0004-shared-libraries-as-source-packages.md): klienten bygger fortfarande bara aktuell källkod, medan servern dessutom laddar frysta byggen.
