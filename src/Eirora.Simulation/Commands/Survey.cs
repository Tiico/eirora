using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Spends Exploration to survey the next area and lift its fog; a lookout makes it a quarter cheaper
    /// (economy-v1.md, section 6).
    /// </summary>
    public sealed class Survey : GameCommand
    {
        public override CommandCategory Category => CommandCategory.Gameplay;

        /// <summary>The Exploration the next survey costs, or null when everything is surveyed.</summary>
        public static int? NextCost(World world)
        {
            if (world is null || world.Settlement.SurveysDone >= PrototypeIsland.Surveys.Count)
            {
                return null;
            }

            int cost = PrototypeIsland.Surveys[world.Settlement.SurveysDone].Cost;
            return world.Settlement.HasBuilt(BuildingKind.Lookout) ? cost * 3 / 4 : cost;
        }

        internal override Rejection Execute(World world)
        {
            if (NextCost(world) is not int cost)
            {
                return Rejection.NothingLeftToSurvey;
            }

            if (!world.TrySpend(Force.Exploration, cost))
            {
                return Rejection.InsufficientForces;
            }

            world.Report(new SitesRevealed(world.Now.UtcSeconds, world.Settlement.CompleteSurvey()));
            return Rejection.None;
        }
    }
}
