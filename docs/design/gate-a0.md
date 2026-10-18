# Grind A0: en hel dag med siffror

> **Status:** [BESLUTAT] v1.0, godkänd av produktägaren 2026-10-18 (D-82–D-87)  
> **Ärende:** [TASK-012](https://github.com/Tiico/eirora/issues/27)  
> **Läge:** `main` 87e91bc, 2026-10-18

Kvalitetsgrind A0 kräver två saker innan M0 stängs (ROADMAP, M0).
Det ska gå att beskriva en hel dag inklusive vila med konkreta siffror, och inget beslut som blockerar prototypen får vara öppet.
Avsnitt 1 är dagen, avsnitt 2 de öppna frågorna, avsnitt 3 det som skiljer dokumenten från kalkylen och avsnitt 4 grindens bedömning.

## 1. Ett helt spelardygn

Avsnittet följer ett spelardygn från gryning till gryning med siffror ur kalkylen [`economy_v1.py`](./models/economy_v1.py).
Varje siffra går att räkna om med kommandona i [avsnitt 1.9](#19-räkna-om-siffrorna), körda från repots rot.

### 1.1 Spelare, strategi och dygn

- **Egenvård:** profilen *måttlig* (`PROFILES["måttlig"]`), med 7 500 steg och 6,5 h sömn varje spelardygn och tre pass i veckan, på dygn 1, 3 och 5.
  Den är referensprofil i [krafternas regler](./forces.md) (avsnitt 4) och [ekonomi v1](./economy-v1.md) (avsnitt 7), och samma som *normal* i [epok 1](./epoch-1.md).
- **Strategi:** *Närområdet* (`strategy_land`).
  Det är den körning i ekonomi v1 som når högst utvecklingsvärde (1 188), och den håller sig till en ö.
- **Dygn:** spelardygn 5 (`day = 4`).
  Det är veckans enda dygn där alla tre vanorna ger kraft och där vi samtidigt ser sparandets tak, ett bygge med Byggkraft, dagsverke, rustning, minimilagret och bybornas tillväxt.
- **Viloperiod:** 23:00–07:00, som i resa 3 i [UX-resorna](./ux-journeys.md).
  Det motsvarar kalkylens 16 aktiva timmar och 8 timmars vila, och kalkylens timme 0 är 07:00–08:00.

### 1.2 Före gryningen

| | Värde |
|---|---|
| Krafter (Utforskning / Byggkraft / Gryningskraft) | 264 / 200 / 238 |
| Lager | 56 fisk, 63,5 säd, 37 timmer, 17 plank, 124,9 sten |
| Lagertak | 300 per resurs (två förråd × 150) |
| Bybor | 16, med bostad för 24 (16 från start och 8 från långhuset vid L1) |
| Byggnader | 10: två förråd, fiskebod, två åkrar, två huggarkojor, stenbrott på stenådern, sågverk och långhus |
| Byggkö | Fiskeboden vid det rika fiskevattnet, 10 arbetstimmar kvar |
| Långsamma processer | 4 mogen säd i varje åker; skogen vid huggarkojan F1 har 31,75 timmer |

### 1.3 Gryning 07:00

Fasen går från `RESTING` via `DAWN` till `ACTIVE`, och det styrs bara av schema och tid (INV-T1, INV-T2).
Simuleringen krediterar Gryningskraftens grundnivå och fyller på med sömnen, krediterar bidrag från natten (INV-T9), låter byborna äta och låter byn växa.

| Vana | Egenvård | Kurva ([krafternas regler](./forces.md), avsnitt 3) | Kraft | Saldo | Sparandets tak (D-35) |
|---|---|---|---|---|---|
| Rörelse | 7 500 steg | 4 000 × 15/1 000 + 3 500 × 7,5/1 000 = 60 + 26,25 = 86,25, avrundat nedåt | 86 Utforskning | 264 → 350 | Under 400, så hela tillskottet räknas |
| Träning | 1 pass, det tredje på sju spelardygn | G(3) − B = 270 − (100 + 0 + 90 + 0) | 80 Byggkraft | 200 → 280 | Under 600, så hela tillskottet räknas |
| Sömn | 6,5 h | 40 + 6,5 × 8 | 92 Gryningskraft | 238 → 315 | 62 räknas fullt upp till 300, och resterande 30 räknas till hälften, alltså 15 |

Avkastningen avtar: steg mellan 4 000 och 8 000 ger hälften så mycket per 1 000 som de första 4 000, och veckans tredje pass ger 80 mot 100 för det första (INV-F3).
Grundnivån 40 krediteras genast vid gryningen, och sömnen fyller på med 52 upp till kurvans värde 92 (INV-F4).
Saldot ökar ändå bara med 77 och inte med 92, eftersom Gryningskraften passerar det fulla taket 300.
Inget av det som redan är sparat förfaller (INV-F6).
Varje vana ger bara sin egen kraft (INV-F2).
Inmatade och uppmätta bidrag ger samma kraft (INV-F7), och kraften blir densamma oavsett hur bidragen delas upp eller i vilken ordning de kommer (INV-F8, INV-B1).
Kalkylen krediterar hela dygnets steg och pass redan vid gryningen, medan spelet krediterar dem när datan kommer in under dygnet (se [avsnitt 3](#3-avvikelser-mellan-dokument-och-kalkyl)).
Det här dygnet påverkar förenklingen inget av världens utfall, vilket varianten med grundnivå i avsnitt 1.8 visar.

**Basbehov och bybor.**
De 16 byborna äter 1 mat var och tar fisk först, så fisken går från 56 till 40 och säden lämnas orörd.
Byborna är mätta och det finns bostad, så antalet går från 16 till 17.
Det finns ingen vadmal, så ingen extra bybo tillkommer för komfort.
Bemanningen fyller 16 platser, och 1 bybo blir över för att bygga (INV-V2).

**Vad spelaren ser.**
Spelaren ser en kort gryningsövergång och sedan den dagliga rapporten, *God morgon*.
Under *Egenvård* står: Rörelse 7 500 steg → Utforskning 86, Träning 1 pass → Byggkraft 80 och Sömn 6 h 30 min → Gryningskraft 92.
Under *I världen* står till exempel att sågverket blev klart i går och att säden har mognat i natt.

### 1.4 Morgonens beslut

| Kommando | Kraft | Varor | Effekt |
|---|---|---|---|
| Bygga långhus vid L2 | 60 Byggkraft | 10 timmer, 15 plank, 10 sten | 8 bostäder till efter 40 arbetstimmar |
| Dagsverke på stenbrottet | 30 Gryningskraft | | Stenbrottet producerar 50 % mer under dygnet |
| Dagsverke på sågverket | 30 Gryningskraft | | Sågverket producerar 50 % mer under dygnet |
| Rusta | 20 Gryningskraft | | Fiskeboden först i kön går från 10 till 2 arbetstimmar |

Efter besluten är krafterna 350 / 220 / 235, eftersom varje kommando minskar saldot med exakt sin kostnad (INV-F5).
Lagret innehåller då 40 fisk, 63,5 säd, 27 timmer, 2 plank och 114,9 sten.
Strategin försöker också uppgradera sågverket och bygga ett tredje långhus.
Båda nekas eftersom bara 2 plank finns kvar, och ett nekat kommando ändrar ingenting (INV-D7).
Utforskningen, 350, blir oanvänd: båda kartläggningarna är redan gjorda, och Närområdet skickar ingen expedition (lärdom 1 i ekonomi v1).

### 1.5 Dagen 07:00–23:00

| Byggnad | Per aktiv timme | Under dagen |
|---|---|---|
| Fiskebod (K1) | 1,25 fisk × 1,0 (1 bricka till förrådet) | +20 fisk |
| Åker (O1) | Säden växer med 0,5 per timme dygnet runt och skördas med högst 2 per timme | +12 säd |
| Åker (O2, 2 brickor från förrådet) | Säden växer med 0,5 per timme och skördas med högst 2 × 0,75 = 1,5 per timme | +12 säd |
| Huggarkoja (F1, 1 skogsbricka) | 0,5 timmer × 1,0 | +8 timmer |
| Huggarkoja (urskogen F3, rikedom 1,25, 2 brickor bort) | 1,0 × 1,25 × 0,75 = 0,9375 timmer | +15 timmer |
| Stenbrott (stenådern M2, rikedom 2) | 0,75 × 2,0 × 1,0 × 1,5 (dagsverke) = 2,25 sten | +36 sten |
| Sågverk (E2) | 1,5 plank av 3 timmer fram till kl. 12, när minimilagret på 20 timmer nås; därefter bara det huggarkojorna hinner ge, ≈ 0,72 plank | +15 plank, −30 timmer |

Den lediga bybon gör klart fiskeboden vid det rika fiskevattnet kl. 09.
Fiskeboden kan ändå inte bemannas förrän två bybor är lediga.
Resten av dagen bygger bybon på långhuset, som har 26 av 40 arbetstimmar kvar kl. 23.

| Tid | Fisk | Säd | Timmer | Plank | Sten |
|---|---|---|---|---|---|
| 07:00, efter besluten | 40 | 63,5 | 27 | 2 | 114,9 |
| 11:00 | 45 | 75,5 | 20,75 | 8 | 123,9 |
| 23:00, skymning | 60 | 87,5 | 20 | 17 | 150,9 |
| Dagens netto | +20 | +24 | −7 | +15 | +36 |

Inget lager hamnar under 0 eller över taket 300 (INV-R1).
Sågverket omvandlar exakt 2 timmer till 1 plank (INV-R2).
Minimilagret är en gratis planeringsinställning som ser till att det alltid finns timmer kvar till byggen (LOG-06, D-44).
Dagsverket på sågverket ger ingenting i dag: med eller utan det blir det 15 plank.
Det beror på att bara (27 − 20) + 23 = 30 timmer finns över minimilagret, och det räcker till 15 plank.
Samma 30 Gryningskraft på fiskeboden hade gett 1,25 × 0,5 × 16 = 10 fisk till.
Det bekräftar lärdom 2 och 3 i ekonomi v1: ved är grundflaskhalsen, och morgonens val har verklig betydelse.

### 1.6 Skymning 22:30–23:00

Klockan 22:30 visas en lugn rad om appen är öppen: *Byn gör sig redo för natten om en halvtimme.*
Raden är bara presentation och skickas aldrig som notis (UX-07).
Kommandon godkänns fram till 23:00, och ett kommando efter 23:00 nekas utan att något blir halvgjort (INV-D7, INV-D8).
Klockan 23:00 går fasen från `ACTIVE` till `RESTING` enbart av schema och tid, aldrig av sömn (INV-T1, INV-T2).

### 1.7 Vila 23:00–07:00 med vilolås

Spelaren ser nattvyn: *Byn vilar. Världen öppnar klockan 7.*
Under *Medan byn sover* står att säden mognar och att skogen vid huggarkojan växer tillbaka.
Vilolåset nekar varje spelpåverkande kommando, som att bygga, lägga dagsverke eller kartlägga (INV-T3, INV-D7).
Administrativa kommandon och registrering av egenvård är tillåtna.
Ett bidrag som skrivs in under vilan, till exempel en kvällspromenad på 30 aktiva minuter, alltså 3 000 steg (D-41), räknas till det pågående spelardygnet och krediteras senast vid gryningen (INV-T9, D-54).
Viloperiodens 8 timmar ligger inom 6–10 timmar (INV-T6).

| | 23:00 | 07:00 | Regel |
|---|---|---|---|
| Lager (fisk / säd / timmer / plank / sten) | 60 / 87,5 / 20 / 17 / 150,9 | Oförändrat | INV-T4, INV-R3 |
| Långhuset vid L2 | 26 h kvar | 26 h kvar | INV-T4 |
| Krafter | 350 / 220 / 235 | Oförändrade | INV-F6 |
| Bybor och byggnader | 17 och 11 | Oförändrade | INV-V1 |
| Mogen säd i åkrarna O1 och O2 | 0 + 0 | 4 + 4 (0,5 × 8 h) | INV-T5 |
| Skogen vid huggarkojan F1 (1 bricka) | 27,75 | 29,75 (0,25 × 8 h) | INV-T5 |
| Urskogen F3 (4 brickor) | 159,06 | 160, som är taket 4 × 40 | INV-T5 |

### 1.8 Nästa gryning 07:00 (spelardygn 6)

| Kraft | Uträkning | Saldo |
|---|---|---|
| Utforskning | 350 + 86: 50 räknas fullt upp till 400 och 36 räknas till hälften, alltså 18 | 418 |
| Byggkraft | Inget pass i dag ger 0, och vilodagar kostar inget (D-63) | 220 |
| Gryningskraft | 235 + 92: 65 räknas fullt upp till 300 och 27 räknas till hälften, alltså 13,5 | 313,5 |

De 17 byborna äter 17 fisk, så fisken går från 60 till 43, och byn växer till 18 bybor.
Två bybor bemannar den nya fiskeboden, som ger 1,25 × 1,5 × 0,75 ≈ 1,41 fisk per timme.
Därmed finns ingen ledig bybo kvar för långhuset, och folk är den knappa resursen (ekonomi v1, avsnitt 1).
De 8 säd som mognat under natten skördas under morgonens första timmar.
En morgonnotis kommer bara om något nytt har hänt, högst en per spelardygn och alltid tyst (D-56).

| | Gryning dygn 5 | Skymning dygn 5 | Gryning dygn 6 |
|---|---|---|---|
| Krafter | 264 / 200 / 238 → 350 / 280 / 315 | 350 / 220 / 235 | 418 / 220 / 313,5 |
| Bybor | 16 → 17 | 17 | 18 |
| Byggnader | 10 | 11 | 11 |

**Samma dygn med grundnivå.**
Läget före gryningen är detsamma, men spelardygn 5 körs med profilen `ingen`: 0 steg, inget pass och ingen sömndata.

| Vana | Egenvård | Kraft | Saldo |
|---|---|---|---|
| Rörelse | 0 steg | 0 | 264 → 264 |
| Träning | Inget pass | 0 | 200 → 200 |
| Sömn | Ingen data (0 h ger också 40) | 40, grundnivån | 238 → 278 |

Med sparade krafter går samma beslut att fatta: 60 av 200 Byggkraft och 80 av 278 Gryningskraft.
Efter besluten är krafterna 264 / 140 / 198.
Världen blir identisk timme för timme: samma lager kl. 23, samma 18 bybor och samma 11 byggnader vid nästa gryning.

| Efter sju spelardygn | Måttlig egenvård varje dygn | Grundnivå på dygn 5 |
|---|---|---|
| Bybor | 19 | 19 |
| Byggnader | 12 | 12 |
| Sten i lager | 208 | 208 |
| Dagsproduktion | 182 | 182 |
| Utvecklingsvärde | 1 188 | 1 188 |
| Sparat (Utforskning / Byggkraft / Gryningskraft) | 461 / 110 / 233 | 418 / 30 / 221 |

Ett lågt dygn ger alltså mindre att spara, men aldrig ett avdrag.
Kurvorna ger 0 vid 0 och blir aldrig negativa (INV-F3), och sömnen ger aldrig mindre än grundnivån (INV-F4).
Inget i världen eller i de sparade krafterna går förlorat (INV-V1, INV-V3, INV-V4, INV-R3, INV-F6, D-17).
Ett pass som uteblir ger 0 just det dygnet, och den rullande veckan räknas vidare utan straff: `build_power` ger 100, 0, 90, 0, 0.
Rapporten har inga rader om saknad egenvård; bara sömnen visas alltid, med minst grundnivån.
En hel vecka utan egenvård och utan beslut finns i ekonomi v1, avsnitt 8: byn växer från 12 till 16 bybor, Gryningskraften blir 310 och inget förfaller.

### 1.9 Räkna om siffrorna

Kör från repots rot.
Kalkylens tabell för ekonomi v1:

```sh
python -I docs/design/models/economy_v1.py
```

Dygnet timme för timme (sätt `PROFIL = "ingen"` för dygnet med grundnivå):

```sh
python -I - <<'EOF'
import sys; sys.path.insert(0, "docs/design/models"); sys.stdout.reconfigure(encoding="utf-8")
import economy_v1 as ev
PROFIL = "måttlig"
w = ev.World()
for day in range(4):
    w.dawn(day, "måttlig"); ev.strategy_land(w, day)
    for h in range(24): w.hour(active=h < ev.ACTIVE_HOURS)
lager = lambda: {k: round(v, 2) for k, v in w.stock.items() if k not in ("ull", "vadmal")}
print("före gryningen:", w.forces, lager(), w.villagers)
w.dawn(4, PROFIL); print("gryning:", w.forces, lager(), w.villagers, w.free_villagers())
ev.strategy_land(w, 4); print("efter besluten:", w.forces, lager(), [(p.label, p.work_left) for p in w.projects])
for h in range(24):
    w.hour(active=h < ev.ACTIVE_HOURS)
    print(f"{(8 + h) % 24:02d}:00", lager(), [(p.label, p.work_left) for p in w.projects])
w.dawn(5, "måttlig"); print("nästa gryning:", w.forces, lager(), w.villagers, w.free_villagers())
EOF
```

Veckan med och utan dygnet med grundnivå:

```sh
python -I - <<'EOF'
import sys; sys.path.insert(0, "docs/design/models"); sys.stdout.reconfigure(encoding="utf-8")
import economy_v1 as ev
for low in (None, 4):
    w = ev.World()
    for day in range(ev.DAYS):
        w.dawn(day, "ingen" if day == low else "måttlig"); ev.strategy_land(w, day)
        for h in range(24): w.hour(active=h < ev.ACTIVE_HOURS)
    print(low, w.snapshot(), round(w.daily_output()), round(w.value()), w.forces)
EOF
```

Kurvorna från vanorna, i en egen process eftersom `epoch_1` utökar tabellerna i `economy_v1`:

```sh
python -I - <<'EOF'
import sys; sys.path.insert(0, "docs/design/models")
import epoch_1 as e
print(e.exploration(7500), e.exploration(0), e.dawn_power(6.5), e.dawn_power(None), e.dawn_power(0))
for passes in ([1, 0, 1, 0, 1], [1, 0, 1, 0, 0]):
    credits = []
    for d in range(5):
        credits.append(e.build_power(passes[:d + 1], credits, d, "teleskop"))
    print(passes, credits)
EOF
```

Förväntat: `86 0 92 40 40`, `[100, 0, 90, 0, 80]` och `[100, 0, 90, 0, 0]`.

## 2. Öppna frågor

Inventeringen omfattar varje `[ÖPPET]` och `[FÖRSLAG]` och varje O-fråga i `PRD.md` och `docs/design/`, inklusive kommentarerna i `docs/design/models/*.py`, samt frågor i avsnitt som heter *Öppna frågor*, *Olösta frågor* och *Kvar att avgöra*.
Varje post är prövad mot `ROADMAP.md`, `docs/adr/`, GitHub-ärendena och koden i `src/` och `client/`.
Med *blockerar M1* menas att ett M1-ärende (TASK-013, TASK-014, TASK-015 eller grind A1) inte kan slutföras utan beslutet.

### 2.1 O-frågor

| ID | Fråga | Nuvarande förslag | Blockerar M1 | Beslutas | Ärende/ADR |
|---|---|---|---|---|---|
| O-01 | Exakta värden för krafter, tak och avtagande avkastning | Startvärdena i forces.md | Nej: `ForceRules.cs` har forces.md:s kurvor och tak; trimningen hör till balansbotarna | M4/M5 | #31 (A1) hänvisar |
| O-02 | Innehållsomfång: byggnader, resurser, kedjor | Färre men tyngre typer; ekonomi v1 har 12 byggnader och 7 resurser | Nej: prototypen kör ekonomi v1 | M4 (ROADMAP säger M10) | – |
| O-03 | Antal och innehåll för epoker vid release | 3–4 epoker | Nej: M1 behöver bara epok 1:s första två veckor | M10 (PRD säger M5) | – |
| O-04 | Hur arkipelagen komponeras av gruppens världar | Fast havsyta med världarna runt ett gemensamt centrum | Nej: gäller grupper | M8 | #69 förbereder |
| O-05 | Sjövägar och handel när någon lämnar gruppen | Inga förluster för någon | Nej: gäller grupper | M8 | – |
| O-06 | Innehåll i supporter-prenumerationen | Kosmetik och bekvämlighet, aldrig kraft (D-25) | Nej: inga intäkter i prototypen | M10 | – |
| O-07 | Schemaregler vid resor och skiftarbete | Användartest; D-37, INV-T6 och INV-T8 gäller redan | Nej: vilolåset följer lokal tid (ADR-0005) | M7 | – |
| O-08 | Health Connect-adapter | – | Avgjord | – | ADR-0007 |
| O-09 | Backend, konton och databas | – | Avgjord | – | ADR-0008 |
| O-10 | Rimlighetskontroll av handel och gruppbidrag | Delad simulering och tak | Nej: gäller grupper | M8 | – |
| O-11 | Namnet Eirora | – | Avgjord: säkras i M12 | – | #40 |
| O-12 | Återstartsbonus | – | Avgjord: ingen bonus (D-52) | – | – |
| O-13 | Kvantitativa kvalitetsmål och målenheter | Budgetförslag i device-measurements.md, avsnitt 6 | Nej: M1 mäter bara på produktägarens telefon; budgetarna sätts i M2 | M2 | #30, #39 |

### 2.2 Markeringar i PRD.md

| Plats | Fråga | Blockerar M1 | Beslutas |
|---|---|---|---|
| PRD.md:130 | Produktpositionering `[FÖRSLAG]` | Nej: text för butik och marknadsföring | M12 |
| PRD.md:185 | Mätbara mål `[FÖRSLAG]` | Nej | M11 |
| PRD.md:296, :320, :373, :392, :423, :439, :457 | Markeringar som hör till O-02, O-03, O-04, O-05 och O-06 | Nej | Se O-frågan |
| PRD.md:459 | Forskning med minst tre inriktningar `[FÖRSLAG]` | Nej: forskning finns inte i prototypen | M5 |
| PRD.md:467 | Liten återstartsbonus `[FÖRSLAG]` | Nej: inaktuell, D-52 avgör den | – |
| PRD.md:555 | Numeriska budgetar `[ÖPPET]` | Nej: se O-13 | M2 |

### 2.3 Frågor i docs/design/

| Plats | Fråga | Blockerar M1 | Beslutas |
|---|---|---|---|
| Statusraden i core-fantasy.md, invariants.md, forces.md, economy-v1.md, epoch-1.md, ux-journeys.md och prototype-diary.md | Dokumenten är `[FÖRSLAG] v0.1` och väntar på produktägarens granskning | Ja, formellt: prototypen bygger på dem | A0, förslag 1 |
| device-measurements.md:114 | Batteriet mätt med fast eller automatisk ljusstyrka? | Ja: avgör hur batterimätningen i TASK-015 görs | M1, förslag 2 |
| device-measurements.md:115 | Behövs en andra, äldre Android-telefon redan i M1? | Nej: M1 mäter bara på produktägarens telefon | M2 (#39) |
| art-direction.md:40 | Gemensamma principer `[FÖRSLAG]` | Nej: D-61 valde riktningen | M9 |
| art-direction.md:55, :431; ux-journeys.md:510 | Följer dygnsljuset viloperioden eller solen? | Nej: avgjort i kod, `Atmosphere.cs` följer viloperioden | Loggas, förslag 3 |
| art-direction.md:70 | Kamera `[FÖRSLAG]`: 40–50° lutning, tre zoomnivåer, fri rotation | Nej: `CameraRig.cs` har 50° och tre zoomnivåer men ingen rotation | M9 |
| art-direction.md:432 | Sänks kameran automatiskt till nattvyn? | Nej: prototypen behåller spelkameran | M7/M9 |
| art-direction.md:433 | Syns byborna som små figurer? | Nej: avgjort i kod, `IslandView.cs` visar figurer på dagen | Loggas, förslag 3 |
| art-direction.md:434 | Syns mytologiska väsen i världen? | Nej | M9 |
| art-direction.md:358, :435 | Prestanda `[FÖRSLAG]` och lågklassenhet för referens | Nej: se O-13 | M2 (#39) |
| epoch-1.md:85; epoch_1.py:23 | Nytt innehåll i epok 1 `[FÖRSLAG]` | Nej: epoch-1.md:196 säger att M1 inte behöver det | M5 |
| epoch-1.md:198, :200 | Forskningens inriktningar; fynd i dimman och från expeditioner | Nej | M5 |
| epoch-1.md:202 | Räcker Gryningskraftens användningar? | Nej: prövas i prototypen som H1 | M5, med underlag från A1 |
| epoch-1.md:204 | Ytteröns innehåll och generatorns placering | Nej | M3 (#65) |
| epoch-1.md:206 | Guidens repliker och introduktionerna | Nej | M9 |
| forces.md:49 | Rullstolsskjut räknas som steg `[FÖRSLAG]` | Nej: aktiva minuter kan matas in manuellt (D-41) | M6 |
| forces.md:159 | Tak för egenrapporterade gruppbidrag `[FÖRSLAG]` | Nej: se O-10 | M8 |
| ux-journeys.md:409 | Ordningen vid gryning `[FÖRSLAG]` | Nej: avgjort i kod, se förslag 3 | Loggas, förslag 3 |
| ux-journeys.md:503 | Utkast för en påbörjad placering vid övergången `[FÖRSLAG]` | Nej: en förlorad obekräftad placering kostar bara ett nytt tryck | M7 |
| ux-journeys.md:505 | Gruppåtgärd köad offline före viloperioden `[FÖRSLAG]` | Nej: gäller grupper | M8 |
| core-fantasy.md:102–106 | Hypoteserna H1–H5 | Nej: det är dem M1 ska pröva | A1 (#31) |
| economy_v1.py:133 | Startkrafter från den guidade turen `[FÖRSLAG]` | Nej: inaktuell, D-45 avgör den | – |

### 2.4 Förslag på beslut

Ingen av O-frågorna blockerar prototypen.
De som gäller M1 är antingen redan avgjorda i koden (O-01:s startvärden) eller handlar om att mäta, inte om att besluta (O-13).
Två punkter behöver ändå avgöras för att grind A0 ska vara uppfylld och TASK-015 ska kunna slutföras, och en tredje är städning.

**1. Godkänn designdokumenten från M0.**
Förslag: när grind A0 godkänns ändras statusen för core-fantasy.md, invariants.md, forces.md, economy-v1.md, epoch-1.md, ux-journeys.md och prototype-diary.md från `[FÖRSLAG] v0.1` till godkänd v1.0.
Siffrorna förblir startvärden under O-01.
De inbäddade `[FÖRSLAG]` som hör till senare milstolpar står kvar med sin milstolpe.
Skäl: varje dokuments öppna frågor är redan besvarade av produktägaren och loggade i PRD (D-34–D-58, D-63–D-67), och prototypen är byggd på dokumenten.
Avvägning: efter godkännandet lyfts varje ändring efter lärdomar från prototypen med produktägaren, vilket veckogenomgången i prototypdagboken redan gör.

**2. Ljusstyrka vid batterimätningen.**
Förslag: det aktiva passet mäts med fast ljusstyrka på den nivå produktägaren brukar använda, och nivån skrivs in i rapporten.
Mätningen över ett helt spelardygn görs med automatisk ljusstyrka, som i vardagen.
Skäl: med fast ljusstyrka kan passet upprepas inom felmarginalen på 10 %, vilket behövs för att jämföra byggen, medan dygnsmätningen ska svara på om prototypen märks på batteriet en vanlig dag.
Avvägning: dygnets siffra kan bara jämföras mot budgeten, inte mellan byggen.

**3. Logga det koden redan avgjort.**
Förslag: logga som beslut att dygnsljuset följer viloperioden, att byborna syns som figurer på dagen, att prototypen behåller spelkameran i nattvyn och att byborna äter före Gryningskraften och bidragen vid gryningen.
Ta bort de inaktuella markeringarna i PRD.md:467 och economy_v1.py:133, och besvara device-measurements.md:115 med att målenheterna bestäms i TASK-024.
Det blockerar inget men gör att listan över öppna frågor stämmer.

## 3. Avvikelser mellan dokument och kalkyl

Alla sex raderna i tabellen i ekonomi v1, avsnitt 7, stämmer exakt med kalkylen, och krafternas siffror i forces.md, avsnitt 4, stämmer med `epoch_1`.
Genomgången i avsnitt 1 följer kalkylen.
Följande skiljer sig ändå mellan text och kalkyl; ingen av punkterna blockerar M1.

1. **Avstånd, dagsverke och nivå 2 påverkar inte vad åkern och fårgården ger.**
   economy-v1.md säger att de gör det (rad 57, 80 och 109).
   I kalkylen växer säd och ull med en fast takt, och faktorerna skalar bara skördetaket, som aldrig nås.
   C#-simuleringen gör likadant (`Settlement.cs`, `Min(b.Ripe, Scale(...))`).
   Kalkylen motsäger också sig själv, eftersom `daily_output()` multiplicerar åker och fårgård med faktorn.
   Det är en designfråga om avstånd ska kosta något för åkrar, och den hör till balanseringen under O-01.
2. **Steg och pass krediteras vid gryningen i kalkylen,** men under dygnet när datan kommer in enligt forces.md.
   Det ändrar inget utfall för dygnet i avsnitt 1.
3. **Krafternas saldon är decimaltal i kalkylen,** medan INV-F1 kräver heltal eller fast decimal.
   Värdena stämmer efter avrundning, och C#-simuleringen räknar i fast decimal.
4. **Kalkylen prövar bara att lagret inte blir negativt,** inte taket och inte lagren på andra öar, fast economy-v1.md säger att den prövar INV-R1.
5. **Havet lägger aldrig dagsverke på fårgården,** som economy-v1.md:136 säger, eftersom fårgården inte hinner byggas under veckan.

## 4. Grindens bedömning

| Krav | Bevis | Läge |
|---|---|---|
| En hel dag inklusive vila med konkreta siffror | Avsnitt 1 | Uppfyllt |
| Siffrorna stämmer när de räknas om med `docs/design/models/` | Avsnitt 1.9; omkört 2026-10-18 | Uppfyllt |
| Ingen öppen fråga blockerar prototypen | Avsnitt 2; förslag 1 och 2 blev D-82 och D-83 | Uppfyllt |
| Produktägaren godkänner grinden | D-82–D-87 | Godkänd 2026-10-18 |

Produktägaren godkände grinden och förslagen 1–3 den 2026-10-18.
M0 är `ACCEPTED` i ROADMAP, och besluten är loggade i PRD som D-82–D-87.
