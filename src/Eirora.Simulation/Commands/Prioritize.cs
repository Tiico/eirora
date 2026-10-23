using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Moves a building to another place in the staffing order, so that it is given villagers before or after others
    /// when they do not suffice for all (CIV-02). Free of forces, like all planning (D-23).
    /// </summary>
    public sealed class Prioritize : GameCommand
    {
        public Prioritize(Hex tile, int place)
        {
            Tile = tile;
            Place = place;
        }

        /// <summary>Where the building stands.</summary>
        public Hex Tile { get; }

        /// <summary>Its new place in <see cref="World.StaffingOrder"/>, counted from 0.</summary>
        public int Place { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            var building = settlement.BuildingAt(Tile);
            if (building is null || settlement.Content.For(building.Kind).Staff == 0)
            {
                return Rejection.NoBuildingThere;
            }

            var order = settlement.StaffingOrder;
            if (Place < 0 || Place >= order.Count)
            {
                return Rejection.NoSuchPlace;
            }

            if (order[Place] == building)
            {
                return Rejection.AlreadyDone;
            }

            settlement.MoveInStaffingOrder(building, Place);
            return Rejection.None;
        }
    }
}
