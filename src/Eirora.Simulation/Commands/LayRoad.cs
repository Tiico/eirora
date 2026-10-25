using System.Collections.Generic;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Lays a road on one tile (D-112): 2 stone and 4 hours of work, and no force. Carriers walk onto it in half the
    /// time, so it helps every carrier whose way crosses it (economy-v2.md, section 7).
    /// </summary>
    public sealed class LayRoad : GameCommand
    {
        public LayRoad(Hex tile) => Tile = tile;

        /// <summary>The tile the road lies on: known land without a building.</summary>
        public Hex Tile { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            var rejection = settlement.CheckRoad(Tile);
            if (rejection != Rejection.None)
            {
                return rejection;
            }

            var cost = new Dictionary<Resource, int> { [Resource.Stone] = Logistics.RoadStone };
            if (!settlement.CanAfford(cost, Tile))
            {
                return Rejection.InsufficientResources;
            }

            settlement.Queue(ProjectKind.Road, null, Tile, cost, Logistics.RoadHours);
            return Rejection.None;
        }
    }
}
