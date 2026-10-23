using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;

namespace Eirora.Balance.Tests;

/// <summary>The hourly check catches what it is there to catch.</summary>
public sealed class InvariantsTests
{
    [Fact]
    public void ForcesThatFallWithoutACommandBreakInvF6()
    {
        var world = World.Create(PrototypeHome.Map, new TimeReading(BalanceRun.InstallUtc, BalanceRun.Zone, 0), BalanceRun.Rest);
        world.AdvanceTo(new TimeReading(BalanceRun.InstallUtc + (15 * 60), BalanceRun.Zone, 0));
        var invariants = new Invariants(world);

        // A survey spends Exploration; told of it, the check takes the forces as they stand after it.
        Assert.True(world.Apply("survey", new Survey()).Accepted);
        invariants.AfterCommands(world);
        invariants.Check(world, 1);

        // Unannounced, spending looks like forces decaying with time.
        Assert.True(world.Apply("upgrade", new Upgrade(new Hex(1, 0))).Accepted);
        var violation = Assert.Throws<InvariantViolationException>(() => invariants.Check(world, 1));
        Assert.Contains("INV-F6", violation.Message, StringComparison.Ordinal);
    }
}
