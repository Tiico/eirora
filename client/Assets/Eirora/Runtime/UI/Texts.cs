using System.Collections.Generic;
using System.Linq;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;

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

        public static string Describe(Site site)
        {
            var kinds = site.Kinds;
            string place =
                (kinds & SiteKind.FishingWater) != 0 ? "Fiskevatten" :
                (kinds & SiteKind.HarborSpot) != 0 ? "Hamnläge" :
                (kinds & SiteKind.Farmland) != 0 ? "Odlingsmark" :
                (kinds & SiteKind.Forest) != 0 ? "Skog" :
                (kinds & SiteKind.Mountain) != 0 ? "Berg" :
                (kinds & SiteKind.Pasture) != 0 ? "Bete" : "Mark";
            return site.RichnessPercent > 100 ? $"{place}, rikt" : place;
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
            Accounts.AccountOutcome.NotThisAccount => "Logga in med samma konto som du vill radera.",
            Accounts.AccountOutcome.ConfirmationUnavailable => "Radera kontot på webbsidan i stället.",
            Accounts.AccountOutcome.Failed => "Det gick inte just nu. Försök igen om en stund.",
            _ => null,
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
            Rejection.NoBuildingThere => "Det behövs en byggnad för det här först.",
            Rejection.GameDayOutOfRange => "Det går att fylla i de senaste sju dygnen.",
            Rejection.AmountOutOfRange => "Värdet verkar inte stämma.",
            _ => "Det gick inte just nu.",
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
            SitesRevealed => "Dimman lättade över nya platser.",
            ExpeditionReturned => "Expeditionen kom hem. Den hittade Nyön i öster.",
            _ => null,
        };

        public static IEnumerable<string> Report(IEnumerable<GameEvent> events) =>
            events.Select(Describe).Where(line => line is not null).Select(line => line!);
    }
}
