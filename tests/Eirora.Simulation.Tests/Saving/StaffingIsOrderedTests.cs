using Eirora.Simulation.Saving;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Rules 5 → 6 (TASK-044): every building is running and staffed in the order it was built, and nothing else in the
/// world changes.
/// </summary>
public sealed class StaffingIsOrderedTests
{
    // Acceptance, on a world a player could have saved: the golden save from rules 5.
    [Fact]
    public void EveryBuildingRunsInTheOrderItWasBuiltAndTheWorldIsOtherwiseUnchanged()
    {
        byte[] bytes = GoldenSaveTests.Bytes("save-3-rules-5");
        var before = SaveSections.Text(System.Text.Encoding.UTF8.GetString(bytes), "latest").Split('\n');
        Assert.Contains("rules 5", before);
        var builtBefore = before.Where(IsBuilding).ToList();

        var world = Save.Load(bytes).World;
        var after = world.Fingerprint().Split('\n');

        // The save keeps the contributions in a part of their own (D-101), which follows the world in the fingerprint.
        var kept = before.SkipWhile(line => !line.StartsWith("map ", StringComparison.Ordinal)).Where(l => l.Length > 0 && !IsBuilding(l)).ToList();
        Assert.Equal(kept, after.Where(l => !IsBuilding(l)).Take(kept.Count));
        Assert.Equal(builtBefore.Select(line => line + " 0"), after.Where(IsBuilding));
        Assert.NotEmpty(builtBefore);
        Assert.DoesNotContain(world.Buildings, b => b.Paused);
        Assert.Equal(
            builtBefore.Select(line => line.Split(' ')).Select(f => $"{f[1]} {f[2]} {f[3]}"),
            world.Buildings.Select(b => $"{b.Kind} {b.Tile.Q} {b.Tile.R}"));
    }

    [Fact]
    public void OnlyTheBuildingsOfRulesFiveAreMigrated()
    {
        string current = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night).Fingerprint();

        Assert.Equal(current, new StaffingIsOrdered().Migrate(RulesFive(current)));
        Assert.Throws<FormatException>(() => new StaffingIsOrdered().Migrate(current));
    }

    /// <summary>The same world as rules 5 wrote it, without whether each building is paused.</summary>
    internal static string RulesFive(string state) =>
        string.Join("\n", state.Split('\n').Select(line => IsBuilding(line) ? line[..line.LastIndexOf(' ')] : line));

    private static bool IsBuilding(string line) => line.StartsWith("building ", StringComparison.Ordinal);
}
