# Tiden kommer in som avläsningar med tidszon och offset

Simuleringen läser aldrig klockan eller en tidszonsdatabas (INV-D2).
Klienten skickar i stället avläsningar: UTC-tid, IANA-tidszon och zonens offset vid den tiden (`TimeReading`).
Zonen och offseten gäller för hela spannet sedan föregående avläsning, så klienten delar ett spann där offseten ändras, till exempel vid en sommartidsövergång under en lång frånvaro.
Vilotiden börjar på schemats lokala klockslag och slutar på schemats lokala slutklockslag så länge zonen är densamma, vilket gör att sommartid ändrar den verkliga längden till 5–11 timmar (D-37, INV-T6).
Byts zonen under vilan behåller slutet sin tidpunkt i UTC, så att en resa aldrig kortar eller förlänger en pågående vila (INV-T8).
Gryningen är ögonblicket då vilan slutar och en ny dag börjar; den rapporteras som en händelse, inte som en fas med längd.

## Considered Options

- **Simuleringen räknar lokal tid själv med en inbäddad tidszonsdatabas:** kräver att databasen versioneras med reglerna och att klient och server har exakt samma version; en uppdatering av tidszonsregler skulle kunna ändra gamla världar.
- **Klienten skickar färdiga vilotider i UTC:** enkelt, men då ligger regeln för sommartid och resor i klienten, utanför simuleringens tester.

## Consequences

- Allt som rör schema, 24-timmarsregeln och vilolåset ligger i simuleringen och testas headless (INV-T1–T8), och tiden går att dela upp godtyckligt inom ett spann med samma offset (INV-D4).
- Klienten ansvarar för att läsa enhetens zon och dela spann vid offsetbyten; vid lång frånvaro antar den att telefonen var kvar i samma zon, vilket är förenligt med att vilolåset är ett ärligt skydd ([ADR-0003](./0003-rest-lock-is-honest-protection.md)).
- En avläsning bakåt i tiden ändrar ingenting (INV-D8); en manipulerad klocka framåt drabbar bara spelaren själv.
