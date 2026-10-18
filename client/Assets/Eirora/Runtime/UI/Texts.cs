using System.Collections.Generic;
using System.Linq;
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
