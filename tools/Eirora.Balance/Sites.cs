using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Balance;

/// <summary>
/// What a bot reads off the map and the world before it decides, as a player reads the build view: which known tiles
/// suit a building, how far they lie from a store, and what a building there would yield (economy-v1.md, sections 3
/// and 5). Only the world's public face is read, so the bots play the game the client plays.
/// </summary>
/// <remarks>
/// Yields are whole millionths of a weighted unit a game day (<see cref="Quantity.PerUnit"/>), so that decisions and
/// reports are the same on every machine. Ties are broken in map order. What a building makes, from what and how fast
/// is read from the world's content catalog; how distance and level scale it is not readable from the world, and
/// <c>ProductionRateTests</c> holds the bots' reading of it to what the simulation produces.
/// </remarks>
internal sealed class Sites
{
    /// <summary>The bots' waking hours a game day, outside the rest they keep.</summary>
    public static readonly int ActiveHours = 24 - (BalanceRun.Rest.DurationMinutes / 60);

    /// <summary>
    /// Weights of the development value (economy_v1.py and economy_v2.py, WEIGHT), indexed by <see cref="Resource"/>:
    /// game, hide, honey, mead and ale as the second model weighs them.
    /// </summary>
    public static readonly int[] Weights = [1, 1, 1, 3, 2, 2, 6, 1, 6, 2, 8, 5];

    private readonly World _world;
    private readonly WorldMap _map;

    public Sites(World world)
    {
        _world = world;
        _map = world.Map;
        Home = _map.IslandAt(_map.Start) ?? throw new ArgumentException("A map starts on land.", nameof(world));
        ExpeditionIsland = world.ExpeditionFinds is Hex finds ? _map.IslandAt(finds) : null;
    }

    public WorldMap Map => _map;

    public string Home { get; }

    /// <summary>The island the first expedition finds, or null when the map has none.</summary>
    public string? ExpeditionIsland { get; }

    /// <summary>Whether the expedition's island has been found.</summary>
    public bool ExpeditionIslandKnown => _world.ExpeditionFinds is Hex finds && _world.IsKnown(finds);

    /// <summary>The map's island of a tile: its own for land, or the first land next to it, as the simulation counts it.</summary>
    public string? IslandOf(Hex tile)
    {
        if (!_map.Contains(tile))
        {
            return null;
        }

        if (_map.IslandAt(tile) is string island)
        {
            return island;
        }

        for (int direction = 0; direction < Hex.Directions; direction++)
        {
            var near = tile.Neighbour(direction);
            if (_map.Contains(near) && _map.IslandAt(near) is string coast)
            {
                return coast;
            }
        }

        return null;
    }

    /// <summary>
    /// Known tiles of <paramref name="island"/> that suit <paramref name="kind"/> now and that nothing stands on or is
    /// queued for, in map order; whether the building may go there yet, such as before a jetty, is not asked.
    /// </summary>
    public List<Hex> Free(BuildingKind kind, string island)
    {
        var requirement = _world.Content.For(kind).Requirement;
        var free = new List<Hex>();
        for (int i = 0; i < _map.Disc.Count; i++)
        {
            var tile = _map.Disc[i];
            if ((_world.UsesOf(tile) & requirement) != 0 && IslandOf(tile) == island && !IsTaken(tile))
            {
                free.Add(tile);
            }
        }

        return free;
    }

    /// <summary>Whether a building stands on the tile or is queued for it.</summary>
    public bool IsTaken(Hex tile) =>
        _world.BuildingAt(tile) is not null || _world.Projects.Any(p => p.Kind == ProjectKind.Build && p.Tile == tile);

    /// <summary>Storehouses and jetties, built or queued: where the bot plans for goods to go.</summary>
    public List<Hex> PlannedDepots()
    {
        var depots = new List<Hex>();
        foreach (var building in _world.Buildings)
        {
            if (IsDepot(building.Kind))
            {
                depots.Add(building.Tile);
            }
        }

        foreach (var project in _world.Projects)
        {
            if (project.Kind == ProjectKind.Build && project.Building is BuildingKind kind && IsDepot(kind))
            {
                depots.Add(project.Tile);
            }
        }

        return depots;
    }

    /// <summary>Producing buildings, standing or queued, in the order they stand and are queued.</summary>
    public List<(BuildingKind Kind, Hex Tile)> Producers()
    {
        var producers = new List<(BuildingKind, Hex)>();
        foreach (var building in _world.Buildings)
        {
            if (Produces(building.Kind))
            {
                producers.Add((building.Kind, building.Tile));
            }
        }

        foreach (var project in _world.Projects)
        {
            if (project.Kind == ProjectKind.Build && project.Building is BuildingKind kind && Produces(kind))
            {
                producers.Add((kind, project.Tile));
            }
        }

        return producers;
    }

    /// <summary>The level a building has or is being upgraded to, as a percentage of level 1's production.</summary>
    public int LevelPercent(Hex tile) =>
        _world.BuildingAt(tile) is { Level: 2 } || _world.Projects.Any(p => p.Kind == ProjectKind.Upgrade && p.Tile == tile) ? 150 : 100;

    /// <summary>Whether a building's way to its depot has every road it can take, laid or in the queue.</summary>
    public bool HasRoad(Hex tile) => _world.TripFrom(tile) is { Reaches: true, Steps: > 1 } && _world.RoadToDepot(tile).Count == 0;

    /// <summary>Known land and fishing water of an island richer than normal, in map order.</summary>
    public List<Hex> RichTiles(string? island)
    {
        var rich = new List<Hex>();
        for (int i = 0; i < _map.Disc.Count; i++)
        {
            var tile = _map.Disc[i];
            if (_map.RichnessAt(tile) > 100 && _world.IsKnown(tile) && island is not null && IslandOf(tile) == island
                && (_map.TerrainAt(tile).IsLand() || _map.Features.IsFishingWater(tile)))
            {
                rich.Add(tile);
            }
        }

        return rich;
    }

    /// <summary>
    /// Steps over land from every tile to the nearest of <paramref name="depots"/> on its island, indexed like the map's
    /// disc; <see cref="int.MaxValue"/> where none can be reached. Read a tile's steps with <see cref="StepsTo"/>.
    /// </summary>
    public int[] StepsFrom(IEnumerable<Hex> depots)
    {
        var steps = new int[_map.Disc.Count];
        Array.Fill(steps, int.MaxValue);
        var queue = new Queue<Hex>();
        foreach (var depot in depots)
        {
            int index = _map.Disc.IndexOf(depot);
            if (index >= 0 && steps[index] != 0)
            {
                steps[index] = 0;
                queue.Enqueue(depot);
            }
        }

        while (queue.Count > 0)
        {
            var hex = queue.Dequeue();
            string? island = _map.IslandAt(hex);
            int next = steps[_map.Disc.IndexOf(hex)] + 1;
            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                var near = hex.Neighbour(direction);
                int index = _map.Disc.IndexOf(near);
                if (index >= 0 && steps[index] == int.MaxValue && island is not null && _map.IslandAt(near) == island)
                {
                    steps[index] = next;
                    queue.Enqueue(near);
                }
            }
        }

        return steps;
    }

    /// <summary>A tile's steps to a depot from <see cref="StepsFrom"/>; a fishing hut on stilts takes its first step ashore.</summary>
    public int StepsTo(Hex tile, int[] steps)
    {
        int index = _map.Disc.IndexOf(tile);
        if (index < 0)
        {
            return int.MaxValue;
        }

        if (_map.IslandAt(tile) is not null)
        {
            return steps[index];
        }

        string? island = IslandOf(tile);
        int fewest = int.MaxValue;
        for (int direction = 0; direction < Hex.Directions; direction++)
        {
            var near = tile.Neighbour(direction);
            int at = _map.Disc.IndexOf(near);
            if (at >= 0 && island is not null && _map.IslandAt(near) == island && steps[at] < fewest)
            {
                fewest = steps[at];
            }
        }

        return fewest == int.MaxValue ? int.MaxValue : fewest + 1;
    }

    /// <summary>
    /// How near a store the bots want what they build, as a share: 100, 75 or 50 % by distance, one step better with a
    /// road. Under economy v1 it was the share of production that reached the store; since economy v2 distance costs the
    /// carriers' time instead (D-110), and the bots still place buildings nearer the store first, as a player would.
    /// </summary>
    public static int DistancePercent(int steps, bool road)
    {
        int step = steps <= 1 ? 0 : steps <= 3 ? 1 : 2;
        if (road && step > 0)
        {
            step--;
        }

        return step == 0 ? 100 : step == 1 ? 75 : 50;
    }

    /// <summary>
    /// What a building on <paramref name="tile"/> yields in a game day at level 1 next to a store, weighted
    /// (economy_v1.py, daily_output), by its recipe; 0 for a building without one. Slow processes grow day and night,
    /// and processing counts what the building makes when its raw materials last.
    /// </summary>
    /// <param name="planning">Whether queued buildings count as standing, so that a woodcutter's forest is read as it will be.</param>
    public long DailyYield(BuildingKind kind, Hex tile, bool planning)
    {
        if (RecipeOf(kind) is not Recipe recipe)
        {
            return 0;
        }

        if (recipe.ForestTiles > 0)
        {
            return Woods(tile, planning).Take(recipe.ForestTiles).Sum(wood => DailyYieldOfWood(recipe, wood));
        }

        // Hunting fells nothing: each forest tile is shared evenly with the other lodges that hunt on it (economy-v2.md,
        // section 4).
        if (recipe.HuntTiles > 0)
        {
            return Woods(tile, planning).Take(recipe.HuntTiles).Sum(wood => DailyYieldOfWood(recipe, wood) / Hunters(recipe, wood, tile, planning));
        }

        long perHour = recipe.IsSlowProcess ? recipe.GrowsPerHour : recipe.PerHour;
        long richness = recipe.Richness ? _map.RichnessAt(tile) : 100;
        return Weights[(int)recipe.Product] * (perHour * richness / 100) * (recipe.IsSlowProcess ? 24 : ActiveHours);
    }

    /// <summary>The hunting lodges that hunt on a forest tile, a lodge on <paramref name="tile"/> among them.</summary>
    private int Hunters(Recipe recipe, Hex wood, Hex tile, bool planning) =>
        1 + _world.Buildings.Count(b => b.Tile != tile && RecipeOf(b.Kind) is { HuntTiles: > 0 } hunting && Woods(b.Tile, planning).Take(hunting.HuntTiles).Contains(wood));

    /// <summary>What a building of this kind makes, or null when it makes nothing.</summary>
    public Recipe? RecipeOf(BuildingKind kind) => _world.Content.For(kind).Recipe;

    /// <summary>The weighted product a felling recipe takes from one forest tile in a game day.</summary>
    public long DailyYieldOfWood(Recipe recipe, Hex wood) =>
        Weights[(int)recipe.Product] * (recipe.PerHour * (recipe.Richness ? _map.RichnessAt(wood) : 100) / 100) * ActiveHours;

    /// <summary>
    /// The forest tiles a woodcutter on <paramref name="tile"/> fells: known forest on the tile and its neighbours that no
    /// other building stands on, richest first, then fullest, then in map order, as the simulation fells them.
    /// </summary>
    public List<Hex> Woods(Hex tile, bool planning)
    {
        var woods = new List<Hex>();
        for (int direction = -1; direction < Hex.Directions; direction++)
        {
            var near = direction < 0 ? tile : tile.Neighbour(direction);
            if (_map.Contains(near) && _map.TerrainAt(near) == Terrain.Forest && _world.IsKnown(near)
                && (near == tile || (planning ? !IsTaken(near) : _world.BuildingAt(near) is null)))
            {
                woods.Add(near);
            }
        }

        woods.Sort((a, b) =>
        {
            int richer = _map.RichnessAt(b).CompareTo(_map.RichnessAt(a));
            int fuller = _world.TimberLeft(b).CompareTo(_world.TimberLeft(a));
            return richer != 0 ? richer : fuller != 0 ? fuller : a.CompareTo(b);
        });
        return woods;
    }

    /// <summary>Land that is neither woods, fields, pasture nor mountain ranks first for buildings that produce nothing there.</summary>
    public int TerrainRank(Hex tile) => _map.TerrainAt(tile) switch
    {
        Terrain.Grassland => 0,
        Terrain.Beach => 1,
        _ => 2,
    };

    public static bool IsDepot(BuildingKind kind) => kind is BuildingKind.Storehouse or BuildingKind.Jetty;

    public bool Produces(BuildingKind kind) => RecipeOf(kind) is not null;
}
