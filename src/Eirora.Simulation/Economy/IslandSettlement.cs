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

        internal IslandSettlement(Settlement economy, string island)
        {
            _economy = economy;
            Island = island;
        }

        /// <summary>The map's id of its island; an island has at most one settlement.</summary>
        public string Island { get; }

        /// <summary>The villagers who live here (GLOSSARY: Bybor).</summary>
        public int Villagers { get; internal set; }

        /// <summary>How many villagers it can house: what it was founded with and its island's cottages and longhouses.</summary>
        public int Housing => _economy.HousingOf(this);

        /// <summary>Villagers not working in a building on the island; they build.</summary>
        public int FreeVillagers => _economy.FreeVillagersOf(this);
    }
}
