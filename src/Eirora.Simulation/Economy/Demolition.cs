using System;
using System.Collections.Generic;
using Eirora.Simulation.Commands;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// What demolishing a building would do, worked out before anything happens, so that the confirmation shows exactly
    /// what comes back and what does not (D-38, D-108; economy-v1.md, section 3.2): half the materials it cost, rounded
    /// down by resource, go to the island's store as far as there is room, and no force comes back. The goods waiting at
    /// the building go to the store first; it is refused when they or the store would not fit in the capacity that is
    /// left (INV-R1, INV-R2).
    /// </summary>
    /// <remarks>Amounts are whole units, by resource; a resource that is not involved is 0.</remarks>
    public sealed class Demolition
    {
        private readonly long[] _refund;
        private readonly long[] _returned;
        private readonly long[] _emptied;
        private readonly long[] _tooMuch;

        internal Demolition(Building building, Rejection rejection, long[] refund, long[] returned, long[] emptied, long[] tooMuch, long unharvested, bool cancelsUpgrade)
        {
            Building = building;
            Rejection = rejection;
            _refund = refund;
            _returned = returned;
            _emptied = emptied;
            _tooMuch = tooMuch;
            Unharvested = unharvested;
            CancelsUpgrade = cancelsUpgrade;
        }

        public Building Building { get; }

        /// <summary>
        /// None when it can be demolished now; otherwise why not: <see cref="Rejection.NoRoomInStore"/> when the island's
        /// goods would not fit in what is left of its store, <see cref="Rejection.LastStorehouse"/> for the home island's
        /// last storehouse. The rest lock is the command's (INV-T3).
        /// </summary>
        public Rejection Rejection { get; }

        /// <summary>Whether everything the demolition gives back comes back: the store has room for it all.</summary>
        public bool ReturnsAll
        {
            get
            {
                for (int i = 0; i < _refund.Length; i++)
                {
                    if (_returned[i] < _refund[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// The upgrade in the building queue that goes with the building; its materials count to what it cost, and its
        /// work so far is lost.
        /// </summary>
        public bool CancelsUpgrade { get; }

        /// <summary>
        /// Whole units of grain or wool that have grown on a field or a sheep farm and not been harvested; they go with
        /// it, since they are not goods until harvested.
        /// </summary>
        public long Unharvested { get; }

        /// <summary>
        /// The other buildings that get villagers once it is gone: its villagers and carriers are given out again in the
        /// staffing order, so one passed over for want of them may be staffed (economy-v1.md, section 4.1).
        /// </summary>
        public IReadOnlyList<Building> Staffs { get; internal set; } = Array.Empty<Building>();

        /// <summary>
        /// The other buildings that lose their villagers once it is gone, when one before them in the staffing order takes
        /// them; a storehouse or a jetty loses its carriers with its first one.
        /// </summary>
        public IReadOnlyList<Building> Unstaffs { get; internal set; } = Array.Empty<Building>();

        /// <summary>How many more of its settlement's villagers build once it is gone, after the staffing has been given out again.</summary>
        public int MoreBuilders { get; internal set; }

        /// <summary>The islands, by the map's id, whose sea route home closes with it: a jetty on the island or the home island's last one.</summary>
        public IReadOnlyList<string> ClosesSeaRoutes { get; internal set; } = Array.Empty<string>();

        /// <summary>Whether it is the home island's last jetty, from which every expedition sets out.</summary>
        public bool EndsExpeditions { get; internal set; }

        /// <summary>Half of what it cost of a resource, its upgrade included, rounded down (D-108).</summary>
        public long Refund(Resource resource) => _refund[(int)resource];

        /// <summary>What of the refund comes back to the island's store, as far as there is room.</summary>
        public long Returned(Resource resource) => _returned[(int)resource];

        /// <summary>What of the refund does not come back for want of room in the store.</summary>
        public long Lost(Resource resource) => _refund[(int)resource] - _returned[(int)resource];

        /// <summary>Whole units of the goods waiting at the building, in its outbox or brought to it, that go to the store.</summary>
        public long Emptied(Resource resource) => _emptied[(int)resource];

        /// <summary>
        /// How much of a resource must be used or shipped first, rounded up, for the island's goods to fit in the store
        /// that is left; 0 when it fits.
        /// </summary>
        public long TooMuch(Resource resource) => _tooMuch[(int)resource];
    }
}
