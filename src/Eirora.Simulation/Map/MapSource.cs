namespace Eirora.Simulation.Map
{
    /// <summary>Where a map comes from (GLOSSARY: Källtyp, ADR-0010); every source gives the same kind of map.</summary>
    public enum MapSource
    {
        /// <summary>Generated from a seed (D-04, MAP-01).</summary>
        Procedural,

        /// <summary>Drawn by hand, like the prototype's Hemön and Nyön.</summary>
        Handmade,

        /// <summary>Derived from real geodata in V2 (D-05, MAP-10).</summary>
        Geographic,
    }

    /// <summary>An island of a map: a stable id and an anchor tile; the island is the land connected to the anchor.</summary>
    public sealed class MapIsland
    {
        public MapIsland(string id, Hex anchor)
        {
            Id = id;
            Anchor = anchor;
        }

        public string Id { get; }

        public Hex Anchor { get; }
    }

    /// <summary>
    /// Where a geographic map's data came from: a dataset, its version, its licence and the area it covers, in whole
    /// microdegrees so that no float enters the map (INV-D3).
    /// </summary>
    public sealed class MapProvenance
    {
        public MapProvenance(string dataset, string version, string licence, long west, long south, long east, long north)
        {
            Dataset = dataset;
            Version = version;
            Licence = licence;
            West = west;
            South = south;
            East = east;
            North = north;
        }

        public string Dataset { get; }

        public string Version { get; }

        public string Licence { get; }

        public long West { get; }

        public long South { get; }

        public long East { get; }

        public long North { get; }
    }
}
