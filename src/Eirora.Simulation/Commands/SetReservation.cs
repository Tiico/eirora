using Eirora.Simulation.Economy;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Reserves whole units of a resource on an island, from 0 to what its store holds, or lower than it is when a
    /// demolition has left it above that (GLOSSARY: Reservering, LOG-06):
    /// the ships leave that much there and processing does not take it, but building still pays from it, since what a
    /// project costs is paid when it is queued. Free of forces, like all planning (D-23).
    /// </summary>
    public sealed class SetReservation : GameCommand
    {
        /// <exception cref="System.ArgumentException">
        /// The island is not a map's id, which is one word, or the resource is not one; a journal could not keep it.
        /// </exception>
        public SetReservation(string island, Resource resource, int units)
        {
            Island = IslandArguments.Id(island, nameof(island));
            Resource = IslandArguments.Defined(resource, nameof(resource));
            Units = units;
        }

        /// <summary>The map's id of the island.</summary>
        public string Island { get; }

        public Resource Resource { get; }

        public int Units { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            if (!settlement.HasStore(Island))
            {
                return Rejection.NoSuchIsland;
            }

            // A demolition can leave it above what the store holds; it can always be lowered, never raised above that.
            if (Units < 0 || (Units > settlement.StorageUnits(Island) && Units > settlement.ReservationOf(Island, Resource)))
            {
                return Rejection.AmountOutOfRange;
            }

            if (settlement.ReservationOf(Island, Resource) == Units)
            {
                return Rejection.AlreadyDone;
            }

            settlement.SetReservation(Island, Resource, Units);
            return Rejection.None;
        }
    }
}
