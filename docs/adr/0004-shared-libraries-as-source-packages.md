# De delade biblioteken kompileras från samma källkod av både .NET och Unity

Simuleringsbiblioteket och kontrakten ligger i `src/` som vanliga .NET-projekt (netstandard2.1, C# 9) och är samtidigt lokala Unity-paket (`package.json` och `.asmdef` bredvid `.csproj`).
Unity-klienten refererar dem med `file:`-sökvägar i `client/Packages/manifest.json` och kompilerar källkoden själv, medan servern och testerna refererar samma projekt med `ProjectReference`.
Vi valde detta framför att bygga en DLL och kopiera in den i Unity-projektet därför att det inte finns något mellanled som kan bli inaktuellt: det som testas headless är exakt de källfiler som Unity bygger till Android och iOS, vilket är förutsättningen för att klient och server ska ge samma tillstånd (NFR-02, INV-D1).

## Considered Options

- **Förkompilerad DLL i `Assets/Plugins`**: Unity ser bara en binär, men varje ändring kräver ett kopieringssteg som är lätt att glömma, och felsökning i Unity tappar källkoden.
- **NuGet-paket via NuGetForUnity eller UnityNuGet**: rätt för tredjepartsbibliotek men onödig ceremoni för kod i samma repo, och versionsnumret måste höjas vid varje ändring.
- **Källkod direkt under `client/Assets`**: enklast för Unity, men servern och de headless testerna måste då peka in i Unity-projektet, och ingenting hindrar att Unity-API:er läcker in.

## Consequences

- De delade biblioteken måste kompilera med Unitys kompilator: netstandard2.1, C# 9 och inga implicita usings; `.csproj` låser `LangVersion` och målramverk så att `dotnet build` underkänner det som Unity inte klarar.
- `noEngineReferences` i `.asmdef`, inga paketberoenden och arkitekturtestet `SharedLibraryArchitectureTests` håller biblioteken fria från Unity; testet underkänner också väggklocka, OS-slump, filsystem, nätverk och flyttal i simuleringen (INV-D2, INV-D3).
- `csc.rsp` bredvid varje `.asmdef` ger Unity samma nullable-kontext och varningar-som-fel som `Directory.Build.props` ger .NET.
- .NET lägger `bin/` och `obj/` i `artifacts/` (`UseArtifactsOutput`) så att Unity bara ser källkod i paketmapparna.
- Unity skriver `.meta`-filer i `src/`; de checkas in så att GUID:er är stabila mellan kloner.
