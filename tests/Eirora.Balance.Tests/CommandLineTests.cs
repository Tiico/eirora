namespace Eirora.Balance.Tests;

/// <summary>The tool's arguments: anything it would otherwise ignore or guess at is an error.</summary>
public sealed class CommandLineTests
{
    [Fact]
    public void WithoutArgumentsItPlaysEconomyV1AndTwentyGeneratedMapsForFourWeeks()
    {
        var command = CommandLine.Parse([]);

        Assert.Equal(6 + 40, command.Runs.Count);
        Assert.Equal(28, command.Runs[^1].Days);
        Assert.Equal(Path.Combine("artifacts", "balance"), command.Output);
    }

    [Fact]
    public void ItPlaysEveryStrategyWithEveryProfileOnEveryMap()
    {
        var command = CommandLine.Parse(["--maps", "generated", "--from", "100", "--seeds", "2", "--strategies", "near,naive", "--profiles", "normal,low", "--days", "14"]);

        Assert.Equal(2 * 2 * 2, command.Runs.Count);
        Assert.All(command.Runs, run => Assert.Equal(14, run.Days));
        Assert.Equal([100UL, 101UL], command.Runs.Select(r => r.Map.Seed!.Value).Distinct());
    }

    [Theory]
    [InlineData("--maps", "protoype")]
    [InlineData("--seed", "5")]
    [InlineData("--suite", "economy-v1", "--days", "14")]
    [InlineData("--suite", "economy-v1", "--from", "3")]
    [InlineData("--maps", "prototype", "--seeds", "5")]
    [InlineData("--suite", "generated", "--days")]
    [InlineData("--suite", "generated", "--suite", "all")]
    [InlineData("--suite", "everything")]
    [InlineData("--days", "-1")]
    [InlineData("--strategies", "nearby")]
    [InlineData("--profiles", "medium")]
    [InlineData("--strategies", ",")]
    [InlineData("--strategies", "")]
    [InlineData("--strategies", "near,,sea")]
    [InlineData("--strategies", "near,near")]
    [InlineData("--profiles", "normal, normal")]
    [InlineData("suite", "all")]
    public void ItRefusesWhatItWouldOtherwiseIgnoreOrGuessAt(params string[] args) =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(args));
}
