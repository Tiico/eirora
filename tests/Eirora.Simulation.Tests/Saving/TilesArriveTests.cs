using System.Globalization;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Serialization;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Rules 3 → 4 (TASK-037): the prototype's site ids become tile coordinates, and nothing built, queued, stored or
/// known changes (INV-V1).
/// </summary>
public sealed class TilesArriveTests
{
    private const string FullForests = "forest F1 40000000\nforest F2 120000000\nforest F3 160000000\n";

    [Fact]
    public void EveryBuildingProjectAndBalanceStaysWhereItWas()
    {
        var world = NewWorld();
        Assert.True(world.Apply("cottage", new Build(BuildingKind.Cottage, L1)).Accepted);
        string state = world.Fingerprint();

        string migrated = new TilesArrive().Migrate(RulesThree(state, FullForests));

        Assert.Equal(state, migrated);
        Assert.Contains("building Woodcutter 1 -1 ", migrated, StringComparison.Ordinal);
        Assert.Contains("project Build Cottage -1 0 ", migrated, StringComparison.Ordinal);
    }

    [Fact]
    public void AFelledForestSiteLeavesItsShareOfTimberOnItsOwnTile()
    {
        // The old-growth forest counted four tiles and had half its timber left: 20 on each.
        string state = NewWorld().Fingerprint();

        string migrated = new TilesArrive().Migrate(RulesThree(state, "forest F1 10000000\nforest F2 120000000\nforest F3 80000000\n"));

        Assert.Equal(state.Replace("\nfog ", "\nforest 1 -1 10000000\nforest -2 0 20000000\nfog ", StringComparison.Ordinal), migrated);
    }

    [Fact]
    public void AQuarryOnWhatIsNowTheCrestStaysAndKeepsQuarrying()
    {
        // Under rules 3 the western mountain was a quarry site; under rules 4 it is the crest, which takes only the
        // Vårdkase (D-92). What was built stays where it stands (INV-V1).
        string state = RulesThree(NewWorld().Fingerprint(), FullForests)
            .Replace("\nforest F1", "\nbuilding Quarry M1 1 1 0 0 0\nforest F1", StringComparison.Ordinal);

        var world = World.Read(new StateReader(new TilesArrive().Migrate(state)), PrototypeHome.Map);
        long stone = world.Stock(Resource.Stone);
        world.AdvanceTo(At("2026-10-14 18:00", 2));

        Assert.Equal(BuildingKind.Quarry, world.BuildingAt(M1)!.Kind);
        Assert.Equal(TileUse.Crest, world.UsesOf(M1));
        Assert.True(world.Stock(Resource.Stone) > stone, "The quarry on the crest stopped.");
    }

    [Fact]
    public void AStateFromAnotherMapOrWithAnUnknownSiteIsRefused()
    {
        string state = RulesThree(NewWorld().Fingerprint(), FullForests);

        Assert.Throws<FormatException>(() => new TilesArrive().Migrate("map 00" + state[state.IndexOf('\n')..]));
        Assert.Throws<FormatException>(() => new TilesArrive().Migrate(state.Replace("building Storehouse S ", "building Storehouse X9 ", StringComparison.Ordinal)));
    }

    /// <summary>The same world as rules 3 wrote it: buildings and projects on site ids, and a forest line per forest site.</summary>
    private static string RulesThree(string state, string forests)
    {
        var lines = state.Split('\n').Select(line =>
        {
            var fields = line.Split(' ');
            int at = fields[0] switch { "building" => 2, "project" => 3, _ => -1 };
            if (at < 0)
            {
                return line;
            }

            var tile = new Hex(int.Parse(fields[at], CultureInfo.InvariantCulture), int.Parse(fields[at + 1], CultureInfo.InvariantCulture));
            string id = PrototypeSites.All.Single(s => s.Hex == tile).Id;
            return string.Join(" ", fields.Take(at).Append(id).Concat(fields.Skip(at + 2)));
        });

        return string.Join("\n", lines.Where(l => !l.StartsWith("forest ", StringComparison.Ordinal)))
            .Replace("\nfog ", "\n" + forests + "fog ", StringComparison.Ordinal);
    }

    private static World NewWorld() => World.Create(At("2026-10-14 14:00", 2), Night);
}
