# Eirora

Ett passivt, kartbaserat strategi- och civilisationsspel där spelarens värld växer när spelaren tar hand om sig själv.
Termerna nedan är kanoniska; kodnamn (engelska) anges inom parentes.

## Värld och karta

**Värld** (`World`):
En spelares egen spelvärld: en ögrupp på en hexkarta med bosättningar, ekonomi och historik.
_Avoid_: rike, sparfil, karta (när hela världen avses)

**Ö** (`Island`):
En sammanhängande landmassa i en värld, omgiven av hav.
_Avoid_: region (när en ö avses)

**Arkipelag** (`Archipelago`):
Den gemensamma havsytan där en grupps världar ligger och förbinds med sjövägar.
_Avoid_: delad värld, server, lobby

**Sjöväg** (`SeaRoute`):
En förbindelse över havet mellan två hamnar, inom en värld eller mellan två världar i en arkipelag.
_Avoid_: handelsrutt (när förbindelsen i sig avses)

## Civilisation och ekonomi

**Bosättning** (`Settlement`):
En samling byggnader och bybor som fungerar som ett ekonomiskt nav i en värld.
_Avoid_: by, stad (som generella termer)

**Bybor** (`Villagers`):
Bosättningens invånare, modellerade som arbetsgrupper med gemensam dygnsrytm, inte som individer.
_Avoid_: invånare, befolkning, NPC:er

**Långsam process** (`SlowProcess`):
En naturlig förändring i världen som fortgår även under vilan, till exempel skogsväxt eller mognad.
_Avoid_: passiv produktion

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

**Bidrag** (`Contribution`):
En registrerad mängd egenvård för ett tidsintervall som omvandlas till kraft.
_Avoid_: belöning, input

**Verifierat bidrag** (`VerifiedContribution`):
Ett bidrag som operativsystemets hälsokälla registrerat automatiskt.
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

**Spelardygn** (`GameDay`):
Tiden från en gryning till nästa; enheten som krafter räknas per.
_Avoid_: dag, kalenderdygn (när spelardygnet avses)

## Simulering

**Kommando** (`GameCommand`):
En handling som spelaren begär i världen; det valideras och godkänns eller nekas, och bär en idempotensnyckel.
_Avoid_: order, action, klick

**Spelpåverkande kommando** (`GameplayCommand`):
Ett kommando som ändrar världen, till exempel att bygga, starta en expedition eller handla; nekas under viloperioden.
_Avoid_: aktiv handling

**Administrativt kommando** (`AdministrativeCommand`):
Ett kommando som rör konto, integritet, tillgänglighet, hjälp, radering eller viloschema; tillåts även under viloperioden.
_Avoid_: inställning (när kommandot avses)

**Händelse** (`GameEvent`):
Ett faktum om en förändring som har skett i världen; tillståndet är summan av alla händelser.
_Avoid_: logg, notis

**Grundnivå** (`Baseline`):
Den neutrala mängd Gryningskraft som ges när sömndata saknas; ingen inloggad sömn ger mindre.
_Avoid_: minimum, straffnivå

## Progression

**Epok** (`Epoch`):
Ett kapitel om ungefär 6–10 veckor som introducerar nya system och mål och avslutas med en milstolpe; inget nollställs.
_Avoid_: säsong, kampanj, level

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
