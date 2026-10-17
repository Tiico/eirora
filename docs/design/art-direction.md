# Art direction v0

> **Status:** [BESLUTAT] riktning A Gryningsljus med detaljer från B (D-59)  
> **Ärende:** TASK-006 · **Milstolpe:** M0 · **PRD:** UX-04, UX-05, NFR-13, D-10, D-29, D-59, D-60, avsnitt 1.3  
> **Visuell jämförelse:** [art/directions.html](./art/directions.html) ([publicerad sida](https://claude.ai/artifact/Y1gXP2z4yc5oDrobiawrba)), samma ö i fyra dygnsljus för varje riktning

Tre riktningar för hur Eiroras värld ser ut i low-poly 3D med lutad kamera.
Alla tre bygger på det nordiska temat (PRD 1.3) och på gryningen som motiv (D-10).
Produktägaren har valt riktning A med detaljer från B (avsnitt *Beslut*); den blir grunden för art bible och produktionsflödet i M9.
Skisserna på jämförelsesidan är ritade i 2D för att visa palett, ljus och formspråk, inte slutlig kvalitet.

## Riktningarna i korthet

| | A Gryningsljus | B Saga | C Skärgård |
|---|---|---|---|
| Känsla | Mjukt, varmt, hoppfullt; världen som vaknar | Mossa, dimma, snidat trä; myten nära | Klart, krispigt, tydligt; sommar i skären |
| Formspråk | Avfasade kanter, rundade stenar, mjuka mångkantiga kronor, björkar | Höga granar i våningar, tjärat trä, torvtak, drakhuvuden på gavlarna | Plana skarpa facetter, faluröda hus med vita knutar, rosa granit, tallar i en triangel |
| Signatur | Norrsken över natten | Dimma och irrbloss | Hällar och skär |
| Referenser för stämning | Nikolai Astrup, Prins Eugen, Tiny Glade, Townscaper | John Bauer, Theodor Kittelsen, Peder Balke | Anders Zorn, Bruno Liljefors, Islanders, Bad North |

Referenserna är målare vars verk är fria från upphovsrätt och spel som bara anger stämning; inget material från dem används.

## Material

| Material | A Gryningsljus | B Saga | C Skärgård |
|---|---|---|---|
| Mark | `#9db58a` | `#6f8752` | `#7fa35b` |
| Åker | `#d9c27a` | `#b49a55` | `#e0c25a` |
| Strand | `#e6d3ae` | `#77736a` | `#c9a8a0` |
| Berg | `#8a93a3` | `#6e6a63` | `#b8968e` |
| Barr | `#5e8c6a` | `#2f4a37` | `#2f6b47` |
| Björk | `#ede7dc` | `#cfc9bb` | `#f2f0ea` |
| Vägg | `#d8c19d` | `#5a4535` | `#a8322d` |
| Tak | `#7c8893` | `#6f8a4a` | `#3b3f45` |
| Detalj | `#efe8da` | `#d9a441` | `#f4f2ec` |
| Hav nära | `#3e8c99` | `#2e5560` | `#1f7fb0` |
| Hav fjärran | `#86bfc2` | `#6f9095` | `#73b8da` |
| Trä | `#a88a64` | `#4d3a2c` | `#8a6b4a` |

## Krafternas färger

Krafterna har samma betydelse i alla riktningar: Utforskning är havets blå, Byggkraft trämaterialets ockra och Gryningskraft gryningens rosa.
De visas alltid tillsammans med en egen ikon (kompassros, hammare, soluppgång), så att färgen aldrig bär betydelsen ensam (UX-05).

| Kraft | A | B | C |
|---|---|---|---|
| Utforskning | `#4ba3b0` | `#5e9aa0` | `#2e8fd0` |
| Byggkraft | `#d08a4e` | `#c29242` | `#e0a030` |
| Gryningskraft | `#f2a0a8` | `#e0b49e` | `#f07c8c` |

## Ljus över dygnet

Varje fas har en himmelsgradient (topp → horisont), en riktad ljuskälla och en ton som multipliceras med alla material.
Det motsvarar en färgtabell per fas i Unity (URP), som interpoleras mellan faserna.

| Fas | | A Gryningsljus | B Saga | C Skärgård |
|---|---|---|---|---|
| Gryning 06:30 | Himmel | `#7884bf` → `#f6bb9c` | `#5b6d79` → `#e4caa2` | `#5e7ebf` → `#f7c7a4` |
| | Sol, lågt i öster | `#ffd9ad` | `#f2d08a` | `#ffe1b9` |
| | Ton | `#ffe6d6` | `#f2e2c6` | `#ffe9dc` |
| Dag 13:00 | Himmel | `#8cc4e6` → `#ddeff4` | `#9cb2b7` → `#dbe3dc` | `#3d9be0` → `#cdebfa` |
| | Sol, högt | `#fff7de` | `#f4f0dc` | `#ffffff` |
| | Ton | `#ffffff` | `#f4f6f0` | `#ffffff` |
| Skymning 21:30 | Himmel | `#4a4985` → `#ef916f` | `#3d3450` → `#c9845a` | `#2e3d79` → `#f2a15e` |
| | Sol, lågt i väster | `#ff9c5e` | `#e8925a` | `#ff8c42` |
| | Ton | `#ffcfb0` | `#d8a890` | `#ffcaa8` |
| Natt 01:00 | Himmel | `#111737` → `#2f3c6d` | `#0a1317` → `#1e3537` | `#08132b` → `#1b3359` |
| | Måne | `#e8eeff` | `#dde8e0` | `#f2f5ff` |
| | Ton | `#59649a` | `#3f5a5c` | `#4d5f8c` |
| | Lyktor | `#ffc46d` | `#e8a94a` | `#ffd27a` |

Natten är aldrig svart.
Lyktor i fönstren och ett svalt ljus från månen eller norrskenet visar att byn sover tryggt, i linje med att vilan ska kännas positiv (princip 3).
Övergången till natt och gryning är en av spelets viktigaste stunder och får egen animation i M7 och M9.

## Jämförelse

| Kriterium | A Gryningsljus | B Saga | C Skärgård |
|---|---|---|---|
| Bär varumärket (Eir och aurora, gryningen som motiv) | Starkast | Medel | Medel |
| Nordisk mytologi | Medel | Starkast | Svagast |
| Läsbarhet på telefon | God med de gemensamma reglerna | Svagast: mörkt, mycket dimma | Starkast |
| Natten känns trygg | Starkast | Risk: kan kännas hotfull | God |
| Särskiljer sig från förebilderna | God | God | Svag, nära Islanders och Bad North |
| Kostnad i prestanda | Medel: gradienter och norrsken | Hög: dimma och volymljus | Låg |

## Rekommendation

**A Gryningsljus**, med C:s krav på tydliga silhuetter och materialkontrast.
Riktningen bär namnet Eirora och gryningen som motiv, gör natten varm i stället för mörk och skiljer sig från de spel som redan äger det krispiga skärgårdsuttrycket.
Risken att pasteller blir otydliga på en liten skärm hanteras med de gemensamma reglerna nedan och prövas i prototypen (M1).
B:s snidade gavlar och guldaccenter kan lånas in som detaljer i A för att stärka det mytologiska utan att mörka ned världen.

## Gäller alla riktningar

- Varje byggnadstyp har en egen silhuett som går att känna igen i 64 px höjd.
- Resurser och krafter skiljs åt med form och ikon, inte bara färg (UX-05).
- Ljuset är en kärnteknik: en riktad ljuskälla, ambient från himlen och en färgtabell per dygnsfas; ingen realtids-GI på mobil (R-08).
- Natten är aldrig svart.
- Low-poly betyder synliga, avsiktliga facetter, inte låg upplösning; inga texturer med brus.
- Kameran lutar cirka 50° och visar horisonten på zoomnivån för hela ön.
- Allt uttrycks som data (paletter, ljustabeller och materialparametrar), inte som handredigerade scener (NFR-14).

## Beslut

- **D-59: riktning A Gryningsljus.**
  Från C gäller kravet på tydliga silhuetter och materialkontrast.
  Från B lånas snidade gavlar och guldaccenten `#d9a441`, som bara används för det heliga och det upptäckta.
  B:s mörka palett, dimma och volymljus tas inte med.
- **D-60: guiden är en ljusgestalt inspirerad av Eir.**
  Den leder den [guidade turen](./ux-journeys.md#resa-1-första-sessionen) och syns tydligast i gryningen.
  Gestalten utformas i art bible (M9); i prototypen räcker en enkel ljuskälla med text.

Riktningarna B och C står kvar ovan som referens för varför valet gjordes.
