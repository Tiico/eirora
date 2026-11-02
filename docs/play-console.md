# Play Console: underlag för Appinnehåll och butikssidan

Det här är svaren som produktägaren klistrar in i [Play Console](https://play.google.com/console) för appen `se.eirora.app`.
Själva kopplingen till det interna testet, signeringen och tjänstkontot står i [development.md](./development.md#google-play).
Svaren beskriver appen som den är på `main` 2026-11-02 (v0.3.0): inloggning med Google, molnsparning och bidragsdelen är påslagna, Apple-inloggning saknas än.
Googles krav kontrollerades samma dag mot Googles och Unitys egna hjälpsidor, som länkas under varje avsnitt.
Ett svar markerat **bedömning** är vår tolkning där Google inte säger något uttryckligt.

Formulären står på engelska eftersom Play Console och Googles granskare läser dem så.
Play Console visar avsnitten under **Policy → Appinnehåll** (engelska: *Policy → App content*).

## Det som stoppar innan formulären kan skickas in

| Vad | Varför | Vem |
|---|---|---|
| Integritetspolicyn är inte publicerad | [deploy/site](../deploy/site/README.md) håller inne `public/privacy/` tills den rättsliga grunden för bidragsdelen (artikel 9.2 a) är bekräftad. Integritetspolicy, dataskydd och hälsodeklarationen kräver alla en publik adress. | Produktägaren, med juridiskt stöd |
| Health Connects förklaringssida är inaktuell | `HealthPrivacyActivity` säger att allt stannar på telefonen, vilket inte stämmer sedan bidragsdelen kan molnsparas, och den visar inte integritetspolicyn. Google kräver att sidan visar samma policy som Play Console ([Health Connect: get started](https://developer.android.com/health-and-fitness/guides/health-connect/develop/get-started)). | Agent, när policyns adress är känd |
| Appen saknar ikon | Alla ikonfält i `ProjectSettings.asset` är tomma, så appen visar Unitys standardikon. Butikssidan kräver en ikon på 512 × 512. | Produktägaren (grafik) |

Internt test fungerar redan utan detta: Google visar en tillfällig butikssida och kräver inte dataskyddsformuläret för appar som bara finns i internt test ([Data safety](https://support.google.com/googleplay/android-developer/answer/10787469), [internt test](https://support.google.com/googleplay/android-developer/answer/9845334)).
Ett slutet test, ett öppet test eller produktion kräver däremot alla avsnitten nedan.

## Integritetspolicy

**Privacy policy URL:** `https://<värdnamn>/privacy/` enligt [deploy/site](../deploy/site/README.md).
Samma adress anges på butikssidan och ska visas av Health Connects förklaringssida i appen.
Google kräver en publik HTTPS-sida som inte är en PDF, inte kräver inloggning och har rubriken integritetspolicy ([App content](https://support.google.com/googleplay/android-developer/answer/9859455)).

## Appåtkomst (App access)

Välj **All or some functionality in my app is restricted** och lägg till en instruktion:

> Eirora is fully playable without an account.
> The app's interface is in Swedish.
> Signing in is optional and only adds cloud save and moving the island to a new phone: tap "Konto" (Account) in the side rail, then "Logga in med Google" (Sign in with Google).
> Any Google account works; no credentials are needed from us.
> Health Connect is optional: every habit can also be entered by hand ("Skriv in det du gjort").

Bedömning: Google ber om instruktioner när inloggning döljer funktioner, och molnsparningen syns bara efter inloggning.

## Annonser (Ads)

**Does your app contain ads?** No.
Eirora har ingen reklam (MON-06) och inget annons-SDK.
Kontrollera vid varje nytt Android-beroende att det sammanslagna manifestet inte får `com.google.android.gms.permission.AD_ID` ([Advertising ID](https://support.google.com/googleplay/android-developer/answer/6048248)).

## Innehållsklassificering (Content rating)

Frågornas exakta lydelse visas bara i Play Console, så svaren nedan följer IARC:s ämnen ([content rating](https://support.google.com/googleplay/android-developer/answer/188189), [IARC](https://globalratings.com/how-iarc-works/)).

| Fråga | Svar |
|---|---|
| Category | Game |
| Violence, blood, fear | No |
| Sexuality, nudity | No |
| Language, crude humour | No |
| Controlled substances (alcohol, tobacco, drugs) | No |
| Gambling, simulated gambling | No |
| Users can interact or exchange content | No |
| Shares the user's location with others | No |
| In-app or digital purchases, including random items | No |
| Unrestricted internet access (browser, search) | No; the app only talks to its own server |

Gör om enkäten när innehållet ändras, till exempel när vänner (sociala funktioner) eller supporterprenumerationen (O-06) kommer, eftersom båda ändrar svaren.

## Målgrupp och innehåll (Target audience and content)

- **Target age groups:** endast **18 and over** (D-30).
- **Restrict Minor Access:** på. Google hindrar då personer som Google bedömer är under 18 från att hitta och ladda ner appen, vilket gör att 18+ sköts av butiken som D-58 bestämmer ([target audience](https://support.google.com/googleplay/android-developer/answer/9867159)).
- **Appeal to children:** No.

Avsnittet kräver att integritetspolicy, appåtkomst och annonser är ifyllda först.

## Finansiella funktioner (Financial features)

**My app doesn't provide any financial features.** ([Financial features](https://support.google.com/googleplay/android-developer/answer/13849271))

## Myndighetsappar och nyhetsappar

Svara **No** om Play Console frågar.
Eirora är varken en myndighetsapp eller en nyhetsapp.

## Hälsoappar och Health Connect (Health apps)

Ett och samma formulär tar både hälsofunktionerna och motiveringen per Health Connect-datatyp ([Health apps](https://support.google.com/googleplay/android-developer/answer/14738291), [declare access](https://developer.android.com/health-and-fitness/guides/health-connect/publish/declare-access)).
Motiveringen per datatyp dyker upp när ett app-paket med Health Connect-behörigheterna har laddats upp, och Google släpper ingen sådan version förrän den är godkänd.

**Health features:** Activity and fitness, Sleep management.
Bedömning: inga medicinska kategorier, eftersom spelet varken mäter, diagnostiserar eller ger råd.

**Use case:** Health-integrated games ([Health Connect policy](https://support.google.com/googleplay/android-developer/answer/9888170)).

Appen begär exakt tre läsbehörigheter, vilket [`AndroidHealthCheck`](../client/Assets/Eirora/Editor/AndroidHealthCheck.cs) kontrollerar i varje bygge.

| Behörighet | Justification |
|---|---|
| `READ_STEPS` | Eirora is a health-integrated game. When the player opens the game, it reads today's step count to grant Exploration, the in-game power that reveals new islands on the map. Only the daily total is kept, on the device. Steps are read in the foreground only, never in the background, never written, and never used for ads. Players who decline can enter their steps by hand and get exactly the same in-game result. |
| `READ_EXERCISE` | Eirora is a health-integrated game. When the player opens the game, it reads the day's exercise sessions to grant Strength, the in-game power used to build and work the island. Only the daily total duration is kept, on the device. Sessions are read in the foreground only, never in the background, never written, and never used for ads. Players who decline can enter their exercise by hand and get exactly the same in-game result. |
| `READ_SLEEP` | Eirora is a health-integrated game. When the player opens the game, it reads last night's sleep duration to grant Stamina, the in-game power that decides how much the island can do that day. Only the total duration is kept, on the device; sleep stages are not used. Sleep is read in the foreground only, never in the background, never written, and never used for ads. Players who decline can enter their sleep by hand and get exactly the same in-game result. |

Krafternas engelska namn (Exploration, Strength, Stamina) är förslag: [GLOSSARY.md](../GLOSSARY.md) har bara kodnamnen `Exploration`, `BuildPower` och `DawnPower`.
Använd samma namn i motiveringarna och på butikssidan.

Policyns krav som appen redan uppfyller: bara läsning för ett godkänt användningsfall, ingen reklam eller försäljning, ingen överföring till andra och ingen människa som läser uppgifterna (bidragsdelen är krypterad på telefonen, D-57).
Kravet som inte är uppfyllt än är förklaringssidan, se [Det som stoppar](#det-som-stoppar-innan-formulären-kan-skickas-in).

## Dataskydd (Data safety)

Google räknar uppgifter som insamlade när appen skickar dem från telefonen, och som delade när de lämnas till någon annan än utvecklaren och dennes tjänsteleverantörer ([Data safety](https://support.google.com/googleplay/android-developer/answer/10787469)).
Uppgifter som bara behandlas på telefonen, och uppgifter som skickas end-to-end-krypterade så att ingen annan än avsändare och mottagare, utvecklaren inräknad, kan läsa dem, räknas inte som insamlade.

### Data collection and security

| Fråga | Svar |
|---|---|
| Does your app collect or share any of the required user data types? | Yes |
| Is all of the user data collected by your app encrypted in transit? | Yes; all calls go over HTTPS through Traefik with Let's Encrypt ([deploy/compose.yaml](../deploy/compose.yaml)) |
| Which account creation methods does your app support? | OAuth (Sign in with Google; Sign in with Apple when it ships) |
| Account deletion URL | `https://<servervärdnamn>/account/delete` |
| Do you provide a way for users to request that some or all of their data is deleted, without requiring them to delete their account? | Yes; Konto → Sluta spara egenvården på kontot deletes the contribution part |

### Data types

| Datatyp | Collected | Shared | Ephemeral | Required | Purposes | Vad det är |
|---|---|---|---|---|---|---|
| Personal info → User IDs | Yes | No | No | Optional | App functionality, Account management | Det slumpade konto-id:t och det id Google eller Apple ger spelaren för Eirora |
| App activity → Other actions | Yes | No | No | Optional | App functionality | Världsdelen av molnsparningen (karta, byggnader, saldon, spelets klocka) och svaren på frågan om bidragsdelen |

Inget annat deklareras:

- **Health info och Fitness info:** hälsodata lämnar bara telefonen som bidragsdelen, krypterad på telefonen med en nyckel som bara spelaren har, och servern kan inte läsa den. Det är Googles undantag för end-to-end-kryptering. Bedömning: Google skriver inte uttryckligen om fallet där avsändare och mottagare är samma spelare.
- **Fitness info i världsdelen:** saldona växer med egenvården men innehåller inga uppgifter per dag. Bedömning; byt till att deklarera Fitness info som Collected, Optional, App functionality om Google invänder.
- **Device or other IDs:** inloggningen per enhet har ett eget slumpat id och ingen identifierare från telefonen.
- **IP-adressen:** används bara i minnet för att begränsa antalet anrop och sparas inte med kontot. Bedömning: flyktig behandling för säkerhet, som inte behöver deklareras.
- **Kameran:** bilderna av biljetten läses på telefonen och skickas aldrig.
- **Block Store och telefonens säkerhetskopia:** nyckeln går end-to-end-krypterad till spelarens eget Google-konto, och säkerhetskopian sköts av Android, inte av appen.
- **Unity:** Diagnostics, Analytics, Performance Reporting och Cloud Diagnostics är avstängda i `UnityConnectSettings.asset`, så Unity samlar inget som behöver deklareras ([Unity: Google data safety](https://docs.unity.com/en-us/cloud/developer-data/google-data-safety)). Slås Diagnostics på deklareras Crash logs, Diagnostics och Device or other IDs som Collected, Required, Analytics.

Uppdatera avsnittet, integritetspolicyn och den här tabellen innan en version som samlar in något nytt släpps.

## Butikssidan (Main store listing)

Butikssidan behövs först för slutet test eller produktion; varumärkesansökan ska lämnas in innan den blir offentlig ([namnkontrollen](./research/eirora-name-check.md)).
Standardspråk English (United States), med svenska som översättning.
Texten lovar ingen hälsoeffekt, i linje med att spelet aldrig rangordnar kroppar.

**App name** (högst 30 tecken): `Eirora`

**Short description** (högst 80 tecken):

- en-US: `A calm Nordic island that grows each dawn as you take care of yourself.`
- sv-SE: `En lugn nordisk ö som växer varje gryning när du tar hand om dig själv.`

**Full description** (högst 4 000 tecken), en-US:

```text
A world worth growing.

Eirora is a calm strategy game about a small Nordic archipelago. Every dawn, the island wakes up with what you gave yourself yesterday: your walks become Exploration, your workouts become Strength and your sleep becomes Stamina.

How much you have depends on you. What it becomes depends on your choices. Build a harbour, open a sea route, send an expedition into the fog, and watch the island's economy keep paying back the decisions you make.

• Self-care is the fuel, strategy is the game. The first steps count the most, so a quiet day still moves your island forward, and a smart plan can always catch up.
• No body is ranked. Steps, exercise and sleep can come from Health Connect or be entered by hand, with exactly the same result.
• Nothing is lost. Powers never decay, and the island waits for you while you live your life.
• Your data stays yours. Health data is read only when you open the game, never in the background, and never leaves your phone in a form we can read.
• No ads, no tracking, no pay-to-win.

An account is optional. Sign in to keep your island in the cloud and take it to a new phone.
```

**Full description**, sv-SE:

```text
En värld värd att växa.

Eirora är ett lugnt strategispel om en liten nordisk ögrupp. Varje gryning vaknar ön med det du gav dig själv i går: dina promenader blir Utforskning, dina träningspass blir Styrka och din sömn blir Ork.

Hur mycket du har beror på dig. Vad det blir avgör dina val. Bygg en hamn, öppna en sjöväg, skicka en expedition in i dimman och se öns ekonomi fortsätta ge tillbaka för besluten du fattar.

• Egenvården är bränslet, strategin är spelet. De första stegen räknas mest, så en lugn dag tar ändå ön framåt, och en klok plan kan alltid hinna ikapp.
• Ingen kropp rangordnas. Steg, träning och sömn kan komma från Health Connect eller skrivas in själv, med exakt samma resultat.
• Inget går förlorat. Krafterna förfaller aldrig, och ön väntar medan du lever ditt liv.
• Dina uppgifter är dina. Hälsodata läses bara när du öppnar spelet, aldrig i bakgrunden, och lämnar aldrig telefonen i en form som vi kan läsa.
• Ingen reklam, ingen spårning, inga köpta fördelar.

Ett konto är frivilligt. Logga in för att spara ön i molnet och ta med den till en ny telefon.
```

Stäm av beskrivningen mot spelet innan den publiceras: expeditioner och dimman kommer med TASK-074 och TASK-075.

**App category:** Game → Strategy.
**Tags:** välj bland Googles förslag, till exempel Simulation, Casual och City building.
**Contact details:** samma kontaktadress som i integritetspolicyn; webbplatsen är valfri.

### Grafik

| Tillgång | Krav | Status |
|---|---|---|
| App icon | 512 × 512 px, 32-bitars PNG med alfa, högst 1 024 kB | Saknas; samma motiv behövs som adaptiv ikon i Unity |
| Feature graphic | 1 024 × 500 px, JPEG eller 24-bitars PNG utan alfa | Saknas |
| Phone screenshots | 2–8 st, JPEG eller 24-bitars PNG, sidor 320–3 840 px och långsidan högst dubbla kortsidan; Google rekommenderar minst 3 i 1 080 × 1 920 för spel | Kan tas med skärmbildsturen (`ScreenshotTour`) när grafiken är slutlig |

Kraven kommer från [Googles riktlinjer för grafik](https://support.google.com/googleplay/android-developer/answer/9866151).

## Väg till produktion

Eirora ligger på ett personligt utvecklarkonto.
Ett sådant konto, om det skapats efter 13 november 2023, får inte släppa i produktion förrän appen har haft ett slutet test med minst 12 testare som varit anmälda utan avbrott i 14 dagar ([production access](https://support.google.com/googleplay/android-developer/answer/14151465)).
Ansökan frågar sedan om testet, om appen och om vad som ändrats efter testet; Google svarar oftast inom 7 dagar.
Det slutna testet kräver Hälsoappar, Dataskydd och Finansiella funktioner, så allt ovan behöver vara inskickat innan det kan börja.
Grind A5 med extern testare (TASK-089) är ett naturligt tillfälle att starta det.

## Ordning

1. Bekräfta den rättsliga grunden och publicera integritetspolicyn.
2. Uppdatera Health Connects förklaringssida så att den visar policyn, och släpp en version.
3. Fyll i Integritetspolicy, Appåtkomst, Annonser, Innehållsklassificering, Målgrupp och Finansiella funktioner.
4. Fyll i Hälsoappar med motiveringarna ovan och skicka in; vänta på godkännandet.
5. Fyll i Dataskydd.
6. Lägg in ikon, grafik och butikstext när de finns, och starta ett slutet test.
