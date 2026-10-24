using System.Collections.Generic;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// The shortcut <em>Road to the storehouse</em> on a producing building's sheet (D-112): lays a road on every tile
    /// between the building and the storehouse or jetty it belongs to that can take one, along the shortest way, for 2
    /// stone and 4 hours of work a tile and no force. Each tile is a project of its own in the queue, nearest the depot
    /// first, as if laid one by one with <see cref="LayRoad"/>.
    /// </summary>
    public sealed class BuildRoad : GameCommand
    {
        public BuildRoad(Hex tile) => Tile = tile;

        /// <summary>Where the building stands.</summary>
        public Hex Tile { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            var building = settlement.BuildingAt(Tile);
            if (building is null || !world.Content.For(building.Kind).Produces || !settlement.TripFrom(Tile).Reaches)
            {
                return Rejection.NoBuildingThere;
            }

            var tiles = settlement.RoadToDepot(Tile);
            if (tiles.Count == 0)
            {
                return Rejection.AlreadyDone;
            }

            var cost = new Dictionary<Resource, int> { [Resource.Stone] = Logistics.RoadStone * tiles.Count };
            if (!settlement.CanAfford(cost))
            {
                return Rejection.InsufficientResources;
            }

            var each = new Dictionary<Resource, int> { [Resource.Stone] = Logistics.RoadStone };
            foreach (var tile in tiles)
            {
                settlement.Queue(ProjectKind.Road, null, tile, each, Logistics.RoadHours);
            }

            return Rejection.None;
        }
    }
}
