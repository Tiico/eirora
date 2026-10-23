using System.Globalization;
using System.Net;
using System.Text;
using Eirora.Simulation.Map;
using Eirora.WorldGeneration;
using Eirora.WorldGeneration.Validation;

/// <summary>
/// The seed tool (TASK-034, MAP-09): a self-contained HTML page with maps drawn from above and the validator's report
/// for each. For a range of seeds it lists, per rule, the maps that lie nearest a limit, so the generator can be
/// improved where it is weakest. It needs nothing but a browser, so it can be attached to a pull request.
/// </summary>
internal static class SeedPage
{
    private const int Nearest = 5;
    private const double Size = 7;
    private const string InnerSeaColour = "#3D93B6";
    private static readonly double s_sqrt3 = Math.Sqrt(3);

    public static string Write(IWorldGenerator generator, ulong from, int count)
    {
        var maps = new List<(ulong Seed, int Attempts, MapAnalysis Analysis)>(count);
        for (int i = 0; i < count; i++)
        {
            ulong seed = unchecked(from + (ulong)i);
            int attempts = 1;
            var map = generator is ICountsAttempts counting ? counting.Generate(seed, out attempts) : generator.Generate(seed);
            maps.Add((seed, attempts, MapValidator.Analyse(map)));
        }

        var shown = new SortedSet<ulong>();
        var html = new StringBuilder();
        html.Append(Head($"{generator.Id} {generator.Version}"));
        html.Append(Invariant($"<h1>{Encode(generator.Id)} {generator.Version}</h1>\n<p class=\"lead\">{(count == 1 ? $"Seed {from}" : $"Seeds {from}–{from + (ulong)count - 1}")}: {maps.Count(m => m.Analysis.Report.Passed)} av {count} {(count == 1 ? "karta" : "kartor")} godkänns av validatorn.</p>\n"));

        if (count > 1)
        {
            html.Append("<h2>Per regel</h2>\n<table><thead><tr><th>Regel</th><th>Gräns</th><th>Godkända</th><th>Min</th><th>Median</th><th>Max</th><th>Närmast gränsen</th></tr></thead><tbody>\n");
            var rules = maps[0].Analysis.Report.Rules;
            for (int r = 0; r < rules.Count; r++)
            {
                var values = maps.Select(m => m.Analysis.Report.Rules[r].Value).OrderBy(v => v).ToList();
                var nearest = maps
                    .Where(m => m.Analysis.Report.Rules[r].Margin is not null)
                    .OrderBy(m => m.Analysis.Report.Rules[r].Margin).ThenBy(m => m.Seed)
                    .Take(Nearest).ToList();
                var failed = maps.Where(m => !m.Analysis.Report.Rules[r].Passed).Take(Nearest).ToList();
                foreach (var m in nearest.Concat(failed))
                {
                    shown.Add(m.Seed);
                }

                string links = string.Join(" ", failed.Select(m => Link(m.Seed, "fail")).Concat(nearest.Select(m => Link(m.Seed, $"+{m.Analysis.Report.Rules[r].Margin}"))));
                html.Append(Invariant($"<tr><td><code>{rules[r].Rule}</code></td><td>{Encode(rules[r].Limit)}</td><td>{maps.Count(m => m.Analysis.Report.Rules[r].Passed)}</td><td>{values[0]}</td><td>{values[(values.Count - 1) / 2]}</td><td>{values[^1]}</td><td>{links}</td></tr>\n"));
            }

            var attempts = maps.Select(m => m.Attempts).OrderBy(a => a).ToList();
            html.Append(Invariant($"<tr><td><code>attempts</code></td><td>–</td><td>{count}</td><td>{attempts[0]}</td><td>{attempts[(count - 1) / 2]}</td><td>{attempts[^1]}</td><td>{string.Join(" ", maps.OrderByDescending(m => m.Attempts).ThenBy(m => m.Seed).Take(Nearest).Select(m => Link(m.Seed, m.Attempts.ToString(CultureInfo.InvariantCulture))))}</td></tr>\n"));
            html.Append("</tbody></table>\n<p class=\"note\">Närmast gränsen: hur långt (+n) en godkänd karta ligger från regelns gräns. De kartorna visas nedan, så att generatorn kan förbättras där den är svagast.</p>\n");
        }
        else
        {
            shown.Add(from);
        }

        html.Append(Legend());
        html.Append("<div class=\"cards\">\n");
        foreach (var (seed, attempts, analysis) in maps.Where(m => shown.Contains(m.Seed)))
        {
            html.Append(Card(seed, attempts, analysis));
        }

        html.Append("</div>\n</main>\n</body>\n</html>\n");
        return html.ToString();
    }

    private static string Card(ulong seed, int attempts, MapAnalysis analysis)
    {
        var report = analysis.Report;
        var card = new StringBuilder();
        card.Append(Invariant($"<section class=\"card\" id=\"seed-{seed}\">\n<h3>Seed {seed} <span class=\"{(report.Passed ? "pass" : "fail")}\">{(report.Passed ? "godkänd" : "underkänd")}</span></h3>\n<p class=\"note\">{attempts} försök</p>\n"));
        card.Append(Map(analysis));
        card.Append("<table class=\"report\"><tbody>\n");
        foreach (var rule in report.Rules)
        {
            card.Append(Invariant($"<tr class=\"{(rule.Passed ? "pass" : "fail")}\"><td><code>{rule.Rule}</code></td><td>{rule.Value}</td><td>{Encode(rule.Limit)}</td><td>{Encode(rule.Detail)}</td></tr>\n"));
        }

        card.Append("</tbody></table>\n</section>\n");
        return card.ToString();
    }

    /// <summary>The map from above: terrain, the inner sea, fairways to the expedition islands, harbours, crest and island names.</summary>
    private static string Map(MapAnalysis analysis)
    {
        var map = analysis.Map;

        // Cropped to the land and shallows with a margin of open sea; the deep sea is one background, which keeps the
        // page light enough for many maps.
        var shown = Enumerable.Range(0, map.Disc.Count).Select(i => map.Disc[i]).Where(h => map.TerrainAt(h) != Terrain.DeepSea).ToList();
        double margin = Size * 3;
        double left = shown.Min(X) - margin, right = shown.Max(X) + margin, top = shown.Min(Y) - margin, bottom = shown.Max(Y) + margin;
        double side = Math.Max(right - left, bottom - top);
        left -= (side - (right - left)) / 2;
        top -= (side - (bottom - top)) / 2;
        var svg = new StringBuilder();
        svg.Append(Invariant($"<svg class=\"map\" viewBox=\"{left:0.#} {top:0.#} {side:0.#} {side:0.#}\" role=\"img\" aria-label=\"Kartan ovanifrån\">\n"));
        svg.Append(Invariant($"<rect x=\"{left:0.#}\" y=\"{top:0.#}\" width=\"{side:0.#}\" height=\"{side:0.#}\" fill=\"#2F7FA3\"/>\n"));
        // The inner sea's deep water is a lighter blue; its shallows keep their colour.
        foreach (var hex in analysis.InnerSea.Where(h => map.TerrainAt(h) == Terrain.DeepSea))
        {
            svg.Append(Invariant($"<polygon points=\"{Corners(hex, Size)}\" fill=\"{InnerSeaColour}\"/>"));
        }

        foreach (var hex in shown)
        {
            svg.Append(Invariant($"<polygon points=\"{Corners(hex, Size)}\" fill=\"{Colour(map, hex)}\"/>"));
        }

        svg.Append('\n');
        foreach (var island in new[] { analysis.FirstIsland, analysis.SecondIsland })
        {
            if (island is null)
            {
                continue;
            }

            var harbours = analysis.Features.LandOf(island).Where(analysis.Features.IsHarborSpot);
            var route = analysis.Features.SeaRoute(analysis.StartHarbours, harbours);
            if (route.Count > 1)
            {
                svg.Append(Invariant($"<polyline class=\"route\" points=\"{string.Join(" ", route.Select(h => Point(h)))}\"/>\n"));
            }
        }

        // The start island's harbours, where the fairways begin; the expedition islands' harbours are the fairways' ends.
        var ends = new[] { analysis.FirstIsland, analysis.SecondIsland }.Where(i => i is not null)
            .Select(i => analysis.Features.SeaRoute(analysis.StartHarbours, analysis.Features.LandOf(i!).Where(analysis.Features.IsHarborSpot)))
            .Where(r => r.Count > 0).Select(r => r[^1]);
        foreach (var harbour in analysis.StartHarbours.Concat(ends))
        {
            svg.Append(Invariant($"<circle class=\"harbour\" cx=\"{X(harbour):0.#}\" cy=\"{Y(harbour):0.#}\" r=\"1.8\"/>"));
        }

        if (analysis.Crest is Hex crest)
        {
            svg.Append(Invariant($"\n<circle class=\"crest\" cx=\"{X(crest):0.#}\" cy=\"{Y(crest):0.#}\" r=\"{Size * 0.8:0.#}\"/>"));
        }

        foreach (var island in map.Islands)
        {
            string name = island.Id == analysis.StartIsland ? "start" : island.Id == analysis.FirstIsland ? "1:a expeditionen" : island.Id == analysis.SecondIsland ? "2:a expeditionen" : string.Empty;
            if (name.Length > 0)
            {
                svg.Append(Invariant($"\n<text class=\"label\" x=\"{X(island.Anchor):0.#}\" y=\"{Y(island.Anchor) - (Size * 2.2):0.#}\">{Encode(name)}</text>"));
            }
        }

        svg.Append("\n</svg>\n");
        return svg.ToString();
    }

    private static string Legend() =>
        "<ul class=\"legend\">"
        + Swatch("#2F7FA3", "djupt hav") + Swatch("#6CC4C0", "grunt hav") + Swatch("#F0D9A8", "strand") + Swatch("#9BCB7A", "gräsmark")
        + Swatch("#3F7F55", "skog") + Swatch("#D9C878", "åker") + Swatch("#B5D98E", "betesmark") + Swatch("#A3A0A8", "berg")
        + "<li><span class=\"key\" style=\"background:#3D93B6\"></span>innerhav</li><li><span class=\"key route-key\"></span>farled till expeditionernas öar</li>"
        + "<li><span class=\"key harbour-key\"></span>hamnläge</li><li><span class=\"key crest-key\"></span>topp</li><li>Mörkare grön eller ljusare turkos: rik mark eller rikt fiskevatten</li></ul>\n";

    private static string Swatch(string colour, string name) => $"<li><span class=\"key\" style=\"background:{colour}\"></span>{name}</li>";

    private static string Link(ulong seed, string text) => Invariant($"<a href=\"#seed-{seed}\">{seed}<small>{Encode(text)}</small></a>");

    private static string Colour(WorldMap map, Hex hex) => map.TerrainAt(hex) switch
    {
        Terrain.DeepSea => "#2F7FA3",
        Terrain.ShallowSea => map.RichnessAt(hex) > 100 ? "#8FD6CF" : "#6CC4C0",
        Terrain.Beach => "#F0D9A8",
        Terrain.Forest => map.RichnessAt(hex) > 100 ? "#2F6B45" : "#3F7F55",
        Terrain.Farmland => map.RichnessAt(hex) > 100 ? "#CDB85E" : "#D9C878",
        Terrain.Pasture => map.RichnessAt(hex) > 100 ? "#9CCB6F" : "#B5D98E",
        Terrain.Mountain => map.RichnessAt(hex) > 100 ? "#8E8B93" : "#A3A0A8",
        _ => map.HeightAt(hex) >= 4 ? "#8BBE6B" : "#9BCB7A",
    };

    private static double X(Hex hex) => Size * s_sqrt3 * (hex.Q + (hex.R / 2.0));

    private static double Y(Hex hex) => Size * 1.5 * hex.R;

    private static string Point(Hex hex) => Invariant($"{X(hex):0.#},{Y(hex):0.#}");

    private static string Corners(Hex hex, double size)
    {
        var points = new StringBuilder();
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 180 * ((60 * i) - 30);
            points.Append(Invariant($"{X(hex) + (size * Math.Cos(angle)):0.##},{Y(hex) + (size * Math.Sin(angle)):0.##} "));
        }

        return points.ToString().TrimEnd();
    }

    private static string Head(string title) => $$"""
        <!doctype html>
        <html lang="sv">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{Encode(title)}}</title>
        <style>
        :root { --bg: #F6EFE4; --panel: #FBF7EF; --text: #2B2A33; --muted: #686575; --line: #E3DACB; --pass: #3F7F55; --fail: #B4473A; }
        @media (prefers-color-scheme: dark) { :root { --bg: #16161C; --panel: #202029; --text: #ECE8F2; --muted: #A9A4B8; --line: #34343F; --pass: #7FC29A; --fail: #E68A7E; } }
        * { box-sizing: border-box; }
        body { margin: 0; background: var(--bg); color: var(--text); font: 15px/1.5 system-ui, sans-serif; }
        main { max-width: 1280px; margin: 0 auto; padding: 24px 16px 48px; }
        h1 { font-size: 26px; margin: 0 0 4px; } h2 { font-size: 19px; margin: 28px 0 8px; } h3 { font-size: 17px; margin: 0; }
        .lead, .note { color: var(--muted); margin: 4px 0 12px; }
        table { border-collapse: collapse; width: 100%; background: var(--panel); border-radius: 8px; overflow: hidden; }
        th, td { text-align: left; padding: 6px 10px; border-bottom: 1px solid var(--line); vertical-align: top; }
        th { font-weight: 600; color: var(--muted); }
        td a { display: inline-block; margin: 0 8px 2px 0; color: inherit; } td a small { color: var(--muted); margin-left: 2px; }
        .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 380px), 1fr)); gap: 16px; margin-top: 16px; }
        .card { background: var(--panel); border-radius: 12px; padding: 14px; border: 1px solid var(--line); }
        .card h3 span { font-size: 13px; font-weight: 600; margin-left: 6px; }
        .pass { color: var(--pass); } .fail { color: var(--fail); }
        .report { font-size: 13px; margin-top: 8px; table-layout: fixed; } .report code { font-size: 12px; }
        .report td { overflow-wrap: anywhere; } .report td:nth-child(1) { width: 46%; } .report td:nth-child(2) { width: 10%; } .report td:nth-child(3) { width: 14%; } .report tr.fail td { color: var(--fail); font-weight: 600; } .report td { padding: 3px 6px; }
        .map { width: 100%; height: auto; display: block; border-radius: 8px; }
        .route { fill: none; stroke: #F3E9DA; stroke-width: 1.6; stroke-dasharray: 4 3; }
        .harbour { fill: #B4473A; stroke: #FBF7EF; stroke-width: 0.6; }
        .crest { fill: none; stroke: #FFD27A; stroke-width: 2; }
        .label { font-size: 8px; fill: #FBF7EF; text-anchor: middle; paint-order: stroke; stroke: rgba(30, 40, 60, 0.75); stroke-width: 2.4px; }
        .legend { display: flex; flex-wrap: wrap; gap: 6px 16px; list-style: none; padding: 0; margin: 20px 0 0; color: var(--muted); font-size: 13px; }
        .legend li { display: flex; align-items: center; gap: 6px; }
        .key { width: 14px; height: 14px; border-radius: 3px; display: inline-block; }
        .route-key { border-top: 2px dashed var(--muted); height: 0; }
        .harbour-key { background: #B4473A; border-radius: 50%; width: 10px; height: 10px; } .crest-key { border: 2px solid #FFD27A; border-radius: 50%; }
        code { font-size: 13px; }
        </style>
        </head>
        <body>
        <main>

        """;

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
