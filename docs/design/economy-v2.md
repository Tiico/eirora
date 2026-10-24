# Ekonomi v2

> **Status:** [BESLUTAT] v2.3, delfrågorna avgjorda som D-109–D-119  
> **Ärende:** TASK-058 (#145) · **Milstolpe:** M4 · **PRD:** D-105–D-107, D-109–D-119, D-125–D-127, CIV-01–09, LOG-01–03, LOG-06, D-12, D-17, D-34, D-39, D-43, D-65, D-87, D-103, O-02  
> **Termer:** enligt [GLOSSARY.md](../../GLOSSARY.md) · **Ekonomi v1:** [economy-v1.md](./economy-v1.md) · **Krafter:** [forces.md](./forces.md) · **Epok 1:** [epoch-1.md](./epoch-1.md) · **Invarianter:** [invariants.md](./invariants.md)  
> **Kalkyl:** [models/economy_v2.py](./models/economy_v2.py) (`python -I economy_v2.py`)

Ekonomi v2 omsätter produktägarens beslut om Anno-bredd med gradvis upplåsning (D-105), en bosättning per ö (D-106) och bärare med kapacitet på land (D-107) i regler och siffror.
Den ersätter avsnitt 2–6 i [ekonomi v1](./economy-v1.md) när den är godkänd; det som inte nämns här gäller som i v1, till exempel kartan, byggplatserna, kartläggningarna, expeditionen och sjövägens kapacitet.
Siffrorna är räknade timme för timme i kalkylen ovan, som läser kartan och egenvårdsprofilerna ur ekonomi v1:s kalkyl i stället för att skriva av dem.
Kalkylen kontrollerar varje timme att inget förfaller och att inga lager eller saldon blir negativa, och den stoppar om ett krav i avsnitt 3 och 9–12 inte håller.
Produktägarens beslut om delfrågorna står i [avsnitt 14](#14-beslut) som D-109–D-119, och kalkylen räknar med dem och skriver ut alternativen bakom dem; inga frågor är öppna.

## 1. Grundidé

- **Första veckan är ekonomi v1.**
  Startbyn, de tolv byggnaderna, deras kostnader och den guidade turen är desamma.
  Det nya kommer när bosättningen växer, ett steg i taget.
- **Bredden växer med bosättningen.**
  Byborna kan nå tre befolkningsnivåer, torpare, bönder och hantverkare, med egna behov, egna bostäder och egna byggnader.
  Epok 1 får 23 byggnader, varav nio är nya, och de låses upp i tre steg efter bosättningens storlek (D-105).
- **Varje behov kan göras utan krafter på två sätt som börjar på olika mark** (INV-R4, D-43).
  Mat kommer ur hav, åker och skog, kläder ur betesmark och skog, dryck ur åker och äng.
- **Brist stoppar bara tillväxt.**
  En bybo som en gång stigit en nivå går aldrig tillbaka, och ingen byggnad tappar sin bemanning för att ett behov saknas (D-17, D-39).
- **Avståndet kostar bärare, inte produktion** (D-107).
  En byggnad producerar fullt var den än står; förrådets bärare avgör hur fort varorna når fram, och vägar gör dem snabbare.
- **Varje ö är en bosättning** (D-106, D-113).
  Byborna bor, äter och arbetar på sin ö, högst en bybo om dygnet flyttar över sjövägen dit det finns plats och arbete, och lediga bybor får bygga på öarna som sjövägen förbinder med deras.

## 2. Befolkningsnivåer och behov

| Nivå | Bor i | Behov per bybo och dag | Kan arbeta i | Fördel | Nås |
|---|---|---|---|---|---|
| Torpare | Startbyns 16 platser, stuga 4, långhus 8, 4 i det förråd eller den brygga som anlägger en bosättning | 1 mat | Byggnader för torpare | | Tillväxt, som i v1 |
| Bönder | Bondgård 6 | 1 mat, ½ kläder | Också bryggeri, mjöderi och tjärdal | Bor tätare än i stugan, utan Styrka | Torpare stiger efter steg 1 |
| Hantverkare | Hantverkargård 8 | 1 mat, ½ kläder, ½ dryck | Också varvet | Bygger dubbelt så fort som andra lediga bybor | Bönder stiger efter steg 2 |

En bybo kan arbeta i varje byggnad som kräver samma nivå eller en lägre.
Byggnader som kräver en högre nivå bemannas först och resten i byggordning; arbetsfördelningen i TASK-044 (#127) lägger till spelarens egen ordning.
Inget behov kräver sin egen nivå för att tillverkas: kläder görs av torpare och dryck av bönder, så ingen nivå kan låsa sig själv.

### Vid gryningen

Krafterna krediteras först och påverkar inget av stegen nedan, som i dag (D-87).
Sedan sker det här, i den här ordningen; stegen 1–4 i varje bosättning för sig och stegen 5–8 för hela världen:

1. **Mat.**
   Varje bybo äter 1 mat, oavsett nivå.
   Maten tas ur den vara det finns mest av, så att lagren jämnas ut och ingen kedja töms först; v1 tog fisk före säd.
2. **Nivåernas behov.**
   Bönder och hantverkare förbrukar sina andra behov, avrundat uppåt för bosättningen: elva bönder förbrukar sex kläder.
   Kläder är tyg eller skinn, dryck är mjöd eller öl, och båda tas på samma sätt som maten.
3. **Att stiga en nivå.**
   När en nivås behov uppfylldes i steg 2, det finns plats i nivåns bostäder och varor till de nya, stiger upp till två bybor från nivån under.
   Steget går före tillväxten, så att kläderna går till dem som stiger innan de används till komfort.
4. **Tillväxt.**
   En mätt bosättning får en torpare till om det finns plats i torparnas bostäder.
   Får också en andra torpare plats, och finns det 2 kläder utöver det nivåerna behöver till nästa gryning, förbrukas de och ger en torpare till; det är v1:s komfort med tyg.
   I v1 förbrukas tyget även när den andra torparen inte får plats; v2 gör inte så.
5. **Flytt.**
   En ledig torpare flyttar till den bosättning som saknar flest bybor till sina byggnader och har lediga bostäder (avsnitt 6).
6. **Bemanning.**
   Byggnaderna bemannas i ordningen ovan.
7. **Bärare.**
   Varje förråd tar så många bärare som varorna kräver av de lediga torparna (avsnitt 7).
   Flytten går före, så en torpare som flyttar blir inte bärare samma gryning.
8. **Upplåsning.**
   Ett steg vars villkor nu är uppfyllt låses upp; byborna stiger till den nya nivån tidigast nästa gryning.

Brist på mat stoppar tillväxten, och brist på en nivås behov stoppar bara att stiga till den nivån.
Ingen bybo går ner en nivå och ingen byggnad förlorar bemanning (D-39, INV-V3).
Behoven ger alltså bara tillväxt och avancemang, inget som kan förloras vid frånvaro (D-115).

## 3. Resurser och kedjor

| Resurs | Roll | Källa |
|---|---|---|
| Fisk | Mat | Fiskebod |
| Vilt | Mat, råvara till skinn | Jaktstuga (ny) |
| Säd | Mat, råvara till öl | Åker (långsam process) |
| Timmer | Byggmaterial, råvara | Skogshuggare; skogen växer tillbaka (långsam process) |
| Plankor | Byggmaterial, knarrar | Sågverk |
| Sten | Byggmaterial | Stenbrott |
| Ull | Råvara | Fårgård (långsam process) |
| Tyg | Kläder | Väveri |
| Skinn | Kläder | Garveri (nytt) |
| Honung | Råvara | Bigård (ny, långsam process) |
| Mjöd | Dryck | Mjöderi (nytt) |
| Öl | Dryck | Bryggeri (nytt) |
| Tjära | Knarrar | Tjärdal (ny) |

Kalkylen prövar kedjorna mot INV-R4 och stoppar om ett behov inte kan göras utan krafter på minst två sätt som börjar på olika mark, eller om ett behov kräver sin egen nivå:

| Behov | Vara | Kedja | Mark | Styrka | Plankor | Utan krafter | Bemannas av |
|---|---|---|---|---|---|---|---|
| Mat | Fisk | Fiskebod | Fiskevatten | 0 | 0 | Ja | Torpare |
| Mat | Vilt | Jaktstuga | Skog | 0 | 0 | Ja | Torpare |
| Mat | Säd | Åker | Åker | 0 | 0 | Ja | Torpare |
| Kläder | Tyg | Fårgård → Väveri | Betesmark | 60 | 15 | Med handkraft (D-65) | Torpare |
| Kläder | Skinn | Jaktstuga → Garveri | Skog | 0 | 0 | Ja | Torpare |
| Dryck | Mjöd | Bigård → Mjöderi | Äng | 0 | 0 | Ja | Bönder |
| Dryck | Öl | Åker → Bryggeri | Åker | 0 | 0 | Ja | Bönder |

- Mat är basbehovet, som alla nivåer har; kläder och dryck prövas ändå mot INV-R4 på samma sätt, eftersom de avgör om en bosättning stiger.
- Utan krafter finns inget sågverk, så plankor kommer bara ur startlagrets 20.
  Kontrollen räknar därför en kedja bara om dess plankor, tillsammans med stenbrottets 4, ryms i startlagret: tygets kedja tar 15, och alla andra kedjor tar inga.
  Mjöderiet kostar timmer och sten i stället för plankor, så att mjöd och tyg båda går att nå utan sågverk.
- Kedjorna är högst två led långa i epok 1.
  Anno-bredden kommer av fler kedjor som konkurrerar om samma mark och bybor, inte av längre kedjor, så att H4 håller.
- Vilt är både mat och råvara till skinn, och säd både mat och råvara till öl.
  Det är avsiktligt: att klä bönderna eller ge hantverkarna dryck kostar mat, och spelaren väljer vilken kedja som får marken.
  Prototypens karta har bara två åkerrutor på Hemön, så öl tävlar med maten om dem; mjöd ur ängen är därför dryckens oberoende källa.

## 4. Byggnader

| Byggnad | Steg | Kostnad | Arbete | Styrka | Bemanning | Effekt | Krav |
|---|---|---|---|---|---|---|---|
| Stuga | 0 | 15 timmer, 5 sten | 32 h | 0 | 0 | +4 bostäder för torpare | Land |
| Långhus | 0 | 10 timmer, 15 plankor, 10 sten | 40 h | 60 | 0 | +8 bostäder för torpare | Land |
| Förråd | 0 | 10 timmer, 5 sten | 16 h | 0 | 1–4 bärare | Lager 150 per resurs; bärare (avsnitt 7) | Land |
| Fiskebod | 0 | 8 timmer | 12 h | 0 | 2 | 1,25 fisk/h | Fiskevatten |
| Åker | 0 | 4 timmer | 8 h | 0 | 2 | 0,5 säd/h dygnet runt; skördas aktiva timmar | Åker |
| Skogshuggare | 0 | 6 timmer | 8 h | 0 | 2 | 0,5 timmer/h per skogsruta den fäller, högst två | Skog |
| Fårgård | 0 | 10 timmer, 5 plankor | 12 h | 0 | 1 | 0,4 ull/h dygnet runt; klipps aktiva timmar | Betesmark |
| Stenbrott | 0 | 10 timmer, 4 plankor | 16 h | 0 | 2 | 0,75 sten/h | Berg |
| Sågverk | 0 | 15 timmer, 5 sten | 24 h | 30 | 2 | 2 timmer → 1 planka per timme | Land |
| Väveri | 0 | 10 plankor, 5 sten | 24 h | 60 | 2 | 2 ull → 1 tyg per två timmar | Land |
| Brygga | 0 | 15 timmer, 10 plankor | 24 h | 60 | 1–4 bärare | Sjöväg, expeditioner, lager 50 per resurs | Hamnläge |
| Utkikstorn | 0 | 10 sten | 12 h | 40 | 0 | Kartläggning och expeditioner kostar 25 % mindre | Land |
| Bondgård | 1 | 20 timmer, 10 sten | 32 h | 0 | 0 | +6 bostäder för bönder | Land |
| Jaktstuga | 1 | 8 timmer | 8 h | 0 | 2 | 0,25 vilt/h per skogsruta den når, högst fyra | Skog |
| Garveri | 1 | 10 timmer, 5 sten | 16 h | 0 | 2 | 2 vilt → 1 skinn per två timmar | Land |
| Hantverkargård | 2 | 20 timmer, 20 sten | 40 h | 0 | 0 | +8 bostäder för hantverkare | Land |
| Bigård | 2 | 6 timmer | 8 h | 0 | 1 | 0,4 honung/h dygnet runt; skattas aktiva timmar | Äng |
| Bryggeri | 2 | 15 timmer, 5 sten | 16 h | 0 | 2 bönder | 2 säd → 1 öl per två timmar | Land |
| Mjöderi | 2 | 15 timmer, 5 sten | 24 h | 0 | 2 bönder | 1 honung → 1 mjöd per två timmar | Land |
| Tjärdal | 3 | 10 timmer, 5 sten | 16 h | 0 | 1 bonde | 2 timmer → 1 tjära per två timmar | Skog |
| Varv | 3 | 20 timmer, 20 plankor, 10 sten | 40 h | 80 | 2 hantverkare | Bygger knarrar: +10 på sjövägarna till och från ön per dygn och knarr, högst fyra | Hamnläge |
| Runsten | | 20 sten | 16 h | 0 | 1 | Forskning ([epok 1](./epoch-1.md)) | Land |
| Vårdkase | | Tre etapper | 3 × 160 h | 3 × 500 | 0 | Epokens milstolpe ([epok 1](./epoch-1.md)) | Topp |
| *Uppgradering till nivå 2* | | 10 plankor, 5 sten | 16 h | 50 | | +50 % produktion | |
| *Väg* | | 2 sten per ruta | 4 h per ruta | 0 | | Bärarna går dubbelt så fort på rutan | Land |

Epok 1 har därmed 23 byggnader, fler än de 20 som D-105 kräver, och 21 av dem räknas i kalkylen; runstenen och vårdkasen räknas i [epok 1:s kalkyl](./models/epoch_1.py).
Steg 0 är ekonomi v1 oförändrad, utom att bryggan får ett litet lager och att förrådets och bryggans bemanning är bärare.

- Jaktstugan når sin egen ruta och de sex grannarna, som skogshuggaren.
  Jakten fäller inte skogen, så en skogshuggare och en jaktstuga kan dela skog; en skogsruta som flera jaktstugor når delar de lika.
- Bigården är en långsam process som åkern: honungen växer dygnet runt och skattas under de aktiva timmarna (INV-T5).
- Mjödet ur honung är dubbelt så effektivt som ölet ur säd, men bigården kräver äng nära förrådet, där bostäderna också vill stå.
- Tjärdalen står vid skogen men tar sitt timmer ur förrådet, som sågverket.
- Varvet bygger en knarr i taget av 10 plankor och 5 tjära på 32 aktiva timmar.
  Eftersom alla sjövägar i epok 1 går till hemön lyfter ett varv på hemön alla öars sjövägar, och ett varv på en annan ö bara den öns.
- Nivå 2, rika rutor och extraarbete skalar produktionen som i v1, även de långsamma processernas tillväxt (D-103).

### Byggplatser

Byggplatserna följer [ekonomi v1, avsnitt 3.1](./economy-v1.md#31-byggplatser), med ett nytt krav:

| Krav | Rutan | Byggnader |
|---|---|---|
| Äng | En gräsruta, alltså land som inte är skog, åker, betesmark, strand eller berg | Bigård |

Hemön har gott om äng, så bigården tävlar med bostäderna om marken nära förrådet och inte med åkern.
Generatorn bör ändå ge startön mer odlingsmark ([ekonomi v1, lärdom 5](./economy-v1.md#9-lärdomar-inför-prototypen)).

## 5. Upplåsning

| Steg | Villkor | Låser upp | Normal | Manuell |
|---|---|---|---|---|
| 0 | Från början | Ekonomi v1:s tolv byggnader, torpare, behovet mat | Dygn 1 | Dygn 1 |
| 1 Bönder | En bosättning har 20 bybor | Bönder, bondgård, jaktstuga, garveri, behovet kläder | Vecka 2 | Vecka 2 |
| 2 Hantverkare | En bosättning har 30 bybor, varav 6 bönder | Hantverkare, hantverkargård, bigård, bryggeri, mjöderi, behovet dryck | Vecka 2 | Vecka 3 |
| 3 Sjöfart | En bosättning har 45 bybor, varav 6 hantverkare, och det finns en sjöväg mellan hemön och en annan bosättning | Tjärdal, varv | Vecka 4 | Vecka 5 |

Veckorna är den guidade turens körning i avsnitt 10, med normal egenvård och med enbart manuell inmatning.

- Upplåsningen gäller hela världen, och ett steg som nåtts låses aldrig igen (INV-V1).
  Bostäder, nivåer och behov hör ändå till varje bosättning (D-114).
- Villkoren är bosättningens storlek, som D-105 föreslår, och ett steg kräver att nivån under har kommit i gång.
  Sjöfarten kräver dessutom en sjöväg, eftersom knarrarna inte gör något utan en; ett förråd på en ö utan brygga räcker inte.
- Upplåsningen kostar ingen kraft och kräver ingen sömn (RST-10).
  Hur fort den nås avgörs av tillväxten, som är högst två bybor per dygn och bosättning, inte av mängden egenvård.

### Första veckan (H4)

Kalkylen spelar den guidade turen ([resa 1](./ux-journeys.md#resa-1-första-sessionen)) och epok 1:s första vecka i både ekonomi v1 och v2, med samma kommandon, och räknar vilka system spelaren möter:

| Profil | Ekonomi v1, i dag | Ekonomi v2 |
|---|---|---|
| Normal | Dimman (dygn 1), Expedition (dygn 1), Förädling och minimilager (dygn 1), Sjöväg (dygn 6) | Dimman (dygn 1), Expedition (dygn 1), Förädling och minimilager (dygn 1), Sjöväg och ny bosättning (dygn 7) |
| Enbart manuell | Dimman (dygn 1), Expedition (dygn 1), Förädling och minimilager (dygn 1) | Dimman (dygn 1), Expedition (dygn 1), Förädling och minimilager (dygn 1) |

Turen och *Byggande och bemanning* (dygn 1 och 2) möter alla och står inte i tabellen.
Med turen möter spelaren samma system som i dag, och sjövägen kommer ett dygn senare med D-106:s regler.
De andra strategierna i avsnitt 9 möter de här systemen första veckan:

| Körning | System som möter spelaren, med första dygnet |
|---|---|
| Närområdet | Dimman (dygn 1), Förädling och minimilager (dygn 3), Bärarna hinner inte (dygn 4) |
| Havet | Expedition (dygn 1), Förädling och minimilager (dygn 1), Sjöväg och ny bosättning (dygn 5) |
| Bygden | Dimman (dygn 1), Förädling och minimilager (dygn 4), Bönder (dygn 7) |
| Naiv | Dimman (dygn 1), Förädling och minimilager (dygn 1) |
| Utan träning | Dimman (dygn 1) |

Jämfört med i dag finns tre avvikelser, och D-118 godtar alla tre:

1. **Ny bosättning.**
   Nyön får egna bybor, egen mat och egen flytt (D-106), och introduktion 6 visar det; i dag är Nyön bara byggnader.
2. **Bärarna.**
   De är förrådets bemanning och presenteras i introduktion 2, *Byggande och bemanning*: *Förrådets bybor bär hem det byarna gör*.
   När de första gången inte hinner med säger diagnostiken det på förrådets blad och under *Värt att titta på*, som när ett lager är fullt; det är ingen ny introduktion.
   Med Närområdets kluster kring stenådern händer det dygn 4, men med turen först vecka 3.
3. **Bönderna för Bygden.**
   En spelare som bygger bostäder och kläder från första dagen når 20 bybor dygn 7 och får bönderna sista dagen i veckan; övriga strategier får dem vecka 2.

Den dagliga sessionen blir inte längre: behoven förbrukas, byborna stiger, flyttar och anställs som bärare av sig själva.
Det spelaren gör i dag, att välja vad som byggs, var och vart krafterna går, är detsamma, med fler byggnader att välja bland när bosättningen växt.

### Epok 1:s ordning

Introduktionerna i [epok 1, avsnitt 3](./epoch-1.md#3-i-vilken-ordning-systemen-möter-spelaren) får tre nya rader och två ändrade, med veckorna ur turens körning:

| # | System | Förutsättning | Normal | Manuell | Introduktion |
|---|---|---|---|---|---|
| 2 | Byggande, bemanning och bärare | Första gryningen efter turen | Dygn 2 | Dygn 2 | Som i dag, och förrådets bärare visas: *Förrådets bybor bär hem det byarna gör* |
| 6 | Sjöväg och ny bosättning | Nyön är hittad och har en brygga | Vecka 1–2 | Vecka 2 | Guiden visar sjövägens kapacitet och att Nyön har egna bybor (LOG-04, D-106) |
| 6a | Bönder | 20 bybor i en bosättning | Vecka 2 | Vecka 2 | *Byn har vuxit. Med kläder kan torpare bli bönder* |
| 8a | Hantverkare | 30 bybor, varav 6 bönder | Vecka 2 | Vecka 3 | *Bönderna har det gott. Med dryck kan några bli hantverkare* |
| 9a | Sjöfart | 45 bybor, varav 6 hantverkare, och en sjöväg | Vecka 4 | Vecka 5 | Varvet lyser vid bryggan: *Med knarrar bär sjövägen mer* |

Högst en introduktion visas per dag, och en som väntar visas nästa gång (PRG-02).
Epokens senare halva har inga nya steg; den hör till vårdkasens etapper, dimman och forskningen, som i dag.

## 6. Bosättningar

- **En bosättning anläggs** av det första förrådet eller den första bryggan på en ö (D-106).
  Byggnaden har 4 bostäder för torpare, och byborna kommer genom flytten; inga nybyggare följer med.
- **Egna bybor, bostäder och lager.**
  Byborna bemannar bara byggnader på sin ö och äter ur sin ös lager.
  Varje bosättning har egna nivåer, och nivåernas behov tas ur dess eget lager.
- **Flytt.**
  Vid gryningen flyttar högst en bybo i hela världen (D-106).
  Det är en ledig torpare, och den flyttar till den bosättning som saknar flest bybor till sina byggnader och har lediga bostäder, från den förbundna bosättning som har flest lediga.
  Bara lediga bybor flyttar, så ingen byggnad blir obemannad av att någon flyttar (D-39).
- **Byggandet.**
  Lediga bybor bygger på kön på sin egen ö och på öar som sjövägen förbinder med deras, men bor och arbetar kvar hemma (D-113).
  På en ö som ännu saknar bosättning, där den första bryggan byggs, bygger hemöns bybor, eftersom expeditionen har nått dit.
- **Byggmaterialet.**
  Bygget betalas ur öns lager när det räcker, annars ur hemöns.
  Material från hemön tar plats på sjövägen: de närmaste dygnens kapacitet går först åt till det, så att taket på 40 gäller åt båda hållen (D-106, D-113).
- **Sjövägen.**
  Den bär 40 enheter per dag som i v1, delat mellan varorna och nu åt båda hållen.
  Först går det öns bosättning behöver för två dygn ut från hemön: mat till alla och kläder och dryck till de bostäder öns nivåer har.
  Sedan går öns varor hem, och ön behåller två dygns behov.
  Importprioriteten och reserveringen i TASK-046 (#129) låter spelaren ändra ordningen för varorna hem och hålla kvar varor på ön; skulden och maten ut går som förut (D-126, D-127, [ekonomi v1, avsnitt 5](./economy-v1.md#lagerregler)).
- **Lagret.**
  Ett förråd rymmer 150 av varje resurs som i v1, och en brygga 50, så att en ö med bara en brygga har ett eget lager.

I de sex veckorna i avsnitt 10 växer Nyön till 18–20 bybor, och i alla sex körningar med Nyön stiger 6–12 av dem till bönder med kläder från hemön; ön har bara sex landrutor, så mer ryms inte.
D-106 gör havet långsammare än i v1, eftersom Nyöns byggnader inte längre kan bemannas från hemön.
Havets strategi bygger därför Nyöns brygga före den första bostaden, så att Nyön tidigt har bostäder dit lediga bybor kan flytta; med v1:s ordning blir Havet 19 % sämre första veckan.

## 7. Bärare och vägar

- **Bärarna är bybor vid förråden och bryggorna** (D-109).
  Varje förråd och brygga har minst en och högst fyra.
  Vid gryningen tar förrådet så många som varorna kräver av de lediga torparna, upp till spelarens tak, och släpper dem som inte behövs; taket är gratis planering (D-23).
  Förrådet tar också byggarna om det behöver dem, så byggkön kan stå still tills bosättningen växt; diagnostiken säger då *Byggkön står still: alla lediga bybor bär*, så att spelaren kan sänka taket.
  Förrådet tar aldrig en bybo från en annan byggnad: bemanningen är klibbig, så en byggnad som är bemannad förblir det, och en ny byggnad eller ett nytt förråd tar bara lediga bybor (D-39).
- **Varje byggnad hör till det bemannade förråd eller den bemannade brygga som bärarna når snabbast på sin ö.**
  Byggnaderna byter förråd bara vid gryningen, när förråden också anställer sina bärare, och ett förråd utan bärare tar inte över några byggnader.
  Ett nytt förråd eller en ny väg kan alltså aldrig minska det som når fram samma dygn (D-39).
  Ett förråd till i ett kluster tar över byggnaderna runt sig vid gryningen efter att det fått sin bärare.
- **En vända** är 4 enheter fram och tillbaka längs sökvägen över land.
  Ett steg tar 15 minuter, och 7,5 minuter in på en ruta med väg.
- **Bärarna hämtar och lämnar.**
  De hämtar det som väntar vid byggnaderna och bär råvara till förädlingen, som håller 4 enheter framme och aldrig tar förrådet under sitt minimilager, 20 som standard, eller under öns reservering (LOG-06, TASK-046).
  De tar först där det väntar mest: det fullaste utlagret eller den förädling som har minst råvara kvar, och spelarens prioritet går före (D-111).
  Spelarens prioritet är ett uttryckligt läge, inte platsen i bemanningsordningen (förtydligande av D-111): en byggnad som spelaren flyttat upp där tar bärarna först, också när andra flyttas upp ovanför den.
  Flyttad ned igen tar de den där det väntar mest, och förråd och bryggor får aldrig läget.
  Byggmaterial betalas när bygget läggs i kön, som i v1, och bärs inte.
- **Inget går förlorat.**
  Det bärarna inte hinner med väntar i byggnadens utlager, som rymmer 8 enheter; när det är fullt stannar byggnaden (INV-R2).
  Bärarna bär inte under vilan (INV-T4).
- **Arbeta extra på ett förråd** låter dess bärare bära 50 % mer under dagen.
  Det är ett nytt morgonbeslut för Orken, som ekonomi v1 saknade ([lärdom 2](./economy-v1.md#9-lärdomar-inför-prototypen)).

| Steg till förrådet | 1 | 2 | 3 | 4 | 6 |
|---|---|---|---|---|---|
| Enheter per bärartimme utan väg | 8 | 4 | 2,7 | 2 | 1,3 |
| Med väg på alla rutor emellan | 8 | 5,3 | 4 | 3,2 | 2,3 |

En fiskebod intill förrådet som gör 1,25 fisk i timmen tar alltså 16 % av en bärare, och ett stenbrott fyra steg bort som gör 0,75 sten i timmen tar 38 %.
Ett sågverk tar dubbelt, eftersom bärarna både lämnar timmer och hämtar plankor.

**Avståndsklasserna försvinner** (D-110).
I v1 nådde 75 % av produktionen fram från två–tre steg och 50 % från fyra; nu når allt fram när bärarna hinner, och avståndet syns som bärare som inte räcker.
Därmed faller avståndsdelen av D-103 bort, medan nivå 2 och extraarbete fortsätter att skala åkerns och fårgårdens tillväxt.

**Vägar läggs ruta för ruta** (D-112), för 2 sten och 4 arbetstimmar per ruta som i v1.
En väg hjälper varje bärare som går över den, så en väg genom ett kluster lyfter alla byggnader längs den.
Byggnadens blad har genvägen *Väg till förrådet*, som lägger vägen längs den kortaste sökvägen.
En väg ligger på en ruta utan byggnad; byggs något där senare blir vägen byggnadens gårdsplan, och bärarna går över den som förut.

### 7.1 Diagnostiken

Diagnostiken i TASK-043 (#126) visar på förrådets blad hur många bärartimmar per aktiv timme dess byggnader behöver och hur många bärare det har.
När bärarna inte räcker visar bladet hur stor del som når fram och vad varje åtgärd skulle ge, räknat med samma regler som simuleringen (CIV-05, LOG-03).
En bärare till och ett nytt förråd behöver en ledig bybo; finns ingen, tar åtgärden ingen från en annan byggnad, och bladet visar både vad den ger i dag och när bäraren kommit på plats.
Kalkylen räknar exemplet med Närområdet vid gryningen dygn 4.
Startförrådet behöver 1,4 bärartimmar och har en bärare, förrådet i klustret är byggt och bemannat, och ingen bybo är ledig:

| Åtgärd | Var | Dagsproduktion i dag | Med bäraren på plats | Största last per bärare | Kostnad |
|---|---|---|---|---|---|
| Nu | En bärare vid startförrådet | 167 | 167 | 1,36 | |
| En bärare till | | 167 | 192 | 0,68 | Ingen ledig bybo i dag; förrådet anställer själv när en blir ledig |
| Väg | Till fiskeboden vid det rika vattnet, 2 rutor | 181 | 181 | 1,12 | 4 sten, 8 arbetstimmar |
| Ett tredje förråd | Vid det rika vattnet | 167 | 192 | 0,56 | 10 timmer, 5 sten, 16 arbetstimmar och en bybo som bärare; obemannat tills en bybo blir ledig |

*Dagsproduktion* är samma viktade balansmått som i ekonomi v1 och visas aldrig för spelaren; bladet visar i stället hur mycket mer av varje vara som når fram.
*Största last per bärare* är bärartimmarna som behövs per bärare vid det mest belastade förrådet; över 1 hinner bärarna inte.
Utan ledig bybo är vägen det enda som hjälper i dag.
En bärare till och ett tredje förråd ger lika mycket när de fått sin bybo, men förrådet lättar mer på bärarna och kostar mer att bygga.
Nästa gryning, när byn vuxit med en bybo, anställer startförrådet själv en bärare till; då når allt fram.
I sju dygn använder strategierna 29–72 % av bärarnas tid; den naiva strategin, som lägger allt kring startförrådet, ligger på 69 %.

## 8. Vad krafterna kostar

Krafternas kostnader i ekonomi v1 gäller oförändrade.
Det nya:

| Kraft | Handling | Kostnad | Effekt |
|---|---|---|---|
| Styrka | Varv | 80 | Knarrar som höjer sjövägarnas kapacitet från 40 upp till 80 per dygn |
| Ork | Arbeta extra på ett förråd | 30 | Bärarna bär 50 % mer under dagen |

- **Styrkan tar fortfarande de stora stegen.**
  Sågverk, väveri, brygga och varv är de effektivare kedjorna och sjöfarten, långhuset bor tätare än stugan, och uppgraderingarna lyfter det som redan står.
  Alla nya bostäder och kedjorna till nivåernas behov kostar ingen Styrka, så att varje nivå nås utan träning (D-43, INV-R4); varvet har ingen handkraft (D-117).
- **Utforskning hopar sig fortfarande.**
  Alla strategier slutar sex veckor med Utforskning på eller nära taket 800, som i [ekonomi v1, lärdom 1](./economy-v1.md#9-lärdomar-inför-prototypen); fynden i M5 och dimman i epok 1 är svaret, inte ekonomin.
- **Styrkan räcker till mer än byggnaderna.**
  I de långa körningarna går överskottet till uppgraderingar till nivå 2, av material som blir över när bostäder och kedjor fått sitt.
  I epok 1 tävlar vårdkasens etapper, 3 × 500 Styrka, om samma kraft; det räknas i TASK-054 (#137).

## 9. Sju dygn med tre strategier

Alla tre körs med samma måttliga egenvård som i v1: 7 500 steg och 6,5 h sömn varje dygn, tre pass i veckan.
Första veckan följer varje strategi sin egen plan.

- **Närområdet:** som i v1, kartlägger Hemön två gånger och bygger ett kluster kring stenådern och urskogen med ett andra förråd, sågverk, tre långhus och fiskebod vid det rika vattnet.
- **Havet:** som i v1, men Nyöns brygga, fårgård och stenbrott går före den första bostaden, som blir en stuga (avsnitt 6).
- **Bygden** (ny): kartlägger närområdet, bygger fårgård och stenbrott vid västra berget med ett förråd emellan, sågverk och väveri, stugor och ett långhus, och klär byborna så att de växer med två om dagen.
  Den når 20 bybor dygn 7 och börjar på bönderna.

Som jämförelse körs samma **naiva** strategi med **hög** egenvård som i v1.

| Körning | Egenvård | Bybor | Nivåer T/B/H | Byggnader | Bosättningar | Plankor | Sten | Bärarnas last | Dagsproduktion | Utvecklingsvärde | Använt U/S/O | Sparat U/S/O | Värde per använd kraft |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Närområdet | måttlig | 19 | 19/0/0 | 11 | 1 | 47 | 210 | 72 % | 228 | 1 407 | 180/260/440 | 461/110/229 | 1,13 |
| Havet | måttlig | 21 | 21/0/0 | 12 | 2 | 42 | 29 | 29 % | 246 | 1 284 | 250/210/360 | 426/160/260 | 1,07 |
| Bygden | måttlig | 20 | 20/0/0 | 12 | 1 | 2 | 67 | 54 % | 207 | 1 237 | 180/150/470 | 461/220/214 | 1,03 |
| Naiv | hög | 19 | 19/0/0 | 10 | 1 | 3 | 54 | 69 % | 211 | 1 173 | 180/360/630 | 520/85/96 | 0,65 |
| Utan träning | utan träning | 19 | 19/0/0 | 11 | 1 | 16 | 150 | 66 % | 156 | 1 162 | 180/0/480 | 461/100/204 | 1,14 |
| Ingen spelare | ingen | 16 | 16/0/0 | 4 | 1 | 20 | 20 | 37 % | 48 | 584 | 0/0/0 | 100/100/310 | – |
| Ingen spelare | måttlig | 16 | 16/0/0 | 4 | 1 | 20 | 20 | 37 % | 48 | 584 | 0/0/0 | 551/370/492 | – |

*Utvecklingsvärde* är v1:s balansmått med nivåerna inräknade: torpare × 15, bönder × 25 och hantverkare × 40, plus halva värdet av lagren och det som väntar vid byggnaderna och förråden, plus tre dygns produktion.
*Dagsproduktion* är produktionskapaciteten per dag med den andel som bärarna hinner få fram.
*Bärarnas last* är den andel av bärarnas tid som gick åt under veckan.
Startvärdet är 409, som i v1.

**Olika men jämbördiga.**
Närområdet slutar med ett stenlager och ett kluster som bärarna hinner med, Havet med två bosättningar, flest bybor och högst dagsproduktion, och Bygden med 20 bybor och bönderna upplåsta.
Utvecklingsvärdena skiljer sig med 12 %, mot 9 % i v1, och värdet per använd kraft med 9 %.

**Strategin hinner ikapp (D-34).**
Den naiva strategin med hög egenvård får 0,65 i värde per använd kraft, och den sämsta genomtänkta 1,03: 1,58 gånger, där [forces.md](./forces.md) kräver minst 1,3.
Kalkylen stoppar om kvoten blir lägre.
Den naiva strategin förlorar på stenbrottet vid toppen, som i v1, och på att allt den bygger hör till startförrådet, vars bärare tas från byggandet.

**Utan träning (D-43).**
Den som valt bort träning växer till 19 bybor som de andra, med stugor, men dagsproduktionen blir 156 mot 207–246, eftersom sågverk, väveri och brygga kräver Styrka.
Den når bönder vecka 2 och hantverkare vecka 3 med garveri, bryggeri, bigård, mjöderi och stugor (avsnitt 10).

### Samma beslut med mer egenvård

D-34:s andra halva säger att mer egenvård alltid syns i världen vid samma strategi.
Kalkylen prövar det med exakt samma beslut: strategin spelas med måttlig egenvård, och samma kommandon spelas sedan med hög.
Den höga profilen lägger det Ork den har utöver den måttliga på extraarbete på de råvarubyggnader som gör mest; Utforskning och Styrka utöver den måttliga sparas, eftersom de skulle ta material eller mark från besluten.

| Strategi | Utvecklingsvärde måttlig/hög dygn 7 | Dygn 14 | Dygn 28 | Dygn 42 | Sämsta dygnet | Dygn under måttlig |
|---|---|---|---|---|---|---|
| Närområdet | 1 407 / 1 407 | 2 250 / 2 250 | 4 601 / 4 610 | 5 824 / 5 834 | +0 % | 0 av 42 |
| Havet | 1 284 / 1 284 | 1 908 / 1 913 | 3 983 / 3 987 | 5 464 / 5 464 | +0 % | 0 av 42 |
| Bygden | 1 237 / 1 243 | 2 409 / 2 421 | 4 848 / 4 884 | 6 421 / 6 429 | Dygn 36: −0,07 % | 6 av 42 |

Mer egenvård syns alltså mest i saldot och i epokens tempo ([epok 1, avsnitt 5](./epoch-1.md#5-tempo)), och lite i världen genom extraarbetet.
Vid varje veckas slut är världen med hög egenvård minst lika stor som med måttlig.
I Bygden är den ändå något mindre i 6 dygn, som mest 0,07 %.
Orsaken är bärarna: att arbeta extra fyller en råvarubyggnads utlager snabbare, bärarna hämtar där det väntar mest, och annat väntar längre, så att till exempel en komfortbybo kommer ett dygn senare.
D-119 godtar det: D-34 gäller över veckor, och kalkylen stoppar om en vecka slutar mindre eller om ett enskilt dygn blir mer än 0,1 % mindre.
Spärren ligger strax över de 0,07 % som kalkylen mäter, så att en regeländring som förvärrar dippen syns.
Med råvaran till förädlingen först, i stället för D-111, blir dippen större: som mest 0,82 %.

## 10. Sex veckor

Från vecka 2 fortsätter alla genomtänkta strategier med samma planerare: bostäder när de tar slut, mat, timmer och sten när de inte räcker, kläder och bondgårdar efter steg 1, dryck och hantverkargårdar efter steg 2, en brygga, en expedition och en bosättning på Nyön med stuga, fiskebod och bondgård, och tjärdal och varv efter steg 3.
Planeraren lämnar alltid två lediga bybor som bygger och uppgraderar bara av material som blir över.
*Turen* är den guidade turen och epok 1:s första vecka (avsnitt 5) följd av samma planerare, och den naiva strategin fortsätter med sin plan.

| Körning | Egenvård | Bönder | Hantverkare | Sjöfart | Bärarna hinner inte | Bybor | Hemön T/B/H | Nyön T/B/H | Byggnader | Utvecklingsvärde vecka 2/4/6 |
|---|---|---|---|---|---|---|---|---|---|---|
| Närområdet | måttlig | vecka 2 | vecka 3 | vecka 4 | vecka 1 | 79 | 37/10/14 | 12/6/0 | 42 | 2 250 / 4 601 / 5 824 |
| Havet | måttlig | vecka 2 | vecka 4 | vecka 5 | vecka 6 | 83 | 33/10/22 | 12/6/0 | 39 | 1 908 / 3 983 / 5 464 |
| Bygden | måttlig | vecka 1 | vecka 2 | vecka 3 | vecka 3 | 105 | 33/16/38 | 12/6/0 | 51 | 2 409 / 4 848 / 6 421 |
| Turen | måttlig | vecka 2 | vecka 2 | vecka 4 | vecka 3 | 84 | 16/16/34 | 12/6/0 | 45 | 2 544 / 4 502 / 5 296 |
| Turen | manuell | vecka 2 | vecka 3 | vecka 5 | vecka 4 | 86 | 26/14/26 | 8/12/0 | 45 | 2 095 / 4 263 / 5 454 |
| Naiv | hög | vecka 2 | – | – | – | 32 | 32/0/0 | – | 10 | 1 592 / 1 940 / 2 230 |
| Utan träning | utan träning | vecka 2 | vecka 3 | – | vecka 5 | 74 | 30/12/32 | – | 40 | 1 353 / 3 093 / 3 955 |
| Havet | hög | vecka 2 | vecka 4 | vecka 5 | vecka 6 | 84 | 32/10/24 | 12/6/0 | 40 | 1 913 / 3 993 / 5 502 |
| Ingen spelare | ingen | – | – | – | – | 16 | 16/0/0 | – | 4 | 643 / 671 / 671 |

- **Upplåsningen sprids över epokens första halva:** bönder vecka 1–2, hantverkare vecka 2–4 och sjöfarten vecka 3–5.
- **Enbart manuell inmatning ligger en vecka efter normal** för hantverkarna och sjöfarten.
- **Nyön får en egen bosättning med egna nivåer.**
  Den växer till 18–20 bybor, och med kläder från hemön stiger 6–12 av dem till bönder; flytten, öns behov och sjövägen åt båda hållen räknas alltså.
- **Utan träning nås alla tre nivåerna**, med garveri, bryggeri, bigård, mjöderi och stugor.
  Sjöfarten kräver en sjöväg, och utan Styrka kommer bryggan med handkraft efter 7 dagar (D-65), som kalkylen inte räknar.
- **Den naiva strategin står still på 32 bybor**, eftersom den aldrig bygger fler bostäder; det visar bara att planen tar slut.
- **Långa körningar säger lite om vilken strategi som är bäst,** eftersom alla följer samma planerare från vecka 2.
  Det prövar balansbotarna i TASK-053 (#136) med egna strategier och kartor.
- **Byarna blir större än i dag.**
  Med nivåernas tätare bostäder når strategierna 74–105 bybor vecka 6, mot 72 vid dygn 45 i [epok 1:s tempo](./epoch-1.md#vecka-då-spelaren-når-varje-steg).
  Elden kräver 50 bybor, så epokens tempo kan bli snabbare; det prövas i TASK-054 (#137) med epokens kalkyl ovanpå ekonomi v2.

## 11. Utan egenvård

Utan spelare och utan egenvård (sista raderna i tabellerna ovan):

- Byborna växer från 12 till 16 av egen kraft och stannar vid bostadstaket, som i v1.
- Startförrådets enda bärare hinner med startbyn; den använder 37 % av sin tid.
- **Inget förfaller.**
  Kalkylen kontrollerar varje timme att antalet bybor, bönder och hantverkare, byggnader, nivåer, upptäckta rutor, bosättningar, upplåsta steg och vägrutor aldrig minskar och att inga lager, utlager eller saldon blir negativa eller lager över sin kapacitet (INV-V1, INV-R1, INV-F1).
- Samma kontroller håller över ett helt år utan spelare och egenvård (INV-V4).
- Med egenvård men utan beslut händer exakt samma sak i världen; krafterna sparas bara (D-14).

## 12. Migration från ekonomi v1

En värld från ekonomi v1 blir en värld i ekonomi v2 utan att något går förlorat ([ADR-0006](../adr/0006-saves-are-migrated-snapshots.md)).
Ekonomi v2 kommer i flera skivor, och varje skiva som ändrar ett utfall höjer regelversionen med sin egen migration; tillsammans gör de det här:

1. **Byggnader, nivåer, krafter, dimma och skog står kvar** som de är.
2. **Byborna blir torpare.**
   De som bemannar byggnader på en annan ö än hemön blir den öns bosättning, med de 4 bostäderna i öns brygga och öns stugor och långhus.
   Har en ö fler bybor än bostäder bor de kvar, och bosättningen växer inte förrän det finns plats.
3. **Lagren.**
   Hemöns lager blir hemöns bosättnings, och varje annan ös lager blir dess bosättnings.
   Det som inte ryms i en ös lager skeppas hem i migrationen.
   Det som inte heller ryms där väntar vid öns förråd eller brygga och flyttas in i lagret när det finns plats, så att inget lager är över sin kapacitet (INV-R1).
4. **Vägar.**
   En väg till en byggnad blir vägrutor längs sökvägen till närmaste bemannade förråd eller brygga, eller till närmaste förråd när inget bemannat nås.
   Stenen för varje betalt vägsteg som inte blir en ny ruta läggs tillbaka i lagret, 2 sten per steg: det sista steget in i förrådet och rutor som en annan väg redan har.
   Varje byggnad hör sedan till det bemannade förråd som bärarna når snabbast på vägarna.
5. **Bärare.**
   Förrådens och bryggornas bemanning blir en bärare var, och vid nästa gryning tar de fler om varorna kräver det.
6. **Upplåsningen börjar där världen står:** en värld med 20 bybor har steg 1 direkt, och introduktionerna kommer en per dag.
7. **Byggkön står kvar** med det arbete som är kvar.
   En väg i kön blir ett vägprojekt på varje ruta längs sökvägen som inte redan har väg, lagd eller i kön, och de delar på arbetet som var kvar, högst en rutas 4 timmar var.
   Stenen för stegen som inte blir någon ruta går tillbaka som i punkt 4.

Journaler från föregående regelversion spelas inte upp, eftersom de skulle ge en annan värld än den spelaren byggde, som vid regelversion 4 och 5.
Bärarnas tillstånd, vägnätet, det som väntar vid förråden och bosättningarnas köer ska tas med i framspolningens jämförelse ([invariants.md, INV-V4](./invariants.md)).
Kalkylen migrerar fyra världar efter sju dygn i v1 och spelar sju dygn till i v2.
*Havet med fulla lager* har hemöns sten vid taket och 120 sten på Nyön, som en brygga med plats för 50 inte rymmer, som en v1-värld som spelats längre:

| Värld | Bybor | Byggnader | Bosättningar | Vägrutor | Sten tillbaka | Väntar vid förrådet | Bybor dygn 14 | Byggnader dygn 14 | Utvecklingsvärde dygn 14 |
|---|---|---|---|---|---|---|---|---|---|
| Närområdet | 19 | 11 | 1 | 0 | 0 | 0 | 27 | 18 | 2 227 |
| Närområdet med väg till fiskeboden | 19 | 11 | 1 | 2 | 2 | 0 | 27 | 19 | 2 200 |
| Havet | 20 | 12 | 2 | 0 | 0 | 0 | 37 | 20 | 2 090 |
| Havet med fulla lager | 20 | 12 | 2 | 0 | 0 | 20 | 38 | 20 | 2 312 |

I alla fyra är byborna, byggnaderna, nivåerna, krafterna och den upptäckta kartan desamma före och efter, ingen vara har försvunnit och inget lager är över sin kapacitet; kalkylen stoppar annars.

## 13. Lärdomar och risker

1. **En bosättning per ö gör havet långsammare.**
   Havet behövde en ny byggordning för att hålla jämna steg, och byggarna från hemön (D-113) är det som håller havet levande; utan dem får Havet 0,85 i värde per kraft i stället för 1,07.
2. **Bärarna gör folk ännu knappare.**
   Varje bärare är en byggare mindre, och utan lediga bybor är vägen den enda åtgärd som hjälper.
3. **Bärarnas ordning kan ta tillbaka en del av extraarbetet.**
   Mer produktion på ett ställe kan låta annat vänta längre, så mer egenvård kan ge en något mindre värld några dygn (D-119).
4. **Nivåerna gör byarna större.**
   Bönder och hantverkare bor tätare, så byarna växer förbi epok 1:s tempo; TASK-054 behöver räkna om tempot.
5. **Åkern är knapp på prototypens karta.**
   Två åkerrutor delas mellan säd till mat och säd till öl; generatorn bör ge startön mer odlingsmark.
6. **Ekonomi v1 förbrukar komfortens tyg även när ingen bybo får plats.**
   V2 rättar det; v1 behåller det tills v2 ersätter den.
7. **Bemanningen är klibbig.**
   Med bemanning som räknades om från början kunde en ny byggnad för bönder ta en bonde från en byggnad längre bak och lämna den obemannad till nästa gryning; v2 låter en ny byggnad bara ta lediga bybor, och kalkylen stoppar om en byggnad förlorar sin bemanning.
8. **Kalkylens planerare är enkel.**
   Från vecka 2 är siffrorna en planerares, inte en skicklig spelares; balansbotarna i TASK-048 (#131) och TASK-053 (#136) ersätter den.

## 14. Beslut

Produktägarens svar på delfrågorna, loggade i PRD:n; F1–F10 besvarades i v2.0 och F5 och F11 i en andra omgång efter granskningen:

- **D-109 (F1):** bärarna är bybor som förrådet anställer själv vid gryningen, en till fyra lediga torpare, med spelarens tak.
  Kalkylen räknar alternativen: bärare för hand ger den naiva strategin 0,46 i värde per kraft i stället för 0,65, och att lämna två byggare åt kön ger Närområdet 0,96 i stället för 1,13.
- **D-110 (F2):** avståndet kostar bärartid i stället för produktion, och avståndsklasserna försvinner; avståndsdelen av D-103 ersätts.
- **D-111 (F3):** bärarna tar först där det väntar mest, och spelarens prioritet från TASK-044 går före.
  Förtydligat i TASK-051: prioriteten är ett uttryckligt läge, så bara en byggnad som spelaren flyttat upp i bemanningsordningen tar bärarna först, och resten tas där mest väntar.
- **D-112 (F4):** vägar läggs ruta för ruta, med genvägen *Väg till förrådet*.
- **D-113 (F5), det enda tillägget till D-106:** lediga bybor får bygga på öar som sjövägen förbinder med deras, eftersom att bygga inte är att bemanna.
  I övrigt gäller D-106: inga nybyggare, högst en flytt per gryning i hela världen, och byggmaterial från hemön tar plats på sjövägen.
  Med D-106 utan tillägget får Havet 0,85 i stället för 1,07, och med byggmaterialet utanför sjövägen 1,09.
- **D-114 (F6):** upplåsningen gäller hela världen.
- **D-115 (F7):** ett uppfyllt behov ger bara tillväxt och att stiga en nivå.
- **D-116 (F8):** hantverkare bygger dubbelt så fort och bemannar varvet.
- **D-117 (F9):** varvet kräver Styrka och har ingen handkraft.
- **D-118 (F10):** avvikelserna från dagens första vecka godtas: bärarna i introduktion 2 med flaskhalsen som diagnos, bönderna dygn 7 för den som satsar på tillväxt, och Nyön som egen bosättning i introduktion 6.
- **D-119 (F11):** D-34:s andra halva gäller över veckor.

Lagerreglerna i TASK-046 (#129) avgjordes efter v2.3 och står i [ekonomi v1, avsnitt 5](./economy-v1.md#lagerregler):

- **D-125:** förädlingen lämnar det största av sitt minimilager och öns reservering, och diagnostiken säger vilket som håller kvar varan.
- **D-126:** importprioriteten är en ordnad lista med alla varor, med dagens fasta ordning som standard.
- **D-127:** reserveringen hindrar bara skeppning hemåt och förädling, inte byggmaterial och mat som skeppas ut till öarna.
  Med samma beslut får mer egenvård aldrig ge en mindre värld vid veckans slut, och ett enskilt dygn får vara högst 0,1 % mindre; kalkylen mäter 0,07 % (avsnitt 9).
