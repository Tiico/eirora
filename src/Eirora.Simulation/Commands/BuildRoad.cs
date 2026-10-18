using System.Collections.Generic;
using Eirora.Simulation.Economy;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Builds a road from a producing building to the nearest store: 2 stone and 4 hours of work per hex, and no
    /// force (economy-v1.md, section 5).
    /// </summary>
    public sealed class BuildRoad : GameCommand
    {
        private const int StonePerHex = 2;
        private const int HoursPerHex = 4;

        public BuildRoad(string siteId) => SiteId = siteId;

        public string SiteId { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            var building = settlement.BuildingAt(SiteId);
            if (building is null || !Upgrade.Produces(building.Kind))
            {
                return Rejection.NoBuildingThere;
            }

            int distance = settlement.DepotDistance(building.Site);
            if (building.HasRoad || distance <= 1 || settlement.IsQueued(SiteId, ProjectKind.Road))
            {
                return Rejection.AlreadyDone;
            }

            var cost = new Dictionary<Resource, int> { [Resource.Stone] = StonePerHex * distance };
            if (!settlement.CanAfford(cost))
            {
                return Rejection.InsufficientResources;
            }

            settlement.Queue(ProjectKind.Road, building.Kind, SiteId, cost, HoursPerHex * distance);
            return Rejection.None;
        }
    }
}
