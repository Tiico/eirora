using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Sets how many whole units of each raw material a processing building leaves in its island's store for building,
    /// from 0 to what the store holds (GLOSSARY: Minimilager, LOG-06). The carriers bring it raw materials only above
    /// this and above the island's reservation from the next quarter hour. Free of forces, like all planning (D-23).
    /// </summary>
    public sealed class SetReserve : GameCommand
    {
        public SetReserve(Hex tile, int units)
        {
            Tile = tile;
            Units = units;
        }

        /// <summary>Where the processing building stands.</summary>
        public Hex Tile { get; }

        public int Units { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            var building = settlement.BuildingAt(Tile);
            if (building is null || settlement.Content.For(building.Kind).Recipe is not { Inputs: { Count: > 0 } })
            {
                return Rejection.NoBuildingThere;
            }

            if (Units < 0 || Units > settlement.StorageUnits(building.Island))
            {
                return Rejection.AmountOutOfRange;
            }

            if (building.Reserve == Units)
            {
                return Rejection.AlreadyDone;
            }

            Settlement.SetReserve(building, Units);
            return Rejection.None;
        }
    }
}
