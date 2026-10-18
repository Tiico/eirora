namespace Eirora.Simulation.Map
{
    /// <summary>What a tile is (ADR-0010). Sea comes first, so every value from <see cref="Beach"/> on is land.</summary>
    public enum Terrain
    {
        DeepSea,
        ShallowSea,
        Beach,
        Grassland,
        Forest,
        Farmland,
        Pasture,
        Mountain,
    }

    public static class Terrains
    {
        public static bool IsLand(this Terrain terrain) => terrain >= Terrain.Beach;

        /// <summary>The one-letter code a tile has in the map's text.</summary>
        internal static char Code(this Terrain terrain) => terrain switch
        {
            Terrain.DeepSea => 'D',
            Terrain.ShallowSea => 'S',
            Terrain.Beach => 'B',
            Terrain.Grassland => 'G',
            Terrain.Forest => 'F',
            Terrain.Farmland => 'A',
            Terrain.Pasture => 'P',
            _ => 'M',
        };

        internal static Terrain? FromCode(char code) => code switch
        {
            'D' => Terrain.DeepSea,
            'S' => Terrain.ShallowSea,
            'B' => Terrain.Beach,
            'G' => Terrain.Grassland,
            'F' => Terrain.Forest,
            'A' => Terrain.Farmland,
            'P' => Terrain.Pasture,
            'M' => Terrain.Mountain,
            _ => null,
        };
    }
}
