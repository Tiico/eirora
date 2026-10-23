# Ekonomi v2

> **Status:** [FÖRSLAG] v2.0, väntar på granskning av produktägaren  
> **Ärende:** TASK-058 (#145) · **Milstolpe:** M4 · **PRD:** D-105–D-107, CIV-01–09, LOG-01–03, LOG-06, D-12, D-17, D-34, D-39, D-43, D-65, D-103, O-02  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Ekonomi v1:** [economy-v1.md](./economy-v1.md) · **Krafter:** [forces.md](./forces.md) · **Epok 1:** [epoch-1.md](./epoch-1.md) · **Invarianter:** [invariants.md](./invariants.md)  
> **Kalkyl:** [models/economy_v2.py](./models/economy_v2.py) (`python -I economy_v2.py`)

Ekonomi v2 omsätter produktägarens beslut om Anno-bredd med gradvis upplåsning (D-105), en bosättning per ö (D-106) och bärare med kapacitet på land (D-107) i regler och siffror.
Den ersätter avsnitt 2–6 i [ekonomi v1](./economy-v1.md) när den är godkänd; det som inte nämns här gäller som i v1, till exempel kartan, byggplatserna, kartläggningarna, expeditionen och sjövägens kapacitet.
Siffrorna är räknade timme för timme i kalkylen ovan, som läser kartan och egenvårdsprofilerna ur ekonomi v1:s kalkyl i stället för att skriva av dem.
Kalkylen kontrollerar varje timme att inget förfaller och att inga lager eller saldon blir negativa, och den stoppar om ett krav i avsnitt 9–12 inte håller.
Där designen har en öppen delfråga räknar kalkylen med rekommendationen; delfrågorna står samlade i [avsnitt 14](#14-öppna-delfrågor-för-produktägaren).

## 1. Grundidé

- **Första veckan är ekonomi v1.**
  Startbyn, de tolv byggnaderna, deras kostnader och den guidade turen är desamma.
  Det nya kommer när bosättningen växer, ett steg i taget.
- **Bredden växer med bosättningen.**
  Byborna kan nå tre befolkningsnivåer, torpare, bönder och hantverkare, med egna behov, egna bostäder och egna byggnader.
  Epok 1 får 23 byggnader, varav nio är nya, och de låses upp i tre steg efter bosättningens storlek (D-105).
- **Varje behov har två oberoende källor, och minst en utan Byggkraft** (INV-R4, D-43).
  Mat kommer ur hav, åker och skog, kläder ur bete och skog, dryck ur åker och äng.
- **Brist stoppar bara tillväxt.**
  En bybo som en gång stigit en nivå går aldrig tillbaka, och ingen byggnad tappar sin bemanning för att ett behov saknas (D-17, D-39).
- **Avståndet kostar bärare, inte produktion** (D-107).
  En byggnad producerar fullt var den än står; förrådets bärare avgör hur fort varorna når fram, och vägar gör dem snabbare.
- **Varje ö är en bosättning** (D-106).
  Byborna bor, äter och arbetar på sin ö och flyttar över sjövägen dit det finns plats och arbete.

## 2. Befolkningsnivåer och behov

| Nivå | Bor i | Behov per bybo och spelardygn | Kan arbeta i | Fördel | Nås |
|---|---|---|---|---|---|
| Torpare | Startbyns 16 platser, stuga 4, långhus 8, nybyggarnas 4 | 1 mat | Byggnader för torpare | | Tillväxt, som i v1 |
| Bönder | Bondgård 6 | 1 mat, ½ kläder | Också bryggeri, mjöderi och tjärdal | Bor tätare än i stugan, utan Byggkraft | Torpare stiger efter steg 1 |
| Hantverkare | Hantverkargård 8 | 1 mat, ½ kläder, ½ dryck | Också varvet | Bygger dubbelt så fort som andra lediga bybor | Bönder stiger efter steg 2 |

En bybo kan arbeta i varje byggnad som kräver samma nivå eller en lägre.
Byggnader som kräver en högre nivå bemannas först och resten i byggordning; arbetsfördelningen i TASK-044 (#127) lägger till spelarens egen ordning.
Inget behov kräver sin egen nivå för att tillverkas: kläder görs av torpare och dryck av bönder, så ingen nivå kan låsa sig själv.

### Vid gryningen

Stegen sker i varje bosättning i den här ordningen, före Gryningskraften och bidragen (D-87):

1. **Mat.**
   Varje bybo äter 1 mat, oavsett nivå.
   Maten tas ur den vara det finns mest av, så att lagren jämnas ut och ingen kedja töms först; v1 tog fisk före säd.
2. **Nivåernas behov.**
   Bönder och hantverkare förbrukar sina andra behov, avrundat uppåt för bosättningen: tio bönder förbrukar fem kläder.
   Kläder är vadmal eller skinn, dryck är mjöd eller öl, och båda tas på samma sätt som maten.
3. **Tillväxt.**
   En mätt bosättning får en torpare till om det finns plats i torparnas bostäder.
   Finns det också 2 kläder förbrukas de och ger en torpare till; det är v1:s komfort med vadmal, som nu kan vara skinn också.
4. **Att stiga en nivå.**
   När en nivås behov uppfylldes i gryningen, det finns plats i nivåns bostäder och varor till de nya, stiger upp till två bybor från nivån under.
   Platsen de lämnar blir ledig, så tillväxten fortsätter i torparnas bostäder.
5. **Brist.**
   Saknas mat stannar tillväxten; saknas en nivås behov stannar bara att stiga till den nivån.
   Ingen bybo går ner en nivå och ingen byggnad förlorar bemanning (D-39, INV-V3).

Behoven ger alltså bara tillväxt och avancemang, inget som kan förloras vid frånvaro; [delfråga F7](#f7-vad-ger-ett-uppfyllt-behov) prövar alternativet.

## 3. Resurser och kedjor

| Resurs | Roll | Källa |
|---|---|---|
| Fisk | Mat | Fiskebod |
| Vilt | Mat, råvara till skinn | Jaktstuga (ny) |
| Säd | Mat, råvara till öl | Åker (långsam process) |
| Timmer | Byggmaterial, råvara | Huggarkoja; skogen växer tillbaka (långsam process) |
| Plank | Byggmaterial, knarrar | Sågverk |
| Sten | Byggmaterial | Stenbrott |
| Ull | Råvara | Fårgård (långsam process) |
| Vadmal | Kläder | Väveri |
| Skinn | Kläder | Garveri (nytt) |
| Honung | Råvara | Bigård (ny, långsam process) |
| Mjöd | Dryck | Mjöderi (nytt) |
| Öl | Dryck | Bryggeri (nytt) |
| Tjära | Knarrar | Tjärdal (ny) |

Kalkylen kontrollerar kedjorna mot INV-R4 och D-43, så att en ändring av innehållet som bryter dem syns direkt:

| Behov | Vara | Kedja | Mark | Byggkraft | Bemannas av |
|---|---|---|---|---|---|
| Mat | Fisk | Fiskebod | Fiskevatten | 0 | Torpare |
| Mat | Vilt | Jaktstuga | Skog | 0 | Torpare |
| Mat | Säd | Åker | Åker | 0 | Torpare |
| Kläder | Vadmal | Fårgård → Väveri | Bete | 60 | Torpare |
| Kläder | Skinn | Jaktstuga → Garveri | Skog | 0 | Torpare |
| Dryck | Mjöd | Bigård → Mjöderi | Äng | 60 | Bönder |
| Dryck | Öl | Åker → Bryggeri | Åker | 0 | Bönder |

- Varje behov har minst två varor vars kedjor börjar på olika mark, så ingen enskild bricka eller byggnad kan svälta en nivå (INV-R4).
- Varje behov har en kedja utan Byggkraft, så en spelare som valt bort träning når alla nivåer (D-43).
- Kedjorna är högst två led långa i epok 1.
  Anno-bredden kommer av fler kedjor som konkurrerar om samma mark och bybor, inte av längre kedjor, så att H4 håller.
- Vilt är både mat och råvara till skinn, och säd både mat och råvara till öl.
  Det är avsiktligt: att klä bönderna eller ge hantverkarna dryck kostar mat, och spelaren väljer vilken kedja som får marken.

## 4. Byggnader

| Byggnad | Steg | Kostnad | Arbete | Byggkraft | Bemanning | Effekt | Krav |
|---|---|---|---|---|---|---|---|
| Stuga | 0 | 15 timmer, 5 sten | 32 h | 0 | 0 | +4 bostäder för torpare | Land |
| Långhus | 0 | 10 timmer, 15 plank, 10 sten | 40 h | 60 | 0 | +8 bostäder för torpare | Land |
| Förråd | 0 | 10 timmer, 5 sten | 16 h | 0 | 1–4 bärare | Lager 150 per resurs; bärare (avsnitt 7) | Land |
| Fiskebod | 0 | 8 timmer | 12 h | 0 | 2 | 1,25 fisk/h | Fiskevatten |
| Åker | 0 | 4 timmer | 8 h | 0 | 2 | 0,5 säd/h dygnet runt; skördas aktiva timmar | Åker |
| Huggarkoja | 0 | 6 timmer | 8 h | 0 | 2 | 0,5 timmer/h per skogsbricka den fäller, högst två | Skog |
| Fårgård | 0 | 10 timmer, 5 plank | 12 h | 0 | 1 | 0,4 ull/h dygnet runt; klipps aktiva timmar | Bete |
| Stenbrott | 0 | 10 timmer, 4 plank | 16 h | 0 | 2 | 0,75 sten/h | Berg |
| Sågverk | 0 | 15 timmer, 5 sten | 24 h | 30 | 2 | 2 timmer → 1 plank per timme | Land |
| Väveri | 0 | 10 plank, 5 sten | 24 h | 60 | 2 | 2 ull → 1 vadmal per två timmar | Land |
| Brygga | 0 | 15 timmer, 10 plank | 24 h | 60 | 1–4 bärare | Sjöväg, expeditioner, lager 50 per resurs | Hamnläge |
| Utkiksvarde | 0 | 10 sten | 12 h | 40 | 0 | Kartläggning och expeditioner kostar 25 % mindre | Land |
| Bondgård | 1 | 20 timmer, 10 sten | 32 h | 0 | 0 | +6 bostäder för bönder | Land |
| Jaktstuga | 1 | 8 timmer | 8 h | 0 | 2 | 0,25 vilt/h per skogsbricka den når, högst fyra | Skog |
| Garveri | 1 | 10 timmer, 5 sten | 16 h | 0 | 2 | 2 vilt → 1 skinn per två timmar | Land |
| Hantverkargård | 2 | 20 timmer, 20 sten | 40 h | 0 | 0 | +8 bostäder för hantverkare | Land |
| Bigård | 2 | 6 timmer | 8 h | 0 | 1 | 0,4 honung/h dygnet runt; skattas aktiva timmar | Äng |
| Bryggeri | 2 | 15 timmer, 5 sten | 16 h | 0 | 2 bönder | 2 säd → 1 öl per två timmar | Land |
| Mjöderi | 2 | 10 plank, 5 sten | 24 h | 60 | 2 bönder | 1 honung → 1 mjöd per två timmar | Land |
| Tjärdal | 3 | 10 timmer, 5 sten | 16 h | 0 | 1 bonde | 2 timmer → 1 tjära per två timmar | Skog |
| Varv | 3 | 20 timmer, 20 plank, 10 sten | 40 h | 80 | 2 hantverkare | Bygger knarrar: +10 på sjövägarna till och från ön per dygn och knarr, högst fyra | Hamnläge |
| Runsten | | 20 sten | 16 h | 0 | 1 | Forskning ([epok 1](./epoch-1.md)) | Land |
| Vårdkase | | Tre etapper | 3 × 160 h | 3 × 500 | 0 | Epokens milstolpe ([epok 1](./epoch-1.md)) | Krön |
| *Uppgradering till nivå 2* | | 10 plank, 5 sten | 16 h | 50 | | +50 % produktion | |
| *Väg* | | 2 sten per bricka | 4 h per bricka | 0 | | Bärarna går dubbelt så fort på brickan | Land |

Epok 1 har därmed 23 byggnader, fler än de 20 som D-105 kräver, och 21 av dem räknas i kalkylen; runstenen och vårdkasen räknas i [epok 1:s kalkyl](./models/epoch_1.py).
Steg 0 är ekonomi v1 oförändrad, utom att bryggan får ett litet lager och att förrådets och bryggans bemanning är bärare.

- Jaktstugan når sin egen bricka och de sex grannarna, som huggarkojan.
  Jakten fäller inte skogen, så en huggarkoja och en jaktstuga kan dela skog; en skogsbricka som flera jaktstugor når delar de lika.
- Bigården är en långsam process som åkern: honungen växer dygnet runt och skattas under de aktiva timmarna (INV-T5).
- Tjärdalen står vid skogen men tar sitt timmer ur förrådet, som sågverket.
- Varvet bygger en knarr i taget av 10 plank och 5 tjära på 32 aktiva timmar.
  Eftersom alla sjövägar i epok 1 går till hemön lyfter ett varv på hemön alla öars sjövägar, och ett varv på en annan ö bara den öns.
- Nivå 2, rika brickor och dagsverke skalar produktionen som i v1, även de långsamma processernas tillväxt (D-103).

### Byggplatser

Byggplatserna följer [ekonomi v1, avsnitt 3.1](./economy-v1.md#31-byggplatser), med ett nytt krav:

| Krav | Brickan | Byggnader |
|---|---|---|
| Äng | En gräsbricka, alltså land som inte är skog, åker, bete, strand eller berg | Bigård |

Hemön har gott om äng, så bigården tävlar med bostäderna om marken nära förrådet och inte med åkern.
Prototypens karta har fortfarande bara två åkerbrickor på Hemön, så säd till mat och säd till öl tävlar om dem; generatorn bör ge startön mer odlingsmark ([ekonomi v1, lärdom 5](./economy-v1.md#9-lärdomar-inför-prototypen)).

## 5. Upplåsning

| Steg | Villkor i någon bosättning | Låser upp | Normal egenvård |
|---|---|---|---|
| 0 | Från början | Ekonomi v1:s tolv byggnader, torpare, behovet mat | Dygn 1 |
| 1 Bönder | 20 bybor | Bönder, bondgård, jaktstuga, garveri, behovet kläder | Vecka 2 (Bygden: dygn 7) |
| 2 Hantverkare | 30 bybor, varav 6 bönder | Hantverkare, hantverkargård, bigård, bryggeri, mjöderi, behovet dryck | Vecka 2–3 |
| 3 Sjöfart | 45 bybor, varav 6 hantverkare, och minst två bosättningar | Tjärdal, varv | Vecka 4 |

- Upplåsningen gäller hela världen, och ett steg som nåtts låses aldrig igen (INV-V1).
  Bostäder, nivåer och behov hör ändå till varje bosättning ([delfråga F6](#f6-upplåsning-per-värld-eller-per-bosättning)).
- Villkoren är bosättningens storlek, som D-105 föreslår, och ett steg kräver att nivån under har kommit i gång.
  Sjöfarten kräver dessutom en sjöväg, eftersom knarrarna inte gör något utan en.
- Upplåsningen kostar ingen kraft och kräver ingen sömn (RST-10).
  Hur fort den nås avgörs av tillväxten, som är högst två bybor per dygn och bosättning, inte av mängden egenvård.

### Första veckan (H4)

Tillväxten är densamma som i v1, så en bosättning når 20 bybor tidigast vid gryningen dygn 7, och bara om den satsar på bostäder och kläder från första dagen.
Med kalkylens strategier möter spelaren de här systemen första veckan:

| Körning | System som möter spelaren, med första dygnet |
|---|---|
| Närområdet | Dimman bricka för bricka (dygn 1), Förädling och minimilager (dygn 3), Bärarna hinner inte (dygn 4) |
| Havet | Expedition (dygn 1), Förädling och minimilager (dygn 1), Sjöväg och ny bosättning (dygn 5) |
| Bygden | Dimman bricka för bricka (dygn 1), Förädling och minimilager (dygn 4), Bönder (dygn 7) |
| Naiv | Dimman bricka för bricka (dygn 1), Förädling och minimilager (dygn 1) |
| Utan träning | Dimman bricka för bricka (dygn 1) |

Turen och *Byggande och bemanning* (dygn 1 och 2) möter alla och står inte i tabellen.
Jämfört med i dag finns två avvikelser, som produktägaren avgör i [delfråga F10](#f10-avvikelserna-första-veckan):

1. **Bärarna.**
   De är förrådets bemanning och presenteras i introduktion 2, *Byggande och bemanning*: *Förrådets bybor bär hem det byarna gör*.
   När de första gången inte hinner med säger diagnostiken det på förrådets blad och under *Värt att titta på*, som när ett lager är fullt; det är ingen ny introduktion.
   Med Närområdets kluster kring stenådern händer det dygn 4, och avståndsklasserna som det ersätter syns i dag på byggnadens blad från dygn 1.
2. **Bönderna för Bygden.**
   En spelare som bygger bostäder och kläder från första dagen når 20 bybor dygn 7 och får bönderna sista dagen i veckan; övriga strategier får dem vecka 2.

Den dagliga sessionen blir inte längre: behoven förbrukas, byborna stiger och bärarna anställs av sig själva.
Det spelaren gör i dag, att välja vad som byggs, var och vart krafterna går, är detsamma, med fler byggnader att välja bland när bosättningen växt.

### Epok 1:s ordning

Introduktionerna i [epok 1, avsnitt 3](./epoch-1.md#3-i-vilken-ordning-systemen-möter-spelaren) får tre nya rader och två ändrade, med veckorna för normal egenvård ur kalkylen:

| # | System | Förutsättning | Normal | Introduktion |
|---|---|---|---|---|
| 2 | Byggande, bemanning och bärare | Första gryningen efter turen | Dygn 2 | Som i dag, och förrådets bärare visas: *Förrådets bybor bär hem det byarna gör* |
| 6 | Sjöväg och ny bosättning | Nyön är hittad och har en brygga | Vecka 1–2 | Guiden visar sjövägens kapacitet och Nyöns egna bybor (LOG-04, D-106) |
| 6a | Bönder | 20 bybor i en bosättning | Vecka 2 | *Byn har vuxit. Med kläder kan torpare bli bönder* |
| 8a | Hantverkare | 30 bybor, varav 6 bönder | Vecka 2–3 | *Bönderna har det gott. Med dryck kan några bli hantverkare* |
| 9a | Sjöfart | 45 bybor, varav 6 hantverkare, och en sjöväg | Vecka 4 | Varvet lyser vid bryggan: *Med knarrar bär sjövägen mer* |

Högst en introduktion visas per spelardygn, och en som väntar visas nästa gång (PRG-02).
Epokens senare halva, vecka 5–7, har inga nya steg; den hör till vårdkasens etapper, dimman och forskningen, som i dag.

## 6. Bosättningar

- **Grundandet.**
  Det första förrådet eller den första bryggan på en ö grundar öns bosättning (D-106).
  Byggnaden har bostäder för 4 nybyggare, och upp till två lediga torpare från hemön följer med; de flyttar, så världens bybor blir inte fler.
- **Egna bybor, bostäder och lager.**
  Byborna bemannar bara byggnader på sin ö och äter ur sin ös lager.
  Varje bosättning har egna nivåer, och nivåernas behov tas ur dess eget lager.
- **Byggandet.**
  Lediga bybor i alla bosättningar bygger på kön, var bygget än ligger; de följer med båtarna som byggarlag men bor kvar hemma.
  Bygget betalas ur lagret på öns bosättning när det räcker och annars ur hemöns, och varorna följer med bygget utanför sjövägens kapacitet ([delfråga F5](#f5-hur-en-ny-bosättning-kommer-i-gång)).
- **Flytt.**
  Vid gryningen flyttar en ledig torpare till en bosättning som har lediga bostäder och en byggnad som saknar bemanning.
  Den kommer från den förbundna bosättning som har flest lediga bybor, och högst en flyttar till varje bosättning per gryning (D-106).
  Bara lediga bybor flyttar, så ingen byggnad blir obemannad av att någon flyttar (D-39).
- **Sjövägen.**
  Den bär 40 enheter per spelardygn som i v1, delat mellan varorna och nu åt båda hållen.
  Först går mat ut till öns bosättning tills den har för två dygn, sedan går öns varor hem, och ön behåller två dygns mat.
  Importprioriteten och reserveringen i TASK-046 (#129) låter spelaren ändra ordningen.
- **Lagret.**
  Ett förråd rymmer 150 av varje resurs som i v1, och en brygga 50, så att en ö med bara en brygga har ett eget lager.

D-106 gör havet långsammare än i v1, eftersom Nyöns byggnader inte längre kan bemannas från hemön.
Havets strategi bygger därför Nyöns brygga före den första bostaden, så att nybyggarna följer med medan det finns lediga bybor; med v1:s ordning blir Havet 18 % sämre första veckan.

## 7. Bärare och vägar

- **Bärarna är bybor vid förråden och bryggorna** ([delfråga F1](#f1-vad-är-bärarna)).
  Varje förråd och brygga har minst en och högst fyra.
  Vid gryningen tar förrådet så många som varorna kräver av de lediga torparna, upp till spelarens tak, och släpper dem som inte behövs; taket är gratis planering (D-23).
- **Varje byggnad hör till det förråd eller den brygga som bärarna når snabbast på sin ö.**
  Ett förråd till i ett kluster tar alltså över byggnaderna runt sig och kommer med egna bärare.
- **En vända** är 4 enheter fram och tillbaka längs sökvägen över land.
  Ett steg tar 15 minuter, och 7,5 minuter in på en bricka med väg.
- **Bärarna hämtar och lämnar.**
  De hämtar det som väntar vid byggnaderna och bär råvara till förädlingen, som håller 4 enheter framme och aldrig tar förrådet under minimilagret på 20 (LOG-06).
  Byggmaterial betalas när bygget läggs i kön, som i v1, och bärs inte.
- **Där det väntar mest först.**
  Bärarna tar först den byggnad vars utlager är fullast eller vars förädling har minst råvara kvar ([delfråga F3](#f3-hur-bärarna-väljer)).
- **Inget går förlorat.**
  Det bärarna inte hinner med väntar i byggnadens utlager, som rymmer 8 enheter; när det är fullt stannar byggnaden (INV-R2).
  Bärarna bär inte under vilan (INV-T4).
- **Dagsverke på ett förråd** låter dess bärare bära 50 % mer under spelardygnet.
  Det är ett nytt morgonbeslut för Gryningskraften, som ekonomi v1 saknade ([lärdom 2](./economy-v1.md#9-lärdomar-inför-prototypen)).

| Steg till förrådet | 1 | 2 | 3 | 4 | 6 |
|---|---|---|---|---|---|
| Enheter per bärartimme utan väg | 8 | 4 | 2,7 | 2 | 1,3 |
| Med väg på alla brickor emellan | 8 | 5,3 | 4 | 3,2 | 2,3 |

En fiskebod intill förrådet som gör 1,25 fisk i timmen tar alltså 16 % av en bärare, och ett stenbrott fyra steg bort som gör 0,75 sten i timmen tar 38 %.
Ett sågverk tar dubbelt, eftersom bärarna både lämnar timmer och hämtar plank.

**Avståndsklasserna försvinner** ([delfråga F2](#f2-ersätter-bärarna-avståndsklasserna)).
I v1 nådde 75 % av produktionen fram från två–tre steg och 50 % från fyra; nu når allt fram när bärarna hinner, och avståndet syns som bärare som inte räcker.
Därmed faller avståndsdelen av D-103 bort, medan nivå 2 och dagsverke fortsätter att skala åkerns och fårgårdens tillväxt.

**Vägar läggs bricka för bricka** ([delfråga F4](#f4-hur-vägar-läggs)), för 2 sten och 4 arbetstimmar per bricka som i v1.
En väg hjälper varje bärare som går över den, så en väg genom ett kluster lyfter alla byggnader längs den.
Byggnadens blad har genvägen *Väg till förrådet*, som lägger vägen längs den kortaste sökvägen.
En väg ligger på en bricka utan byggnad; byggs något där senare blir vägen byggnadens gårdsplan, och bärarna går över den som förut.

### 7.1 Diagnostiken

Diagnostiken i TASK-043 (#126) visar på förrådets blad hur många bärartimmar per aktiv timme dess byggnader behöver och hur många bärare det har.
När bärarna inte räcker visar bladet hur stor del som når fram och vad varje åtgärd skulle ge, räknat med samma regler som simuleringen (CIV-05, LOG-03).
Kalkylen räknar exemplet med Närområdet vid gryningen dygn 4, då startförrådet behöver 1,4 bärartimmar och har en bärare medan förrådet i klustret byggs:

| Åtgärd | Var | Dagsproduktion | Kostnad |
|---|---|---|---|
| Nu | En bärare vid startförrådet | 167 | |
| En bärare till | | 192 | En ledig bybo |
| Väg | Till fiskeboden vid det rika vattnet, 2 brickor | 181 | 4 sten, 8 arbetstimmar |
| Ett förråd till | Vid det rika vattnet | 192 | 10 timmer, 5 sten, 16 arbetstimmar, en bybo |

*Dagsproduktion* är samma viktade balansmått som i ekonomi v1 och visas aldrig för spelaren; bladet visar i stället hur mycket mer av varje vara som når fram.
Det är tre verkliga val: bäraren kostar en byggare, vägen kostar sten men ingen bybo, och förrådet kostar mest men flyttar hela klustrets tyngdpunkt.
I sju dygn använder strategierna 28–71 % av bärarnas tid; den naiva strategin, som lägger allt kring startförrådet, ligger på 69 %.

## 8. Vad krafterna kostar

Krafternas kostnader i ekonomi v1 gäller oförändrade.
Det nya:

| Kraft | Handling | Kostnad | Effekt |
|---|---|---|---|
| Byggkraft | Mjöderi | 60 | Mjöd ur honung, dubbelt så effektivt som öl ur säd |
| Byggkraft | Varv | 80 | Knarrar som höjer sjövägarnas kapacitet från 40 upp till 80 per dygn |
| Gryningskraft | Dagsverke på ett förråd | 30 | Bärarna bär 50 % mer under spelardygnet |

- **Byggkraften tar fortfarande de stora stegen.**
  Väveri, mjöderi och varv är de effektivare kedjorna och sjöfarten, och långhuset bor tätare än stugan.
  Alla nya bostäder och de enklare kedjorna, garveri och bryggeri, kostar ingen Byggkraft, så att varje nivå nås utan träning (D-43).
- **Utforskning hopar sig fortfarande.**
  Alla strategier slutar sex veckor med Utforskning på eller nära taket 800, som i [ekonomi v1, lärdom 1](./economy-v1.md#9-lärdomar-inför-prototypen); fynden i M5 och dimman i epok 1 är svaret, inte ekonomin.
- **Byggkraften räcker till mer än byggnaderna.**
  I de långa körningarna går överskottet till uppgraderingar till nivå 2, och strategierna slutar vecka 6 med 100–130 Byggkraft.
  I epok 1 tävlar vårdkasens etapper, 3 × 500 Byggkraft, om samma kraft; det räknas i TASK-054 (#137).

## 9. Sju dygn med tre strategier

Alla tre körs med samma måttliga egenvård som i v1: 7 500 steg och 6,5 h sömn varje dygn, tre pass i veckan.
Första veckan följer varje strategi sin egen plan.

- **Närområdet:** som i v1, kartlägger Hemön två gånger och bygger ett kluster kring stenådern och urskogen med ett andra förråd, sågverk, tre långhus och fiskebod vid det rika vattnet.
- **Havet:** som i v1, men Nyöns brygga, fårgård och stenbrott går före den första bostaden, som blir en stuga (avsnitt 6).
- **Bygden** (ny): kartlägger närområdet, bygger fårgård och stenbrott vid västra berget med ett förråd emellan, sågverk och väveri, stugor och ett långhus, och klär byborna så att de växer med två om dagen.
  Den når 20 bybor dygn 7 och börjar på bönderna.

Som jämförelse körs samma **naiva** strategi med **hög** egenvård som i v1.

| Körning | Egenvård | Bybor | Nivåer T/B/H | Byggnader | Bosättningar | Plank | Sten | Bärarnas last | Dagsproduktion | Utvecklingsvärde | Använt U/B/G | Sparat U/B/G | Värde per använd kraft |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Närområdet | måttlig | 19 | 19/0/0 | 11 | 1 | 47 | 210 | 71 % | 228 | 1 401 | 180/260/440 | 461/110/229 | 1,13 |
| Havet | måttlig | 21 | 21/0/0 | 12 | 2 | 42 | 29 | 28 % | 246 | 1 265 | 250/210/330 | 426/160/275 | 1,08 |
| Bygden | måttlig | 20 | 20/0/0 | 12 | 1 | 2 | 67 | 54 % | 207 | 1 225 | 180/150/470 | 461/220/214 | 1,02 |
| Naiv | hög | 19 | 19/0/0 | 10 | 1 | 3 | 54 | 69 % | 211 | 1 173 | 180/360/630 | 520/85/96 | 0,65 |
| Utan träning | utan träning | 19 | 19/0/0 | 11 | 1 | 16 | 150 | 66 % | 156 | 1 162 | 180/0/480 | 461/100/204 | 1,14 |
| Ingen spelare | ingen | 16 | 16/0/0 | 4 | 1 | 20 | 20 | 37 % | 48 | 584 | 0/0/0 | 100/100/310 | – |
| Ingen spelare | måttlig | 16 | 16/0/0 | 4 | 1 | 20 | 20 | 37 % | 48 | 584 | 0/0/0 | 551/370/492 | – |

*Utvecklingsvärde* är v1:s balansmått med nivåerna inräknade: torpare × 15, bönder × 25 och hantverkare × 40, plus halva värdet av lagren och det som väntar vid byggnaderna, plus tre dygns produktion.
*Dagsproduktion* är produktionskapaciteten per spelardygn med den andel som bärarna hinner få fram.
*Bärarnas last* är den andel av bärarnas tid som gick åt under veckan.
Startvärdet är 409, som i v1.

**Olika men jämbördiga.**
Närområdet slutar med ett stenlager och ett kluster som bärarna hinner med, Havet med två bosättningar och högst dagsproduktion, och Bygden med flest bybor och vadmal på lager inför bönderna.
Utvecklingsvärdena skiljer sig med 13 %, mot 9 % i v1, och värdet per använd kraft med 10 %.

**Strategin hinner ikapp (D-34).**
Den naiva strategin med hög egenvård får 0,65 i värde per använd kraft, och den sämsta genomtänkta 1,02: 1,56 gånger, där [forces.md](./forces.md) kräver minst 1,3.
Kalkylen stoppar om kvoten blir lägre.
Den naiva strategin förlorar på stenbrottet vid krönet, som i v1, och på att allt den bygger hör till startförrådet, vars bärare tas från byggandet.

**Utan träning (D-43).**
Den som valt bort träning växer till 19 bybor som de andra, med stugor, men dagsproduktionen blir 156 mot 207–246, eftersom sågverk, väveri och brygga kräver Byggkraft.
Den når bönder vecka 2 och hantverkare vecka 3 med garveri och bryggeri (avsnitt 10).

### Samma strategi med mer egenvård

| Strategi | Utvecklingsvärde dygn 7, måttlig/hög | Dygn 14 | Dygn 28 | Dygn 42 |
|---|---|---|---|---|
| Närområdet | 1 401 / 1 401 | 2 338 / 2 338 | 4 722 / 4 742 | 6 042 / 6 381 |
| Havet | 1 265 / 1 265 | 2 185 / 2 207 | 4 093 / 4 263 | 5 206 / 5 209 |
| Bygden | 1 225 / 1 225 | 2 523 / 2 523 | 4 863 / 4 830 | 6 389 / 6 436 |

Första veckan sparar den höga profilen det den får utöver planen, som i v1, och världen är densamma; mer egenvård syns i saldot och i epokens tempo ([epok 1, avsnitt 5](./epoch-1.md#5-tempo)).
Från vecka 2 lägger planeraren överskottet på uppgraderingar och dagsverken, och den höga profilen ligger 0–6 % före.
Bygden dygn 28 är 1 % efter, eftersom planeraren väljer andra byggen med mer Byggkraft och ligger efter i några dygn innan den går om; kalkylen kräver att hög aldrig är under måttlig första veckan.

## 10. Sex veckor

Från vecka 2 fortsätter alla genomtänkta strategier med samma planerare: bostäder när de tar slut, mat, timmer och sten när de inte räcker, kläder och bondgårdar efter steg 1, dryck och hantverkargårdar efter steg 2, en brygga och en expedition, och tjärdal och varv efter steg 3.
Planeraren lämnar alltid två lediga bybor som bygger, och den naiva strategin fortsätter med sin plan.

| Körning | Egenvård | Bönder | Hantverkare | Sjöfart | Bärarna hinner inte | Bybor | Nivåer T/B/H | Byggnader | Bosättningar | Utvecklingsvärde vecka 2/4/6 |
|---|---|---|---|---|---|---|---|---|---|---|
| Närområdet | måttlig | vecka 2 | vecka 3 | vecka 4 | vecka 1 | 82 | 44/14/24 | 42 | 2 | 2 338 / 4 722 / 6 042 |
| Havet | måttlig | vecka 2 | vecka 3 | vecka 4 | vecka 4 | 84 | 32/22/30 | 39 | 2 | 2 185 / 4 093 / 5 206 |
| Bygden | måttlig | vecka 1 | vecka 2 | vecka 4 | vecka 3 | 94 | 40/14/40 | 45 | 2 | 2 523 / 4 863 / 6 389 |
| Naiv | hög | vecka 2 | – | – | – | 32 | 32/0/0 | 10 | 1 | 1 586 / 1 850 / 2 065 |
| Utan träning | utan träning | vecka 2 | vecka 3 | – | vecka 4 | 81 | 47/18/16 | 44 | 1 | 1 353 / 3 006 / 3 488 |
| Havet | hög | vecka 2 | vecka 3 | vecka 4 | vecka 4 | 82 | 28/16/38 | 38 | 2 | 2 207 / 4 263 / 5 209 |
| Ingen spelare | ingen | – | – | – | – | 16 | 16/0/0 | 4 | 1 | 643 / 671 / 671 |

- **Upplåsningen sprids över epokens första halva:** bönder vecka 1–2, hantverkare vecka 2–3 och sjöfarten vecka 4.
- **Utan träning nås alla tre nivåerna**, med garveri, bryggeri och stugor.
  Sjöfarten kräver en sjöväg, och utan Byggkraft kommer bryggan med handkraft efter 7 spelardygn (D-65), som kalkylen inte räknar.
- **Den naiva strategin står still på 32 bybor**, eftersom den aldrig bygger fler bostäder; det är samma som i v1:s långa körningar och visar bara att planen tar slut.
- **Långa körningar säger lite om vilken strategi som är bäst,** eftersom alla följer samma planerare från vecka 2.
  Det prövar balansbotarna i TASK-053 (#136) med egna strategier och kartor.
- **Byarna blir större än i dag.**
  Med nivåernas tätare bostäder når strategierna 82–94 bybor vecka 6, mot 72 vid dygn 45 i [epok 1:s tempo](./epoch-1.md#vecka-då-spelaren-når-varje-steg).
  Elden kräver 50 bybor, så epokens tempo kan bli snabbare; det prövas i TASK-054 (#137) med epokens kalkyl ovanpå ekonomi v2.

## 11. Utan egenvård

Utan spelare och utan egenvård (sista raderna i tabellerna ovan):

- Byborna växer från 12 till 16 av egen kraft och stannar vid bostadstaket, som i v1.
- Startförrådets enda bärare hinner med startbyn; den använder 37 % av sin tid.
- **Inget förfaller.**
  Kalkylen kontrollerar varje timme att antalet bybor, bönder och hantverkare, byggnader, nivåer, upptäckta brickor, bosättningar, upplåsta steg och vägbrickor aldrig minskar och att inga lager, utlager eller saldon blir negativa (INV-V1, INV-R1, INV-F1).
- Samma kontroller håller över ett helt år utan spelare och egenvård (INV-V4).
- Med egenvård men utan beslut händer exakt samma sak i världen; krafterna sparas bara (D-14).

## 12. Migration från ekonomi v1

En värld från ekonomi v1 blir en värld i ekonomi v2 utan att något går förlorat ([ADR-0006](../adr/0006-saves-are-migrated-snapshots.md)).
Ekonomi v2 kommer i flera skivor, och varje skiva som ändrar ett utfall höjer regelversionen med sin egen migration; tillsammans gör de det här:

1. **Byggnader, nivåer, krafter, dimma och skog står kvar** som de är.
2. **Byborna blir torpare.**
   De som bemannar byggnader på en annan ö än hemön blir den öns bosättning, med nybyggarnas 4 bostäder och öns stugor och långhus.
   Har en ö fler bybor än bostäder bor de kvar, och bosättningen växer inte förrän det finns plats.
3. **Lagren.**
   Hemöns lager blir hemöns bosättnings, och varje annan ös lager blir dess bosättnings.
   Det som inte ryms i en ös lager skeppas hem i migrationen; det som inte heller ryms där ligger kvar och kan bara minska tills det ryms.
4. **Vägar.**
   En väg till en byggnad blir vägbrickor längs sökvägen till närmaste förråd.
   Stenen för vägsteg som inte längre behövs, 2 sten per väg, läggs tillbaka i lagret.
5. **Bärare.**
   Förrådens och bryggornas bemanning blir en bärare var, och vid nästa gryning tar de fler om varorna kräver det.
6. **Upplåsningen börjar där världen står:** en värld med 20 bybor har steg 1 direkt, och introduktionerna kommer en per spelardygn.
7. **Byggkön står kvar** med det arbete som är kvar; en väg i kön blir vägbrickor när den blir klar.

Journaler från föregående regelversion spelas inte upp, eftersom de skulle ge en annan värld än den spelaren byggde, som vid regelversion 4 och 5.
Kalkylen migrerar tre världar efter sju dygn i v1 och spelar sju dygn till i v2:

| Värld | Bybor | Byggnader | Bosättningar | Vägbrickor | Sten tillbaka | Bybor dygn 14 | Byggnader dygn 14 | Utvecklingsvärde dygn 14 |
|---|---|---|---|---|---|---|---|---|
| Närområdet | 19 | 11 | 1 | 0 | 0 | 26 | 17 | 2 339 |
| Närområdet med väg till fiskeboden | 19 | 11 | 1 | 2 | 2 | 27 | 19 | 2 306 |
| Havet | 20 | 12 | 2 | 0 | 0 | 34 | 18 | 2 338 |

I alla tre är byborna, byggnaderna, nivåerna, krafterna och den upptäckta kartan desamma före och efter, och ingen vara har försvunnit; kalkylen stoppar annars.

## 13. Lärdomar och risker

1. **En bosättning per ö gör havet långsammare.**
   Havet behövde en ny byggordning för att hålla jämna steg; spelaren måste tänka på vem som ska bo på Nyön innan bryggan byggs.
   Nybyggarna i grundandet ([delfråga F5](#f5-hur-en-ny-bosättning-kommer-i-gång)) är det som håller havet levande.
2. **Bärarna gör folk ännu knappare.**
   Varje bärare är en byggare mindre, så valet mellan bärare, väg och förråd är ett verkligt val; att förrådet anställer själv håller sessionen kort.
3. **Nivåerna gör byarna större.**
   Bönder och hantverkare bor tätare, så byarna växer förbi epok 1:s tempo; TASK-054 behöver räkna om tempot.
4. **Fler bybor ger inte mer egenvård.**
   Mer egenvård syns första veckan som sparade krafter, som i v1, och i epokens tempo; i världen syns den först när planen kan använda överskottet.
5. **Åkern är knapp på prototypens karta.**
   Två åkerbrickor delas mellan säd till mat och säd till öl; generatorn bör ge startön mer odlingsmark.
6. **Kalkylens planerare är enkel.**
   Från vecka 2 är siffrorna en planerares, inte en skicklig spelares; balansbotarna i TASK-048 (#131) och TASK-053 (#136) ersätter den.

## 14. Öppna delfrågor för produktägaren

Varje delfråga har en rekommendation, som kalkylen räknar med.
Produktägaren avgör dem i PR:en, och besluten loggas i PRD:n med egna D-nummer.

### F1. Vad är bärarna?

1. **Bybor som förrådet anställer själv, med spelarens tak (rekommendation).**
   Förrådet tar vid gryningen så många lediga torpare som varorna kräver, en till fyra, och spelaren kan sätta ett lägre tak.
   *För:* fler bärare är en verklig åtgärd med ett pris, en byggare mindre, som D-107 vill; sessionen blir inte längre.
   *Mot:* bemanningen ändras av sig själv varje gryning, vilket diagnostiken måste visa tydligt.
2. **Bybor som spelaren sätter för hand.**
   *För:* allt är spelarens beslut.
   *Mot:* ett beslut till varje dag på varje förråd, och den naiva spelaren tappar halva produktionen utan att förstå varför; kalkylen ger den naiva strategin 0,46 i stället för 0,65 i värde per kraft.
3. **Förrådets egen kapacitet, utan bybor.**
   *För:* enklast; inget att bemanna.
   *Mot:* *fler bärare* blir samma sak som *ett förråd till*, så D-107:s tre åtgärder blir två, och folk är inte längre den knappa resursen i logistiken.

### F2. Ersätter bärarna avståndsklasserna?

1. **Ja, avståndet kostar bärartid i stället för produktion (rekommendation).**
   *För:* en enda regel som diagnostiken kan förklara; en byggnad långt bort ger fullt när bärarna räcker.
   *Mot:* avståndsdelen av D-103 faller bort, och en spelare med gott om bybor straffas mindre för avstånd än i v1.
2. **Båda: avståndsklasserna gäller och bärarna har kapacitet.**
   *För:* närheten väger lika tungt som i v1.
   *Mot:* två straff för samma avstånd, som är svåra att skilja åt på telefonen (LOG-03).

### F3. Hur bärarna väljer

1. **Där det väntar mest först, och spelarens prioritet går före (rekommendation).**
   *För:* ingen byggnad står still länge, och spelaren kan styra med prioriteten från TASK-044.
   *Mot:* ger inte mest totalt när bärarna är för få, eftersom korta vändor inte går först.
2. **Närmast först.**
   *För:* mest varor per bärartimme.
   *Mot:* byggnader långt bort står still helt, vilket känns som ett straff.
3. **En fast varuordning, mat först.**
   *För:* byborna blir alltid mätta först.
   *Mot:* en ordning till att förklara; maten har redan tre källor.

### F4. Hur vägar läggs

1. **Bricka för bricka, med genvägen *Väg till förrådet* (rekommendation).**
   *För:* en väg genom ett kluster hjälper alla, och vägnätet blir en del av kartan (LOG-01, MAP-03).
   *Mot:* fler tryck på telefonen; genvägen täcker det vanligaste fallet.
2. **Per byggnad, som i v1.**
   *För:* ett tryck.
   *Mot:* vägar kan inte delas, och nätet syns inte som ett nät.
3. **Av sig själva där bärarna går ofta.**
   *För:* inget att göra.
   *Mot:* spelaren beslutar inte; strider mot att planering är spelarens (D-23).

### F5. Hur en ny bosättning kommer i gång

1. **Nybyggare följer med, byggarlagen är gemensamma och bygget betalas ur hemön när ön saknar varor (rekommendation).**
   Grundandet ger 4 bostäder och upp till två lediga torpare; lediga bybor i alla bosättningar bygger; bryggan rymmer 50 av varje resurs.
   *För:* Havet håller jämna steg med de andra strategierna (1,08 i värde per kraft).
   *Mot:* byggmaterial till öarna går utanför sjövägens kapacitet.
2. **Bara flytt, en bybo per dygn, och varje ö bygger med sina egna bybor och sitt eget lager.**
   *För:* strikt D-106, och sjövägen bär allt.
   *Mot:* en ny ö står still i flera dygn, tills tillräckligt många har flyttat; kalkylen räknar inte alternativet, men redan med alternativ 1 blir Havet 18 % sämre om Nyöns brygga kommer efter bostaden.
3. **Som 1, men byggmaterialet till öarna tar plats på sjövägen.**
   *För:* sjövägen förklarar all transport.
   *Mot:* byggen på öarna väntar på skeppningar, och kön behöver en ny regel för material som är på väg.

### F6. Upplåsning per värld eller per bosättning

1. **Per värld: ett steg som nåtts i någon bosättning gäller alla (rekommendation).**
   *För:* byggkatalogen är densamma överallt, och varje steg introduceras en gång.
   *Mot:* en ny ö får hantverkargårdar innan den själv har vuxit; den måste ändå ha sina egna bönder för att fylla dem.
2. **Per bosättning.**
   *För:* varje ö har sin egen resa.
   *Mot:* samma introduktioner flera gånger, och olika kataloger på olika öar att förklara.

### F7. Vad ger ett uppfyllt behov?

1. **Bara tillväxt och att stiga en nivå (rekommendation).**
   *För:* inget kan förloras vid frånvaro (D-17, D-39), och nivåerna ger sitt värde genom bostäderna, byggnaderna och hantverkarnas byggande.
   *Mot:* en bosättning som en gång nått en nivå behöver inte fortsätta försörja den; behoven driver bara framåt.
2. **Också en produktionsbonus medan behoven uppfylls.**
   *För:* mer Anno; att försörja nivåerna lönar sig varje dag.
   *Mot:* bonusen försvinner vid brist, och en frånvaro märks som en sämre värld, vilket strider mot andan i D-17.

### F8. Hantverkarnas fördel

1. **De bygger dubbelt så fort och bemannar varvet (rekommendation).**
   *För:* syns i varje strategi, och byggandet är det som är knappt.
   *Mot:* en fördel till att förklara.
2. **Bara varvet och sina bostäder.**
   *För:* enklast.
   *Mot:* nivån ger lite för det den kostar i dryck.
3. **De ger mer i de byggnader de bemannar.**
   *För:* Anno-likt.
   *Mot:* bemanningen avgör produktionen per byggnad, vilket diagnostiken måste visa för varje byggnad.

### F9. Varv med handkraft

1. **Nej, varvet kräver Byggkraft (rekommendation).**
   *För:* sjöfarten är ett lyft, inte något som tillväxten eller milstolpen kräver, så D-43 och D-65 håller utan den.
   *Mot:* den som valt bort träning får aldrig fler än 40 enheter per dygn på sjövägarna.
2. **Ja, med handkraft på 7 spelardygn, som bryggan.**
   *För:* allt i epok 1 nås utan träning.
   *Mot:* Byggkraftens stora steg blir färre.

### F10. Avvikelserna första veckan

1. **Godkänn dem (rekommendation).**
   Bärarna presenteras som förrådets bemanning i introduktion 2, och deras första flaskhals är en diagnos; Bygden får bönderna dygn 7 för att den satsat på tillväxt.
   *För:* den guidade turen och de första introduktionerna är oförändrade, och den som växer snabbt belönas.
   *Mot:* en spelare kan möta en ny nivå sista dagen i första veckan.
2. **Höj steg 1 till 22 bybor.**
   *För:* ingen strategi når bönderna första veckan.
   *Mot:* bönderna kommer ett dygn senare för alla.
3. **Ge varje förråd två bärare från början.**
   *För:* bärarna hinner med hela första veckan för alla strategier.
   *Mot:* startbyn får en byggare mindre, så första veckan blir långsammare än i dag.
