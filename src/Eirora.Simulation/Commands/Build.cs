using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Queues a new building on a known tile that meets its requirement (D-79); materials and Build Power are paid at
    /// once (economy-v1.md, section 3).
    /// </summary>
    public sealed class Build : GameCommand
    {
        public Build(BuildingKind building, Hex tile)
        {
            Building = building;
            Tile = tile;
        }

        public BuildingKind Building { get; }

        /// <summary>The building site.</summary>
        public Hex Tile { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            var spec = settlement.Content.For(Building);
            var site = settlement.CheckBuild(Building, Tile);
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

            settlement.Queue(ProjectKind.Build, Building, Tile, spec.Cost, spec.WorkHours);
            return Rejection.None;
        }
    }
}
