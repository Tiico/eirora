using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Serialization;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Rules 12 → 13 (TASK-065): every project is work by the free villagers, with no dawns left, so a world goes on as it
/// did under rules 12 (ADR-0006).
/// </summary>
public sealed class HandBuildArrivesTests
{
    private const string Golden = "save-3-rules-12";

    // A save from the previous rules loads with its queue as the villagers' work and nothing built by hand.
    [Fact]
    public void ASaveFromRulesTwelveHasNothingBuiltByHand()
    {
        byte[] bytes = GoldenSaveTests.Bytes(Golden);
        Assert.Contains("rules 12", SaveSections.Text(Encoding.UTF8.GetString(bytes), "latest").Split('\n'));

        var save = Save.Load(bytes);

        Assert.True(save.ChangedOnLoad);
        Assert.NotEmpty(save.World.Projects);
        Assert.DoesNotContain(save.World.Projects, p => p.ByHand);
    }

    // A world of the current rules with nothing built by hand, written as rules 12 wrote it, migrates back to itself.
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    public void ARulesTwelveStateMigratesBackToItself(int seed)
    {
        var world = BuiltOutWorld.Generate(seed);
        string state = world.Fingerprint();

        Assert.Equal(state, new HandBuildArrives().Migrate(RulesTwelve(state), world.Map));
        var reader = new StateReader(state);
        World.Read(reader, world.Map);
        reader.End();
    }

    [Theory]
    [InlineData("project Build Cottage -1 0 8000 0")]
    [InlineData("project Road -1 0")]
    [InlineData("project Build Cottage -1 0")]
    public void AProjectLineThatIsNotOfRulesTwelveIsRefused(string line)
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        world.AdvanceTo(TestClock.At("2026-10-15 07:00", 2));
        Assert.True(world.Apply("cottage", new Build(BuildingKind.Cottage, Tiles.L1)).Accepted);
        var lines = RulesTwelve(world.Fingerprint()).Split('\n').ToList();
        lines[lines.FindIndex(l => l.StartsWith("project ", StringComparison.Ordinal))] = line;

        Assert.Throws<FormatException>(() => new HandBuildArrives().Migrate(string.Join('\n', lines), PrototypeHome.Map));
    }

    /// <summary>
    /// A state of rules 13 or later with nothing built by hand as rules 12 wrote it: each project's line without its
    /// dawns left; a state of rules 12 or earlier stays as it is.
    /// </summary>
    internal static string RulesTwelve(string state)
    {
        var lines = state.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var fields = lines[i].Split(' ');
            if (fields[0] == "project" && fields.Length == (fields[1] == "Road" ? 6 : 7))
            {
                lines[i] = fields[^1] == "0" ? lines[i][..lines[i].LastIndexOf(' ')] : throw new ArgumentException($"Rules 12 built nothing by hand: '{lines[i]}'.", nameof(state));
            }
        }

        return string.Join('\n', lines);
    }
}
