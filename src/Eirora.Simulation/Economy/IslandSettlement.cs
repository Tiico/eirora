namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// An island's settlement (GLOSSARY: Bosättning; D-106): its own villagers, housing and store. The home island's is
    /// there from the start; on another island the first building, its jetty, founds it with housing for
    /// <see cref="SettlerHousing"/> and no villagers, who come by moving over the sea route (economy-v2.md, section 6).
    /// </summary>
    /// <remarks>
    /// The villagers staff only buildings on their own island and eat from its store at dawn; free villagers also build on
    /// the islands that a sea route joins to theirs, but live and work at home (D-113). A settlement from rules 7 or
    /// earlier can have more villagers than housing, those who staffed its buildings then; they stay, and it grows only
    /// once there is room (economy-v2.md, section 12; D-39).
    /// </remarks>
    public sealed class IslandSettlement
    {
        /// <summary>Housing in the storehouse or jetty that founds a settlement off the home island.</summary>
        public const int SettlerHousing = 4;

        private readonly Settlement _economy;
        private readonly int[] _tiers; // The villagers of each population tier.

        internal IslandSettlement(Settlement economy, string island, int tiers)
        {
            _economy = economy;
            Island = island;
            _tiers = new int[tiers];
        }

        /// <summary>The map's id of its island; an island has at most one settlement.</summary>
        public string Island { get; }

        /// <summary>The villagers who live here (GLOSSARY: Bybor), of every population tier.</summary>
        public int Villagers
        {
            get
            {
                int villagers = 0;
                foreach (int count in _tiers)
                {
                    villagers += count;
                }

                return villagers;
            }
        }

        /// <summary>
        /// How many villagers it can house: what it was founded with, its island's cottages and longhouses and the housing
        /// of every other tier.
        /// </summary>
        public int Housing => _economy.HousingOf(this);

        /// <summary>The villagers of a population tier who live here (economy-v2.md, section 2).</summary>
        public int VillagersOf(PopulationTier tier) => (int)tier < _tiers.Length ? _tiers[(int)tier] : 0;

        /// <summary>The housing for a population tier on the island; the crofters' is what it was founded with, cottages and longhouses.</summary>
        public int HousingOf(PopulationTier tier) => _economy.HousingOf(this, tier);

        /// <summary>The crofters' housing that no one lives in, which a farmer without a farmstead of its own lives in too (D-124).</summary>
        public int CroftersRoom => _economy.CroftersRoom(this);

        /// <summary>Villagers not working in a building on the island; they build.</summary>
        public int FreeVillagers => _economy.FreeVillagersOf(this);

        /// <summary>The free villagers of a population tier, such as the craftsmen who build twice as fast (D-116).</summary>
        public int FreeVillagersOf(PopulationTier tier)
        {
            var free = new int[_tiers.Length];
            _economy.FreeVillagersOf(this, free);
            return (int)tier < free.Length ? free[(int)tier] : 0;
        }

        /// <summary>Adds villagers of a tier, or takes them away with a negative <paramref name="count"/>.</summary>
        internal void Add(PopulationTier tier, int count) => _tiers[(int)tier] += count;

        /// <summary>
        /// Whole units of each resource that neither the ships take home nor processing takes from the island's store
        /// (GLOSSARY: Reservering, TASK-046); null when nothing is reserved. Only a command changes it, never a tick.
        /// </summary>
        internal int[]? Reserved { get; set; }

        /// <summary>
        /// The order the ships take the island's goods home in (GLOSSARY: Importprioritet, TASK-046); null for the
        /// resources' fixed order. Never changed in place, so that it can be shown as it is; only a command replaces it.
        /// </summary>
        internal Resource[]? Imports { get; set; }
    }
}
