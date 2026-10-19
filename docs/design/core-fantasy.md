# Kärnfantasi: varför egenvård som motor blir ett bra strategispel

> **Status:** [BESLUTAT] v1.0, godkänd av produktägaren vid grind A0 (D-82)  
> **Ärende:** TASK-001 · **Milstolpe:** M0 · **PRD:** princip 1, D-12, D-14, D-23, R-01, AC-06  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md)

## Fantasin i en mening

**Du vakar över en liten nordisk ögrupp som vaknar varje gryning med det du gav dig själv i går, och vad den blir avgör du.**

Egenvården är inte målet i spelet, den är bränslet.
Spelet handlar om vad spelaren gör med det.

## Två sätt att misslyckas

Eirora kan haverera åt två håll, och designen måste ligga mellan dem:

| Misslyckande | Kännetecken | Vad spelaren säger |
|---|---|---|
| **Stegräknare med dekor** (R-01) | Mängden egenvård avgör utfallet; besluten är bara knappar för att spendera | "Jag gick mer, så min värld blev större. Det var allt." |
| **Strategispel med hälsoklistermärke** | Egenvården märks inte; ett skickligt val slår alltid en aktiv dag | "Jag kan lika gärna mata in lite och spela smart." |

Det första bryter princip 1 och gör spelet tomt efter en vecka.
Det andra bryter D-14 och mål 4 ("spegeln känns") och gör produktlöftet falskt.

## Egenvården ger budgeten, strategin avgör avkastningen

Kärnidén är samma som skillnaden mellan inkomst och investering.
Egenvården avgör **hur mycket** kraft spelaren har.
Spelarens beslut avgör **vad varje enhet kraft är värd**.

Tre egenskaper gör att besluten väger tyngre än mängden:

1. **Egenvården växer linjärt med tak, besluten växer med ränta på ränta.**
   Avtagande avkastning och mjuka tak (HLT-05) gör att en extra timme sömn eller tiotusen extra steg ger allt mindre.
   Ett bra beslut, till exempel en hamn som öppnar en sjöväg, fortsätter däremot att ge avkastning varje dag i världens ekonomi.
2. **Krafterna är olika slags makt, inte en valuta** (princip 5).
   Utforskning svarar på *var*, Byggkraft på *vad* och Gryningskraft på *hur väl i dag*.
   De kan inte växlas mot varandra, så pusslet är att få dem att samverka.
   En spelare med mycket Utforskning men lite Byggkraft hittar öar som ännu inte går att exploatera, och måste välja vilka fynd som får vänta.
3. **Ekonomin är rumslig och sammankopplad.**
   Samma byggnad är värd olika mycket beroende på var den står, vad den ligger nära och om varorna når fram (CIV-02, LOG-02).
   Därför ger samma Byggkraft helt olika utfall hos två spelare.

**Designmål (D-34):** inom normalt spann av egenvård ska skillnaden mellan ett genomtänkt och ett slarvigt beslut vara större än skillnaden mellan en låg och en hög egenvårdsdag.
Mer egenvård ska ändå alltid synas i världen när strategin är densamma, annars dör spegeln.

## Spelarens viktigaste beslut

Inget av besluten nedan avgörs av hur mycket egenvård spelaren har haft; mängden avgör bara hur många av dem som kan genomföras.

| # | Beslut | Avvägning | Varför mängden inte avgör |
|---|---|---|---|
| 1 | **Vart Utforskning riktas** | En lång expedition mot en ny ö (stor möjlighet, lång horisont) *eller* kartläggning av närområdet (säkra resurser nu) | Rätt svar beror på vad världen saknar, inte på hur mycket Utforskning som finns |
| 2 | **Vad Byggkraft läggs på** | Produktion (ett sågverk), infrastruktur (en hamn eller väg) eller gruppens gemensamma bygge | Tillväxt nu, tillväxt senare eller tillväxt tillsammans är tre olika strategier |
| 3 | **Var det byggs** | Nära råvaran, nära bosättningen eller nära kusten | Placering och närhet avgör värdet; fel plats kostar varje dag |
| 4 | **Hur Gryningskraften används på morgonen** | Vilken flaskhals som ska lösas i dag; vilken arbetsgrupp bland byborna som prioriteras | Det är ett diagnosbeslut: spelaren måste läsa sin ekonomi |
| 5 | **Vilken forskningsinriktning som följs** | Produktion, sjöfart/logistik eller kartläggning/kultur (RES-01) | Forskning ändrar vad krafterna är värda, inte hur många de är |
| 6 | **Hur världen specialiseras och knyts ihop** | Vilken ö gör vad, vilka sjövägar som prioriteras | Planering är alltid gratis (D-23); här kan även en spelare utan egenvård i dag göra stor skillnad |

Beslut 6 är särskilt viktigt för inkludering: en spelare som är sjuk eller vilar kan fortfarande spela väl.

## Anno × Islanders × Pikmin Bloom (D-12)

| Förebild | Vad vi tar | Vad vi inte tar |
|---|---|---|
| **Anno** | Produktionskedjor, lager, logistik, specialiserade öar, sjöfart och handel; ekonomin lever sitt eget liv | Mikrostyrning i realtid, strid, missnöje och förfallsspiraler (D-08, D-17) |
| **Islanders** | Få men tunga beslut, placering där närhet ger värde, lugn skönhet, att varje drag syns direkt | Game over och omstart; i Eirora nollställs ingenting (D-13) |
| **Pikmin Bloom** | Dygnsrytmen, att vardagens rörelse blir något i världen, mjuk gemenskap, en dagssammanfattning | GPS och plats (D-24), samlande utan strategi |

Tillsammans blir det ett dygn med fast form:

- **Dag:** livet levs; bidragen samlas i bakgrunden.
- **Kväll eller morgon, 2–5 minuter:** rapporten läses, krafterna riktas, ett par tunga beslut fattas.
- **Viloperiod:** byborna sover, långsamma processer fortgår, inga kommandon.
- **Gryning:** världen visar vad besluten och egenvården gav.

Vilolåset och den korta sessionen gör strategin starkare, inte svagare.
När spelaren bara fattar ett par beslut om dagen väger varje beslut tyngre, precis som i Islanders.

## Så motverkas R-01

Varje skydd nedan går att verifiera:

1. **Avtagande avkastning och mjuka tak på alla tre krafter** (HLT-05, TASK-003).
   Verifieras med räkneexempel och unit-tester: ingen får obegränsad fördel av extrem egenvård.
2. **Ingen gemensam valuta.**
   Krafterna kan aldrig växlas mot varandra eller mot resurser.
   Verifieras som invariant i simuleringen (TASK-002).
3. **Varje kraftutgift har minst två rimliga alternativ med olika horisont.**
   Ekonomi v1 (TASK-004) ska visa minst tre sådana beslut om var krafterna ska riktas.
4. **Planering är alltid gratis** (D-23).
   Logistik, specialisering och prioritering kostar ingen kraft, så strategi kan utövas varje dag oavsett egenvård.
5. **Balansbotar** (M4/M5): två botar med identisk egenvård och olika strategier ska få mätbart olika världar (AC-06).
   En bot med måttlig egenvård och god strategi ska kunna mäta sig med en bot med hög egenvård och naiv strategi (D-34).
6. **Grind A5:** en extern testare kan förklara hur egenvård blir krafter och krafter blir beslut, och spelet är roligt även med enbart manuell inmatning.

## Hypoteser för prototypen (M1)

Dessa avgörs genom dagligt spel och skrivs in i [prototypdagboken](./prototype-diary.md) (TASK-009); läget per vecka förs i [lärdomsloggen](./prototype-learnings.md):

- **H1 Beslutet är verkligt:** det är sällan uppenbart vart krafterna ska riktas.
- **H2 Taket känns inte som straff:** en mycket aktiv dag känns bra även när avkastningen avtar.
- **H3 Spegeln syns:** spelaren ser skillnad i världen mellan en aktiv och en lugn vecka.
- **H4 Kort räcker:** 2–5 minuter per dag ger känslan av att ha spelat på riktigt.
- **H5 Vilan känns som en gåva:** kvällens stängning upplevs lugn och inte som ett hinder (D-48).

## Beslut

Frågorna som utkastet lämnade öppna är avgjorda av produktägaren och loggade i PRD:n (införs i #14):

- **D-34:** strategin kan hinna ikapp egenvården.
  Måttlig egenvård med god strategi ska kunna mäta sig med hög egenvård och naiv strategi, men vid samma strategi syns mer egenvård alltid.
- **D-35:** oanvända krafter sparas med mjukt tak och förfaller aldrig.
  Spelaren kan alltså spara ihop till stora beslut, men inte samla en obegränsad hög under lång frånvaro.
