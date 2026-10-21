using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>Spends Dawn Power so one building produces 50 % more for the rest of the game day.</summary>
    public sealed class Workday : GameCommand
    {
        public const int Cost = 30;

        public Workday(Hex tile) => Tile = tile;

        /// <summary>Where the building stands.</summary>
        public Hex Tile { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var building = world.Settlement.BuildingAt(Tile);
            if (building is null || !building.Staffed || !Upgrade.Produces(building.Kind))
            {
                return Rejection.NoBuildingThere;
            }

            if (building.WorkdayToday)
            {
                return Rejection.AlreadyDone;
            }

            if (!world.TrySpend(Force.DawnPower, Cost))
            {
                return Rejection.InsufficientForces;
            }

            building.WorkdayToday = true;
            return Rejection.None;
        }
    }
}
