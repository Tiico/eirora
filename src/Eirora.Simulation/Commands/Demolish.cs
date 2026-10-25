using Eirora.Simulation.Events;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Demolishes a building, the only way anything in the world becomes less (D-38): the tile is free, its villagers
    /// and carriers build instead, half the materials it cost come back to the island's store as far as there is room,
    /// and no force comes back (D-108, INV-F5). The client shows <see cref="World.Demolition"/> to confirm it first.
    /// </summary>
    public sealed class Demolish : GameCommand
    {
        public Demolish(Hex tile) => Tile = tile;

        /// <summary>Where the building stands.</summary>
        public Hex Tile { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            if (settlement.Demolition(Tile) is not { } demolition)
            {
                return Rejection.NoBuildingThere;
            }

            if (demolition.Rejection != Rejection.None)
            {
                return demolition.Rejection;
            }

            settlement.Demolish(demolition);
            world.Report(new BuildingDemolished(world.Now.UtcSeconds, demolition.Building.Kind, Tile));
            return Rejection.None;
        }
    }
}
