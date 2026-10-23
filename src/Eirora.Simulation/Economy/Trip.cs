using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// The way a carrier walks from a tile to the storehouse or jetty it belongs to (economy-v2.md, section 7): the
    /// staffed depot on the same island that the carriers reach fastest over land, a step taking 15 minutes and 7.5 onto
    /// a tile with a road (D-110, D-112).
    /// </summary>
    public readonly struct Trip
    {
        internal Trip(long time, Hex depot, int steps)
        {
            Time = time;
            Depot = depot;
            Steps = steps;
        }

        /// <summary>Millionths of an hour one way; 0 when no staffed storehouse or jetty can be reached.</summary>
        public long Time { get; }

        /// <summary>Where the depot stands; meaningless when <see cref="Reaches"/> is false.</summary>
        public Hex Depot { get; }

        /// <summary>Steps over land on the way, a fishing hut's first step ashore counted.</summary>
        public int Steps { get; }

        /// <summary>Whether a staffed storehouse or jetty can be reached.</summary>
        public bool Reaches => Time > 0;

        /// <summary>Millionths of a unit a carrier moves in an hour: a load of 4 there and back (economy-v2.md, section 7).</summary>
        public long PerCarrierHour => Reaches ? Quantity.Units(Logistics.CarrierLoad) * Quantity.PerUnit / (2 * Time) : 0;
    }
}
