# Krafternas regler v1

> **Status:** [FÖRSLAG] v0.1, väntar på granskning av produktägaren  
> **Ärende:** TASK-003 · **Milstolpe:** M0 · **PRD:** HLT-05–09, D-15, D-16, D-18, D-34, D-35, D-36, O-01  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Invarianter:** [invariants.md](./invariants.md)

Det här dokumentet bestämmer hur egenvård blir krafter: omvandling, avtagande avkastning, mjuka tak, grundnivå, manuell inmatning och hur mycket som kan sparas.
Alla siffror är startvärden för prototypen (O-01).
De ligger i versionerad regeldata, inte i kod, så att de kan trimmas efter dagligt spel utan att gamla världar ändras (NFR-09, NFR-14).
Vad krafterna kostar att använda bestäms i ekonomin (TASK-004).

## 1. Grundprinciper

1. **En bra, hållbar dag ger ungefär 100** av Utforskning och Gryningskraft; Byggkraft räknas per vecka eftersom vilodagar är en del av träning.
2. **De första stegen är värda mest.**
   Kurvorna är branta i början och planar ut, så den som rör sig lite får mycket ut av lite, och den som gör mycket når snart taket.
3. **Extrem egenvård ger högst ungefär 20–30 % mer än en måttlig nivå.**
   Därmed kan god strategi hinna ikapp (D-34), medan mer egenvård vid samma strategi ändå alltid syns.
4. **Samma regler för alla källor.**
   Manuell inmatning och hälsokälla ger exakt samma krafter i den egna världen (HLT-07, INV-F7).
5. **Inget går förlorat.**
   Krafter förfaller aldrig och sparas med mjukt tak (D-35); en korrigering nedåt klipps vid noll (D-36).

## 2. Spelardygnet

Krafter räknas per **spelardygn**: från en gryning till nästa.
Spelardygnet följer alltså spelarens eget viloschema, inte midnatt, och fungerar likadant för skiftarbetare.

- Steg och träningspass räknas till det spelardygn de sker i.
- Sömn räknas till det spelardygn då sömnperioden **slutar**, så att nattens sömn hör till morgonen den leder till.
  Tupplurar räknas också, inom samma tak.
- Vid gryningen krediteras genast Gryningskraftens grundnivå.
  När sömndata kommer in fylls det på upp till kurvans värde; kurvan är aldrig lägre än grundnivån (INV-F4), så påfyllningen är aldrig negativ.

## 3. Omvandling

Varje kurva är styckvis linjär med avtagande lutning och därmed växande, konkav och begränsad (INV-F3).

### Steg → Utforskning (per spelardygn)

| Steg | Utforskning per 1 000 steg | Ackumulerat vid intervallets slut |
|---|---|---|
| 0–4 000 | 15 | 60 |
| 4 000–8 000 | 7,5 | 90 |
| 8 000–12 000 | 2,5 | 100 |
| 12 000–20 000 | 1,25 | 110 |
| över 20 000 | 0 | **110 (tak)** |

Rullstolsskjut behandlas som steg [FÖRSLAG].
Både Health Connect och HealthKit har en egen datatyp för dem; det verifieras i TASK-007.

### Träningspass → Byggkraft (rullande sju spelardygn)

Ett pass är ett pass: intensitet och längd utöver minimigränsen spelar ingen roll.
Ett pass räknas om det varar **minst 10 minuter**; yoga, stretching och en promenad räknas lika mycket som ett löppass.

| Passets ordning inom sju spelardygn | 1 | 2 | 3 | 4 | 5 | 6 och fler |
|---|---|---|---|---|---|---|
| Byggkraft för passet | 100 | 90 | 80 | 50 | 25 | 0 |
| Ackumulerat | 100 | 190 | 270 | 320 | **345 (tak)** | 345 |

Ett spelardygns Byggkraft är `G(n + m) − G(n)`, där `G` är den ackumulerade raden, `n` antalet pass under de sex föregående spelardygnen och `m` antalet pass under dagen.
Avtagandet räknas per vecka och inte per dag, så att vilodagar aldrig kostar något: tre pass i veckan ger 78 % av taket.
Ett pass som också registreras som promenad ger både Byggkraft och steg; det är två olika vanor.

### Sömnlängd → Gryningskraft (per spelardygn)

| Sömn | Gryningskraft per timme | Ackumulerat vid intervallets slut |
|---|---|---|
| Saknad data | — | **40 (grundnivå)** |
| 0–7 h | 8 | 96 |
| 7–9 h | 2 | 100 |
| över 9 h | 0 | **100 (tak)** |

Kurvan börjar på grundnivån, så ingen inloggad sömn ger mindre än saknad data (D-16, HLT-06).
Taket är mjukt kring 7–8 h: 96 % av värdet nås vid 7 h.
Lång sömn ger aldrig avdrag.
Bara längd räknas, aldrig kvalitet eller sömnstadier.

## 4. Räkneexempel och extremvärden

| Fall | Utforskning | Byggkraft | Gryningskraft | Kommentar |
|---|---|---|---|---|
| 0 steg | 0 | | | Världen står still men förfaller inte (D-17) |
| 2 000 steg | 30 | | | Lite rörelse ger märkbar kraft |
| 7 500 steg | 86 | | | |
| 15 000 steg | 103 | | | |
| **50 000 steg** | **110** | | | Taket; ingen obegränsad fördel |
| 0 pass på en vecka | | 0 | | |
| 3 pass på en vecka | | 270 | | |
| 14 pass på en vecka | | 345 | | Taket |
| Saknad sömndata | | | 40 | Grundnivå |
| **0 h sömn** | | | **40** | Golv; inget straff |
| 6,5 h sömn | | | 92 | |
| **12 h sömn** | | | **100** | Taket; inget avdrag |

**Hinner strategin ikapp (D-34)?**
Veckosummor för två profiler med samma strategi:

| Profil | Utforskning | Byggkraft | Gryningskraft |
|---|---|---|---|
| Måttlig: 7 500 steg, 3 pass, 6,5 h | 602 | 270 | 644 |
| Hög: 15 000 steg, 6 pass, 8 h | 721 | 345 | 686 |
| Kvot hög/måttlig | 1,20 | 1,28 | 1,07 |

Den höga profilen har som mest 28 % mer av någon kraft.
Kravet på ekonomin (TASK-004) och på balansbotarna (M4/M5) blir därför att god strategi ska ge minst ungefär 1,3 gånger så mycket värde per kraft som naiv strategi.

## 5. Sparande (D-35)

Oanvända krafter sparas och förfaller aldrig (INV-F6).
Ett mjukt tak per kraft begränsar bara nya tillskott:

| Kraft | Fullt tillskott upp till | Halvt tillskott upp till | Inget tillskott över |
|---|---|---|---|
| Utforskning | 400 | 800 | 800 |
| Byggkraft | 600 | 1 200 | 1 200 |
| Gryningskraft | 300 | 600 | 600 |

Byggkraft får det högsta taket eftersom den är till för stora projekt som det ska gå att spara ihop till under ett par veckor.
Gryningskraft får det lägsta eftersom den hör till morgonens planering.
När ett förråd är fullt säger spelet det lugnt, utan uppmaning att spendera (princip 11).

## 6. Manuell inmatning (HLT-07, D-18)

| Vana | Inmatning | Rimlighetsgräns per spelardygn |
|---|---|---|
| Rörelse | Steg, eller aktiva minuter som räknas om med 100 steg per minut [FÖRSLAG] | 60 000 steg |
| Träning | Ett pass med starttid och längd | 6 pass |
| Sömn | Sömnperiod med start och slut, i steg om 15 minuter | 16 h |

- Inmatning kan göras för innevarande och föregående spelardygn.
- Om samma vana och spelardygn har både data från hälsokällan och egen inmatning gäller det **högre** värdet, inte summan.
  Spelaren kan alltså komplettera en dag då telefonen låg hemma, utan att något räknas två gånger.
- Manuella pass som överlappar ett registrerat pass i tid slås ihop till ett.
- Formuleringar i UI är neutrala: inmatning är en fullvärdig väg, aldrig ett misstänkt undantag (R-04).

## 7. Gruppbidrag (från M8)

I den egna världen spelar klassningen ingen roll.
I gruppen har egenrapporterade bidrag ett lägre dagligt tak (D-18, INV-G3) [FÖRSLAG]:

| Vana | Tak för verifierat gruppbidrag | Tak för egenrapporterat gruppbidrag |
|---|---|---|
| Rörelse | 100 Utforskning per spelardygn | 60 |
| Träning | 100 Byggkraft per spelardygn | 60 |
| Sömn | 100 Gryningskraft per spelardygn | 60 |

Taken prövas på nytt i M8 tillsammans med reglerna för gemensamma byggen.

## 8. Beräkning och determinism

- All beräkning sker i heltal (INV-D3): kurvorna räknas exakt med gemensam nämnare, och dagens värde per vana avrundas nedåt **en gång** till hela krafter.
- Tillskott krediteras som skillnaden mellan dagens nya och tidigare värde.
  Summan blir därför densamma oavsett i vilken ordning och i hur många delar data kommer in (INV-B1).
- Sparandets halva tillskott räknas i en fast bråkdel av en kraft så att uppdelningen inte ger avrundningsfel (INV-F8).
- Konkaviteten i INV-F3 gäller den exakta kurvan; efter avrundning kan den avvika med högst en kraft, men kurvan förblir växande och begränsad.

## 9. Öppna frågor till produktägaren

1. **Aktiva minuter som rörelse:** ska spelare som inte kan eller vill räkna steg kunna mata in aktiva minuter (100 steg per minut), eller räcker steg och rullstolsskjut?
2. **Minsta passlängd:** är 10 minuter rätt gräns för att ett träningspass ska räknas?
3. **Spelardygnet:** är det rätt att krafterna räknas från gryning till gryning i stället för från midnatt till midnatt?
