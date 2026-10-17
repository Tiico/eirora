# UX-resor

> **Status:** [FÖRSLAG] v0.1, väntar på granskning av produktägaren  
> **Ärende:** TASK-005 · **Milstolpe:** M0 · **PRD:** UX-02, UX-03, UX-05, UX-07, RST-01–RST-11, PRG-01, HLT-03, HLT-07, HLT-12, D-17, D-22, D-33, D-37, D-45, D-49–D-58  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Krafter:** [forces.md](./forces.md) · **Invarianter:** [invariants.md](./invariants.md)

Tre resor steg för steg: första sessionen, återkomst efter sju dagars frånvaro och en kväll när vilolåset börjar.
Varje resa har mål, förutsättningar, steg, gränsfall och de krav och invarianter den verifierar.
Varje steg beskriver vad spelaren ser, gör och känner och vad simuleringen gör.

Texterna i spelet är utkast på svenska och skrivs i *kursiv* eller i skisser; den engelska versionen skrivs parallellt (UX-06).
Utseende, ljus och guidens gestalt bestäms i art direction (TASK-006) och M9.
Kraftsiffrorna följer [krafternas regler](./forces.md) exakt.
Byggnader, resurser och kraftkostnader följer [ekonomi v1](./economy-v1.md), se [beroende till ekonomin](#beroende-till-ekonomi-v1).

## Gemensamt för alla resor

### Tonläge

- **Lugnt och varmt.**
  Spelet konstaterar och bjuder in; det manar aldrig på.
- **Aldrig skuld.**
  Inga ord som *missat*, *glömt*, *svit*, *förlorat*, *bara*, *äntligen* eller *vi har saknat dig*, och ingen räkning av dagar borta (princip 11).
- **Världen i fokus, inte kroppen.**
  Spelet beskriver vad världen fick, inte hur bra spelaren var.
  Inga betyg, inga jämförelser, inga råd om sömn eller motion och inga medicinska påståenden.
- **Ingen misstro.**
  I den egna världen visas aldrig om ett bidrag är verifierat eller egenrapporterat, eftersom det inte spelar någon roll där (INV-F7, R-04).
- **Inga utropstecken och ingen brådska.**
  Inga nedräkningar, inga tidsbegränsade erbjudanden och inget som går ut.
- **Två likvärdiga val ser likvärdiga ut.**
  *Ja* och *Nej tack*, *Koppla* och *Jag skriver in själv*, har samma storlek, färg och placering.

### Daglig rapport

Den dagliga rapporten (UX-02) visas första gången spelaren öppnar spelet efter en gryning.
Den sammanfattar allt sedan spelaren senast läste en rapport, så efter en frånvaro är det en rapport, inte en per spelardygn.

| Del | Innehåll | Regel |
|---|---|---|
| Rubrik | *God morgon*, *God dag* eller *God kväll* efter lokal tid; *Sedan sist* efter mer än ett spelardygn | Ingen räkning av dagar |
| Egenvård | En rad per vana: mängd och kraft som blev av den | Vanor utan data utelämnas, utom sömn som alltid visar minst grundnivån |
| I världen | Högst fem händelser och fynd, viktigast först | Bara det som hänt, aldrig det som kunde ha hänt |
| Gruppen | Gruppens framsteg och hälsningar (från M8) | Bara positivt; aldrig vem som inte bidragit |
| Värt att titta på | Högst tre förslag på nästa beslut, som leder till rätt plats på kartan | Förslag, inte uppgifter; inga bockar eller räknare |
| Knappar | *Till ön* och den diskreta länken *Lägg till egenvård* | Länken finns alltid, oavsett vad spelaren registrerat |

### Beroende till ekonomi v1

Resorna använder bara det ur [ekonomi v1](./economy-v1.md) som står i tabellen nedan.
Ändras ekonomin byts raderna här och motsvarande ord i texterna, utan att resornas form ändras.

| Används i resorna | Värde i ekonomi v1 | Används till |
|---|---|---|
| Startkrafter | 100 Utforskning, 100 Byggkraft, 40 Gryningskraft (D-45) | Den guidade turen |
| Handling för Gryningskraft | Dagsverke på en byggnad, 30 | Resa 1, steg 4 |
| Handling för Utforskning | Kartlägg närområdet, 60 | Resa 1, steg 5 |
| Gratis bygge | Huggarkoja | Resa 1, steg 6 |
| Handling för Byggkraft | Brygga, 60 | Resa 1, steg 7 |
| Expedition | 250 Utforskning; kräver brygga; framsteg bara under aktiva timmar | Resa 3 |
| Bybornas tillväxt utan spelare | Från 12 till bostadstaket 16 på sju spelardygn | Resa 2 |
| Händelser i rapporterna | Säd, timmer, plank, fisk, sågverk, långhus, ny ö | Skisserna |

Startkrafterna är en del av starttillståndet och inte en händelse, så INV-F2 gäller oförändrad: efter starten ökar bara egenvård ett kraftsaldo.

## Resa 1: Första sessionen

**Mål:** spelaren förstår de tre krafterna genom att använda dem en gång var, ser att planering är gratis, väljer sin viloperiod med nattläget framför sig och väljer källa för egenvården, utan att någon hälsobehörighet krävs (UX-03, HLT-12, PRG-01).
Sessionen tar 5–8 minuter och har ett tydligt slut.

**Förutsättningar:**

- Ny installation, inget konto och ingen värld.
- Inga hälsobehörigheter och ingen egenvård registrerad i spelet.
- Tiden på dygnet är godtycklig; gränsfallet när installationen sker under den valda viloperioden beskrivs nedan.
- Åldersgränsen 18+ (D-30) sköts av butikernas åldersklassning; turen frågar inte om ålder (D-58).

**Ordning:** valet av hälsokälla och viloperiod kommer efter turen (D-49, PRD 4.4).
Spelaren ska ha sett vad krafterna gör innan behörighetsfrågan, och ha sett nattläget när viloperioden väljs.

### Steg

1. **Titel i gryningsljus.**
   - *Ser:* havet och en ö i gryningsljus, namnet *Eirora* och *A world worth growing.*
   - *Gör:* trycker *Börja*.
   - *Känner:* nyfikenhet; det första intrycket är vackert, inte ett formulär (D-29).
   - *Simuleringen:* ingen värld finns ännu.
2. **Språk och tillgänglighet.**
   - *Ser:* språk, textstorlek, kontrast, reducerad rörelse och ljud, förvalda från telefonens inställningar.
     *Välj hur Eirora ska se ut och låta. Du kan ändra allt senare.*
   - *Gör:* bekräftar eller ändrar.
   - *Känner:* att spelet anpassar sig efter spelaren, inte tvärtom (UX-05).
   - *Simuleringen:* inget; inställningarna är klientens.
3. **Världen vaknar.**
   - *Ser:* kameran glider över en ögrupp i dimma och landar vid en liten bosättning i gryningen; byborna går ut till sina sysslor.
     *Det här är din ö. Varje gryning vaknar den med det du gav dig själv i går.*
     *I dag får du en gåva att börja med.*
     Tre krafter visas med sina saldon: *Utforskning 100 · Byggkraft 100 · Gryningskraft 40*.
   - *Gör:* tittar.
   - *Känner:* att ön är hens egen.
   - *Simuleringen:* `WorldGenerator` skapar världen från en seed; starttillståndet innehåller startkrafterna.
     Det första spelardygnet börjar vid lokal midnatt samma dag, så att dagens steg räknas (D-51).
4. **Gryningskraft.**
   - *Ser:* guiden pekar på fiskeboden.
     *Gryningskraft kommer av sömn. Med den ger du byn en extra god dag där den behövs bäst.*
   - *Gör:* trycker *Dagsverke · 30* på fiskeboden.
   - *Känner:* att ett litet val syns direkt: båtarna går ut och fisken ökar snabbare.
   - *Simuleringen:* ett spelpåverkande kommando godkänns; Gryningskraft 40 → 10 (INV-F5).
5. **Utforskning.**
   - *Ser:* dimman runt byn.
     *Utforskning kommer av rörelse. Den lyfter dimman och visar vad som finns runt omkring.*
   - *Gör:* trycker *Kartlägg närområdet · 60*.
   - *Känner:* upptäckarglädje: dimman drar sig undan och en urskog och en stenåder syns.
   - *Simuleringen:* Utforskning 100 → 40; brickorna blir utforskade och förblir det (INV-V1).
6. **Planering är gratis.**
   - *Ser:* urskogen markerad.
     *Att planera och bygga smått kostar ingen kraft. Rika platser ger mer.*
   - *Gör:* placerar en huggarkoja i urskogen.
   - *Känner:* att strategi är något man alltid kan utöva, och att platsen spelar roll (D-23).
   - *Simuleringen:* bygget godkänns utan kraftkostnad; lediga bybor börjar bygga.
7. **Byggkraft.**
   - *Ser:* kusten vid byn.
     *Byggkraft kommer av träning. Den räcker till de stora stegen.*
   - *Gör:* placerar en brygga och trycker *Bygg · 60*.
   - *Ser sedan:* *Från bryggan kan en expedition en dag segla mot det okända.*
   - *Känner:* ett mål några dagar bort, utan tidspress.
   - *Simuleringen:* Byggkraft 100 → 40; bryggan läggs i byggkön.
8. **Var ska krafterna komma ifrån?**
   - *Ser:* integritetsförklaringen, se [skissen nedan](#integritetsförklaringen).
   - *Gör:* väljer *Koppla Health Connect* eller *Jag skriver in själv*.
   - *Känner:* kontroll och förtroende; båda vägarna är lika goda.
   - *Simuleringen:* inget; behörigheter och källa är administrativa och klientens.
9. **Spegeln.**
   - *Om hälsokällan är kopplad och har steg från i dag:* *I dag har du redan rört dig 3 200 steg. Det blev 48 Utforskning.*
     Saldot räknas upp från 40 till 88 framför spelaren.
   - *Om spelaren skriver in själv:* *Vill du skriva in något från i dag? Det går lika bra senare.* med knapparna *Skriv in* och *Senare*.
   - *Om det inte finns något:* steget hoppas över utan kommentar.
   - *Känner:* aha-ögonblicket; det jag redan gjort i dag finns i världen.
   - *Simuleringen:* bidragen importeras och omvandlas; 3 200 steg ger 3,2 × 15 = 48 Utforskning, lika mycket oavsett källa (INV-F7).
10. **Viloperioden och natten.**
    - *Ser:* ett dygnsur där viloperioden dras mellan 6 och 10 timmar, förvalt 23–07.
      Ön tonar till natt medan spelaren väljer: lyktorna tänds och byborna går hem.
      *När du vilar, vilar byn. Då kan du titta, men inte bygga eller ge order.*
      *Skogen växer och säden mognar ändå.*
      *Byn vilar 23:00–07:00. Du kan ändra tiden senare; en ändring gäller efter ett dygn.*
    - *Gör:* väljer tid och trycker *Klart*.
    - *Känner:* att vilan är en del av spelet, inte en spärr (princip 3); inga överraskningar första kvällen (RST-03).
    - *Simuleringen:* ett administrativt kommando sätter viloschemat; det gäller från det att turen är slut (D-50).
11. **Morgonnotisen.**
    - *Ser:* *Vill du ha en tyst hälsning när byn vaknar och något nytt har hänt? Högst en om dagen, aldrig under natten.*
      Knapparna *Ja tack* och *Nej tack*.
    - *Gör:* väljer; ingen av knapparna är förvald.
    - *Känner:* att notiser är spelarens val (D-33, UX-07).
    - *Simuleringen:* inget.
12. **Vänner.**
    - *Ser:* *Eirora kan spelas tillsammans med upp till fem vänner. Det går att ordna när som helst.*
      Knapparna *Bjud in* och *Senare*.
    - *Gör:* väljer.
    - *Känner:* inget tryck; skärmen visas en gång.
    - *Simuleringen:* inget; grupper kommer i M8, och i prototypen hoppas steget över.
13. **Ön, fri.**
    - *Ser:* ön i dagsljus och kraftsaldot: *Utforskning 40 · Byggkraft 40 · Gryningskraft 10*, plus det som steg 9 gav.
      *Byn fortsätter i sin egen takt. Kom tillbaka när du vill.*
    - *Gör:* tittar runt eller stänger appen.
    - *Känner:* att ha spelat färdigt för i dag.
    - *Simuleringen:* byborna bygger huggarkojan och bryggan under aktiva timmar.

### Integritetsförklaringen

En skärm, före eventuell systemdialog (HLT-03).
Texten gäller Android; på iOS står HealthKit i stället (M6).

```text
┌──────────────────────────────────────────────┐
│  Var ska krafterna komma ifrån?              │
│                                              │
│  Eirora kan läsa tre saker från Health       │
│  Connect: dina steg, dina träningspass och   │
│  hur länge du sover.                         │
│  Av sömnen används bara längden, aldrig hur  │
│  du sov. Ingen plats, ingen puls.            │
│                                              │
│  Uppgifterna räknas om till krafter på din   │
│  telefon. I molnsparningen är de krypterade  │
│  så att bara du kan läsa dem.                │
│  Ingenting delas med andra om du inte själv  │
│  väljer det, vana för vana.                  │
│  De används aldrig till reklam.              │
│  Du kan pausa, se och radera allt när du     │
│  vill.                                       │
│                                              │
│  Vill du hellre skriva in själv fungerar     │
│  spelet precis lika bra.                     │
│                                              │
│  [ Koppla Health Connect ] [ Jag skriver in  │
│                              själv         ] │
│                                              │
│  Läs mer om hur Eirora hanterar data         │
└──────────────────────────────────────────────┘
```

- **Koppla:** spelet frågar om steg, träningspass och sömn var för sig, med minsta nödvändiga behörighet per vana (HLT-03).
  Bakgrundsläsning efterfrågas i samma flöde med förklaringen *Så att krafterna finns redo när byn vaknar.*
  Nekar spelaren en vana används egen inmatning för just den, utan kommentar.
- **Skriva in själv:** en kort visning av inmatningen: steg eller aktiva minuter (D-41), träningspass och sömn.
- *Läs mer* öppnar integritetspolicyn i klartext.
- Valet kan alltid ändras under *Inställningar → Egenvård och integritet*, även under vilan (RST-07).
- Rå hälsodata lämnar aldrig telefonen (HLT-10).
  Molnsparningen innehåller bidrag per spelardygn, end-to-end-krypterade så att servern aldrig kan läsa dem (D-57).

### Gränsfall

- **Ingen egenvård alls, ingen sensor eller ingen förmåga att gå:** hela turen går att genomföra på startkrafterna; inget steg kräver data (princip 6, AC-04).
- **Spelaren nekar allt i systemdialogen:** spelet går vidare till egen inmatning som om spelaren valt den, utan felmeddelande eller ny fråga.
- **Health Connect saknas på telefonen:** *Koppla*-knappen ersätts av *Health Connect finns inte på den här telefonen. Du kan skriva in själv och koppla senare.*
- **Installationen sker under den valda viloperioden**, till exempel 23:30 med viloperiod 23–07: turen genomförs till slut, eftersom schemat börjar gälla först när turen är klar.
  Efter steg 13 övergår ön direkt till nattvyn: *Byn vilar redan i natt. Världen öppnar klockan 7.*
  Vilolåset blir därmed konkret redan första kvällen (D-50).
- **Turen avbryts:** nästa start fortsätter på samma steg.
  Kommandon som redan godkänts står kvar och körs inte igen (INV-D6).
- **Reducerad rörelse:** kameraflykten i steg 3 och nattoningen i steg 10 ersätts av mjuka övertoningar.
- **Befintligt konto på ny telefon:** turen hoppas över; världen och de krypterade bidragen hämtas från molnsparningen (ACC-01, D-57) och spelaren möts av *Välkommen tillbaka till din ö.*, och därefter som i resa 2.

### Verifierar

- **PRD:** UX-03, HLT-03, HLT-07, HLT-12, PRG-01, PRG-02, RST-01, RST-03, UX-05, UX-07, D-15, D-23, D-33, D-49–D-51, D-57, D-58, AC-04.
- **Invarianter:** INV-F2 (startkrafter i starttillståndet), INV-F5, INV-F7, INV-D6, INV-T1.
- **Ärendets acceptanskriterium:** spelaren lär sig de tre krafterna genom att använda dem, utan krav på hälsobehörighet.

## Resa 2: Återkomst efter sju dagars frånvaro

**Mål:** spelaren möts varmt och utan skuld, förstår på under en minut vad som hänt och har något meningsfullt att göra direkt.
Återkomsten ska väcka nyfikenhet, inte dåligt samvete (PRD mål 3).

**Förutsättningar:**

- Resa 1 är genomförd och spelaren har inte öppnat spelet på sju spelardygn.
- Viloperiod 23–07; spelaren öppnar spelet en vardag klockan 18:10.
- Tre varianter av frånvaron:
  - **A, ingen egenvård registrerad,** till exempel sjukdom.
    Det är huvudfallet och skissen nedan.
  - **B, hälsokällan har registrerat egenvård,** till exempel en aktiv semester.
  - **C, spelaren skriver in själv och har varit aktiv** men inte loggat.
- Inga morgonnotiser har skickats om inget nytt hänt.

### Vad simuleringen har gjort

När spelaren öppnar appen spolas världen fram från senaste snapshot i ett steg (INV-D4).
Det ger exakt samma värld som om spelaren haft appen öppen hela veckan, inklusive sju viloperioder.

- Grundtakten har fortsatt under aktiva timmar och långsamma processer hela tiden (INV-T5).
- Byborna har vuxit från 12 till bostadstaket 16; huggarkojan och bryggan är klara.
- Inget har förfallit: inga bybor, byggnader, nivåer, lager eller utforskade brickor har minskat (INV-V1, INV-R3, INV-V4).
- Fullt lager har stoppat produktionen av det som är fullt, utan att något gått förlorat.
- Varje gryning har gett Gryningskraftens grundnivå 40, eftersom sömndata saknas (D-16): 10 + 7 × 40 = 290, under sparandets tak 300.
- Utforskning och Byggkraft står kvar på 40 var; krafter förfaller aldrig (INV-F6).
- I variant B importeras hälsokällans historik för perioden och krediteras per spelardygn som om spelaren varit där, en gång per intervall (INV-B1), inom sparandets tak (D-35).

### Steg

1. **Öppnar appen.**
   - *Ser:* ön direkt, i dagens kvällsljus, med fyra nya bybor på väg mellan husen.
     *Välkommen tillbaka.*
   - *Känner:* att världen har levt vidare och väntat utan krav.
   - *Simuleringen:* framspolningen sker innan ön visas och ska rymmas i laddningsbudgeten (NFR-03); den visas aldrig som en väntan på spelaren.
2. **Daglig rapport: Sedan sist.**
   - *Ser:* rapporten, se [skissen nedan](#skiss-sedan-sist).
   - *Gör:* läser, och kan trycka *Se veckan* för ett kort tidsförlopp över ön där skogen växer och husen fylls.
   - *Känner:* glädje över det som hänt; inget om det som inte hänt.
   - *Simuleringen:* rapporten byggs av händelserna sedan den senast lästa rapporten.
3. **Krafterna.**
   - *Ser:* *Gryningskraft 290 · Utforskning 40 · Byggkraft 40* och *Byn har samlat gryningskraft medan du var borta.*
   - *Känner:* att det finns något att göra, även utan egenvård.
   - *Simuleringen:* inget nytt.
4. **Nästa beslut.**
   - *Ser:* högst tre förslag, till exempel *Ett nytt långhus ger plats för fler.* och *Stenådern väntar på ett stenbrott.*
   - *Gör:* trycker på ett förslag och hamnar på rätt plats på kartan, eller trycker *Till ön*.
   - *Känner:* att ett par minuter räcker för att spela väl (D-34, beslut 4 och 6 i [kärnfantasin](./core-fantasy.md)).
   - *Simuleringen:* vanliga kommandon; inget särskilt återkomstläge.
5. **Spelar som vanligt.**
   - Ingen särskild återkomstmeny, ingen bonusskärm och ingen tutorial som börjar om.

### Skiss: Sedan sist

Variant A, ingen egenvård registrerad.

```text
┌──────────────────────────────────────────────┐
│  Sedan sist                                  │
│                                              │
│  Egenvård                                    │
│   Sömn   ingen registrerad → Gryningskraft   │
│                              40 varje gryning│
│                                              │
│  I världen                                   │
│   · Fyra nya bybor har flyttat in.           │
│     Alla bostäder är fulla.                  │
│   · Bryggan och huggarkojan är klara.        │
│   · Åkern har gett 84 säd.                   │
│   · Förrådet är fullt av timmer, så huggarna │
│     har tagit det lugnt.                     │
│                                              │
│  Krafter att använda                         │
│   Gryningskraft 290 · Utforskning 40         │
│   Byggkraft 40                               │
│                                              │
│  Värt att titta på                           │
│   · Ett nytt långhus ger plats för fler.     │
│   · Stenådern väntar på ett stenbrott.       │
│                                              │
│  [ Se veckan ]                [ Till ön ]    │
│                  Lägg till egenvård          │
└──────────────────────────────────────────────┘
```

Världens händelser i skissen är illustrationer tills ekonomin är beslutad; kraftsiffrorna följer [forces.md](./forces.md).

### Gränsfall

- **Variant B, aktiv vecka med hälsokälla:** med 7 500 steg varje spelardygn blir det 86 Utforskning per dygn, 602 för veckan.
  Saldot går från 40 till 400 med fullt tillskott och sedan till 521 med halvt (D-35).
  Raden lyder *Rörelse 52 500 steg → Utforskning 481*; när ett förråd når taket står det bara *Förrådet av Utforskning är fullt.*, utan uppmaning att spendera.
- **Variant C, aktiv vecka med egen inmatning:** spelaren kan skriva in för varje spelardygn sedan senaste besöket, högst sju ([forces.md](./forces.md#6-manuell-inmatning-hlt-07-d-18), D-53).
  Krafterna krediteras per spelardygn precis som hälsokällans historik, inom sparandets tak, så båda vägarna är likvärdiga (HLT-07).
  Länken *Lägg till egenvård* leder till dygnen sedan senast; rapporten räknar inte dagarna och påpekar inte vad som saknas.
- **Återkomst under viloperioden,** till exempel klockan 01:00: nattvyn visas med rapporten tillgänglig.
  *Välkommen tillbaka. Byn vilar nu och världen öppnar klockan 7.*
  Rapporten går att läsa men inga kommandon går att ge (INV-T3).
- **En expedition kom tillbaka under frånvaron:** fyndet väntar i rapporten och på kartan.
  Inget fynd eller erbjudande går ut med tiden (princip 11).
- **Gruppen (från M8):** *Gruppen har byggt vidare på fyren.* och väntande hälsningar som *Ada: Lycka till med bygget.*
  Aldrig vem som inte bidragit, och spelarens andel i gemensamma byggen är oförändrad (SOC-04, AC-12).
- **Notiser under frånvaron (D-56):** morgonnotisen skickas bara när något nytt har hänt.
  Efter två morgonnotiser i följd som inte öppnats pausas de tills spelaren öppnar spelet igen, så en vecka borta ger högst två notiser.
- **Mycket lång frånvaro,** till exempel ett halvår: samma resa.
  Rapporten visar de fem viktigaste händelserna och *och 23 saker till* som går att fälla ut; världen är minst lika utvecklad som före (INV-V4).
- **Appen har uppdaterats under frånvaron:** sparningen migreras (NFR-09); rapporten nämner bara det som är nytt för spelaren, under *Nytt i Eirora*.
- **Resa i annan tidszon:** viloperioden följer telefonens lokala tid (D-37); resten av reglerna avgörs i O-07.

### Återstartsbonus (O-12)

Beslutet är **ingen särskild återstartsbonus** (D-52).

- Den sparade Gryningskraften och en värld som vuxit av sig själv är redan ett varmt mottagande.
- En bonus i krafter skulle kräva ett undantag från INV-F2, att bara egenvård skapar krafter.
- En bonus för att ha varit borta riskerar att belöna frånvaro, eller att kännas som tröst och därmed påminna om frånvaron.


### Verifierar

- **PRD:** AC-02, D-08, D-16, D-17, D-35, HLT-06, CIV-04, CIV-06, UX-02, UX-07, princip 4 och 11; SOC-04 och AC-12 från M8.
- **Invarianter:** INV-D4, INV-V1, INV-V4, INV-R3, INV-F4, INV-F6, INV-B1, INV-T3, INV-T5.
- **Ärendets acceptanskriterium:** efter sju dagars frånvaro möts spelaren varmt och utan skuld.

## Resa 3: Kväll med vilolås

**Mål:** vilan stänger kvällen tydligt och vänligt, spelaren vet vad som går och inte går, och gryningen ger en anledning att titta in utan press (JTBD 5, D-22).

**Förutsättningar:**

- Viloperiod 23:00–07:00, satt för mer än 24 timmar sedan.
- Morgonnotisen är påslagen.
- Spelaren har läst morgonens rapport och öppnar spelet en fredag klockan 21:30.

### Steg

1. **21:30 Kvällens beslut.**
   - *Ser:* ön i kvällsljus.
   - *Gör:* skickar en expedition från bryggan och lägger ett sågverk i byggkön.
   - *Känner:* att kvällen är en naturlig planeringsstund.
   - *Simuleringen:* två spelpåverkande kommandon godkänns och kostar Utforskning respektive Byggkraft (INV-F5).
2. **22:30 Skymningsbeskedet.**
   - *Ser:* om appen är öppen, en lugn rad överst medan ljuset blir varmare: *Byn gör sig redo för natten om en halvtimme.*
   - *Känner:* framförhållning, inte stress; det finns ingen nedräkning.
   - *Simuleringen:* inget; beskedet är bara presentation och skickas aldrig som notis (UX-07).
3. **23:00 Byn går till vila.**
   - *Ser:* lyktor tänds i fönstren, byborna går hem, ljudet dämpas och nattvyn tonar fram, se [skissen nedan](#skiss-nattvy).
     *Byn vilar. Världen öppnar klockan 7.*
   - *Känner:* att spelet tydligt stänger för kvällen.
   - *Simuleringen:* fasen går från `ACTIVE` till `RESTING` enbart av schema och tid (INV-T1, INV-T2).
     Byborna slutar arbeta; expeditionen ligger för ankar och bygget står still (INV-T4).
     Skogen, säden och ullen fortsätter att växa (INV-T5).
4. **23:05 Försöker bygga.**
   - *Gör:* trycker på sågverkets byggplats.
   - *Ser:* informationen om bygget som vanligt, men handlingsknapparna är ersatta av *Byborna sover. Det här kan du göra i morgon.*
   - *Känner:* ett vänligt nej, inget fel.
   - *Simuleringen:* om ett kommando ändå skickas nekas det utan sidoeffekter med orsaken att viloperioden pågår (INV-D7, INV-T3, AC-01).
5. **23:10 Skriver in kvällspromenaden.**
   - *Gör:* *Lägg till egenvård → Rörelse → 30 aktiva minuter*.
   - *Ser:* *Sparat. Det blir kraft när byn vaknar.*
   - *Simuleringen:* bidraget, 3 000 steg enligt D-41, hör till det pågående spelardygnet och krediteras senast vid gryningen (INV-T9).
     Det är ett bidrag, inte ett spelpåverkande kommando; under vilan har manuell inmatning samma villkor som bakgrundssynken (RST-05, HLT-07, D-54).
6. **23:12 Ändrar viloperioden.**
   - *Gör:* *Inställningar → Viloperiod*, flyttar till 22:30–06:30.
   - *Ser:* *Den nya tiden gäller från söndag kväll klockan 22:30. I natt och i morgon natt vilar byn som vanligt, 23–07.*
   - *Känner:* att regeln är tydlig och förutsägbar.
   - *Simuleringen:* ett administrativt kommando godkänns.
     Ändringen gäller tidigast lördag 23:12; då pågår lördagens viloperiod, som aldrig kortas, så den nya tiden börjar söndag (INV-T7, RST-09, AC-05).
     Texten räknas fram av simuleringen, aldrig skrivs för hand.
7. **23:20 Stänger appen.**
   - Inget mer händer i telefonen den här kvällen; inga notiser under vilan (RST-06).
8. **03:10 Vaken mitt i natten.**
   - *Gör:* öppnar appen.
   - *Ser:* nattvyn med dämpad ljusstyrka och samma text som vid 23:00.
     Ingenting om sömn, klockslag eller att spelaren borde sova.
   - *Känner:* lugn; spelet dömer inte.
   - *Simuleringen:* inget; att titta är alltid tillåtet.
9. **07:00 Gryning.**
   - *Simuleringen:* fasen går till `DAWN` och sedan `ACTIVE`, i denna ordning [FÖRSLAG]:
     1. Gryningskraftens grundnivå krediteras, och sömn som redan synkats fyller på upp till kurvans värde ([forces.md](./forces.md#2-spelardygnet)).
     2. Bidrag som kommit in under vilan krediteras (INV-T9).
     3. Byborna äter och byn växer enligt ekonomins regler.
     4. Den dagliga rapporten byggs.
     5. Morgonnotisen skickas om något nytt har hänt.
   - *Ser, på låsskärmen:* *Byn har vaknat. Expeditionen har siktat land i nordväst.*
     Notisen kommer utan ljud och vibration (Androids tysta notiskanal, iOS `passive`; D-56), så att den aldrig väcker någon.
10. **07:40 Morgonen.**
    - *Gör:* öppnar appen.
    - *Ser:* en kort gryningsövergång över ön, första gången efter gryningen, och sedan den dagliga rapporten, se [skissen nedan](#skiss-daglig-rapport).
    - *Känner:* att natten gav något, och nyfikenhet på den nya ön.
    - *Gör sedan:* läser och riktar dagens krafter.

### Vad som går och inte går under vilan

| Under viloperioden | Går | Regel |
|---|---|---|
| Titta på ön, röra kameran, trycka på saker för information | Ja | D-23 |
| Läsa daglig rapport och historik | Ja | |
| Bygga, placera, uppgradera, riva | Nej | INV-T3 |
| Använda krafter: dagsverke, kartläggning, expedition | Nej | INV-T3 |
| Planera: prioriteringar, bemanning, minimilager, vägar | Nej; planering är gratis men ändrar världen | RST-02 |
| Handel, hälsningar och andra gruppåtgärder | Nej; servern nekar | RST-02 |
| Ta emot gruppens interaktioner | Köas till gryningen | RST-11 |
| Synka egenvård i bakgrunden | Ja; krafter vid gryningen | RST-05, INV-T9 |
| Skriva in egenvård | Ja; krafter vid gryningen | HLT-07, D-54 |
| Ändra viloperioden | Ja; gäller efter 24 h | RST-09, INV-T7 |
| Inställningar, tillgänglighet, integritet, hjälp, konto, radering | Ja | RST-07 |
| Hantera eller avsluta prenumeration | Ja, under konto | RST-07 |
| Köpa kosmetika | Nej | RST-08, D-55 |
| Notiser | Inga | RST-06 |
| Väcka byn tidigare | Nej; ingen knapp, inget köp, inget sömnbevis | RST-08–RST-10 |

### Skiss: Nattvy

```text
┌──────────────────────────────────────────────┐
│                                       ☾      │
│      (ön i månsken, lyktor i fönstren,       │
│       norrsken långsamt över havet)          │
│                                              │
│  Byn vilar.                                  │
│  Världen öppnar klockan 7.                   │
│                                              │
│  Medan byn sover                             │
│   · Säden mognar.                            │
│   · Skogen vid huggarkojan växer tillbaka.   │
│   · Expeditionen ligger för ankar.           │
│                                              │
│  [ Rapport ]  [ Historik ]  [ Inställningar ]│
└──────────────────────────────────────────────┘
```

- Raderna under *Medan byn sover* visar långsamma processer som pågår just nu i världen (RST-03, INV-T5).
- Nattens budskap växlar mellan några lugna rader om världen, till exempel *Det är stilla i hamnen.*, aldrig om spelarens sömn eller hälsa.
- Nattvyn har mörka färger men full textkontrast (UX-05); ljud spelas bara om telefonen inte är tystad och aldrig högre än dagens.
- Hur norrsken, måne och ljus ser ut avgörs i art direction (TASK-006).

### Skiss: Daglig rapport

Spelaren gick 7 800 steg, gjorde ett yogapass på 25 minuter, veckans andra, och sov 7 h 10 min.
Kvällspromenaden från steg 5 ingår i stegen.

```text
┌──────────────────────────────────────────────┐
│  God morgon                                  │
│                                              │
│  Egenvård                                    │
│   Rörelse  7 800 steg   → Utforskning    88  │
│   Träning  1 pass       → Byggkraft      90  │
│   Sömn     7 h 10 min   → Gryningskraft  96  │
│                                              │
│  I världen                                   │
│   · Expeditionen har siktat land i nordväst. │
│   · Sågverket är halvvägs.                   │
│   · Säden har mognat i natt.                 │
│                                              │
│  Värt att titta på                           │
│   · Den nya ön har bete.                     │
│   · Stenbrottet hinner inte med sågverket.   │
│                                              │
│                               [ Till ön ]    │
│                  Lägg till egenvård          │
└──────────────────────────────────────────────┘
```

- Siffrorna följer [forces.md](./forces.md): 7 800 steg ger 60 + 3,8 × 7,5 = 88, veckans andra pass ger 90 och 7 h 10 min ger 96.
- Varje egenvårdsrad går att trycka på för att se och ändra källan; ingen rad visar om bidraget är inmatat eller uppmätt.
- Rapporten följer UX-02; gruppens framsteg läggs till i M8.

### Gränsfall

- **Ett kommando vid övergången:** ett kommando som tillämpas före 23:00 lokalt godkänns, ett som tillämpas efter nekas; inget blir till hälften utfört (INV-D7, INV-D8).
- **En påbörjad placering vid övergången [FÖRSLAG]:** en obekräftad placering sparas som ett utkast i klienten och visas vid gryningen för bekräftelse.
  Utkastet är inte ett kommando och kan inte skapas under vilan; det finns bara för att inget spelaren påbörjat ska försvinna.
- **Gruppåtgärd köad offline före 23:00, uppkoppling under vilan [FÖRSLAG]:** klienten håller kvar åtgärden till gryningen och skickar den då, och servern validerar den på nytt.
  Om den inte längre går att utföra står det i rapporten, utan förlust (AC-08, RST-02).
- **Appen i bakgrunden vid 23:00:** nästa öppning visar nattvyn direkt, med en kort övertoning.
- **Spelaren lägger sig tidigare:** stänger bara appen; byborna arbetar till 23:00 som vanligt.
- **Skiftarbete,** till exempel viloperiod 08–16: samma resa på andra klockslag, och nattvyn visas under viloperioden oavsett tid på dygnet.
  Hur världens dygnsljus följer schemat avgörs i art direction (TASK-006).
- **Övergång till sommartid eller normaltid under natten:** byn vilar till 07:00 lokal tid, även om vilan då blir 7 eller 9 verkliga timmar (D-37, INV-T6).
- **Tidszonsbyte under vilan,** till exempel en flygresa: pågående vila ändras inte (INV-T8).
  Öppningstiden visas i den nya lokala tiden: *Världen öppnar klockan 8 (lokal tid).*
- **Sömndata saknas eller kommer sent:** vid gryningen finns grundnivån 40.
  Raden lyder *Sömn ingen registrerad än → Gryningskraft 40*, och när data kommer fylls det på: *Sömnen kom in: +56 Gryningskraft.*
  Ingen inloggad sömn ger mindre än grundnivån (INV-F4).
- **Ändrad klocka på telefonen:** accepterad risk som bara drabbar spelaren själv (ADR-0003); spelet varnar inte och misstänkliggör ingen.
- **Reducerad rörelse:** lyktor, byborna och gryningen visas som mjuka övertoningar.

### Verifierar

- **PRD:** AC-01, AC-05, AC-08, RST-01–RST-11, UX-04, UX-05, UX-07, D-22, D-33, D-37, HLT-07, princip 3.
- **Invarianter:** INV-D7, INV-D8, INV-F4, INV-T1–INV-T9.

## Resorna som testscenarier

Varje resa blir ett automatiserat scenario i headless-simuleringen med virtuell klocka och fake health provider (NFR-11), från M1 för resa 1 och 3 och från M5 för hela resa 2.

| Resa | Scenario | Kontrollerar |
|---|---|---|
| 1 | Turen med alla hälsobehörigheter nekade | Alla tre krafthandlingar godkänns på startkrafterna; saldot efteråt är 40/40/10 |
| 1 | Installation 23:30 med viloperiod 23–07 | Turens kommandon godkänns; fasen är `RESTING` direkt efter turen |
| 2 | Sju spelardygn utan bidrag och kommandon | INV-V4; Gryningskraft 290; rapporten innehåller inga rader om saknad egenvård utöver sömnens grundnivå |
| 2 | Sju spelardygn med 7 500 steg per dygn, importerade efteråt i slumpad ordning med dubbletter | Utforskning 521 (INV-B1, D-35) |
| 3 | Kommandon av varje spelpåverkande typ var femte minut 22:55–07:05 | Godkända före 23:00 och från 07:00, nekade däremellan utan sidoeffekter |
| 3 | Schemaändring fredag 23:12 till 22:30–06:30 | Vilan lördag är 23–07; första vilan med nya tiden börjar söndag 22:30 |
| 3 | Sommartidsövergång och tidszonsbyte under vilan | Vilan slutar på schemats lokala klockslag; pågående vila ändras inte |

## Beslut

Produktägarens svar på de öppna frågorna i v0.1, loggade i PRD 1.1:

- **D-49:** första sessionen börjar med den guidade turen; hälsokälla och viloperiod väljs efteråt, och Utforskning visas med *kartlägg närområdet* (PRD 4.4).
- **D-50:** den första viloperioden gäller direkt efter turen, även om installationen sker under viloperioden.
- **D-51:** det första spelardygnet börjar vid lokal midnatt på installationsdagen.
- **D-52:** ingen återstartsbonus efter frånvaro (O-12).
- **D-53:** manuell inmatning kan göras för varje spelardygn sedan senaste besöket, högst sju.
- **D-54:** manuell inmatning är tillåten under vilan; krafterna krediteras vid gryningen.
- **D-55:** köp av kosmetika är stängda under vilan; prenumerationen går alltid att hantera.
- **D-56:** morgonnotisen kommer bara när något nytt har hänt, alltid tyst, och pausas efter två oöppnade i följd.
- **D-57:** molnsparningen innehåller bidrag per spelardygn, end-to-end-krypterade så att servern aldrig kan läsa dem.
- **D-58:** åldersgränsen 18+ sköts av butikernas åldersklassning, utan egen bekräftelse i spelet.

Kvar att avgöra: **guiden**, vem eller vad som hälsar spelaren välkommen, avgörs med art direction (TASK-006).
