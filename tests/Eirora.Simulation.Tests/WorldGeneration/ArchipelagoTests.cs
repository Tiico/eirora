using Eirora.Simulation.Commands;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using Eirora.WorldGeneration;
using Eirora.WorldGeneration.Validation;

namespace Eirora.Simulation.Tests.WorldGeneration;

/// <summary>
/// The map format is ready for M8 and V2 without building them (TASK-035, ROADMAP M3 point 6): worlds can be laid
/// out in a shared archipelago (MAP-07, ADR-0010), and a map made outside the generator, with a geographic source,
/// is validated and simulated like any other (D-05, MAP-10). How an archipelago is composed is O-04, decided in M8;
/// here worlds simply surround a common sea in the middle, which is O-04's starting point.
/// </summary>
public sealed class ArchipelagoTests
{
    private const int Radius = 24;
    private static readonly ProceduralArchipelagoV1 s_generator = new();

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void GeneratedWorldsLaidOutAroundACommonSeaNeverOverlap(int worlds)
    {
        var placed = Place(worlds);

        var tiles = placed.SelectMany(Tiles).ToList();

        Assert.Equal(tiles.Count, tiles.Select(t => t.Global).Distinct().Count());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void EveryWorldHasAHarbourOnTheCommonSea(int worlds)
    {
        var placed = Place(worlds);
        var land = new HashSet<Hex>(placed.SelectMany(Tiles).Where(t => t.Terrain.IsLand()).Select(t => t.Global));

        foreach (var world in placed)
        {
            var harbours = MapValidator.Analyse(world.Map).StartHarbours.Select(h => Global(world, h)).ToList();
            Assert.NotEmpty(harbours);
            Assert.True(ReachesBeyond(world, harbours, land), $"World {world.Index} has no harbour on the common sea.");
        }
    }

    [Fact]
    public void AWorldTurnedInStepsOf60DegreesKeepsItsShape()
    {
        var map = s_generator.Generate(3);
        var hex = new Hex(5, -2);

        Assert.Equal(hex, Turn(hex, 6));
        Assert.Equal(hex.DistanceTo(Hex.Origin), Turn(hex, 1).DistanceTo(Hex.Origin));
        Assert.Equal(map.Disc.Count, Enumerable.Range(0, map.Disc.Count).Select(i => Turn(map.Disc[i], 2)).Distinct().Count(map.Contains));
    }

    [Fact]
    public void AGeographicMapIsReadValidatedAndPlayedLikeAnyOther()
    {
        var built = MapValidatorTests.GoodMap(source: MapSource.Geographic).Build();

        var map = WorldMap.Parse(built.ToText());
        var save = Save.Begin(map, new TimeReading(1_792_000_000, "Europe/Stockholm", 7200), new RestSchedule(23 * 60, 8 * 60));
        for (int day = 1; day <= 3; day++)
        {
            save.AdvanceTo(new TimeReading(1_792_000_000 + (day * 24 * 3600), "Europe/Stockholm", 7200));
            save.Apply($"steps-{day}", new RecordSelfCare(save.World.GameDay, Habit.Steps, ContributionSource.Verified, 8_000));
        }

        var loaded = Save.Load(save.ToBytes());

        Assert.Equal((MapSource.Geographic, "handmade-test"), (map.Source, map.Provenance!.Dataset));
        Assert.True(MapValidator.Validate(map).Passed, MapValidator.Validate(map).ToText());
        Assert.Equal(save.World.Fingerprint(), loaded.World.Fingerprint());
        Assert.Equal(built.Hash, loaded.World.Map.Hash);
    }

    [Fact]
    public void TheValidatorJudgesAMapTheSameWhateverItsSource()
    {
        var handmade = MapValidator.Validate(MapValidatorTests.GoodMap().Build()).ToText();

        Assert.Equal(handmade, MapValidator.Validate(MapValidatorTests.GoodMap(source: MapSource.Geographic).Build()).ToText());
    }

    /// <summary>
    /// Worlds on the six discs that touch a common disc in the middle, each turned a further 60°: discs of radius R
    /// tile the plane with their centres 2R + 1 steps apart.
    /// </summary>
    private static List<Placed> Place(int worlds)
    {
        var step = new Hex((2 * Radius) + 1, -Radius);
        return Enumerable.Range(0, worlds)
            .Select(i => new Placed(i, s_generator.Generate((ulong)(i + 1)), Turn(step, i), i))
            .ToList();
    }

    private static IEnumerable<(Hex Global, Terrain Terrain)> Tiles(Placed world) =>
        Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i]).Select(h => (Global(world, h), world.Map.TerrainAt(h)));

    private static Hex Global(Placed world, Hex local) => world.Offset + Turn(local, world.Turns);

    /// <summary>Turns a hex 60° counter-clockwise about the origin <paramref name="turns"/> times; exact in axial integers.</summary>
    private static Hex Turn(Hex hex, int turns)
    {
        for (int i = 0; i < turns % 6; i++)
        {
            hex = new Hex(-hex.R, -hex.S);
        }

        return hex;
    }

    /// <summary>Whether a ship from the harbours reaches water outside its own world: the common sea or a neighbour's.</summary>
    private static bool ReachesBeyond(Placed world, List<Hex> harbours, HashSet<Hex> land)
    {
        var seen = new HashSet<Hex>(harbours);
        var queue = new Queue<Hex>(harbours);
        int bound = 4 * ((2 * Radius) + 1);
        while (queue.Count > 0)
        {
            var hex = queue.Dequeue();
            if (hex.DistanceTo(world.Offset) > Radius)
            {
                return true;
            }

            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                var next = hex.Neighbour(direction);
                if (next.DistanceTo(Hex.Origin) <= bound && !land.Contains(next) && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return false;
    }

    private sealed record Placed(int Index, WorldMap Map, Hex Offset, int Turns);
}
