# Vilolåset är ett ärligt skydd, inte fuskskydd

Vilolåset gäller alla spelare (vilotid 6–10 timmar, schemaändringar börjar gälla först efter 24 timmar), men det är byggt för att hålla den ärliga spelaren i en svag stund, inte för att stoppa en bestämd fuskare.
Eftersom spelaren själv väljer sitt schema kan den som vill kringgå låset alltid sätta en obekväm vilotid utan att hacka något; serververifierat, absolut lås (som PRD v0.1 krävde) skulle därför kosta en helt serverauktoritativ arkitektur och offlinespel utan att skydda mot något verkligt.

## Consequences

- Den egna världen tillämpar låset lokalt med enhetens klocka offline och serverns tid när den är tillgänglig; servern nekar alla gruppåtgärder under spelarens vilotid.
- Klockmanipulation på den egna enheten accepteras som en risk som bara drabbar spelaren själv.
- Möjliggör [ADR-0001](./0001-local-world-server-for-shared-state.md).
