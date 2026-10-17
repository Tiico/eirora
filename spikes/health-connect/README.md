# Spike: Health Connect (TASK-007)

En liten Unity-app som läser steg, träningspass och sömn från Health Connect och visar varje post med värde, intervall, källa och `recordingMethod`.
Den undersöker också dubbletter från flera källor, historik, ändringar och bakgrundsläsning.
Slutsatserna står i [ADR-0006](../../docs/adr/0006-own-health-connect-adapter.md).

Spiken är kod att kasta: den ska svara på frågor, inte flyttas in som den är.

## Varför ett eget Unity-projekt

- Ett eget app-id (`com.eirora.spike.healthconnect`) gör att spiken kan installeras bredvid prototypen utan att skriva över den, och avinstallation tar bort spikens behörigheter och data.
- Produktionsklienten får inga hälsobehörigheter, ingen minSdk 26 och inga Gradle-ändringar innan ADR-0006 är beslutad; varje deklarerad hälsobehörighet kräver en deklaration i Google Play.
- Spikens felsöknings-UI och bygginställningar hålls borta från `client/`.

Projektet ingår inte i CI, eftersom Unity-workflowen bara bygger `client/`.

## Integritet

- Appen har ingen internetbehörighet; bygget underkänns om `android.permission.INTERNET` hamnar i APK:n (`artifacts/spike-health-connect/permissions.txt` listar alla behörigheter).
- Den begär bara läsbehörigheter och skriver aldrig till Health Connect.
- Hälsovärden finns bara i minnet medan appen visar dem; de loggas inte, varken i Unity eller i logcat.
- Det enda som sparas är en ändringstoken och resultatet av bakgrundsläsningar (tid, utfall, antal poster och om appen var öppen).
- Rapportfliken visar en text utan hälsovärden, tider eller titlar som produktägaren själv väljer att kopiera.

## Struktur

| Sökväg | Innehåll |
|---|---|
| `Assets/Spike/Plugins/Android/HealthConnectBridge.androidlib/` | Kotlin-adaptern: behörigheter, läsning, aggregat, ändringar och bakgrundsläsning. |
| `Assets/Plugins/Android/gradleTemplate.properties` | Slår på AGP:s inbyggda Kotlin, som Unity 6.3 stänger av (se ADR-0006). |
| `Assets/Spike/Runtime/` | C#-gränssnittet `IHealthConnectSource`, JNI-implementationen, fejkdata för Editorn, dubblettanalys och UI:t. |
| `Assets/Spike/Editor/SpikeBuild.cs` | Konfigurerar och bygger APK:n från kod och kontrollerar behörigheter och Kotlin-klasser. |
| `Assets/Spike/Tests/EditMode/` | Tester av dubblettanalys, JSON-kontraktet med Kotlin, manifestet och rapporten. |
| `../health-connect-seeder/` | En separat app som skriver kända testdata från två källor, bara för emulatorn. |

## Bygga och testa

Bygg APK:n (cirka tio minuter första gången) från repots rot:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -quit -projectPath spikes/health-connect -buildTarget Android -executeMethod Eirora.Spike.HealthConnect.Editor.SpikeBuild.BuildApk -logFile artifacts/spike-build.log
```

APK:n hamnar i `artifacts/spike-health-connect/eirora-hc-spike.apk` och innehåller ARM64 för telefoner och x86_64 för emulatorn.

Kör EditMode-testerna:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -nographics -projectPath spikes/health-connect -runTests -testPlatform EditMode -testResults artifacts/spike-editmode.xml
```

I Editorn visar spiken fejkdata med samma form som två överlappande källor.

### På en emulator

Health Connect ingår i Android 14 och senare, så en emulator med API 34 räcker.
Starta den med mjukvarurendering; emulatorns Vulkan-emulering kraschar annars när Unity börjar rendera:

```bash
emulator -avd Pixel_8_API_34 -gpu swiftshader_indirect
```

Bygg seedern med Unitys Gradle och JDK (från `spikes/health-connect-seeder/`, med `JAVA_HOME` och `ANDROID_HOME` satta till Unitys OpenJDK och Android SDK):

```bash
java -cp "C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Data/PlaybackEngines/AndroidPlayer/Tools/gradle/lib/gradle-launcher-9.3.1.jar" org.gradle.launcher.GradleMain assembleDebug
```

Installera båda varianterna, ge dem skrivbehörighet med `adb shell pm grant com.eirora.spike.seeder.watch android.permission.health.WRITE_STEPS` (och `WRITE_EXERCISE`, `WRITE_SLEEP`, samma för `.phone`) och skriv testdata:

```bash
adb shell am start -n com.eirora.spike.seeder.watch/com.eirora.spike.seeder.SeedActivity --es action seed
```

Åtgärden `edit` ändrar ett träningspass, `delete` raderar ett och `clear` tar bort allt seedern skrivit.
Installera aldrig seedern på en telefon med riktig hälsodata.

## Testinstruktion för produktägaren

Det här tar ungefär 30 minuter plus en natt, och kräver en dator med `adb` eller att APK:n skickas till telefonen.

### Installera

1. Bygg APK:n enligt ovan; den hamnar i `artifacts/spike-health-connect/eirora-hc-spike.apk` (APK:er checkas inte in).
2. Antingen med kabel: slå på USB-felsökning under **Inställningar → Om telefonen → tryck sju gånger på Versionsnummer → Utvecklaralternativ**, anslut telefonen och kör:

   ```bash
   adb install -r eirora-hc-spike.apk
   ```

3. Eller utan dator: kopiera APK:n till telefonen, öppna den i Filer och tillåt installation från den källan när Android frågar.
4. På Android 13 och äldre måste appen Health Connect vara installerad från Play Butik; spiken erbjuder en knapp om den saknas.

### Kontrollera

Gör stegen i ordning och notera svaren i rapportmallen nedan.

1. **Status:** att Health Connect är tillgängligt och om bakgrundsläsning och historik finns.
2. **Neka först:** tryck *Ge behörighet för steg, träning och sömn → Fortsätt* och tryck *Avbryt* i Health Connect.
   Appen ska inte krascha och ska visa vad som nekades.
3. **Länken till integritetspolicyn** i Health Connects dialog ska öppna spikens förklaring på svenska.
4. **Ge behörighet** för alla tre vanorna, och sedan för bakgrund och historik om knappen går att trycka på.
5. **Steg, Träning och Sömn:** läs 7 dagar och sedan 365 dagar för varje vana.
   Titta på några poster: stämmer värde och tid med källappen, och vilken `recordingMethod` och enhet har de?
   Mata gärna in ett pass eller steg för hand i källappen först och se om det blir `MANUAL_ENTRY`.
6. **Dubbletter:** finns det dagar med flera källor, och hur stor är skillnaden mellan rå summa och Health Connect?
7. **Ändringar:** tryck *Starta spårning*, ändra eller radera ett träningspass i källappen, kom tillbaka och tryck *Hämta ändringar*.
8. **Bakgrund:** tryck *Kör var 15:e minut*, lämna appen över natten och tryck *Uppdatera logg* på morgonen.
   Tryck sedan *Stoppa schemalagda körningar*.
9. **Rapport:** läs igenom texten, tryck *Kopiera rapporten* och klistra in den som en kommentar i pull requesten.
10. Avinstallera spiken när du är klar; då försvinner dess behörigheter och allt den sparat.

### Rapportera tillbaka

Klistra in rapporten från appen och svara kort på frågorna.
Skriv inga hälsovärden; källappar, inspelningsmetoder och beteenden räcker.

```markdown
### Telefon och källor
- Telefon och Android-version:
- Appar som skriver till Health Connect (klocka, ring, Samsung Health, Fit …):

### Rapport från appen
(klistra in)

### Frågor
- Kraschade appen någon gång, särskilt när du nekade? 
- Stämde poster och tider med källappen?
- Vilken recordingMethod hade steg, träningspass och sömn från varje källa? Blev handinmatade poster MANUAL_ENTRY?
- Fanns det poster med UNKNOWN, och från vilken källa?
- Gick det att ge bakgrund och historik? Kom det poster äldre än 30 dagar med 365 dagar?
- Syntes ändringen eller raderingen under Ändringar?
- Hur många bakgrundskörningar blev det över natten, och hade de poster?
- Något i appen som såg fel ut eller var svårt att förstå?
```

## Rapport från riktig enhet

Produktägarens första körning 2026-10-18; slutsatserna står i [ADR-0006](../../docs/adr/0006-own-health-connect-adapter.md).
Rapporten innehåller inga hälsovärden.

```markdown
### Eirora HC-spike: rapport (utan hälsovärden)

- Datum: 2026-10-18
- Telefon: Google Pixel 7, Android 17 (API 37)
- Health Connect: SDK_AVAILABLE; com.google.android.apps.healthdata 2026.09.03.00.release, com.google.android.healthconnect.controller 17
- Funktioner: bakgrundsläsning finns, historik finns
- Beviljat: steg ja, träningspass ja, sömn ja, bakgrundsläsning nej, historik nej

#### Steg
- Läst intervall: 7 dagar, 1940 poster
- Äldsta post: 6 dagar gammal
- Källa com.google.android.apps.fitness: 800 poster; AUTOMATICALLY_RECORDED 800; enhet: PHONE
- Källa com.android.healthconnect.phone.jab800b2fb50733c8afd9852651374c94: 588 poster; AUTOMATICALLY_RECORDED 588; enhet: PHONE
- Källa com.heytap.health.international: 552 poster; UNKNOWN 552; enhet: ingen enhet
- Överlappande par: 2712 mellan olika källor, 595 inom samma källa
- Dagar med flera källor: 7 av 7; dagar där rå summa är större än Health Connects aggregat: 7
- Aggregatdagar äldre än 30 dagar: 0

#### Träningspass
- Inte läst

#### Sömn
- Läst intervall: 7 dagar, 9 poster
- Äldsta post: 5 dagar gammal
- Källa com.heytap.health.international: 9 poster; UNKNOWN 9; enhet: ingen enhet
- Överlappande par: 0 mellan olika källor, 0 inom samma källa
- Dagar med flera källor: 0 av 6; dagar där rå summa är större än Health Connects aggregat: 2
- Aggregatdagar äldre än 30 dagar: 0

#### Bakgrund
- Inga körningar

#### Ändringar
- Inte hämtade
```

Kvar att köra på telefonen: träningspass, 365 dagar, bakgrund och historik (funktionerna finns), ändringar och en natt med bakgrundskörningar.
