using System.Globalization;
using System.Text;
using Eirora.Simulation.Map;
using Eirora.WorldGeneration;

/// <summary>
/// Draws maps from above as an SVG contact sheet in the art direction's palette (D-61), so the product owner can choose
/// among seeds before a generator version is locked (TASK-031). Floats are fine here: this is only a picture.
/// </summary>
internal static class ContactSheet
{
    private const double Size = 6; // A hex's corner radius in pixels.
    private const int Columns = 4;
    private static readonly double s_sqrt3 = Math.Sqrt(3);

    public static string Draw(IWorldGenerator generator, IReadOnlyList<ulong> seeds)
    {
        double cell = 2 * s_sqrt3 * Size * 24.6;
        double labelled = cell + 28;
        int rows = (seeds.Count + Columns - 1) / Columns;
        var svg = new StringBuilder();
        svg.Append(Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Columns * cell:0}\" height=\"{(rows * labelled) + 40:0}\" font-family=\"sans-serif\">\n"));
        svg.Append("<rect width=\"100%\" height=\"100%\" fill=\"#F6EFE4\"/>\n");
        svg.Append(Invariant($"<text x=\"16\" y=\"28\" font-size=\"18\" fill=\"#2B2A33\">{generator.Id} {generator.Version}</text>\n"));
        for (int i = 0; i < seeds.Count; i++)
        {
            int attempts = 1;
            var map = generator is ICountsAttempts counting ? counting.Generate(seeds[i], out attempts) : generator.Generate(seeds[i]);
            double left = (i % Columns) * cell;
            double top = 40 + ((i / Columns) * labelled);
            svg.Append(Invariant($"<g transform=\"translate({left + (cell / 2):0.#},{top + (cell / 2):0.#})\">\n"));
            for (int index = 0; index < map.Disc.Count; index++)
            {
                var hex = map.Disc[index];
                svg.Append(Invariant($"<polygon points=\"{Corners(hex)}\" fill=\"{Colour(map, hex)}\"/>"));
            }

            svg.Append("\n</g>\n");
            svg.Append(Invariant($"<text x=\"{left + 12:0}\" y=\"{top + cell + 18:0}\" font-size=\"15\" fill=\"#2B2A33\">seed {seeds[i]} · {attempts} {(attempts == 1 ? "attempt" : "attempts")}</text>\n"));
        }

        svg.Append("</svg>\n");
        return svg.ToString();
    }

    private static string Corners(Hex hex)
    {
        double x = Size * s_sqrt3 * (hex.Q + (hex.R / 2.0));
        double y = Size * 1.5 * hex.R;
        var points = new StringBuilder();
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 180 * ((60 * i) - 30);
            points.Append(Invariant($"{x + (Size * Math.Cos(angle)):0.##},{y + (Size * Math.Sin(angle)):0.##} "));
        }

        return points.ToString().TrimEnd();
    }

    /// <summary>The palette of art-direction.md; rich ground a little warmer, the crest snow-capped.</summary>
    private static string Colour(WorldMap map, Hex hex) => map.TerrainAt(hex) switch
    {
        Terrain.DeepSea => "#2F7FA3",
        Terrain.ShallowSea => map.RichnessAt(hex) > 100 ? "#8FD6CF" : "#6CC4C0",
        Terrain.Beach => "#F0D9A8",
        Terrain.Forest => map.RichnessAt(hex) > 100 ? "#2F6B45" : "#3F7F55",
        Terrain.Farmland => "#D9C878",
        Terrain.Pasture => "#B5D98E",
        Terrain.Mountain => map.HeightAt(hex) >= 6 ? "#F5F3F8" : "#A3A0A8",
        _ => map.HeightAt(hex) >= 4 ? "#8BBE6B" : "#9BCB7A",
    };

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
