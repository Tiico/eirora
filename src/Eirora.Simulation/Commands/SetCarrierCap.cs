using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Sets the most carriers a storehouse or a jetty may take at dawn, one to four (D-109). Carriers above the new cap
    /// go back to building at once; a higher cap is taken up at the next dawn. Free of forces, like all planning (D-23).
    /// </summary>
    public sealed class SetCarrierCap : GameCommand
    {
        public SetCarrierCap(Hex tile, int cap)
        {
            Tile = tile;
            Cap = cap;
        }

        /// <summary>Where the storehouse or jetty stands.</summary>
        public Hex Tile { get; }

        public int Cap { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            var depot = settlement.BuildingAt(Tile);
            if (depot is null || !Logistics.IsDepot(depot.Kind))
            {
                return Rejection.NoBuildingThere;
            }

            if (Cap < 1 || Cap > Logistics.MostCarriers)
            {
                return Rejection.AmountOutOfRange;
            }

            if (depot.CarrierCap == Cap)
            {
                return Rejection.AlreadyDone;
            }

            Settlement.SetCarrierCap(depot, Cap);
            return Rejection.None;
        }
    }
}
