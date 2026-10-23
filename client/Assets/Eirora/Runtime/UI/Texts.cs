using System.Collections.Generic;
using System.Linq;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using MapTerrain = Eirora.Simulation.Map.Terrain;

namespace Eirora.Client.UI
{
    /// <summary>
    /// The prototype's Swedish texts in one place, ready to move into the localisation system (UX-06).
    /// Tone: calm and warm, never guilt (ux-journeys.md).
    /// </summary>
    public static class Texts
    {
        public static string Name(BuildingKind kind) => kind switch
        {
            BuildingKind.Longhouse => "Långhus",
            BuildingKind.Cottage => "Stuga",
            BuildingKind.Storehouse => "Förråd",
            BuildingKind.FishingHut => "Fiskebod",
            BuildingKind.Field => "Åker",
            BuildingKind.Woodcutter => "Huggarkoja",
            BuildingKind.SheepFarm => "Fårgård",
            BuildingKind.Quarry => "Stenbrott",
            BuildingKind.Sawmill => "Sågverk",
            BuildingKind.Weavery => "Väveri",
            BuildingKind.Jetty => "Brygga",
            _ => "Utkiksvarde",
        };

        public static string Name(Resource resource) => resource switch
        {
            Resource.Fish => "fisk",
            Resource.Grain => "säd",
            Resource.Timber => "timmer",
            Resource.Planks => "plank",
            Resource.Stone => "sten",
            Resource.Wool => "ull",
            _ => "vadmal",
        };

        public static string Name(Force force) => force switch
        {
            Force.Exploration => "Utforskning",
            Force.BuildPower => "Byggkraft",
            _ => "Gryningskraft",
        };

        /// <summary>What a tile is: its ground, or the crest, and whether it is rich.</summary>
        public static string Ground(WorldMap map, Hex tile, TileUse uses)
        {
            string ground = (uses & TileUse.Crest) != 0 ? "Krönet" : map.TerrainAt(tile) switch
            {
                MapTerrain.DeepSea => "Djupt hav",
                MapTerrain.ShallowSea => "Grunt vatten",
                MapTerrain.Beach => "Strand",
                MapTerrain.Forest => "Skog",
                MapTerrain.Farmland => "Åker",
                MapTerrain.Pasture => "Bete",
                MapTerrain.Mountain => "Berg",
                _ => "Äng",
            };
            return map.RichnessAt(tile) > 100 ? $"{ground}, rik" : ground;
        }

        /// <summary>The timber left on a forest tile, which woodcutters fell and which grows back.</summary>
        public static string Timber(long left) => left >= World.TimberPerForest
            ? $"{World.TimberPerForest} timmer att fälla."
            : $"{left} av {World.TimberPerForest} timmer kvar att fälla. Skogen växer tillbaka.";

        /// <summary>Items joined as Swedish lists them: "a, b och c".</summary>
        public static string List(IEnumerable<string> items)
        {
            var all = items.ToList();
            return all.Count <= 1 ? string.Concat(all) : string.Join(", ", all.Take(all.Count - 1)) + " och " + all[all.Count - 1];
        }

        /// <summary>What a tile is good for, as a building site (D-79, economy-v1.md, section 3.1).</summary>
        public static string Uses(TileUse uses)
        {
            var parts = new List<string>();
            void Add(TileUse use, string name)
            {
                if ((uses & use) != 0)
                {
                    parts.Add(name);
                }
            }

            Add(TileUse.Land, "byggmark");
            Add(TileUse.Farmland, "åker");
            Add(TileUse.Pasture, "bete");
            Add(TileUse.Forest, "skog att fälla");
            Add(TileUse.Mountain, "berg att bryta");
            Add(TileUse.FishingWater, "fiskevatten");
            Add(TileUse.HarborSpot, "hamnläge");
            Add(TileUse.Crest, "Vårdkasens plats");
            return parts.Count == 0 ? "Här kan inget byggas." : "Duger till " + List(parts) + ".";
        }

        public static string Cost(BuildingSpec spec)
        {
            var parts = spec.Cost.Select(c => $"{c.Value} {Name(c.Key)}").ToList();
            if (spec.BuildPower > 0)
            {
                parts.Add($"{spec.BuildPower} Byggkraft");
            }

            parts.Add($"{spec.WorkHours} h arbete");
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// What a building does in short, as the table in economy-v1.md (section 3) says it: from its recipe or housing in
        /// the content catalog, and for the storehouse, the jetty and the lookout from the rules. Null for a building that
        /// does none of these.
        /// </summary>
        public static string? Effect(BuildingKind kind, BuildingSpec spec)
        {
            if (spec.Recipe is Recipe recipe)
            {
                return Effect(recipe);
            }

            if (spec.Housing > 0)
            {
                return $"+{spec.Housing} bostadsplatser";
            }

            return kind switch
            {
                BuildingKind.Storehouse => "Lager 150 per vara",
                BuildingKind.Jetty => "Sjöväg och expeditioner",
                BuildingKind.Lookout => "25 % mindre Utforskning",
                _ => null,
            };
        }

        /// <summary>Whether a building that needs staff works, is paused or waits for villagers; null for one that needs none.</summary>
        public static string? Staffing(Building building, BuildingSpec spec) =>
            spec.Staff == 0 ? null : building.Paused ? "pausad" : building.Staffed ? "i arbete" : "väntar på bybor";

        /// <summary>What a project in the queue makes: a building, a level or a road.</summary>
        public static string Project(Project project) => project.Kind switch
        {
            ProjectKind.Upgrade => $"{Name(project.Building)} till nivå 2",
            ProjectKind.Road => $"Väg till {Name(project.Building).ToLowerInvariant()}",
            _ => Name(project.Building),
        };

        /// <summary>The work hours a project has left, rounded up.</summary>
        public static string WorkLeft(Project project) => $"{(project.WorkLeft + 999) / 1000} h arbete kvar";

        /// <summary>A number of villagers: "en bybo", "två bybor".</summary>
        public static string Villagers(int count) => count == 1 ? "en bybo" : $"{Count(count)} bybor";

        /// <summary>A number of free villagers: "en ledig bybo", "tre lediga bybor".</summary>
        public static string FreeVillagers(int count) => count == 1 ? "en ledig bybo" : $"{Count(count)} lediga bybor";

        /// <summary>
        /// What a finished building that produces nothing does, in a sentence for its sheet: the housing the content
        /// catalog gives it, or what the rules let the storehouse and the lookout do. Null for a building that does none.
        /// </summary>
        public static string? Purpose(BuildingKind kind, BuildingSpec spec)
        {
            if (spec.Housing > 0)
            {
                return spec.Housing == 1 ? "Ger plats åt en bybo." : $"Ger plats åt {Count(spec.Housing)} bybor.";
            }

            return kind switch
            {
                BuildingKind.Storehouse => "Rymmer 150 av varje vara. Byggnader nära förrådet får fram mer.",
                BuildingKind.Lookout => "Kartläggning och expeditioner kostar en fjärdedel mindre Utforskning.",
                _ => null,
            };
        }

        private static string Effect(Recipe recipe)
        {
            string product = Name(recipe.Product);
            if (recipe.Inputs.Count > 0)
            {
                string inputs = List(recipe.Inputs.Select(i => $"{i.PerProduct} {Name(i.Resource)}"));
                return recipe.PerHour == Quantity.PerUnit ? $"{inputs} → 1 {product}/h"
                    : Quantity.PerUnit % recipe.PerHour == 0 ? $"{inputs} → 1 {product} per {Quantity.PerUnit / recipe.PerHour} h"
                    : $"{inputs} → 1 {product}, {Amount(recipe.PerHour)}/h";
            }

            if (recipe.ForestTiles > 0)
            {
                return recipe.ForestTiles == 1
                    ? $"{Amount(recipe.PerHour)} {product}/h från en skogsbricka"
                    : $"{Amount(recipe.PerHour)} {product}/h per skogsbricka, högst {Count(recipe.ForestTiles)}";
            }

            return recipe.IsSlowProcess ? $"{Amount(recipe.GrowsPerHour)} {product}/h dygnet runt" : $"{Amount(recipe.PerHour)} {product}/h";
        }

        /// <summary>Millionths of a unit as a Swedish decimal, such as 1,25.</summary>
        private static string Amount(long millionths)
        {
            string whole = (millionths / Quantity.PerUnit).ToString(System.Globalization.CultureInfo.InvariantCulture);
            long fraction = millionths % Quantity.PerUnit;
            return fraction == 0 ? whole : $"{whole},{fraction.ToString("000000", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0')}";
        }

        /// <summary>A small count in words, as the texts say it; larger counts in digits.</summary>
        private static string Count(int count) => count switch
        {
            2 => "två",
            3 => "tre",
            4 => "fyra",
            5 => "fem",
            6 => "sex",
            7 => "sju",
            8 => "åtta",
            9 => "nio",
            10 => "tio",
            11 => "elva",
            12 => "tolv",
            _ => count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        /// <summary>Which build this is, so the prototype diary can refer to it (TASK-014).</summary>
        public static string Build(string appVersion) => $"Version {appVersion} · regelversion {RulesVersion.Current}";

        public static string Provider(string provider) => provider switch
        {
            Contracts.LoginProviders.Apple => "Apple",
            Contracts.LoginProviders.Google => "Google",
            _ => provider,
        };

        /// <summary>What to tell the player after an account action, or null when there is nothing to say.</summary>
        public static string? Account(Accounts.AccountOutcome outcome) => outcome switch
        {
            Accounts.AccountOutcome.Created => "Kontot är skapat. Välkommen!",
            Accounts.AccountOutcome.Deleted => "Kontot är raderat. Din värld finns kvar på telefonen.",
            Accounts.AccountOutcome.Offline => "Servern går inte att nå just nu. Spelet fungerar som vanligt.",
            Accounts.AccountOutcome.SignedOut => "Du är utloggad. Logga in igen för att fortsätta.",
            Accounts.AccountOutcome.SignInFailed => "Inloggningen gick inte. Försök igen.",
            Accounts.AccountOutcome.ProviderUnavailable => "Den inloggningen finns inte på den här telefonen.",
            Accounts.AccountOutcome.LoginOfAnotherAccount => "Den inloggningen hör redan till ett annat konto.",
            Accounts.AccountOutcome.LastLogin => "Kontot behöver minst en inloggning.",
            Accounts.AccountOutcome.NotThisAccount => "Bekräfta med en inloggning som hör till det här kontot.",
            Accounts.AccountOutcome.ConfirmationUnavailable => "Telefonen kan inte logga in med någon av kontots inloggningar.",
            Accounts.AccountOutcome.Failed => "Det gick inte just nu. Försök igen om en stund.",
            _ => null,
        };

        /// <summary>What to tell the player after a recovery code was tried, or null when the island is being unlocked.</summary>
        public static string? RecoveryCode(App.RecoveryCodeOutcome outcome) => outcome switch
        {
            App.RecoveryCodeOutcome.Unlocked => null,
            App.RecoveryCodeOutcome.NotACode => "Det är inte en hel återställningskod. Kontrollera tecknen.",
            App.RecoveryCodeOutcome.OlderCode => "Koden är från en äldre nyckel. Visa en ny kod på telefonen som har ön.",
            App.RecoveryCodeOutcome.NotThisAccountsCode => "Koden hör inte till det här kontots ö.",
            App.RecoveryCodeOutcome.Offline => "Servern går inte att nå just nu. Försök igen om en stund.",
            _ => "Det gick inte just nu. Försök igen om en stund.",
        };

        /// <summary>How a key transfer stands on the new phone, under the QR code (TASK-023b).</summary>
        public static string KeyTransfer(CloudSaving.KeyTransferOutcome outcome, System.DateTimeOffset? expiresAt) => outcome switch
        {
            CloudSaving.KeyTransferOutcome.Waiting => expiresAt is { } at
                ? $"Väntar på din gamla telefon. Koden gäller till {at.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)}."
                : "Väntar på din gamla telefon.",
            CloudSaving.KeyTransferOutcome.Received => "Nyckeln kom fram.",
            CloudSaving.KeyTransferOutcome.Expired => "Koden gäller inte längre.",
            CloudSaving.KeyTransferOutcome.Unreadable => "Det som kom gick inte att läsa här. Försök med en ny kod.",
            CloudSaving.KeyTransferOutcome.Rejected => "Nyckeln som kom hör inte till kontots ö. Försök med en ny kod från telefonen som har ön.",
            CloudSaving.KeyTransferOutcome.Offline => expiresAt is { } until
                ? $"Servern går inte att nå just nu. Koden gäller ändå till {until.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)}, och försöket fortsätter."
                : "Servern går inte att nå just nu. Försök igen om en stund.",
            CloudSaving.KeyTransferOutcome.SignedOut => "Logga in igen för att hämta nyckeln.",
            _ => "Det gick inte just nu. Försök igen om en stund.",
        };

        /// <summary>What to tell the old phone's player after a scanned code was acted on (TASK-023b).</summary>
        public static string KeyDelivery(CloudSaving.KeyDeliveryOutcome outcome) => outcome switch
        {
            CloudSaving.KeyDeliveryOutcome.Delivered => "Nyckeln är skickad. Den nya telefonen hämtar din ö nu.",
            CloudSaving.KeyDeliveryOutcome.NoKey => "Den här telefonen har ingen nyckel till kontots egenvård.",
            CloudSaving.KeyDeliveryOutcome.BadTicket => "Det är inte en kod från Eirora. Sök koden på den nya telefonen.",
            CloudSaving.KeyDeliveryOutcome.AlreadyDelivered => "En nyckel är redan skickad till den telefonen.",
            CloudSaving.KeyDeliveryOutcome.Expired => "Koden gäller inte längre. Visa en ny på den nya telefonen.",
            CloudSaving.KeyDeliveryOutcome.Offline => "Servern går inte att nå just nu. Försök igen om en stund.",
            CloudSaving.KeyDeliveryOutcome.SignedOut => "Logga in igen för att skicka nyckeln.",
            _ => "Nyckeln kunde inte skickas. Försök igen om en stund.",
        };

        /// <summary>What the old phone's player sees over the camera's picture (TASK-023b).</summary>
        public static string Scanner(ScannerStatus status) => status switch
        {
            ScannerStatus.Starting => "Startar kameran …",
            ScannerStatus.Scanning => "Söker koden …",
            ScannerStatus.Denied => "Eirora får inte använda kameran. Tillåt det i telefonens inställningar, eller skriv in återställningskoden på den nya telefonen.",
            _ => "Den här telefonen har ingen kamera som går att använda. Skriv in återställningskoden på den nya telefonen i stället.",
        };

        /// <summary>How the cloud save stands, for the account sheet, or null when there is no sign-in.</summary>
        public static string? CloudSync(App.CloudSyncStatus status, System.DateTimeOffset? syncedAt) => status switch
        {
            App.CloudSyncStatus.Off => null,
            App.CloudSyncStatus.Working => "Molnsparning: laddar upp …",
            App.CloudSyncStatus.Synced => syncedAt is { } at
                ? $"Molnsparning: din ö är sparad på kontot, senast {at.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)}"
                : "Molnsparning: din ö är sparad på kontot",
            App.CloudSyncStatus.NoWorld => "Molnsparning: ingen ö att spara ännu",
            App.CloudSyncStatus.Offline => "Molnsparning: väntar på uppkoppling, din ö sparas när servern nås",
            App.CloudSyncStatus.ChoiceNeeded => "Molnsparning: telefonen och kontot har olika öar",
            App.CloudSyncStatus.KeyNeeded => "Molnsparning: kontots ö väntar på nyckeln från din gamla telefon",
            App.CloudSyncStatus.AppOutdated => "Molnsparning: kontots ö sparades av en nyare version av Eirora. Uppdatera appen.",
            App.CloudSyncStatus.SignedOut => "Molnsparning: logga in igen för att spara din ö på kontot",
            _ => "Molnsparning: gick inte just nu, försöker igen vid nästa ändring",
        };

        public static string Reason(Rejection rejection) => rejection switch
        {
            Rejection.Resting => "Byborna sover. Det här kan du göra i morgon.",
            Rejection.InsufficientResources => "Det finns inte tillräckligt i förrådet än.",
            Rejection.InsufficientForces => "Kraften räcker inte än. Den växer med din egenvård.",
            Rejection.SiteUnavailable => "Det går inte att bygga här.",
            Rejection.AlreadyDone => "Det är redan gjort.",
            Rejection.NothingLeftToSurvey => "Hela ön är kartlagd.",
            Rejection.NothingToRush => "Det finns inget bygge att rusta.",
            Rejection.NotNextToTheKnown => "Dimman lättar från kanten av det kända. Lyft en bricka närmare först.",
            Rejection.FoundBySurvey => "Något ligger gömt här. Kartlägg ön för att se vad.",
            Rejection.FoundByExpedition => "Här ligger en okänd ö. En expedition kan hitta den.",
            Rejection.NoBuildingThere => "Det behövs en byggnad för det här först.",
            Rejection.GameDayOutOfRange => "Det går att fylla i de senaste sju dygnen.",
            Rejection.AmountOutOfRange => "Värdet verkar inte stämma.",
            Rejection.NoSuchPlace => "Längre än så går den inte att flytta.",
            Rejection.NotQueued => "Det bygget ligger inte i kön längre.",
            _ => "Det gick inte just nu.",
        };

        /// <summary>
        /// What the island across the sea is called: Nyön on the prototype's map, "den nya ön" on a generated one, whose
        /// islands have no names yet and can lie in any direction.
        /// </summary>
        public static string IslandName(string? island) => island == PrototypeHome.Nyon ? "Nyön" : "den nya ön";

        /// <summary>A building in the definite form, as a sentence begins with it: Sågverket, Fiskeboden.</summary>
        public static string Definite(BuildingKind kind) => kind switch
        {
            BuildingKind.Longhouse => "Långhuset",
            BuildingKind.Cottage => "Stugan",
            BuildingKind.Storehouse => "Förrådet",
            BuildingKind.FishingHut => "Fiskeboden",
            BuildingKind.Field => "Åkern",
            BuildingKind.Woodcutter => "Huggarkojan",
            BuildingKind.SheepFarm => "Fårgården",
            BuildingKind.Quarry => "Stenbrottet",
            BuildingKind.Sawmill => "Sågverket",
            BuildingKind.Weavery => "Väveriet",
            BuildingKind.Jetty => "Bryggan",
            _ => "Utkiksvarden",
        };

        /// <summary>
        /// How a building works now, for its sheet beside the ground (TASK-043): how it produces and how much an hour, or
        /// for a building that produces nothing whether it has its villagers; null when it needs none.
        /// </summary>
        public static string? Status(BuildingDiagnosis building, BuildingSpec spec)
        {
            if (building.Product is not Resource product)
            {
                return Staffing(building.Building, spec);
            }

            return building.Building.Paused ? "pausad" : building.Production switch
            {
                Production.Full => $"går för fullt · {Rate(building.PerHour)} {Name(product)}/h",
                Production.Reduced => $"går långsammare · {Rate(building.PerHour)} {Name(product)}/h",
                _ => "står still",
            };
        }

        /// <summary>How a building is while the village rests, when nothing is made and nothing is diagnosed.</summary>
        public static string? Resting(Building building, BuildingSpec spec) =>
            !spec.Produces ? Staffing(building, spec) : building.Paused ? "pausad" : "vilar";

        /// <summary>What holds a building back, in a sentence for its sheet (TASK-043, LOG-03).</summary>
        public static string Hindrance(Hindrance hindrance, Resource? product) => hindrance switch
        {
            MissingStaff staff => $"Det saknas {Villagers(staff.Villagers)} här. Flytta upp den i bemanningen eller pausa en byggnad före den.",
            Paused => "Pausad: byborna bygger i stället. Nivån och lagret står kvar.",
            WaitingForInput input =>
                $"Väntar på {Name(input.Resource)}: {input.Missing} till behövs för en timmes arbete. Minimilagret på {input.Reserve} sparas till byggen.",
            StoreFull { Capacity: 0 } full => $"Ön har inget förråd för {Name(full.Resource)}.",
            StoreFull full => $"Lagret är fullt: {full.Capacity} {Name(full.Resource)}. Inget går förlorat.",
            ForestFelled { InReach: 0 } => "Här finns ingen skog kvar att fälla.",
            ForestFelled forest when forest.Felled == forest.InReach => "Skogen är fälld och växer tillbaka dag och natt.",
            ForestFelled forest => $"{forest.Felled} av {forest.InReach} skogsbrickor är fällda. Skogen växer tillbaka dag och natt.",
            LongWay way => Way(way, product),
            _ => $"Något håller tillbaka {Rate(hindrance.LostPerHour)} i timmen.",
        };

        /// <summary>What a road gives a building far from the storehouse, beside the road's cost on its sheet.</summary>
        public static string RoadGives(LongWay way, Resource product) =>
            $"{Rate(way.WithRoadPerHour)} {Name(product)}/h i stället för {Rate(way.PerHour)}";

        /// <summary>The sea route's use now and what keeps goods from reaching home, for the jetties' sheets.</summary>
        public static IEnumerable<string> SeaRoute(SeaRouteDiagnosis route)
        {
            yield return route.UsedPercent >= 100 ? "Skeppen går fulla."
                : route.ShippedPerHour == 0 ? "Skeppen har ingen last just nu."
                : $"Skeppen använder {route.UsedPercent} % av sin plats.";
            foreach (var hindrance in route.Hindrances)
            {
                if (hindrance is StoreFull full)
                {
                    yield return $"Lagret hemma är fullt av {Name(full.Resource)}, så den väntar på andra sidan.";
                }
            }
        }

        /// <summary>An island's largest bottleneck in a sentence, and where a workday eases it (economy-v1.md, section 6).</summary>
        public static string Bottleneck(Bottleneck bottleneck)
        {
            var kind = bottleneck.Building.Building.Kind;
            string building = Definite(kind);
            string held = bottleneck.Hindrance switch
            {
                WaitingForInput input => $"{building} väntar på {Name(input.Resource)}.",
                MissingStaff staff => $"{building} saknar {Villagers(staff.Villagers)}.",
                StoreFull full => $"Lagret är fullt av {Name(full.Resource)}, så {building.ToLowerInvariant()} "
                    + (bottleneck.Building.Production == Production.Stopped ? "står still." : "går långsammare."),
                ForestFelled => $"Skogen kring {building.ToLowerInvariant()} är fälld.",
                LongWay way when bottleneck.Building.Product is Resource product =>
                    $"{Share(way.Percent)} av {building.ToLowerInvariant()}s {Name(product)} når fram.",
                _ => $"{building} hålls tillbaka.",
            };

            return bottleneck.Workday is Building workday && bottleneck.Hindrance is WaitingForInput missing
                ? $"{held} Ett dagsverke på {Definite(workday.Kind).ToLowerInvariant()} ger mer {Name(missing.Resource)}."
                : held;
        }

        /// <summary>Millionths of a unit an hour, to two decimals at most: 0,94.</summary>
        public static string Rate(long millionths)
        {
            long hundredths = (millionths + 5_000) / 10_000;
            string whole = (hundredths / 100).ToString(System.Globalization.CultureInfo.InvariantCulture);
            long fraction = hundredths % 100;
            return fraction == 0 ? whole
                : fraction % 10 == 0 ? $"{whole},{fraction / 10}"
                : $"{whole},{fraction.ToString("00", System.Globalization.CultureInfo.InvariantCulture)}";
        }

        private static string Way(LongWay way, Resource? product)
        {
            string steps = way.Steps == int.MaxValue ? "inget förråd nås över land" : $"{Count(way.Steps)} steg till närmaste förråd";
            string text = $"{Share(way.Percent)} når fram, {steps}.";
            return product is Resource made && way.BesideDepotPerHour > way.PerHour
                ? $"{text} Ett förråd intill ger {Rate(way.BesideDepotPerHour)} {Name(made)}/h."
                : text;
        }

        private static string Share(int percent) => percent switch
        {
            50 => "Hälften",
            75 => "Tre fjärdedelar",
            _ => $"{percent} %",
        };

        /// <summary>One line in the daily report, or null for events the report leaves out.</summary>
        public static string? Describe(GameEvent gameEvent) => gameEvent switch
        {
            DawnBroke dawn => $"Byn vaknade till spelardygn {dawn.GameDay}.",
            ForceCredited { Amount: > 0 } credit => $"+{credit.Amount} {Name(credit.Force)} från din egenvård.",
            VillagersMovedIn moved => moved.Count == 1 ? "En ny bybo flyttade in." : $"{moved.Count} nya bybor flyttade in.",
            ProjectCompleted { Kind: ProjectKind.Build } done => $"{Name(done.Building)} är färdig.",
            ProjectCompleted { Kind: ProjectKind.Upgrade } done => $"{Name(done.Building)} är uppgraderad.",
            ProjectCompleted { Kind: ProjectKind.Road } => "En ny väg är klar.",
            TilesRevealed => "Kartläggningen lyfte dimman över nya brickor.",
            ExpeditionReturned { Island: PrototypeHome.Nyon } => "Expeditionen kom hem. Den hittade Nyön i öster.",
            ExpeditionReturned => "Expeditionen kom hem. Den hittade en ny ö.",
            FogLifted => FogLiftedOver(1),
            _ => null,
        };

        /// <summary>
        /// The daily report: one line per event, except that all the fog the player lifted is one line where the first
        /// lift was, since a day can lift tens of tiles.
        /// </summary>
        public static IEnumerable<string> Report(IEnumerable<GameEvent> events)
        {
            var lines = new List<string>();
            int lifted = 0;
            int fogLine = -1;
            foreach (var gameEvent in events)
            {
                if (gameEvent is FogLifted)
                {
                    if (lifted++ == 0)
                    {
                        fogLine = lines.Count;
                        lines.Add(string.Empty);
                    }

                    continue;
                }

                if (Describe(gameEvent) is string line)
                {
                    lines.Add(line);
                }
            }

            if (fogLine >= 0)
            {
                lines[fogLine] = FogLiftedOver(lifted);
            }

            return lines;
        }

        private static string FogLiftedOver(int tiles) =>
            tiles == 1 ? "Dimman lättade över en bricka." : $"Dimman lättade över {tiles} brickor.";
    }
}
