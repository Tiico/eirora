using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Queues a new building built by hand (GLOSSARY: Handkraft; D-65): its materials are paid at once, but no Build
    /// Power, and it stands at the dawn its fixed number of game days later, without the free villagers' work. Only a
    /// building the catalog marks <see cref="BuildingSpec.HandBuild"/> can be built so.
    /// </summary>
    public sealed class BuildByHand : GameCommand
    {
        public BuildByHand(BuildingKind building, Hex tile)
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
            if (!spec.HandBuild)
            {
                return Rejection.NotByHand;
            }

            var site = settlement.CheckBuild(Building, Tile);
            if (site != Rejection.None)
            {
                return site;
            }

            if (!settlement.CanAfford(spec.Cost, Tile))
            {
                return Rejection.InsufficientResources;
            }

            settlement.QueueByHand(Building, Tile);
            return Rejection.None;
        }
    }
}
