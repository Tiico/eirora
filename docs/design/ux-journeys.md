# UX-resor

> **Status:** [FÖRSLAG] v0.1, väntar på granskning av produktägaren  
> **Ärende:** TASK-005 · **Milstolpe:** M0 · **PRD:** UX-02, UX-03, UX-05, UX-07, RST-03–RST-09, PRG-01, HLT-03, HLT-12, D-33, D-37, O-12  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Siffror:** [forces.md](./forces.md), [economy-v1.md](./economy-v1.md)

Tre resor steg för steg: första sessionen, återkomst efter sju dagars frånvaro och en kväll när vilolåset börjar.
Texterna i spelet är utkast på svenska; den engelska versionen skrivs parallellt (UX-06).
Skärmarna beskrivs i ord och skisser; utseendet bestäms i art direction (TASK-006) och M9.

## Tonläge för alla texter

- **Lugnt och varmt.**
  Spelet konstaterar och bjuder in; det manar aldrig på.
- **Aldrig skuld.**
  Inga ord som *missat*, *glömt*, *svit*, *förlorat*, *bara* eller *äntligen*, och ingen räkning av dagar borta.
- **Aldrig kroppen i fokus.**
  Spelet beskriver vad världen fick, inte hur bra spelaren var; inga betyg, inga jämförelser och inga medicinska påståenden.
- **Ingen misstro.**
  I den egna världen visas aldrig om ett bidrag är verifierat eller egenrapporterat, eftersom det inte spelar någon roll där (INV-F7, R-04).

## Resa 1: Första sessionen (5–8 minuter)

**Mål:** spelaren förstår de tre krafterna genom att använda dem, väljer sin viloperiod och väljer källa för egenvården, utan att någon hälsobehörighet krävs (UX-03, HLT-12).

| # | Skärm | Spelaren gör | Spelet säger (utkast) | Varför |
|---|---|---|---|---|
| 1 | Titel i gryningsljus, havet och en ö | Trycker *Börja* | *Eirora. A world worth growing.* | Det första intrycket ska vara vackert, inte ett formulär (D-29) |
| 2 | Språk och tillgänglighet | Bekräftar förval från telefonen | *Välj hur du vill att Eirora ska se ut och låta.* | Textstorlek, kontrast och reducerad rörelse före allt annat (UX-05) |
| 3 | Viloperiod | Drar i ett dygnsur, 6–10 h (förval 23–07) | *När du vilar, vilar byn. Då kan du titta, men inte ge order.* | Vilan är en spelregel från första minuten (princip 3) |
| 4 | Ön vid gryning; guiden hälsar | Tittar | *Byn har vaknat. Varje morgon får den kraft av det du gjort för dig själv. I dag får du en gåva att börja med.* | Startkrafterna 100/100/40 gör att allt kan läras utan data |
| 5 | **Gryningskraft** | Väljer dagsverke på fiskeboden (30) | *Gryningskraft kommer av sömn. Använd den för att ge byn en extra god dag där den behövs.* | Fisken syns öka direkt |
| 6 | **Utforskning** | Kartlägger närområdet (60) | *Utforskning kommer av rörelse. Den lyfter dimman och hittar nya platser.* | Dimman lättar, urskogen och stenådern syns |
| 7 | Planering | Placerar en huggarkoja i urskogen | *Att planera och bygga smått kostar ingen kraft. Rika platser ger mer.* | Visar att strategi alltid är gratis (D-23) och att platsen spelar roll |
| 8 | **Byggkraft** | Bygger en brygga (60) | *Byggkraft kommer av träning. Den räcker till de stora stegen. Från bryggan kan en expedition en dag segla mot det okända.* | Ger ett mål några dagar bort, utan tidspress |
| 9 | **Var ska krafterna komma ifrån?** | Väljer hälsokälla eller egen inmatning | Se *Integritetsförklaringen* nedan | Frågan ställs först när spelaren har sett vad krafterna gör |
| 10 | Förhandsvisning av natten | Ser ön i nattljus en kort stund | *Klockan 23 vilar byn till klockan 7. Skogen växer och säden mognar ändå.* | Inga överraskningar första kvällen (RST-03) |
| 11 | Morgonhälsning | Väljer *Ja* eller *Nej tack*, båda lika stora | *Vill du ha en tyst hälsning när byn vaknar? Högst en om dagen.* | Notiser är valbara och få (D-33, UX-07) |
| 12 | Vänner | *Bjud in* eller *Senare*, båda lika stora | *Eirora kan spelas tillsammans med 1–5 vänner. Det går att göra när som helst.* | Inget tryck; visas en gång (grupper kommer i M8) |
| 13 | Ön, fri | Tittar runt eller stänger | *Världen fortsätter i sin takt. Kom tillbaka när du vill.* | Sessionen får ett tydligt slut |

Efter turen har spelaren 40 Utforskning, 40 Byggkraft och 10 Gryningskraft kvar, en brygga, en ny huggarkoja och två nya platser.

### Integritetsförklaringen (steg 9)

En skärm, före eventuell systemdialog (HLT-03).
De två valen har samma storlek och vikt.

> **Var ska krafterna komma ifrån?**
>
> Eirora kan läsa tre saker från Health Connect: hur många steg du tar, vilka träningspass du gör och hur länge du sover.
> Inget annat: ingen plats, ingen puls och ingen sömnkvalitet.
>
> Allt stannar på din telefon.
> Ingen annan ser något om du inte själv väljer att dela det med din grupp, vana för vana.
> Du kan pausa, se och radera allt när du vill.
>
> Vill du hellre skriva in själv fungerar spelet precis lika bra.
>
> **[ Koppla Health Connect ]**  **[ Jag skriver in själv ]**

- **Koppla:** spelet frågar om steg, träningspass och sömn var för sig.
  Bakgrundsläsning efterfrågas i samma flöde med förklaringen *så att krafterna finns redo vid gryningen*.
  Nekar spelaren något används egen inmatning för den vanan, utan kommentar.
- **Skriva in själv:** en kort visning av inmatningen: steg eller aktiva minuter, träningspass och sömn (D-41).
- Valet kan alltid ändras under *Inställningar → Egenvård*, även under vilan (RST-07).
- På iOS gäller samma text med HealthKit (M6).

## Resa 2: Återkomst efter sju dagars frånvaro

**Utgångsläge:** spelaren har varken öppnat spelet eller haft någon egenvård registrerad på sju spelardygn.
Enligt [ekonomikalkylen](./economy-v1.md) har byn då växt till bostadstaket, grundtakten har fortsatt och inget har förfallit.
Saknad sömndata har gett grundnivån 40 Gryningskraft varje spelardygn.

| # | Skärm | Spelaren gör | Spelet säger (utkast) | Varför |
|---|---|---|---|---|
| 1 | Ön i aktuellt dygnsljus | Öppnar appen | *Välkommen tillbaka.* | Ingen räkning av dagar, inget utropstecken |
| 2 | **Sedan sist**, en sammanfattning i stället för sju dagliga rapporter | Bläddrar | Se exemplet nedan | Bara det som hänt; inget om det som inte hänt |
| 3 | Krafter | Ser saldot | *Byn har samlat gryningskraft medan du var borta.* | Det finns något meningsfullt att göra direkt |
| 4 | Nästa beslut | Väljer ett av högst tre förslag, eller inget | *Det här kan vara värt att titta på.* | Förslag, inte uppgifter; inga brickor eller räknare |
| 5 | Ön, fri | Spelar som vanligt | | Ingen särskild återkomstmeny |

Om en hälsokälla är kopplad läses historiken för perioden in, och krafterna krediteras per spelardygn som om spelaren varit där, inom sparandets tak (D-35).
Når ett förråd taket säger spelet bara *Förrådet av Utforskning är fullt*, utan uppmaning att använda det.

### Exempel: Sedan sist

```text
┌──────────────────────────────────────────────┐
│  Sedan sist                                  │
│                                              │
│  Byn                                         │
│   · Fyra nya bybor har flyttat in.           │
│     Långhuset är nu fullt.                   │
│   · Åkern har gett 84 säd.                   │
│   · Skogen vid byn har vuxit tillbaka.       │
│                                              │
│  Krafter att använda                         │
│   Gryningskraft  290                         │
│   Utforskning     40                         │
│   Byggkraft       40                         │
│                                              │
│  Det här kan vara värt att titta på          │
│   · Ett nytt långhus ger plats för fler.     │
│   · Stenådern väntar på ett stenbrott.       │
│                                              │
│                     [ Till ön ]              │
└──────────────────────────────────────────────┘
```

### Återstartsbonus (O-12)

Förslaget är **ingen särskild bonus** i prototypen.
Den sparade Gryningskraften och en värld som vuxit av sig själv räcker som varmt mottagande.
En bonus för att ha varit borta riskerar att belöna frånvaro eller kännas som tröst, och det prövas hellre i prototypen än byggs in från början.

## Resa 3: Kväll med vilolås

**Utgångsläge:** viloperioden är 23–07 (lokal tid, D-37).
Spelaren öppnar spelet 21:30.

| Tid | Skärm | Spelaren gör | Spelet säger (utkast) | Regel |
|---|---|---|---|---|
| 21:30 | Ön i skymningsljus; kvällens rapport | Startar ett sågverk, köar ett långhus, skickar en expedition | | Kvällen är en naturlig planeringsstund |
| 22:30 | En lugn rad överst; ljuset blir varmare | Fortsätter eller stänger | *Byn gör sig redo för natten om en halvtimme.* | Bara i appen; ingen notis (RST-06) |
| 23:00 | Övergång till natt: lyktor tänds, byborna går hem, ljudet dämpas | Tittar | *Byn vilar. Världen öppnar klockan 7.* | Faser enligt INV-T2; reducerad rörelse ger en mjuk toning |
| 23:05 | Nattvy | Trycker på *Bygg* | *Byborna sover. Det här kan du göra i morgon.* | Kommandot nekas utan sidoeffekter (INV-D7, INV-T3) |
| 23:10 | Inställningar | Flyttar viloperioden till 00–08 | *Ändringen gäller från i övermorgon natt. I natt vilar byn som vanligt.* | Gäller efter 24 h; pågående vila kortas aldrig (INV-T7) |
| 07:00 | Gryningsövergång | Sover; ingen notis före gryningen | | Inga notiser under vilan (RST-06) |
| 07:00 | Valfri morgonhälsning, utan ljud | | *Byn har vaknat. Expeditionen är tillbaka.* | Högst en om dagen (UX-07) |
| 07:40 | Gryningsövergång i appen, sedan dagens rapport | Läser och riktar dagens krafter | Se exemplet nedan | Krafterna finns vid gryningen (RST-05) |

**Under vilan är detta öppet:** att titta på ön och röra kameran, läsa rapporter och historik, inställningar, viloschema, egenvård och integritet, hjälp, konto och radering (RST-07).
**Under vilan står detta still:** bygge, produktion, transport och expeditioner, eftersom byborna sover (INV-T4).
**Under vilan fortsätter:** skogen, säden och ullen växer (INV-T5), och egenvård kan synkas i bakgrunden.

Ett kommando som påbörjats före 23:00 men bekräftas efter nekas som alla andra.
En bekräftelsedialog som är öppen vid övergången stängs med samma vänliga besked.

### Exempel: Nattvy

```text
┌──────────────────────────────────────────────┐
│                                       ☾      │
│        (ön i månsken, lyktor i fönstren)     │
│                                              │
│  Byn vilar.                                  │
│  Världen öppnar klockan 7.                   │
│                                              │
│  Medan byn sover                             │
│   · Säden mognar.                            │
│   · Skogen vid Urskogen växer.               │
│   · Expeditionen ligger för ankar.           │
│                                              │
│  [ Rapport ]  [ Historik ]  [ Inställningar ]│
└──────────────────────────────────────────────┘
```

Nattens budskap växlar mellan några lugna rader om världen, aldrig om spelarens sömn eller hälsa.

### Exempel: Daglig rapport efter natten

Spelaren gick 7 800 steg, gjorde ett yogapass på 25 minuter (veckans andra) och sov 7 h 10 min.

```text
┌──────────────────────────────────────────────┐
│  God morgon                                  │
│                                              │
│  I går och i natt                            │
│   Rörelse   7 800 steg   → Utforskning    88 │
│   Träning   1 pass       → Byggkraft      90 │
│   Sömn      7 h 10 min   → Gryningskraft  96 │
│                                              │
│  I världen                                   │
│   · Expeditionen hittade en ö i nordväst.    │
│   · Sågverket gav 16 plank.                  │
│   · Långhuset är halvvägs.                   │
│                                              │
│  Det här kan vara värt att titta på          │
│   · Stenbrottet hinner inte med sågverket.   │
│   · Den nya ön har bete.                     │
│                                              │
│                     [ Till ön ]              │
└──────────────────────────────────────────────┘
```

Rapporten följer UX-02: egenvårdens effekt, händelser, fynd och nästa beslut.
Gruppens framsteg läggs till i M8.
Varje rad om egenvård går att trycka på för att se och ändra källan; ingen rad visar om bidraget är inmatat eller uppmätt.

## Öppna frågor till produktägaren

1. **Ordningen i första sessionen:** förslaget flyttar valet av hälsokälla från steg 2 i PRD 4.4 till efter den guidade turen, så att spelaren vet vad krafterna gör innan behörighetsfrågan.
   Godkänns det, uppdateras PRD 4.4.
2. **Första viloperioden:** ska den valda viloperioden gälla redan första natten?
   Förslaget är ja, eftersom 24-timmarsregeln skyddar mot ändringar i stunden och inte mot det första valet.
3. **Återstartsbonus (O-12):** förslaget är ingen bonus i prototypen.
4. **Morgonhälsningen:** ska den vara avstängd som förval och alltid komma utan ljud?
5. **Guiden:** vem eller vad hälsar spelaren välkommen?
   Avgörs tillsammans med art direction (TASK-006).
