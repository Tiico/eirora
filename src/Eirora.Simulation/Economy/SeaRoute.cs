namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// A sea route (GLOSSARY: Sjöväg) from an island with a jetty to the home island, open while the home island has a
    /// jetty too; ships carry goods both ways, <see cref="UnitsPerGameDay"/> a game day shared by all goods
    /// (economy-v1.md, section 5), and each of its knarrs more (economy-v2.md, section 4).
    /// </summary>
    public sealed class SeaRoute
    {
        /// <summary>What the ships carry on one route in a game day without knarrs, all goods together.</summary>
        public const int UnitsPerGameDay = 40;

        internal SeaRoute(string from, string to, int knarrs = 0, int perGameDay = UnitsPerGameDay)
        {
            From = from;
            To = to;
            Knarrs = knarrs;
            PerGameDay = perGameDay;
        }

        /// <summary>The map's id of the island the goods are shipped from.</summary>
        public string From { get; }

        /// <summary>The map's id of the home island, where the goods are shipped to.</summary>
        public string To { get; }

        /// <summary>The knarrs that carry on it, its island's and the home island's, at most what a route has room for.</summary>
        public int Knarrs { get; }

        /// <summary>What the ships carry on it in a game day with its knarrs, both ways and all goods together.</summary>
        public int PerGameDay { get; }
    }
}
