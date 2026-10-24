# Eirora

Ett passivt, kartbaserat strategi- och civilisationsspel där spelarens värld växer när spelaren tar hand om sig själv.
Termerna nedan är kanoniska; kodnamn (engelska) anges inom parentes.

## Värld och karta

**Värld** (`World`):
En spelares egen spelvärld: en ögrupp på en hexkarta med bosättningar, ekonomi och historik.
_Avoid_: rike, sparfil, karta (när hela världen avses)

**Ö** (`Island`):
En sammanhängande landmassa i en värld, omgiven av hav.
Öarna och deras id kommer från kartan, och varje ö där spelaren har byggt har eget lager (TASK-045).
_Avoid_: region (när en ö avses)

**Arkipelag** (`Archipelago`):
Den gemensamma havsytan där en grupps världar ligger och förbinds med sjövägar.
_Avoid_: delad värld, server, lobby

**Sjöväg** (`SeaRoute`):
En förbindelse över havet mellan två hamnar, inom en värld eller mellan två världar i en arkipelag.
Inom en värld har varje ö med brygga en egen sjöväg till hemön, med egen kapacitet (TASK-045).
_Avoid_: handelsrutt (när förbindelsen i sig avses)

**Karta** (`WorldMap`):
En världs oföränderliga hexskiva med brickor och öar, skapad en gång när världen skapas och sparad hel ([ADR-0010](./docs/adr/0010-world-map-format-and-generator-version.md)).
Det som förändras på kartan under spelet, som dimma, skog och byggnader, är världens tillstånd.
_Avoid_: värld (när bara kartan avses), seed

**Bricka** (`HexTile`):
En hexagon på kartan med axiala koordinater, terräng, höjd och rikedom; varje landbricka är en möjlig byggplats (D-79).
_Avoid_: ruta, cell, plats

**Byggplats**:
En bricka som uppfyller en byggnads krav (`TileUse`), till exempel skog, berg, fiskevatten eller hamnläge; vad brickan duger till följer av terräng, höjd och grannar och sparas inte (D-79).
Skog räknas bara när det finns upptäckt skog att fälla, så att byggvyn aldrig avslöjar dimman (D-93).
_Avoid_: plats (som namngiven plats), tomt

**Fiskevatten** (`FishingWater`):
Grunt vatten intill land, där fiskeboden står på pålar (D-88).
_Avoid_: fiskeplats, kust

**Hamnläge** (`HarborSpot`):
En landbricka utom berg intill grunt vatten som når djuphavet; där kan en brygga byggas och skepp lägga till (D-89).
[Kartvalideringen](./docs/design/map-validation.md), och därmed generator v1, räknar bara stränder, som alltid är hamnlägen.
_Avoid_: hamnplats, kaj, strand (som krav)

**Krön** (`Crest`):
Startöns enda högsta bricka, där Vårdkasen byggs och som inte bär något annat (D-92); delar två brickor toppen finns inget krön.
_Avoid_: topp, högsta punkt, berg

**Generator** (`WorldGenerator`):
Det som skapar en karta, av en källtyp och med en fast generatorversion.
En låst generatorversion ändras aldrig, och en ny version används bara för nya världar.
_Avoid_: kartskript

**Seed**:
Talet som en procedurgenererad karta skapas från; tillsammans med generatorversionen ger det alltid samma karta.
_Avoid_: frö

**Källtyp** (`sourceType`):
Var en karta kommer ifrån: procedurgenererad, handgjord eller geografisk.

**Validator** (`MapValidator`):
Det som avgör om en karta ger en bra start och goda vägar att växa, med ett mätvärde och en gräns per regel ([kartvalidering](./docs/design/map-validation.md)).
_Avoid_: kartkontroll

**Dimma** (`Fog`):
Det som täcker de delar av kartan som ännu inte är utforskade; den lyfts med Utforskning och kommer aldrig tillbaka.
Den hör till världens tillstånd bricka för bricka, inte till kartan; kartläggningar och expeditioner lyfter den också (D-96, D-97).
_Avoid_: fog of war, mörker

## Civilisation och ekonomi

**Bosättning** (`Settlement`):
En ös byggnader, bybor, bostäder och lager, anlagd av öns första förråd eller brygga; varje ö har högst en bosättning (D-106).
_Avoid_: by, stad (som generella termer), grundande (som är det en ny värld börjar med)

**Bybor** (`Villagers`):
Bosättningens invånare, modellerade som arbetsgrupper med gemensam dygnsrytm, inte som individer.
_Avoid_: invånare, befolkning, NPC:er

**Bemanningsordning** (`StaffingOrder`):
Den ordning som byggnaderna får bybor i när byborna inte räcker till alla, från början byggordningen; spelaren kan ändra den utan kraft (TASK-044).
_Avoid_: prioritetslista, kö (om bemanningen)

**Pausad byggnad** (`Paused`):
En byggnad som spelaren har pausat: den bemannas inte och dess bybor bygger, men nivå och lager står kvar tills den återupptas (D-39).
_Avoid_: avstängd, riven

**Byggkö** (`Projects`):
Byggen, uppgraderingar och vägar som väntar på de lediga bybornas arbete, i den ordning de får det; spelaren kan flytta ett projekt först (TASK-044).
_Avoid_: bygglista, order

**Basbehov** (`BasicNeed`):
Det behov som alla befolkningsnivåer har, mat, en per bybo och spelardygn; brist stoppar bara tillväxt, aldrig något som redan finns.
_Avoid_: hunger, underhåll

**Förråd** (`Depot`):
En byggnad som lagrar resurser och vars bärare hämtar det byggnaderna närmast gör; en brygga är också ett förråd, med ett mindre lager (D-107).
_Avoid_: lager (om byggnaden), magasin

**Bärare** (`Carrier`):
Bybor som ett förråd eller en brygga anställer själv vid gryningen, en till fyra, för att flytta varor på land mellan byggnaderna och förrådet; avståndet kostar bärartid, och vägar gör bärarna snabbare (D-107, D-109, D-110).
_Avoid_: transportör, kärra (som term)

**Minimilager** (`Reserve`):
En gratis planeringsinställning som anger hur mycket av en råvara förädlingen alltid lämnar kvar.
_Avoid_: buffert, spärr

**Flaskhals** (`Bottleneck`):
Det som just nu håller tillbaka en byggnad eller en ö mest: en paus, bemanning som saknas, råvara under minimilagret, fälld skog, fullt lager, inget bemannat förråd eller bärare som inte hinner; flaskhalsdiagnostiken läser den ur världen utan att ändra den och visar vad en väg, ett förråd eller fler bärare skulle ge (CIV-05, LOG-03, D-107).
_Avoid_: blockering, stopp (som term)

**Långsam process** (`SlowProcess`):
En naturlig förändring i världen som fortgår även under vilan, till exempel skogsväxt eller mognad.
_Avoid_: passiv produktion

**Handkraft** (`HandBuild`):
Att bygga något som annars kostar Byggkraft utan kraft, på en fast tid, så att en bortvald vana aldrig blockerar milstolpen.
_Avoid_: gratisbygge, långsamt läge

**Runsten** (`RuneStone`):
Byggnaden där forskningen ristas och där världens historik bevaras.
_Avoid_: forskningscenter, bibliotek

**Befolkningsnivå** (`PopulationTier`):
Det steg en bybo har nått, i epok 1 torpare, bönder eller hantverkare, med egna bostäder, behov och byggnader att arbeta i; en bybo stiger när nivåns behov uppfylls och går aldrig tillbaka (D-105, D-39, D-115).
_Avoid_: klass, rang, stånd

**Torpare** (`Crofters`):
Den första befolkningsnivån: alla bybor börjar som torpare, bor i stugor och långhus och behöver bara mat.
_Avoid_: trälar, arbetare

**Bönder** (`Farmers`):
Den andra befolkningsnivån, som torpare stiger till när de får kläder; bor i bondgårdar och kan också bemanna bryggeri, mjöderi och tjärdal.
_Avoid_: odlare, jordbrukare (som nivå)

**Hantverkare** (`Craftsmen`):
Den tredje befolkningsnivån i epok 1, som bönder stiger till när de också får dryck; bor i hantverkargårdar, bygger dubbelt så fort och bemannar varvet (D-116).
_Avoid_: mästare, borgare

**Behov** (`Need`):
Det en befolkningsnivå förbrukar varje spelardygn, mat (basbehovet), kläder eller dryck, och som kan uppfyllas utan krafter med minst två varor ur kedjor som börjar på olika mark (INV-R4).
_Avoid_: krav, efterfrågan, hunger

**Upplåsning** (`Unlock`):
Ett steg i epoken där nya befolkningsnivåer, behov och byggnader blir tillgängliga i hela världen när någon bosättning uppfyller stegets villkor, till exempel en viss storlek; ett steg som nåtts låses aldrig igen (D-105, D-114).
_Avoid_: nivå (om steget), teknikträd

**Utlager** (`Outbox`):
Det som väntar vid en byggnad på att bärarna ska hämta det; när utlagret är fullt stannar byggnaden, och inget går förlorat (D-107).
_Avoid_: buffert, kö (om varorna)

**Väg** (`Road`):
En bricka där bärarna går dubbelt så fort; vägar läggs bricka för bricka och hjälper alla bärare som går över dem (D-112).
_Avoid_: stig, gata

**Knarr** (`Knarr`):
Ett lastskepp som varvet bygger och som ökar sjövägarnas kapacitet till och från varvets ö.
_Avoid_: skepp (när knarren som kapacitet avses), båt

## Egenvård och krafter

**Egenvård** (`SelfCare`):
Spelarens verkliga hälsosamma vanor som driver världens tillväxt.
_Avoid_: aktivitet, träningsdata, hälsopoäng

**Vana** (`Habit`):
En av de spårade egenvårdskategorierna: rörelse, träning eller sömn.
_Avoid_: mål, uppdrag

**Kraft** (`Force`):
Den spelresurs som en vana omvandlas till och som spelaren riktar för att påverka världen.
_Avoid_: energi, valuta, poäng

**Utforskning** (`Exploration`):
Kraften från rörelse/steg; driver expeditioner och lyfter dimman.
_Avoid_: stegpoäng

**Byggkraft** (`BuildPower`):
Kraften från träningspass; driver större byggprojekt och uppgraderingar.
_Avoid_: träningspoäng, arbetskraft

**Gryningskraft** (`DawnPower`):
Kraften från sömnlängd; ger bybornas energi och morgonens planeringsdrag.
_Avoid_: sömnpoäng, vilobonus

**Grundande** (`Founding`):
Det en ny värld börjar med på sin karta: startbyns byggnader, kartläggningarnas dolda brickor och ön som den första expeditionen hittar.
Det följer av kartan och sparas aldrig; prototypens karta har sitt handgjorda grundande och en genererad karta får ett efter samma regel.
_Avoid_: startplan, spawn

**Kartläggning** (`Survey`):
En handling som kostar Utforskning och lyfter dimman från utvalda brickor nära bosättningen.
_Avoid_: scouting, spaning

**Expedition** (`Expedition`):
En resa från en brygga som kostar Utforskning och efter en tid hittar en ny ö.
_Avoid_: uppdrag, quest

**Dagsverke** (`Workday`):
En handling som kostar Gryningskraft och låter en byggnad producera mer under ett spelardygn.
_Avoid_: boost, buff

**Rusta** (`Rush`):
En handling som kostar Gryningskraft och lägger extra arbetstimmar på det första bygget i kön.
_Avoid_: snabba upp, skippa

**Bidrag** (`Contribution`):
En registrerad mängd egenvård för ett tidsintervall som omvandlas till kraft.
_Avoid_: belöning, input

**Läge** (`Position`):
Allt tillskott en kraft fått före sparandets mjuka tak; saldot följer av läget, och en korrigering flyttar läget tillbaka.
_Avoid_: råsaldo, dolt saldo

**Startkrafter** (`StartingForces`):
De krafter som finns i en ny världs starttillstånd så att den guidade turen kan spelas utan egenvård; de är inte ett bidrag.
_Avoid_: bonus, gåva (som term)

**Verifierat bidrag** (`VerifiedContribution`):
Ett bidrag som operativsystemets hälsokälla registrerat utan att det är markerat som manuellt inmatat, även när källan inte anger hur det spelades in (D-68).
_Avoid_: äkta bidrag

**Egenrapporterat bidrag** (`SelfReportedContribution`):
Ett bidrag som spelaren matat in manuellt, eller som hälsokällan markerat som manuellt inmatat.
_Avoid_: fusk, overifierat, manuell data

## Dygnscykel

**Viloperiod** (`RestPeriod`):
Spelarens återkommande, självvalda dagliga period om 6–10 timmar då byborna sover.
_Avoid_: natt, sovtid, offlinetid

**Vilolås** (`RestLock`):
Regeln att inga spelpåverkande handlingar kan utföras under viloperioden.
_Avoid_: nattlås, sömnlås, paus

**Gryning** (`Dawn`):
Övergången när viloperioden slutar och världen öppnar igen.
_Avoid_: upplåsning, reset

**Nattvy** (`NightView`):
Det spelaren ser under viloperioden: den sovande världen, när den öppnar och vilka långsamma processer som pågår.
_Avoid_: låsskärm, pausmeny

**Morgonnotis** (`MorningNotification`):
Den valfria, tysta notisen vid gryningen när något nytt har hänt i världen; högst en per spelardygn.
_Avoid_: påminnelse, push

**Spelardygn** (`GameDay`):
Tiden från en gryning till nästa; enheten som krafter räknas per.
_Avoid_: dag, kalenderdygn (när spelardygnet avses)

**Frånvaro** (`Absence`):
Tiden då spelaren inte öppnar spelet; världen spolas fram till klockan när spelaren kommer tillbaka och är minst lika utvecklad som förut, hur länge frånvaron än varade (INV-V4).
Kvartar där bara långsamma processer rör sig, under viloperioden eller när byborna inte har något att göra, räknas i ett steg.
_Avoid_: offlinetid, paus, inaktivitet

## Simulering

**Kommando** (`GameCommand`):
En handling som spelaren begär i världen; det valideras och godkänns eller nekas, och bär en idempotensnyckel.
_Avoid_: order, action, klick

**Spelpåverkande kommando** (`GameplayCommand`):
Ett kommando som ändrar världen, till exempel att bygga, starta en expedition eller handla; nekas under viloperioden.
_Avoid_: aktiv handling

**Administrativt kommando** (`AdministrativeCommand`):
Ett kommando som rör konto, integritet, tillgänglighet, hjälp, radering, viloschema eller registrering av egenvård; tillåts även under viloperioden (D-54).
_Avoid_: inställning (när kommandot avses)

**Händelse** (`GameEvent`):
Ett faktum om en förändring som har skett i världen; tillståndet är summan av alla händelser.
_Avoid_: logg, notis

**Grundnivå** (`Baseline`):
Den neutrala mängd Gryningskraft som ges när sömndata saknas; ingen inloggad sömn ger mindre.
_Avoid_: minimum, straffnivå

**Regelversion** (`RulesVersion`):
Versionen av simuleringens regler, inklusive hur befintligt innehåll verkar; samma starttillstånd, regelversion och kommandon ger alltid samma tillstånd.
_Avoid_: spelversion, appversion

**Innehållsversion** (`ContentVersion`):
Versionen av spelets innehåll (byggnader, recept, resurser, fynd, epoker), som bara ökar när innehåll läggs till.
_Avoid_: datapaket, innehållsuppdatering (när versionen avses)

**Innehållskatalog** (`ContentCatalog`):
Innehållet vid en innehållsversion: vad varje byggnad kostar, kräver och producerar, som simuleringen och klienten läser i stället för egna siffror.
_Avoid_: regeldata (om byggnaderna), konfiguration

**Recept** (`Recipe`):
Vad en byggnad producerar och av vad: råvaror, produkt, takt och om rikedomen påverkar den, eller att den är en långsam process eller fäller skog.
_Avoid_: formel, produktionsregel

**Journal** (`Journal`):
Den ordnade följden av kommandon och avläsningar som en värld har fått sedan den fick sin regelversion.
_Avoid_: logg, historik (som är världens berättelse för spelaren)

**Replay** (`Replay`):
En världs start och indata utan regelversion, som körs med de regler bygget har; replay-korpusen visar att servern och klienten får samma tillstånd.
_Avoid_: inspelning, testfall

**Snapshot** (`SimulationSnapshot`):
Hela en världs tillstånd vid en tidpunkt, märkt med regelversion och innehållsversion.
_Avoid_: ögonblicksbild, sparfil

**Fingeravtryck** (`Fingerprint`):
En världs hela tillstånd i kanonisk textform: lika världar har lika fingeravtryck, och det är fingeravtrycket som en snapshot innehåller.
_Avoid_: kontrollsumma, hash (som snapshoten anger för fingeravtrycket)

**Sparning** (`Save`):
En världs lagrade form på enheten eller i molnet: den senaste snapshoten och journalen.
_Avoid_: sparfil, backup

**Segment**:
En snapshot och journalen efter den under samma regelversion; sparningen har det pågående segmentet och, fram till nästa regelbyte, det föregående.
_Avoid_: kapitel, del

**Migration** (`StateMigration`):
Omvandlingen av en snapshot från en regelversion till nästa när appen uppdateras; den ändrar aldrig det som redan hänt.
_Avoid_: konvertering, uppgradering (när omvandlingen avses)

## Progression

**Epok** (`Epoch`):
Ett kapitel om ungefär 6–10 veckor som introducerar nya system och mål och avslutas med en milstolpe; inget nollställs.
_Avoid_: säsong, kampanj, level

**Milstolpe** (`EpochMilestone`):
Det som avslutar en epok och som spelaren ser i världen när det nås; i epok 1 att Vårdkasen tänds.
_Avoid_: bossnivå, slutmål, achievement

**Vårdkase** (`Beacon`):
Signalelden på Hemöns krön som byggs i tre etapper och vars tändning är epok 1:s milstolpe.
_Avoid_: fyr, monument

**Forskning** (`Research`):
Steg för steg i en inriktning som öppnar nya strategier; kostar varor och tid, ingen kraft.
_Avoid_: teknikträd, uppgradering (när forskning avses)

**Introduktion** (`Introduction`):
Det korta ögonblick då ett nytt system visas för första gången, när spelaren når dess förutsättning; högst en per spelardygn.
_Avoid_: tutorial, uppdrag, quest

**Guidad tur** (`GuidedTour`):
Första sessionens introduktion där spelaren använder var och en av de tre krafterna en gång.
_Avoid_: tutorial (om helheten), onboarding (när turen avses)

**Guiden** (`Guide`):
Ljusgestalten inspirerad av Eir som leder den guidade turen och syns tydligast i gryningen.
_Avoid_: maskot, tutor, NPC

**Daglig rapport** (`DailyReport`):
Sammanfattningen som visas första gången spelaren öppnar spelet efter en gryning och täcker allt sedan den senast lästa rapporten.
_Avoid_: logg, nyhetsflöde, dagbok

## Konto

**Konto** (`Account`):
Spelarens identitet på servern, som skapas när spelaren första gången loggar in med Apple eller Google; den egna världen kräver aldrig ett konto.
_Avoid_: profil, användare, inloggning (som är sättet att nå kontot)

**Molnsparning** (`CloudSave`):
Kontots kopia av världens sparning på servern, uppdelad i en världsdel och en end-to-end-krypterad bidragsdel.
_Avoid_: backup, synk

**Världsdel** (`Save.ToParts().World`):
Den del av sparningen som servern kan läsa: kartan och senaste snapshoten utom det som hålls per spelardygn; den räcker för att världen ska fortsätta.
_Avoid_: grunddata, publik del

**Bidragsdel** (`Save.ToParts().Contributions`):
Den del av sparningen som krypteras på enheten: egenvården och krediteringen per spelardygn, de använda idempotensnycklarna, segmentens start och journalerna (D-100).
_Avoid_: hälsodata (som aldrig lämnar telefonen), krypterad backup

**Bidragsnyckel** (`ContributionKey`):
Nyckeln som bara spelarens enheter har och som krypterar molnsparningens bidrag per spelardygn; förloras den går bidragshistoriken förlorad, aldrig världen.
_Avoid_: lösenord, återställningskod (som är en av nyckelns kopior)

**Nyckelepok** (`CloudSave.KeyEpoch`):
Bidragsnyckelns generation, från 1; en ny telefon utan nyckeln börjar en ny epok, och molnsparningens epok sjunker aldrig.
_Avoid_: nyckelversion, nyckel-id

**Låst bidragsdel** (`LockedContributions`):
En bidragsdel från en tidigare nyckelepok, som servern sparar i 90 dagar om nyckeln skulle dyka upp och sedan raderar.
_Avoid_: arkiv, gammal backup

**Nyckelöverföring** (`KeyTransfer`):
Bidragsnyckelns väg från en av spelarens telefoner till en annan via servern, som bara förmedlar den förseglad för den nya telefonens engångsnyckel och glömmer den inom tio minuter.
_Avoid_: nyckelsynk (som är nyckelbackupen), nyckeldelning

**Nyckelbackup** (`IKeyBackup`):
Bidragsnyckelns kopia i plattformens end-to-end-krypterade backup, iCloud-nyckelringen på iOS och Block Store på Android, där en ny telefon på samma plattform hittar den utan att fråga; utan skärmlås stannar den på telefonen.
_Avoid_: molnnyckel, nyckelsynk

**Biljett** (`KeyTransferTicket`):
Det den nya telefonen visar den gamla, i en QR-kod: överföringens id, den nya telefonens publika engångsnyckel och en hemlighet som aldrig går till servern; den är hemlig tills nyckeln levererats eller överföringen gått ut.
_Avoid_: QR-nyckel, kod

**Återställningskod** (`RecoveryCode`):
Bidragsnyckeln som 64 tecken i åtta grupper, med nyckelepoken och en kontrollsumma, som spelaren kan skriva ner och skriva in på en ny telefon; en av nyckelns kopior.
_Avoid_: lösenord, PIN

**Sammanslagning** (`Save.Merge`):
Två sparningar av samma värld som spelats var för sig blir en: journalerna spelas upp från den senaste segmentstart de delar, i tidsordning, och varje kommando räknas en gång (D-99).
_Avoid_: konfliktlösning, synkronisering (som är hela flödet mot servern)

## Socialt

**Grupp** (`Group`):
2–6 spelare som bjudit in varandra och vars världar delar en arkipelag.
_Avoid_: klan, guild, vänlista

**Gemensamt bygge** (`SharedProject`):
Ett byggprojekt i arkipelagen som gruppens medlemmar bidrar till tillsammans.
_Avoid_: monument (som generell term), raid

## Prototyparbete

Processbegrepp för hur prototypen spelas och utvärderas; de har inga kodnamn.

**Prototypdagbok**:
Produktägarens korta dagliga anteckningar om hur det var att spela prototypen, en kommentar per spelardygn i ett GitHub-ärende per vecka; innehåller aldrig hälsodata.
_Avoid_: speltestlogg, journal, hälsodagbok

**Veckogenomgång**:
Det veckovisa tillfälle då prototypdagbokens observationer sorteras och lärdomarna blir ärenden eller designändringar.
_Avoid_: retro, sprintgranskning

**Lärdom**:
En observation från prototypspel som ska leda till en ändring eller som belägger en hypotes; får ett ID (`L-nn`) i lärdomsloggen.
_Avoid_: feedback, insikt, fynd (fynd är expeditionernas resultat)
