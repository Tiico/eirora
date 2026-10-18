using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>A place where one building can stand, with its axial hex coordinates and what grows or lies there.</summary>
    public sealed class Site
    {
        public Site(string id, int q, int r, SiteKind kinds, bool hidden = false, int forestTiles = 0, int richnessPercent = 100, IslandId island = IslandId.Home)
        {
            Id = id;
            Q = q;
            R = r;
            Kinds = kinds;
            Hidden = hidden;
            ForestTiles = forestTiles;
            RichnessPercent = richnessPercent;
            Island = island;
        }

        public string Id { get; }

        /// <summary>The island the site lies on; goods travel between islands only by sea route.</summary>
        public IslandId Island { get; }

        public int Q { get; }

        public int R { get; }

        public SiteKind Kinds { get; }

        /// <summary>Under the fog until found by surveying (MAP-04).</summary>
        public bool Hidden { get; }

        /// <summary>Forest tiles a woodcutter here can fell; each holds 40 timber and grows back.</summary>
        public int ForestTiles { get; }

        /// <summary>How rich the site is: 150 for rich fishing water, 200 for a stone vein.</summary>
        public int RichnessPercent { get; }

        /// <summary>The hex the site lies on.</summary>
        public Hex Hex => new(Q, R);

        /// <summary>The number of hex steps to another site.</summary>
        public int DistanceTo(Site other) => Hex.DistanceTo(other.Hex);
    }
}
