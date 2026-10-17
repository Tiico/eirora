# Simuleringen delas med Unity som ett lokalt paket i källkodsform

Simuleringsbiblioteket (`src/Eirora.Simulation`) och kontrakten (`src/Eirora.Contracts`) är samtidigt vanliga .NET-projekt och lokala Unity-paket.
Varje mapp har både en `.csproj` för .NET-verktygen och en `package.json` och en `.asmdef` för Unity.
Klientens `Packages/manifest.json` pekar på mapparna med `file:`-sökvägar.
Vi valde källkod framför en förbyggd DLL i `Assets/Plugins` därför att Unity då kompilerar exakt samma kod som servern och testerna, utan ett kopieringssteg som kan glömmas bort eller bli inaktuellt.
Unitys `asmdef` med `noEngineReferences: true` gör dessutom att Unity själv vägrar kompilera simuleringen om någon försöker använda `UnityEngine` (ADR-0002).

## Considered Options

- **Förbyggd DLL i `Assets/Plugins`:** enkel för Unity, men kräver ett byggsteg före varje Unity-körning och gör det lätt att testa en annan version än den som körs.
- **Paket från git-URL:** fungerar för färdiga versioner men inte för samtidig utveckling av klient och simulering i samma gren.

## Consequences

- Simuleringen och kontrakten skrivs i **C# 9 mot .NET Standard 2.1**, det som Unity 6 kompilerar; nyare språkfunktioner kan bara användas i server och tester.
- .NET:s byggutdata hamnar i `artifacts/` i repots rot (`UseArtifactsOutput`), annars skulle Unity importera `bin/` och `obj/` från paketmapparna.
- Unity genererar `.meta`-filer i paketmapparna; de committas så att GUID:erna är stabila, och CI kontrollerar att ingen saknas (`tools/check-unity-meta.sh`).
- Determinism skyddas på två nivåer: klocka, slump och I/O är förbjudna vid kompilering (`BannedSymbols.txt`, INV-D2), och ett arkitekturtest underkänner flyttal i signaturer och IL (INV-D3).
- Samma testfall körs i .NET och i Unitys EditMode-tester, som ett första bevis för att klient och server räknar lika (NFR-02).
