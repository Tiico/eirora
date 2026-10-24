# Krafternas regler v1

> **Status:** [BESLUTAT] v1.0, godkänd av produktägaren vid grind A0 (D-82)  
> **Ärende:** TASK-003 · **Milstolpe:** M0 · **PRD:** HLT-05–09, D-15, D-16, D-18, D-34–D-36, D-40–D-42, D-53, D-54, D-62, D-63, D-69, D-74, O-01  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Invarianter:** [invariants.md](./invariants.md)

Det här dokumentet bestämmer hur egenvård blir krafter: omvandling, avtagande avkastning, mjuka tak, grundnivå, manuell inmatning och hur mycket som kan sparas.
Alla siffror är startvärden för prototypen (O-01).
De ligger i versionerad regeldata, inte i kod, så att de kan trimmas efter dagligt spel utan att gamla världar ändras (NFR-09, NFR-14).
Vad krafterna kostar att använda bestäms i ekonomin (TASK-004).

## 1. Grundprinciper

1. **En bra, hållbar dag ger ungefär 100** av Utforskning och Ork; Styrka räknas per vecka eftersom vilodagar är en del av träning.
2. **De första stegen är värda mest.**
   Kurvorna är branta i början och planar ut, så den som rör sig lite får mycket ut av lite, och den som gör mycket når snart taket.
3. **Extrem egenvård ger högst ungefär 20–30 % mer än en måttlig nivå.**
   Därmed kan god strategi hinna ikapp (D-34), medan mer egenvård vid samma strategi ändå alltid syns.
4. **Samma regler för alla källor.**
   Manuell inmatning och hälsokälla ger exakt samma krafter i den egna världen (HLT-07, INV-F7).
5. **Inget går förlorat.**
   Krafter förfaller aldrig och sparas med mjukt tak (D-35); en korrigering nedåt klipps vid noll (D-36).

## 2. Dagen

Krafter räknas per **dag**: från en gryning till nästa (D-40).
Dagen följer alltså spelarens eget viloschema, inte midnatt, och fungerar likadant för skiftarbetare.

- Steg och träningspass räknas till den dag de sker i.
- Sömn räknas till den dag då sömnperioden **slutar**, så att nattens sömn hör till morgonen den leder till.
  Tupplurar räknas också, inom samma tak.
- Vid gryningen krediteras genast Orkens grundnivå.
  När sömndata kommer in fylls det på upp till kurvans värde; kurvan är aldrig lägre än grundnivån (INV-F4), så påfyllningen är aldrig negativ.

## 3. Omvandling

Varje kurva är styckvis linjär med avtagande lutning och därmed växande, konkav och begränsad (INV-F3).

### Steg → Utforskning (per dag)

| Steg | Utforskning per 1 000 steg | Ackumulerat vid intervallets slut |
|---|---|---|
| 0–4 000 | 15 | 60 |
| 4 000–8 000 | 7,5 | 90 |
| 8 000–12 000 | 2,5 | 100 |
| 12 000–20 000 | 1,25 | 110 |
| över 20 000 | 0 | **110 (tak)** |

Rullstolsskjut behandlas som steg [FÖRSLAG].
Både Health Connect och HealthKit har en egen datatyp för dem; det verifieras i TASK-007.

### Träningspass → Styrka (rullande sju dagar)

Ett pass är ett pass: intensitet och längd utöver minimigränsen spelar ingen roll.
Ett pass räknas om det varar **minst 10 minuter** (D-42); yoga, stretching och en promenad räknas lika mycket som ett löppass.

| Passets ordning inom sju dagar | 1 | 2 | 3 | 4 | 5 | 6 och fler |
|---|---|---|---|---|---|---|
| Styrka för passet | 100 | 90 | 80 | 50 | 25 | 0 |
| Ackumulerat | 100 | 190 | 270 | 320 | **345 (tak)** | 345 |

En dag med pass ger `max(0, G(n + m) − B)`, där `G` är den ackumulerade raden, `n` antalet pass under de sex föregående dagarna, `m` antalet pass under dagen och `B` den Styrka som de sex föregående dagarna gav (D-63).
En dag utan pass ger 0.
Varje rullande vecka som slutar med ett pass ger därmed minst kurvans värde för sina pass och aldrig mer än taket.
Med sex pass i veckan, måndag till lördag, ger den första veckan 100, 90, 80, 50, 25 och 0.
Nästa måndag ger 100, eftersom tisdag till söndag gav 245 och kurvan för veckans sex pass är 345.
Avtagandet räknas per vecka och inte per dag, så att vilodagar aldrig kostar något: tre pass i veckan ger 78 % av taket, vecka efter vecka.
Mer träning ger aldrig mindre Styrka, och det finns ingen fast veckodag.
`B` är det som dygnen gav enligt regeln, före sparandets tak (avsnitt 5), så ett fullt förråd ändrar inte vad kommande pass ger.
Ett pass som kommer in i efterhand räknar om de senare dagarna med pass; det de då ger mindre dras av enligt D-36, D-62 och D-74.
Ett pass som också registreras som promenad ger både Styrka och steg; det är två olika vanor.

### Sömnlängd → Ork (per dag)

| Sömn | Ork per timme | Ackumulerat vid intervallets slut |
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

| Fall | Utforskning | Styrka | Ork | Kommentar |
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

| Profil | Utforskning | Styrka | Ork |
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
| Styrka | 600 | 1 200 | 1 200 |
| Ork | 300 | 600 | 600 |

Styrka får det högsta taket eftersom den är till för stora projekt som det ska gå att spara ihop till under ett par veckor.
Ork får det lägsta eftersom den hör till morgonens planering.

Taket räknas på kraftens **läge**: allt tillskott kraften fått före taket (D-69).
Saldot följer av läget enligt tabellen, så samma bidrag ger samma saldo oavsett ordning och uppdelning.
En korrigering nedåt flyttar läget tillbaka och tar därmed tillbaka kraft i samma takt som den gavs.
Har kraften spenderats sedan bidraget krediterades tar korrigeringen aldrig mer än bidraget gav (D-62, D-74).
Utan spendering däremellan räcker läget, så att Styrka som flyttas mellan dagar ger samma saldo i vilken ordning pass än kommer.
Tillskott över taket syns inte i saldot men finns kvar i läget, så att en senare korrigering tar av dem först.
Spendering sänker läget till det lägsta som ger saldot som är kvar, så att taket alltid räknas från det som faktiskt är sparat.
När ett förråd är fullt säger spelet det lugnt, utan uppmaning att spendera (princip 11).

## 6. Manuell inmatning (HLT-07, D-18)

| Vana | Inmatning | Rimlighetsgräns per dag |
|---|---|---|
| Rörelse | Steg, eller aktiva minuter som räknas om med 100 steg per minut (D-41) | 60 000 steg |
| Träning | Ett pass med starttid och längd | 6 pass |
| Sömn | Sömnperiod med start och slut, i steg om 15 minuter | 16 h |

- Inmatning kan göras för varje dag sedan senaste besöket, högst sju bakåt, så att den är likvärdig med hälsokällans historik (D-53).
- Inmatning är tillåten även under vilotiden; krafterna krediteras vid gryningen (D-54, INV-T9).
- Om samma vana och dag har både data från hälsokällan och egen inmatning gäller det **högre** värdet, inte summan.
  Spelaren kan alltså komplettera en dag då telefonen låg hemma, utan att något räknas två gånger.
- Hälsokällans summa består av en verifierad del och den del som källan markerat som manuellt inmatad (D-68).
  Källan har redan tagit bort dubbletter mellan dem, så de två delarna räknas ihop innan summan jämförs med spelarens egen inmatning.
- Manuella pass som överlappar ett registrerat pass i tid slås ihop till ett.
- Formuleringar i UI är neutrala: inmatning är en fullvärdig väg, aldrig ett misstänkt undantag (R-04).

## 7. Gruppbidrag (från M8)

I den egna världen spelar klassningen ingen roll.
I gruppen har egenrapporterade bidrag ett lägre dagligt tak (D-18, INV-G3) [FÖRSLAG]:

| Vana | Tak för verifierat gruppbidrag | Tak för egenrapporterat gruppbidrag |
|---|---|---|
| Rörelse | 100 Utforskning per dag | 60 |
| Träning | 100 Styrka per dag | 60 |
| Sömn | 100 Ork per dag | 60 |

Taken prövas på nytt i M8 tillsammans med reglerna för gemensamma byggen.

## 8. Beräkning och determinism

- All beräkning sker i heltal (INV-D3): kurvorna räknas exakt med gemensam nämnare, och dagens värde per vana avrundas nedåt **en gång** till hela krafter.
- Tillskott krediteras som skillnaden mellan dagens nya och tidigare värde.
  Summan blir därför densamma oavsett i vilken ordning och i hur många delar data kommer in (INV-B1), även över sparandets tak, eftersom taket räknas på läget (D-69).
- Sparandets halva tillskott räknas i en fast bråkdel av en kraft så att uppdelningen inte ger avrundningsfel (INV-F8).
- Konkaviteten i INV-F3 gäller den exakta kurvan; efter avrundning kan den avvika med högst en kraft, men kurvan förblir växande och begränsad.

## 9. Beslut

Produktägarens svar på de öppna frågorna i v0.1:

- **D-40:** krafter räknas per dag, från gryning till gryning.
- **D-41:** aktiva minuter kan matas in som rörelse, med 100 steg per minut.
- **D-42:** 10 minuter är minsta passlängd.

Senare beslut som ändrar reglerna ovan:

- **D-63** (från [epok 1](./epoch-1.md#7-styrkans-rullande-vecka)): formeln `G(n + m) − G(n)` gav mindre Styrka ju fler pass som gjordes vecka efter vecka, till exempel 0 i veckan med sex pass.
  Den är ersatt av kurvan för passen under sju dagar minus det de sex föregående dagarna redan gav, aldrig under noll (avsnitt 3).
- **D-69:** en korrigering nedåt drog av kraft i full takt även när tillskottet gavs till halva, så att ett sent pass över sparandets tak gav en lägre slutsumma än pass i ordning.
  Taket räknas nu på kraftens läge, se [avsnitt 5](#5-sparande-d-35).
- **D-74:** ett sent pass kunde tillfälligt lyfta saldot över sparandets tak, så att dagen räknades ha gett mindre än sitt tillskott, och gränsen i D-62 lämnade sedan kvar kraft när Styrkan flyttades till andra dag.
  Gränsen gäller nu bara när kraften har spenderats sedan bidraget krediterades, se [avsnitt 5](#5-sparande-d-35).
