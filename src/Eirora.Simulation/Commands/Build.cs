using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;

namespace Eirora.Simulation.Commands
{
    /// <summary>Queues a new building on a site; materials and Build Power are paid at once (economy-v1.md, section 3).</summary>
    public sealed class Build : GameCommand
    {
        public Build(BuildingKind building, string siteId)
        {
            Building = building;
            SiteId = siteId;
        }

        public BuildingKind Building { get; }

        public string SiteId { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            var spec = BuildingSpec.For(Building);
            var site = settlement.CheckSite(Building, SiteId);
            if (site != Rejection.None)
            {
                return site;
            }

            if (!settlement.CanAfford(spec.Cost))
            {
                return Rejection.InsufficientResources;
            }

            if (!world.TrySpend(Force.BuildPower, spec.BuildPower))
            {
                return Rejection.InsufficientForces;
            }

            settlement.Queue(ProjectKind.Build, Building, SiteId, spec.Cost, spec.WorkHours);
            return Rejection.None;
        }
    }
}
