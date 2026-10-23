namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// A sea route (GLOSSARY: Sjöväg) from an island with a jetty to the home island, open while the home island has a
    /// jetty too; ships carry the island's goods home, <see cref="UnitsPerGameDay"/> a game day shared by all goods
    /// (economy-v1.md, section 5).
    /// </summary>
    public sealed class SeaRoute
    {
        /// <summary>What the ships carry on one route in a game day, all goods together.</summary>
        public const int UnitsPerGameDay = 40;

        internal SeaRoute(string from, string to)
        {
            From = from;
            To = to;
        }

        /// <summary>The map's id of the island the goods are shipped from.</summary>
        public string From { get; }

        /// <summary>The map's id of the home island, where the goods are shipped to.</summary>
        public string To { get; }
    }
}
