# Utveckling

Hur repot är uppbyggt, hur allt byggs och testas, och vad CI gör.
Arkitekturen beskrivs i [ADR-0002](./adr/0002-unity-csharp-shared-simulation.md) och [ADR-0004](./adr/0004-simulation-as-local-unity-package.md).

## Struktur

```text
Eirora.slnx                  .NET-lösningen (simulering, kontrakt, server, tester)
src/
├── Eirora.Simulation/       motoroberoende simulering; även lokalt Unity-paket (C# 9, .NET Standard 2.1)
├── Eirora.Contracts/        typer som delas mellan klient och server; även lokalt Unity-paket
└── Eirora.Server/           ASP.NET Core-backend (.NET 10)
tests/
├── Eirora.Simulation.Tests/ exempel-, property-based- och arkitekturtester
└── Eirora.Server.Tests/     integrationstester mot servern i minnet
client/                      Unity 6.3 LTS-projektet (6000.3.26f1, URP)
└── Assets/Eirora/
    ├── Runtime/             klientkod; världen byggs i kod (Bootstrap)
    ├── Editor/              projektinställningar som kod (ProjectSetup) och byggen (Builds)
    └── Tests/EditMode/      Unity-tester, bland annat att simuleringen räknar lika i Unity
tools/                       skript för CI och lokal användning
docs/                        design, ADR:er och agentinstruktioner
```

## Krav

- .NET SDK 10.0.402 eller senare 10.0.4xx (`global.json`).
- Unity 6000.3.26f1 med Android Build Support (inklusive SDK, NDK och OpenJDK) och iOS Build Support.
- Git LFS för bilder, modeller, ljud och typsnitt (`.gitattributes`).

## .NET

```bash
dotnet build Eirora.slnx -c Release
```

```bash
dotnet test --solution Eirora.slnx -c Release
```

```bash
dotnet format Eirora.slnx --verify-no-changes
```

Bygget behandlar varningar som fel, kör .NET-analysatorer och stoppar paket med kända sårbarheter (NuGetAudit).
Byggutdata hamnar i `artifacts/`.

Simuleringen får inte läsa klockan, använda plattformens slump eller göra I/O (`src/Eirora.Simulation/BannedSymbols.txt`, INV-D2), och arkitekturtesterna underkänner flyttal (INV-D3).

## Unity

Öppna `client/` i Unity Hub med version 6000.3.26f1.
Simuleringen och kontrakten kommer in som lokala paket från `src/`; ändringar i dem syns direkt i Unity.
Unity skapar `.meta`-filer för nya filer i paketen; committa dem (`tools/check-unity-meta.sh` kontrollerar).

Kommandona nedan körs från repots rot i Git Bash, med `UNITY` satt till sökvägen till `Unity.exe`.

Applicera projektinställningarna (idempotent):

```bash
"$UNITY" -batchmode -nographics -quit -projectPath client -executeMethod Eirora.Editor.ProjectSetup.Apply
```

Kör EditMode-testerna:

```bash
"$UNITY" -batchmode -nographics -projectPath client -runTests -testPlatform EditMode -testResults artifacts/editmode.xml
```

Bygg en Android-APK:

```bash
"$UNITY" -batchmode -nographics -quit -projectPath client -buildTarget Android -executeMethod Eirora.Editor.Builds.Android -buildOutput Builds/Android/Eirora.apk
```

## CI

`.github/workflows/ci.yml` körs på varje pull request och på `main`:

| Jobb | Vad det gör | Kräver |
|---|---|---|
| Simulation, contracts and server | Återställer, kontrollerar formatering, bygger, testar och kontrollerar `.meta`-filer | Inget |
| Unity EditMode tests | Kör Unity-testerna i GameCI | Unity-licens |
| Android build | Bygger en APK och laddar upp den som artefakt | Unity-licens |
| iOS Xcode project | Exporterar Xcode-projektet från Linux | Unity-licens |
| iOS build (unsigned, macOS) | Bygger Xcode-projektet utan signering för att bekräfta kedjan | Xcode-projektet |

Unity-jobben hoppas över med en varning tills tre repository secrets finns:

| Secret | Innehåll |
|---|---|
| `UNITY_EMAIL` | E-postadressen till Unity-kontot |
| `UNITY_PASSWORD` | Lösenordet till Unity-kontot |
| `UNITY_LICENSE` | Innehållet i licensfilen `Unity_lic.ulf`, enligt [GameCI:s instruktioner för aktivering](https://game.ci/docs/github/activation) |

Signering för Google Play och App Store läggs till som egna secrets när butiksbyggen behövs (M12).
Dependabot håller NuGet-paket och GitHub Actions uppdaterade varje vecka.
