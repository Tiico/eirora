# Den egna världen simuleras lokalt; servern äger bara det delade

Spelarens värld simuleras deterministiskt på telefonen och går att spela helt offline.
Backend äger enbart det som delas eller har ekonomiskt värde: konto, molnsparning, grupp, arkipelag, sjövägar och handel mellan spelare, gemensamma byggen, delade hälsosiffror och köpvalidering.
Vi valde detta framför en helt serverauktoritativ modell eftersom det saknas en motståndare att skydda den egna världen mot (ingen PvP, ingen pay-to-win, vilolåset är ett ärligt skydd enligt [ADR-0003](./0003-rest-lock-is-honest-protection.md)), och lokal simulering ger offlinespel, omedelbara förhandsvisningar, lägre driftkostnad och att rå hälsodata aldrig behöver lämna enheten.

## Consequences

- Fusk i den egna världen drabbar bara fuskaren; allt som påverkar andra spelare valideras och rimlighetskontrolleras av servern.
- Gruppåtgärder kräver uppkoppling och köas annars.
- Samma simuleringskod måste kunna köras på servern för att rimlighetskontrollera handel och bidrag, se [ADR-0002](./0002-unity-csharp-shared-simulation.md).
- Ersätter PRD v0.1:s krav på att servern är auktoritativ för alla spelkommandon och att klienten är skrivskyddad offline.
