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
            return parts.Count == 0 ? "Här kan inget byggas." : "Duger till " + Join(parts) + ".";
        }

        private static string Join(List<string> parts) =>
            parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " och " + parts[parts.Count - 1];

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
            _ => "Det gick inte just nu.",
        };

        /// <summary>
        /// What the island across the sea is called: Nyön on the prototype's map, "den nya ön" on a generated one, whose
        /// islands have no names yet and can lie in any direction.
        /// </summary>
        public static string IslandName(string? island) => island == PrototypeHome.Nyon ? "Nyön" : "den nya ön";

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
