using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>Upgrades a producing building to level 2 for 50 % more production.</summary>
    public sealed class Upgrade : GameCommand
    {
        public Upgrade(Hex tile) => Tile = tile;

        /// <summary>Where the building stands.</summary>
        public Hex Tile { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            var building = settlement.BuildingAt(Tile);
            if (building is null || !settlement.Content.For(building.Kind).Produces)
            {
                return Rejection.NoBuildingThere;
            }

            if (building.Level >= settlement.Content.For(building.Kind).MaxLevel || settlement.IsQueued(Tile, ProjectKind.Upgrade))
            {
                return Rejection.AlreadyDone;
            }

            var spec = settlement.Content.Upgrade;
            if (!settlement.CanAfford(spec.Cost))
            {
                return Rejection.InsufficientResources;
            }

            if (!world.TrySpend(Force.BuildPower, spec.BuildPower))
            {
                return Rejection.InsufficientForces;
            }

            settlement.Queue(ProjectKind.Upgrade, building.Kind, Tile, spec.Cost, spec.WorkHours);
            return Rejection.None;
        }
    }
}
