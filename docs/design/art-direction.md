# Art direction v0

> **Status:** [BESLUTAT] v0.2, riktning A med nattvyn från C vald av produktägaren (D-49)  
> **Ärende:** TASK-006 · **Milstolpe:** M0 (förs vidare till art bible i M9) · **PRD:** D-10, D-29, D-49, avsnitt 1.3, princip 12, UX-04, UX-05, NFR-03, NFR-13, RST-03, MAP-04, R-08, R-12, O-13  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Jämförelse:** [Eirora art direction v0 (Artifact)](https://claude.ai/artifact/VBWXLoy6yaxZxpukWsAg2t), källkod i [art-direction-comparison.html](./art-direction-comparison.html)

Det här dokumentet lägger fram tre riktningar för Eiroras utseende i low-poly 3D med nordiskt mytologiskt tema.
Varje riktning har kärnidé, moodboard, palett, formspråk, ljusstämningar för gryning, dag, skymning och natt, läsbarhet, tillgänglighet och prestandakonsekvenser.
Produktägaren har valt **riktning A, Gryningsdiorama, med nattvyn från riktning C, Norrskensmyt** (D-49); se avsnitt 11.
Avsnitt 3–8 står kvar som underlag för valet.

## Beslut

| # | Fråga | Alternativ | Beslut |
|---|---|---|---|
| 1 | Vilken riktning blir grund för art bible och M1-prototypens ö? | **A** Gryningsdiorama · **B** Torv och tjära · **C** Norrskensmyt | **A** |
| 2 | Ska nattvyn under vilolåset lånas från C? | Ja · Nej, följ den valda riktningen | **Ja** |

## Så granskas riktningarna

Jämförelsen visar samma procedurgenererade ö på hexkarta i alla tre riktningar och alla fyra ljusstämningar, så att bara utseendet skiljer.
Klicka på en ruta för att granska den i ungefärlig telefonstorlek med spelets gränssnitt ovanpå.
Sidan kan simulera protanopi, deuteranopi, tritanopi och gråskala, visa hexrutnätet och växla mellan spelkameran och den sänkta nattvykameran.
Skisserna är en grov förhandsvisning i webbläsaren med three.js, inte spelets rendering i Unity.
De visar palett, ljus, siluetter och läsbarhet, inte slutlig kvalitet på modeller, skuggor eller vatten.

## 1. Utgångspunkter

Det här är redan bestämt och gäller oavsett riktning:

- **Low-poly 3D med lutad kamera, och visuell skönhet är ett kvalitetskrav** (D-29, princip 12, R-12).
- **Dynamiskt dygnsljus** med gryning, dag, skymning och natt (UX-04) och ljusmiljöer för hela dygnet som kvalitetsgrind (NFR-13).
- **Gryningen är det visuella och tematiska motivet** (D-10).
  Namnet förenar Eir, som i nordisk mytologi förknippas med läkande och omsorg, med aurora, gryning och ny början (PRD 1.3).
- **Nattvyn under vilolåset** ska vara lugn och tala om när världen öppnar (RST-03, AC-01).
- **Dimma över outforskade områden** som Utforskning lyfter (MAP-04).
- **Tillgänglighet:** kontrast, skalbar text och färgoberoende uttryck (UX-05).
- **Prestanda** mäts på riktiga låg- och mellanklassenheter; budgeten ska hålla skönheten på målenheterna (NFR-03, NFR-13, R-08, O-13).

## 2. Gemensamma principer för alla riktningar [FÖRSLAG]

1. **Kartan läses först.**
   Terrängtyper skiljs åt med färg, höjd och siluett samtidigt, så att ingen enskild kanal bär informationen.
2. **Gryningen är belöningen.**
   Gryningen är spelets vackraste ögonblick, eftersom det är då krafterna kommer och världen visar vad gårdagen gav.
3. **Natten är vila, inte spektakel.**
   Nattvyn ses ofta vid sänggående, så den har låg ljusstyrka, långsamma eller inga rörelser, inga blinkande effekter och varmt ljus före kallt.
4. **Färg bär aldrig information ensam** (UX-05).
   Krafterna har egna former: Utforskning är en fyruddig kompasstjärna, Byggkraft ett gavelhus och Gryningskraft en sol vid horisonten.
5. **Myten är en underström.**
   Nordisk mytologi syns i form, material, ornamentik och ljus, inte genom gudar i bild eller tunga symboler i gränssnittet.
6. **Skönheten ska hålla på målenheterna.**
   Varje riktning har en lågnivå där dyra effekter ersätts med billigare motsvarigheter utan att stämningen går förlorad.

### Dygnsljuset följer spelardygnet [FÖRSLAG]

Enligt ordlistan är gryning övergången när viloperioden slutar.
Därför föreslås att dygnsljuset följer spelarens viloschema i stället för solen på spelarens ort:

| Ljusstämning | När |
|---|---|
| Natt | Under viloperioden |
| Gryning | Från viloperiodens slut och ungefär en timme framåt |
| Dag | Resten av spelardygnet fram till skymningen |
| Skymning | Ungefär en timme före viloperiodens början |

Ljuset interpoleras mjukt mellan de fyra nyckelstämningarna.
Då ser en skiftarbetare som vaknar klockan 14 ändå sin värld vakna i gryningsljus.

### Kamera [FÖRSLAG]

- **Spelkamera:** lutad cirka 40–50° med tre zoomnivåer (nära, normal, översikt) och fri rotation (UX-01).
- **Nattvykamera:** när viloperioden börjar glider kameran långsamt ned mot horisonten så att himlen, stjärnorna och den sovande bosättningen syns.
  Spelaren kan fortfarande panorera och titta, men inte ge kommandon.

### Bybor

Byborna modelleras som arbetsgrupper, inte individer.
I bild syns de som små figurer kring byggnaderna på dagen och försvinner in i husen på natten, när fönstren dämpas.
Figurerna är cirka en femtedel av en hexbrickas bredd höga och läses som grupper, inte som personer att följa.

## 3. Riktning A: Gryningsdiorama

### Kärnidé

Världen som ett handbyggt diorama i morgonljus.
Rena facetter, mjuka gradienter och ljusa men mättade färger.
Det nordiska bärs av arkitekturen och färgerna från svenska och norska byar: falurött, björkvitt och havsturkos.
Det mytologiska kommer genom ljuset och enstaka inslag, som en samlingshall med torn och runristade gränsstenar.

### Moodboard

| Referens | Vad vi tar | Länk |
|---|---|---|
| Townscaper (Oskar Stålberg) | Facetterade, lekfulla hus i hamnmiljö; tydliga siluetter och mjukt ljus | [Steam](https://store.steampowered.com/app/1291340/Townscaper/), [oskarstalberg.com](https://www.oskarstalberg.com/) |
| Islanders (Grizzly Games) | Platta färger, ren låg polygontäthet, öar som pjäser på ett bord; få men tunga beslut | [Steam](https://store.steampowered.com/app/1046030/ISLANDERS/) |
| Dorfromantik (Toukana Interactive) | Hexbrickor som läses på ett ögonkast; mjuk, varm palett | [Steam](https://store.steampowered.com/app/1455840/Dorfromantik/) |
| Before We Leave (Balancing Monkey Games) | Lugnt civilisationsbygge på hexöar i low-poly | [Steam](https://store.steampowered.com/app/1073910/Before_We_Leave/) |
| Alto's Odyssey (Snowman) | Gradienthimlar och dygnsljus som fungerar på mobil | [altosodyssey.com](https://www.altosodyssey.com/) |
| Nikolai Astrup (målare) | Klara, mättade nordiska färger och ljusa nätter | [Wikipedia](https://en.wikipedia.org/wiki/Nikolai_Astrup) |
| Falu rödfärg | Det röda som gör en by svensk; tak och gavlar | [Wikipedia](https://en.wikipedia.org/wiki/Falu_red) |

### Palett

| Roll | Hex | Kommentar |
|---|---|---|
| Djupt hav | `#2F7FA3` | Klart, kallt blått |
| Grunt hav | `#6CC4C0` | Turkos över sandbotten; markerar farleder och strandhugg |
| Sand | `#F0D9A8` | Ljus strand, lägsta landnivån |
| Gräs | `#9BCB7A` | Ljus vårgrön |
| Skog | `#3F7F55` | Djup grön; björk och tall |
| Berg | `#A3A0A8` | Sval grå med en aning violett |
| Snö | `#F5F3F8` | Toppar |
| Dimma | `#C9C2DD` | Pärlemorlila moln över outforskat |
| Tak, falurött | `#B4473A` | Bosättningens signalfärg |
| Vägg, björk | `#F3E9DA` | Ljusa väggar |
| Krafter | `#2A7FB0` · `#B4473A` · `#C27A12` | Utforskning, Byggkraft, Gryningskraft |
| Gränssnitt | `#FBF7EF` panel · `#2B2A33` text | Ljusa paneler |

### Formspråk

- **Polygontäthet:** låg och jämn.
  Hexbricka 12–40 trianglar, träd 20–60, hus 60–200, specialbyggnad högst 600, bybo 30–60.
- **Siluetter:** branta sadeltak, torn och skorstenar som syns även när huset är 20 px högt.
- **Arkitektur:** faluröda stugor, båthus, en samlingshall med torn; enkla former med tydlig gavel.
- **Bybor:** enkla kägelfigurer i krafternas och byns färger.
- **Vatten:** facetterad yta med svag vågrörelse, turkos grundområde runt kusten och ljus glans från solen.

### Ljusstämningar

| Stämning | Zenit | Horisont | Sol eller måne | Himmelsljus | Karaktär |
|---|---|---|---|---|---|
| Gryning | `#8FA2D4` | `#F6C7A8` | `#FFBE94`, 14° | `#E2D2EC` | Persikoljus från sidan, långa skuggor, ljust dis över havet |
| Dag | `#66AEE6` | `#D3ECF7` | `#FFF6E8`, 55° | `#DDF0FB` | Högt, klart arbetsljus med korta skuggor |
| Skymning | `#4A4A8C` | `#F2A07E` | `#FF9466`, 12° | `#A898CC` | Korallrött motljus, violetta skuggor, fönster tänds |
| Natt (vilolås) | `#10172E` | `#2B3B60` | Måne `#A9BEE6`, 38° | `#3A4A78` | Blått månljus, stjärnor, dämpade fönster `#FFC27A` |

### Läsbarhet på liten skärm

Den mest läsbara riktningen.
Ljusa, åtskilda terrängfärger och tydliga höjdsteg gör att hexbrickorna läses även i översiktszoom.
Faluröda tak sticker ut mot all terräng, så bosättningar hittas direkt.
Svagheten är att ljusa ytor kan bli bländande i starkt solljus utomhus; dagsljuset hålls därför under full vithet.

### Tillgänglighet

- Text i gränssnittet har kontrasten 13,3:1 mot panelen, dämpad text 6,3:1.
- Krafternas färger har 3,2–5,0:1 mot panelen, över WCAG:s krav 3:1 för grafiska objekt.
- Svagaste terrängparet är sand mot gräs vid protanopi (ΔE 7,9); höjdsteget mellan strand och inland skiljer dem ändå.
- Dimma mot snö håller ΔE 10,6 även vid tritanopi, så outforskat förväxlas inte med fjälltoppar.

### Prestanda

Billigast av de tre.
Allt kan ritas med vertexfärger eller en liten palettextur i en enda materialtyp, ett riktat ljus och himmelsljus.
Inga efterbehandlingseffekter behövs; dimma och gradienthimmel är billiga.
Förslag till budget: högst 150 000 synliga trianglar och färre än 100 draw calls med SRP Batcher och GPU-instansiering.
Lågnivå: skuggor från en kaskad med kort räckvidd eller bakade skuggfläckar.

### Styrkor och risker

| Styrkor | Risker |
|---|---|
| Tydligast hexkarta och terräng på liten skärm | Kan förväxlas med Townscaper, Islanders och Dorfromantik |
| Lägst prestandakostnad och batteriförbrukning | Mytologin syns svagt utan särskilda inslag |
| Varm och inbjudande; spegeln av egenvården syns direkt | Nattens himmel är ungefär dubbelt så ljus som i B och C och kan kännas mindre som vila |

## 4. Riktning B: Torv och tjära

### Kärnidé

Sagans vardag, där materialen är huvudsaken.
Dova jordfärger, havsdis och härdens glöd.
Stavkyrkor, långhus med torvtak och båtar med ullsegel.
Allvar och värme som i John Bauers skogar och Theodor Kittelsens nätter.

### Moodboard

| Referens | Vad vi tar | Länk |
|---|---|---|
| Northgard (Shiro Games) | Nordisk mytologi i strategispel; långhus, klaner, årstider | [Steam](https://store.steampowered.com/app/466560/Northgard/) |
| Bad North (Plausible Concept) | Nordiska öar i minimalistisk low-poly; dimmigt, dovt hav | [Steam](https://store.steampowered.com/app/688420/Bad_North_Jotunn_Edition/) |
| Valheim (Iron Gate) | Ljus, dimma och nordisk atmosfär ur enkel geometri | [Steam](https://store.steampowered.com/app/892970/Valheim/) |
| Tiny Glade (Pounce Light) | Handbyggd känsla, material och mjukt ljus | [Steam](https://store.steampowered.com/app/2198150/Tiny_Glade/) |
| Röki (Polygon Treehouse) | Skandinavisk folklore i stiliserad 3D | [Steam](https://store.steampowered.com/app/1079990/Rki/) |
| John Bauer (illustratör) | Skogens mossa, dunkel och allvar | [Wikipedia](https://en.wikipedia.org/wiki/John_Bauer_(illustrator)) |
| Theodor Kittelsen (konstnär) | Nordiskt mörker, natt och väsen i landskapet | [Wikipedia](https://en.wikipedia.org/wiki/Theodor_Kittelsen) |
| Borgunds stavkyrka | Siluett med staplade tak, tjärat trä | [Wikipedia](https://en.wikipedia.org/wiki/Borgund_Stave_Church) |
| Glaumbær och torvhus | Torvtak, låga hus som växer ur marken | [Glaumbær](https://en.wikipedia.org/wiki/Glaumb%C3%A6r), [Turf house](https://en.wikipedia.org/wiki/Turf_house) |
| Osebergsskeppet och urnesstil | Skeppsform och ornamentik för detaljer | [Oseberg](https://en.wikipedia.org/wiki/Oseberg_ship), [Urnes](https://en.wikipedia.org/wiki/Urnes_style) |

### Palett

| Roll | Hex | Kommentar |
|---|---|---|
| Djupt hav | `#24414F` | Mörkt nordatlantiskt hav |
| Grunt hav | `#4F7C7A` | Grågrön över klapper |
| Sten och strand | `#B8A98A` | Klapper och sand |
| Gräs | `#7E8F55` | Dov hedgrön |
| Skog | `#3B5233` | Gran |
| Berg | `#77716A` | Varm granit |
| Snö | `#E4E1DA` | Gråvit |
| Dimma | `#B9B6AE` | Havsdis |
| Torvtak | `#6B7F3E` | Levande torv |
| Tjärat trä | `#4A3426` | Väggar och stavkyrka |
| Accenter | `#E39B4A` härdglöd · `#9E3B2E` ullrött | Eld och segel |
| Krafter | `#6A9CB8` · `#B85F38` · `#E6B04F` | Utforskning, Byggkraft, Gryningskraft |
| Gränssnitt | `#2A2420` panel · `#EDE6D8` text | Mörka paneler som brunt trä |

### Formspråk

- **Polygontäthet:** medel, med oregelbundenhet.
  Hus 150–500 trianglar, stavkyrka högst 1 500, granar 40–120 i staplade lager, stenblock och staket som detaljer.
- **Siluetter:** låga långhus mot staplade, spetsiga stavkyrkor; tjocka gavlar och rökpelare gör husen synliga trots torvtaken.
- **Arkitektur:** långhus, stavkyrka, båthus, rökstugor, gärdsgårdar; ornamentik i urnesstil på gavlar och stävar.
- **Bybor:** figurer i ull, oblekt linne och ullrött.
- **Vatten:** mörkt, nästan stilla, med låg dimma som ligger över vattnet i gryningen.

### Ljusstämningar

| Stämning | Zenit | Horisont | Sol eller måne | Himmelsljus | Karaktär |
|---|---|---|---|---|---|
| Gryning | `#66789A` | `#E2B48F` | `#F0BC8E`, 13° | `#C4C0C8` | Havsdis som lättar, dämpat guldljus, rök från härdarna |
| Dag | `#8CA6BA` | `#D8DEDC` | `#F5EFE2`, 40° | `#D3DCDF` | Lätt molnigt nordiskt dagsljus, mjuka skuggor |
| Skymning | `#3B3D5A` | `#C47A4E` | `#E5884A`, 12° | `#8A7E90` | Kopparljus, härdarna lyser genom dörröppningarna |
| Natt (vilolås) | `#0B0F15` | `#1D2530` | Måne `#8C9CB2`, 30° | `#2E3846` | Nästan svart; härdens glöd `#E39B4A` är det enda varma |

### Läsbarhet på liten skärm

Den svagaste av de tre för hexkartan.
Dova färger ligger närmare varandra i ljushet, så terrängtyper kräver tydligare höjdsteg och kanter.
**Torvtak mot gräs** är svagaste paret (ΔE 6,5 i alla färgseenden): husen smälter in, vilket är autentiskt men gör bosättningar svårare att hitta.
Det kräver mörka gavlar, rök, ljusa dörröppningar och tydligare siluetter.
Utomhus i starkt solljus tappar mörka scener mer än ljusa.

### Tillgänglighet

- Text i gränssnittet har kontrasten 12,3:1 mot panelen, dämpad text 7,0:1.
- Krafternas färger har 3,4–7,8:1 mot panelen.
- Torvtak mot gräs (ΔE 6,5) är under målet 10 och måste lösas med form, inte färg.
- Natten är mörkast av de tre (horisontens relativa luminans 0,019) och lugnast, men detaljer blir svårare att läsa för spelare med nedsatt syn.

### Prestanda

Medel.
Materialkänslan kräver antingen fler polygoner eller en liten brustextur för trä och torv utöver vertexfärgerna.
Havsdis i gryningen ritas som några få halvgenomskinliga lager, vilket ger överritning på mobilens GPU.
Förslag till budget: högst 250 000 synliga trianglar; dislagren begränsas till två och stängs av på lågnivån, där tätare avståndsdimma tar över.

### Styrkor och risker

| Styrkor | Risker |
|---|---|
| Mest genuint nordiskt; tydlig och egen identitet | Torvtak mot gräs smälter ihop |
| Material och hantverk åldras väl och bär epoker med nya byggnadsstilar | Dovare färger ger lägre läsbarhet i solljus utomhus |
| Härdens glöd ger en varm, trygg natt | Kan uppfattas som tungt eller dystert i stället för lugnt, vilket strider mot välmåendet i PRD 1.3 |

## 5. Riktning C: Norrskensmyt

### Kärnidé

Myten blir synlig genom ljus.
Djupa kalla toner, självlysande runor och ett världsträd vid bosättningen.
Gryningskraften syns som guldljus i världen.
Natten med dämpat norrsken är riktningens höjdpunkt och knyter direkt an till namnets aurora.

### Moodboard

| Referens | Vad vi tar | Länk |
|---|---|---|
| Sky: Children of the Light (thatgamecompany) | Ljus som berättelse och belöning; emissiva effekter som fungerar på mobil | [thatskygame.com](https://www.thatskygame.com/) |
| Journey (thatgamecompany) | Siluetter mot ljus himmel; få färger med stor verkan | [thatgamecompany.com](https://thatgamecompany.com/journey/) |
| Spirit of the North (Infuse Studio) | Low-poly nordisk natur, norrsken och myt | [Steam](https://store.steampowered.com/app/867290/Spirit_of_the_North/) |
| Harald Sohlberg (målare) | Blått mörker och stillhet, *Vinternatt i Rondane* | [Wikipedia](https://en.wikipedia.org/wiki/Harald_Sohlberg) |
| Peder Balke (målare) | Dramatiskt ljus över nordiskt hav med begränsad palett | [Wikipedia](https://en.wikipedia.org/wiki/Peder_Balke) |
| Norrsken och Lofoten | Norrskenets färger och former; öar mot natthimmel | [Aurora](https://en.wikipedia.org/wiki/Aurora), [Lofoten](https://en.wikipedia.org/wiki/Lofoten) |
| Eir | Läkande och omsorg som tema för världsträdets ljus | [Wikipedia](https://en.wikipedia.org/wiki/Eir) |

### Palett

| Roll | Hex | Kommentar |
|---|---|---|
| Djupt hav | `#13314A` | Djupblått |
| Grunt hav | `#2A7A8C` | Kall turkos |
| Sand | `#D9C9A3` | Ljus strand |
| Gräs | `#6FAE7E` | Kall grön med dragning åt turkos |
| Skog | `#1F5A55` | Blågrön barrskog |
| Berg | `#5A6480` | Blågrå |
| Snö | `#E6EEF5` | Kall vit |
| Dimma | `#7F8CB0` | Blå dimma |
| Tak, skiffer | `#3D4A6B` | Mörka, branta tak |
| Vägg, kalk | `#D8D2E2` | Ljusa väggar som fångar glöden |
| Ljus | `#FFD27A` runglöd · `#6EF0B4` och `#9B7BF0` norrsken | Endast som ljus, aldrig som yta |
| Krafter | `#59C3E8` · `#F08A5D` · `#FFD27A` | Utforskning, Byggkraft, Gryningskraft |
| Gränssnitt | `#101830` panel · `#E8EEF8` text | Mörka paneler som natthimmel |

### Formspråk

- **Polygontäthet:** låg som i A, men högre och smalare former.
  Hus 80–250 trianglar, världsträd högst 800, smala spirgranar 15–40.
- **Siluetter:** vertikala, spetsiga och höga, så att de syns mot ljus himmel; runstenar i ring kring världsträdet.
- **Arkitektur:** branta skiffertak, kalkade väggar, runstenar, världsträdet som bosättningens hjärta.
- **Bybor:** ljusa mantlar med guldaccenter.
- **Vatten:** mörkt och blankt, speglar runglöd och norrsken på natten.

### Ljusstämningar

| Stämning | Zenit | Horisont | Sol eller måne | Himmelsljus | Karaktär |
|---|---|---|---|---|---|
| Gryning | `#3B3F8E` | `#F3A7B4` | `#FFC0A8`, 14° | `#CDB6E2` | Rosa och violett; runorna slocknar långsamt, Gryningskraften syns som guldljus i världsträdet |
| Dag | `#4A9CD8` | `#C3E5F3` | `#FFF3DA`, 55° | `#CFE6F5` | Krispigt, kallt dagsljus; inga glödeffekter |
| Skymning | `#2A1F5C` | `#E07088` | `#FF8A6A`, 12° | `#9A7EC4` | Magenta och violett; runorna börjar glöda, första stjärnorna |
| Natt (vilolås) | `#060A1A` | `#10274A` | Måne `#86AEE0`, 38° | `#2E4A78` | Dämpat norrsken, stjärnor, glödande runor över en sovande bosättning |

### Läsbarhet på liten skärm

Dagen läses väl tack vare höga siluetter och tydliga terrängskillnader.
Gryning och skymning är mörkare och mer färgade än i A, så terrängen läses sämre just då.
Glöd och norrsken drar blicken; de får aldrig ligga över spelinformation och får aldrig vara det enda som visar ett tillstånd.

### Tillgänglighet

- Text i gränssnittet har kontrasten 15,1:1 mot panelen, dämpad text 8,4:1.
- Krafternas färger har 7,1–12,3:1 mot panelen, högst av de tre.
- Svagaste paret är Byggkraft mot Gryningskraft vid deuteranopi (ΔE 12,1), fortfarande över målet 10.
- Norrskenet rör sig långsamt och stängs av med reducerad rörelse; det får inte flimra.
- Ljust grönt norrsken vid sänggående kan väcka i stället för lugna; därför är det kraftigt dämpat och ligger nära horisonten.

### Prestanda

Dyrast av de tre.
Glöd kräver HDR och bloom, vilket kostar minnesbandbredd och flera millisekunder per bildruta på mellanklass-GPU:er.
Norrskenet är genomskinliga, additiva ytor som ger överritning, och stjärnorna är partiklar.
Förslag: bloom i låg upplösning med få steg på mellanklass; på lågnivån ersätts bloom av bakade glorior som sprites och norrskenet av en målad himmelstextur.
Geometrin är lika billig som i A.

### Styrkor och risker

| Styrkor | Risker |
|---|---|
| Mest särpräglad; knyter an till namnet (aurora) och till Gryningskraften | Högst prestandakostnad och batteriförbrukning |
| Natten blir en gåva: vilolåset blir vackert att titta på | Kall dagspalett kan kännas mindre varm och omhändertagande |
| Starka siluetter mot ljus himmel | Glöd kan bli effektsökeri och konkurrera med spelinformation |

## 6. Jämförelse

| Kriterium | A Gryningsdiorama | B Torv och tjära | C Norrskensmyt |
|---|---|---|---|
| Läsbarhet av hexkartan | Bäst | Svagast | God på dagen, sämre i gryning och skymning |
| Nordisk-mytologisk identitet | Svag till medel | Starkast i material | Stark i ljus |
| Särprägel på marknaden | Lägst | Medel | Högst |
| Gryningen som motiv (D-10) | Tydlig och varm | Dämpad | Tydlig och magisk |
| Nattvyn som vila | Lugn men ljus | Mörkast och lugnast | Vackrast; kräver dämpning |
| Tillgänglighet | God | Kräver formstöd | God |
| Prestanda och batteri | Lägst kostnad | Medel | Högst kostnad |
| Kosmetik (MON-02) | Lätt att variera färg och byggstil | Byggstilar och material | Ljusteman och glöd |

## 7. Prestanda för alla riktningar [FÖRSLAG]

Siffrorna är startvärden som mäts och fastställs i M1 och M2 (O-13).

- **Bildfrekvens:** stabil 30 fps på lågklassenhet, valfri 60 fps på mellanklass.
- **Nattvyn:** renderas med låg bildfrekvens eller bara vid förändring, eftersom den är nästan stillastående; mörka pixlar sparar dessutom ström på OLED-skärmar.
- **Rendering i Unity:** URP med mobilprofil, en gemensam shader med vertexfärger eller palettextur, SRP Batcher och GPU-instansiering för träd och hus.
- **Skuggor:** ett ljus med en kaskad och kort räckvidd; lågnivån använder bakade skuggfläckar.
- **Kvalitetsnivåer:** varje riktning har en lågnivå där de dyraste effekterna har en billigare ersättare enligt avsnitten ovan.

## 8. Rekommendation (vald, D-49)

**Välj A, Gryningsdiorama, och låna nattvyn från C.**

- A ger den tydligaste hexkartan på en liten skärm, lägst prestandakostnad och den ljusaste spegeln av egenvården.
  Det passar ett spel där spelaren fattar ett par tunga beslut på 2–5 minuter och måste läsa kartan snabbt.
- A:s svaghet är att den liknar andra lugna byggspel.
  Den vägs upp av att nattvyn lånas från C: dämpat norrsken, stjärnor och glödande runor när byborna sover.
  Vilolåset, spelets mest särpräglade mekanik, blir då också spelets mest särpräglade bild, och namnets aurora syns.
- Nattens norrsken är ett avgränsat inslag som bara syns under viloperioden, så C:s prestandakostnad gäller en nästan stillastående vy som ändå renderas med låg bildfrekvens.
- B:s material och ornamentik kan användas som kosmetiska byggnadsstilar senare (MON-02) utan att bli grunden.

Om produktägaren hellre prioriterar särprägel framför läsbarhet är C det starkaste alternativet; om nordisk autenticitet väger tyngst är det B.

## 9. Nästa steg efter valet

1. Valet är infört i avsnitt 11 och i PRD:ns beslutslogg som D-49.
2. M1-prototypens ö (ROADMAP M1 punkt 2) byggs i den valda riktningen med enkelt dygnsljus.
3. Paletten och ljusstämningarna läggs som versionerad data, inte i scener (NFR-14).
4. Prestandabudgeten mäts på produktägarens Android och en lågklassenhet (O-13).
5. Riktningen byggs ut till art bible i M9: arkitektur, biomer, bybor, sjöfart och ljus över hela dygnet.

## 10. Metod

- **Kontrast** är beräknad enligt WCAG 2.2: textkrav 4,5:1, grafiska objekt 3:1.
- **Färgseende** är simulerat med Machado, Oliveira och Fernandes (2009) vid full styrka för protanopi, deuteranopi och tritanopi.
- **Färgskillnad** är mätt som CIEDE2000 (ΔE).
  Som tumregel räknas ΔE minst 10 som tydlig skillnad även på små ytor, 5–10 som märkbar men i behov av formstöd och under 5 som riskabel.
  Tumregeln är ett arbetsantagande för den här granskningen, inte en standard.
- **Kontrollerade par:** grunt hav och sand, sand och gräs, gräs och skog, gräs och berg, skog och berg, berg och snö, dimma och snö, dimma och sand, tak och gräs, tak och skog, vägg och sand samt krafterna parvis.
- **Länkarna** i moodboarden är kontrollerade och svarade 2026-10-17.
- **Skisserna** i jämförelsen är byggda med three.js r128 och använder samma värden som tabellerna ovan.

## 11. Beslut

**D-49 [BESLUTAT] 2026-10-17:** Eiroras art direction är **riktning A, Gryningsdiorama, med nattvyn från riktning C, Norrskensmyt**.

Det betyder:

- **Grunden är A.**
  A:s palett, formspråk, polygonbudget och ljusstämningar för gryning, dag och skymning i avsnitt 3 gäller.
- **Natten är C:s.**
  Under viloperioden ersätts A:s nattstämning av C:s: zenit `#060A1A`, horisont `#10274A`, måne `#86AEE0` på 38°, himmelsljus `#2E4A78`, stjärnor och dämpat norrsken `#6EF0B4` mot `#9B7BF0`.
- **Glöden får en plats i A:s värld.**
  Runglöd `#FFD27A` läggs till i A:s palett som ljusfärg, aldrig som ytfärg.
  Den syns i runristade gränsstenar och samlingshallens fönster och tänds i skymningen, starkast under natten.
- **Natten förblir vila.**
  Norrskenet är dämpat, ligger nära horisonten, rör sig knappt och stängs av med reducerad rörelse (avsnitt 2, princip 3).
- **Prestanda:** A:s budget gäller hela dygnet.
  Nattvyns glöd och norrsken renderas med låg bildfrekvens; på lågnivån ersätts bloom av bakade glorior och norrskenet av en målad himmelstextur (avsnitt 5).

Jämförelsen visar den valda kombinationen i en egen rad överst.
Nästa steg följer avsnitt 9.

## 12. Olösta frågor

1. Ska dygnsljuset följa spelarens viloschema (förslaget i avsnitt 2) eller solen på spelarens ort?
2. Ska kameran sänkas automatiskt till nattvyn när viloperioden börjar, eller ska spelaren behålla spelkameran?
3. Ska byborna synas som små figurer i världen, trots att de modelleras som arbetsgrupper?
4. Ska mytologiska väsen (vättar, troll, gudar) synas i världen, eller ska myten bara bäras av form, ornamentik och ljus?
5. Vilken lågklassenhet ska vara referens för prestandamätningen (O-13)?
