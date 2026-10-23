using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// The player's say over the villagers' work (economy-v1.md, section 4.1; TASK-044, CIV-02): pausing a building, the
/// staffing order and the order of the building queue, all free of forces (D-23) and stopped by the rest lock (INV-T3).
/// </summary>
/// <remarks>
/// The worlds are written as state text, so that the number of villagers and who is staffed are exactly what each test
/// needs: the start village of twelve villagers, seven of them in its four buildings, plus the buildings the test adds.
/// </remarks>
public sealed class StaffingTests
{
    private const long WorkPerVillagerPerQuarter = 250;
    private static int s_key;

    // Acceptance: a paused quarry produces nothing, and its two villagers build.
    [Fact]
    public void APausedQuarryProducesNothingAndItsVillagersBuild()
    {
        var running = Morning("building Quarry -3 -1 1 1 0 0 0 0 0 0 0");
        Assert.True(Apply(running, new Build(BuildingKind.Cottage, L1)).Accepted);
        var paused = Copy(running);

        Assert.True(Apply(paused, new PauseBuilding(M3)).Accepted);

        Assert.Equal((3, 5), (running.FreeVillagers, paused.FreeVillagers));
        long stone = paused.Settlement.Stock(Resource.Stone);
        long work = paused.Projects[0].WorkLeft;
        running.AdvanceTo(At("2026-10-15 08:00", 2));
        paused.AdvanceTo(At("2026-10-15 08:00", 2));

        Assert.Equal(stone, paused.Settlement.Stock(Resource.Stone));
        Assert.True(running.Settlement.Stock(Resource.Stone) > stone, "The running quarry produced no stone.");
        Assert.Equal(work - (4 * 5 * WorkPerVillagerPerQuarter), paused.Projects[0].WorkLeft);
        Assert.Equal(work - (4 * 3 * WorkPerVillagerPerQuarter), running.Projects[0].WorkLeft);

        var quarry = paused.BuildingAt(M3)!;
        Assert.Equal((false, true, 1), (quarry.Staffed, quarry.Paused, quarry.Level));
    }

    // A paused building keeps its level and what has grown (D-39), and is staffed again when it is resumed. A paused
    // field is not tended and does not grow, so that pausing it costs what it makes.
    [Fact]
    public void APausedBuildingLosesNothingAndIsStaffedAgainWhenResumed()
    {
        var world = Morning("building Field -1 2 2 1 0 3000000 0 0 0 0 0");
        Assert.True(Apply(world, new PauseBuilding(O2)).Accepted);

        world.AdvanceTo(At("2026-10-15 12:00", 2));

        var field = world.BuildingAt(O2)!;
        Assert.Equal((2, 3_000_000L), (field.Level, field.Ripe));
        Assert.True(world.BuildingAt(O1)!.Ripe > 0 || world.Stock(Resource.Grain) > 0, "The running field grew nothing.");
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new Workday(O2)).Rejection);

        Assert.True(Apply(world, new ResumeBuilding(O2)).Accepted);

        Assert.Equal((true, false), (field.Staffed, field.Paused));
        Assert.Equal(3, world.FreeVillagers);
    }

    // Acceptance: when the villagers do not suffice for every building, the new staffing order holds from the next
    // quarter hour.
    [Fact]
    public void BuildingsAreStaffedInTheNewOrderFromTheNextQuarterHour()
    {
        var world = Morning(
            "building FishingHut -3 3 1 1 0 0 0 0 0 0 0",
            "building Field -1 2 1 1 0 0 0 0 0 0 0",
            "building Quarry -3 -1 1 0 0 0 0 0 0 0 0");
        Assert.Equal(1, world.FreeVillagers);
        long stone = world.Settlement.Stock(Resource.Stone);

        Assert.True(Apply(world, new Prioritize(M3, 0)).Accepted);

        Assert.Equal([M3, S, K1, O1, F1, K2, O2], world.StaffingOrder.Select(b => b.Tile));
        Assert.Equal([true, true, true, true, true, true, false], world.StaffingOrder.Select(b => b.Staffed));
        Assert.Equal(1, world.FreeVillagers);
        Assert.Equal(stone, world.Settlement.Stock(Resource.Stone));

        world.AdvanceTo(At("2026-10-15 07:15", 2));

        Assert.True(world.Settlement.Stock(Resource.Stone) > stone, "The quarry first in the order produced no stone.");
    }

    // A building that does not fit is passed over for the next one that does, so no villager who could work stands idle.
    [Fact]
    public void ABuildingThatDoesNotFitIsPassedOverForOneThatDoes()
    {
        var world = Morning(
            "building FishingHut -3 3 1 1 0 0 0 0 0 0 0",
            "building Field -1 2 1 1 0 0 0 0 0 0 0",
            "building Quarry -3 -1 1 0 0 0 0 0 0 0 0",
            "building Storehouse 2 -1 1 1 0 0 0 1 4 0 0");
        Assert.Equal(0, world.FreeVillagers);

        Assert.True(Apply(world, new Prioritize(M3, 0)).Accepted);

        // The quarry takes the field's villagers; the one left over is too few for the field but staffs the storehouse.
        Assert.Equal([M3, S, K1, O1, F1, K2, O2, E1], world.StaffingOrder.Select(b => b.Tile));
        Assert.Equal([true, true, true, true, true, true, false, true], world.StaffingOrder.Select(b => b.Staffed));
        Assert.Equal(0, world.FreeVillagers);
    }

    // The buildings that need no staff keep their places among the others, and the order is also the order of work.
    [Fact]
    public void MovingABuildingKeepsTheOthersInTheirOrder()
    {
        var world = Morning("building Longhouse -1 0 1 0 0 0 0 0 0 0 0", "building Quarry -3 -1 1 1 0 0 0 0 0 0 0");

        Assert.True(Apply(world, new Prioritize(M3, 2)).Accepted);
        Assert.Equal([S, K1, M3, O1, F1, L1], world.Buildings.Select(b => b.Tile));

        Assert.True(Apply(world, new Prioritize(S, 4)).Accepted);
        Assert.Equal([K1, M3, O1, F1, S, L1], world.Buildings.Select(b => b.Tile));
        Assert.Equal([K1, M3, O1, F1, S], world.StaffingOrder.Select(b => b.Tile));
    }

    // A building that is completed comes last in the staffing order and is staffed if villagers are left for it.
    [Fact]
    public void ACompletedBuildingComesLastInTheStaffingOrder()
    {
        var world = Morning("building Quarry -3 -1 1 1 0 0 0 0 0 0 0");
        Assert.True(Apply(world, new Prioritize(M3, 0)).Accepted);
        Assert.True(Apply(world, new Build(BuildingKind.Storehouse, E1)).Accepted);

        world.AdvanceTo(At("2026-10-15 16:00", 2));

        Assert.Equal([M3, S, K1, O1, F1, E1], world.StaffingOrder.Select(b => b.Tile));
        Assert.All(world.StaffingOrder, b => Assert.True(b.Staffed, $"{b.Kind} on {b.Tile} is not staffed."));
    }

    // Acceptance: of two projects in the queue, the one moved first gets the work hours first.
    [Fact]
    public void AProjectMovedFirstInTheQueueGetsTheWorkFirst()
    {
        var world = Morning();
        Assert.True(Apply(world, new Build(BuildingKind.Cottage, L1)).Accepted);
        Assert.True(Apply(world, new Build(BuildingKind.Cottage, L2)).Accepted);
        Apply(world, new Rush());

        Assert.True(Apply(world, new QueueFirst(ProjectKind.Build, L2)).Accepted);

        Assert.Equal([L2, L1], world.Projects.Select(p => p.Tile));
        Assert.Equal(24_000, world.Projects[1].WorkLeft); // The rush stays with the cottage it went into.
        world.AdvanceTo(At("2026-10-15 08:00", 2));
        Assert.Equal((32_000 - (4 * 5 * WorkPerVillagerPerQuarter), 24_000L), (world.Projects[0].WorkLeft, world.Projects[1].WorkLeft));
    }

    [Fact]
    public void ACommandThatChangesNothingOrNamesNothingIsRejected()
    {
        var world = Morning("building Longhouse -1 0 1 0 0 0 0 0 0 0 0");
        Assert.True(Apply(world, new Build(BuildingKind.Cottage, L2)).Accepted);
        string before = world.Fingerprint();

        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new PauseBuilding(H1)).Rejection);
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new PauseBuilding(S)).Rejection); // A storehouse produces nothing to pause.
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new ResumeBuilding(H1)).Rejection);
        Assert.Equal(Rejection.AlreadyDone, Apply(world, new ResumeBuilding(K1)).Rejection);
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new Prioritize(L1, 0)).Rejection); // A longhouse needs no staff.
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new Prioritize(H1, 0)).Rejection);
        Assert.Equal(Rejection.NoSuchPlace, Apply(world, new Prioritize(K1, -1)).Rejection);
        Assert.Equal(Rejection.NoSuchPlace, Apply(world, new Prioritize(K1, 4)).Rejection);
        Assert.Equal(Rejection.AlreadyDone, Apply(world, new Prioritize(K1, 1)).Rejection);
        Assert.Equal(Rejection.NotQueued, Apply(world, new QueueFirst(ProjectKind.Build, L1)).Rejection);
        Assert.Equal(Rejection.NotQueued, Apply(world, new QueueFirst(ProjectKind.Upgrade, L2)).Rejection);
        Assert.Equal(Rejection.AlreadyDone, Apply(world, new QueueFirst(ProjectKind.Build, L2)).Rejection);
        Assert.Equal(before, world.Fingerprint()); // INV-D7.

        Assert.True(Apply(world, new PauseBuilding(K1)).Accepted);
        Assert.Equal(Rejection.AlreadyDone, Apply(world, new PauseBuilding(K1)).Rejection);
    }

    // Acceptance: during the rest every one of them is rejected and changes nothing (INV-T3, INV-D7), and none costs a
    // force (D-23).
    [Fact]
    public void DuringTheRestPausingPrioritizingAndOrderingTheQueueAreRejectedWithoutSideEffects()
    {
        var world = Village(At("2026-10-15 22:45", 2), "building Quarry -3 -1 1 1 0 0 0 0 0 0 0");
        Assert.True(Apply(world, new Build(BuildingKind.Cottage, L1)).Accepted);
        Assert.True(Apply(world, new Build(BuildingKind.Cottage, L2)).Accepted);
        Assert.True(Apply(world, new PauseBuilding(K1)).Accepted);
        world.AdvanceTo(At("2026-10-15 23:30", 2));
        string before = world.Fingerprint();
        GameCommand[] commands = [new PauseBuilding(M3), new ResumeBuilding(K1), new Prioritize(M3, 0), new QueueFirst(ProjectKind.Build, L2)];

        Assert.All(commands, command => Assert.Equal(Rejection.Resting, Apply(world, command).Rejection));
        Assert.Equal(before, world.Fingerprint());

        world.AdvanceTo(At("2026-10-16 07:30", 2));
        var forces = Enum.GetValues<Force>().Select(world.Available).ToArray();
        Assert.All(commands, command => Assert.True(Apply(world, command).Accepted, $"{command.GetType().Name} was rejected after dawn."));
        Assert.Equal(forces, Enum.GetValues<Force>().Select(world.Available));
    }

    // INV-V2 after every command: never more villagers at work than there are, and none in a building that is paused.
    [Fact]
    public void TheVillagersAtWorkNeverOutnumberTheVillagers()
    {
        var world = Morning(
            "building FishingHut -3 3 1 1 0 0 0 0 0 0 0",
            "building Field -1 2 1 1 0 0 0 0 0 0 0",
            "building Storehouse 2 -1 1 1 0 0 0 1 4 0 0",
            "building Quarry -3 -1 1 0 0 0 0 0 0 0 0");
        var random = new Random(44);
        for (int i = 0; i < 200; i++)
        {
            var tile = world.StaffingOrder[random.Next(world.StaffingOrder.Count)].Tile;
            GameCommand command = random.Next(3) switch
            {
                0 => new PauseBuilding(tile),
                1 => new ResumeBuilding(tile),
                _ => new Prioritize(tile, random.Next(world.StaffingOrder.Count)),
            };
            Apply(world, command);

            int atWork = world.StaffingOrder.Where(b => b.Staffed).Sum(b => world.Content.For(b.Kind).Staff);
            Assert.InRange(world.FreeVillagers, 0, world.Villagers);
            Assert.Equal(world.Villagers - atWork, world.FreeVillagers);
            Assert.DoesNotContain(world.StaffingOrder, b => b.Staffed && b.Paused);
        }
    }

    /// <summary>The start village at seven in the morning with <paramref name="buildings"/> after its own, in that order.</summary>
    private static World Morning(params string[] buildings) => Village(At("2026-10-15 07:00", 2), buildings);

    /// <summary>The start village with twelve villagers at <paramref name="at"/> and <paramref name="buildings"/> after its own.</summary>
    private static World Village(TimeReading at, params string[] buildings)
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(at);
        var lines = world.Fingerprint().Split('\n').ToList();
        lines[lines.FindIndex(l => l.StartsWith("settlement ", StringComparison.Ordinal))] = "settlement 12 0 0";
        lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, buildings);
        return World.Read(new StateReader(string.Join("\n", lines)), PrototypeHome.Map);
    }

    private static World Copy(World world) => World.Read(new StateReader(world.Fingerprint()), world.Map);

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"staffing-{Interlocked.Increment(ref s_key)}", command);
}
