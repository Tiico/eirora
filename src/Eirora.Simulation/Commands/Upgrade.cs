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
            if (building is null || !Produces(building.Kind))
            {
                return Rejection.NoBuildingThere;
            }

            if (building.Level >= BuildingSpec.For(building.Kind).MaxLevel || settlement.IsQueued(Tile, ProjectKind.Upgrade))
            {
                return Rejection.AlreadyDone;
            }

            var spec = BuildingSpec.Upgrade;
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

        internal static bool Produces(BuildingKind kind) => kind is BuildingKind.FishingHut or BuildingKind.Field
            or BuildingKind.Woodcutter or BuildingKind.SheepFarm or BuildingKind.Quarry or BuildingKind.Sawmill or BuildingKind.Weavery;
    }
}
