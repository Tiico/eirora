# Unity 6 LTS + C#, med ett motoroberoende simuleringsbibliotek delat med en ASP.NET Core-server

Klienten byggs i Unity 6 LTS med C#.
Spelsimuleringen är ett rent C#-bibliotek utan Unity-beroenden som både klienten och en ASP.NET Core-backend använder, så att servern kan rimlighetskontrollera handel och bidrag med exakt samma regler.
Vi valde Unity framför Godot (förslaget i PRD v0.1) därför att visuell skönhet i low-poly 3D är ett uttalat kvalitetskrav, Unity är det mest mogna alternativet på Android och iOS, och Godots C#-export till mobil var fortfarande officiellt experimentell i 4.7 (oktober 2026); GDScript hade omöjliggjort delad simuleringskod med servern.

## Considered Options

- **Godot 4.7 + C#**: öppen källkod och textbaserade scener som agenter enkelt redigerar, men experimentell mobilexport är en risk för en kommersiell release.
- **Godot 4.7 + GDScript**: stabilt på mobil, men simuleringen kan inte delas med servern utan headless Godot eller dubbelimplementation.
- **Flutter/Flame, Kotlin Multiplatform**: bra hälsobibliotek men ingen 3D-motor.

## Consequences

- Unity-scener och prefabs är mindre agentvänliga än text; så mycket som möjligt ska därför uttryckas i kod och data (ScriptableObjects/JSON), inte i handredigerade scener.
- Licens: Unity Personal är gratis upp till 200 000 USD i intäkter och finansiering; därefter krävs Pro.
- Det finns inga mogna Godot- eller Unity-plugins vi kan lita på blint för Health Connect/HealthKit; HealthBridge (Fab) utvärderas, annars skrivs egna native-adaptrar i Kotlin och Swift bakom ett gemensamt C#-gränssnitt.
