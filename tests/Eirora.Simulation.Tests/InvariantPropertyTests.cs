using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Time;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests;

/// <summary>
/// The determinism and persistence invariants (invariants.md, sections 1–4) over random sequences of commands and
/// time steps, with snapshots taken and restored at random points (INV-D4, INV-D5).
/// </summary>
public sealed class InvariantPropertyTests
{
    private const int StoragePerStorehouse = 150; // economy-v1.md, section 2.

    // INV-D1: the same start and inputs give the same world.
    [Property(MaxTest = 50)]
    public bool TheSameInputsGiveTheSameWorld(int seed)
    {
        var scenario = Scenario.Generate(seed);

        return scenario.Run().Fingerprint() == scenario.Run().Fingerprint();
    }

    // INV-D5: run(restore(snapshot(run(S, L1))), L2) == run(S, L), also against the journal replayed from the start.
    [Property(MaxTest = 100)]
    public bool ASnapshotRestoredAtAnyPointContinuesAsTheWorldWould(int seed, int split)
    {
        var scenario = Scenario.Generate(seed);
        int at = Math.Abs(split % (scenario.Inputs.Count + 1));

        var world = scenario.Start();
        for (int i = 0; i < scenario.Inputs.Count; i++)
        {
            if (i == at)
            {
                world = SimulationSnapshot.FromBytes(SimulationSnapshot.Take(world).ToBytes()).Restore();
            }

            scenario.Inputs[i].ApplyTo(world);
        }

        var journal = Journal.Start(scenario.Install, scenario.Schedule);
        foreach (var input in scenario.Inputs)
        {
            input.ApplyTo(journal);
        }

        var replayed = Journal.Load(journal.ToText());
        replayed.AdvanceTo(journal.World.Now);

        string expected = scenario.Run().Fingerprint();
        return world.Fingerprint() == expected && replayed.World.Fingerprint() == expected;
    }

    // The same state always gives the same bytes, also after a round trip.
    [Property(MaxTest = 50)]
    public bool ASnapshotIsTheSameBytesEveryTime(int seed)
    {
        var world = Scenario.Generate(seed).Run();

        var bytes = SimulationSnapshot.Take(world).ToBytes();
        var again = SimulationSnapshot.Take(world).ToBytes();
        var restored = SimulationSnapshot.Take(SimulationSnapshot.FromBytes(bytes).Restore()).ToBytes();

        return bytes.SequenceEqual(again) && bytes.SequenceEqual(restored);
    }

    // INV-D6 across a load: every key used before the snapshot changes nothing afterwards and gives its first result.
    [Property(MaxTest = 50)]
    public bool AKeyUsedBeforeASnapshotChangesNothingAfterIt(int seed)
    {
        var scenario = Scenario.Generate(seed);
        var world = scenario.Start();
        var first = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var input in scenario.Inputs)
        {
            if (input is Scenario.Command command)
            {
                var result = world.Apply(command.Key, command.For(world));
                if (result.Accepted)
                {
                    first.TryAdd(command.Key, Describe.Result(result));
                }
            }
            else
            {
                input.ApplyTo(world);
            }
        }

        var restored = SimulationSnapshot.FromBytes(SimulationSnapshot.Take(world).ToBytes()).Restore();
        string before = restored.Fingerprint();

        // The key alone decides: any command sent again with a used key gives the first result.
        bool sameResults = first.All(used => Describe.Result(restored.Apply(used.Key, new Rush())) == used.Value);
        return sameResults && restored.Fingerprint() == before;
    }

    // INV-D4: advancing in one step gives the same world as advancing in many small ones.
    [Property(MaxTest = 50)]
    public bool TimeAdvancedInPartsGivesTheSameWorld(int seed)
    {
        var scenario = Scenario.Generate(seed);
        var random = new Random(seed);
        var world = scenario.Start();
        foreach (var input in scenario.Inputs)
        {
            if (input is Scenario.Advance advance)
            {
                int parts = random.Next(1, 6);
                long from = world.Now.UtcSeconds;
                for (int part = 1; part < parts; part++)
                {
                    long at = from + ((advance.To.UtcSeconds - from) * part / parts);
                    world.AdvanceTo(new TimeReading(at, advance.To.ZoneId, advance.To.OffsetSeconds));
                }
            }

            input.ApplyTo(world);
        }

        return world.Fingerprint() == scenario.Run().Fingerprint();
    }

    // Checked after every step: INV-D7, INV-D8, INV-R1, INV-V1, INV-V2 and INV-F1.
    [Property(MaxTest = 50)]
    public void EveryStepKeepsTheWorldsInvariants(int seed)
    {
        var scenario = Scenario.Generate(seed);
        var world = scenario.Start();
        foreach (var input in scenario.Inputs)
        {
            string before = world.Fingerprint();
            var was = Measures.Of(world);
            if (input is Scenario.Command command)
            {
                var result = world.Apply(command.Key, command.For(world));
                if (!result.Accepted)
                {
                    Assert.Equal(before, world.Fingerprint()); // INV-D7.
                }
            }
            else
            {
                input.ApplyTo(world);
                Measures.Of(world).AssertNotLowerThan(was); // INV-V1.
            }

            Assert.True(world.Now.UtcSeconds >= was.UtcSeconds); // INV-D8.
            Assert.InRange(world.FreeVillagers, 0, world.Villagers); // INV-V2.
            long capacity = StoragePerStorehouse * world.Buildings.Count(b => b.Kind == BuildingKind.Storehouse);
            Assert.All(Enum.GetValues<Resource>(), r => Assert.InRange(world.Stock(r), 0, capacity)); // INV-R1.
            Assert.All(Enum.GetValues<Force>(), f => Assert.True(world.Available(f) >= 0)); // INV-F1.
        }
    }

    // The properties prove little unless the generator reaches the states that are hard to restore.
    [Fact]
    public void TheGeneratorReachesTheStatesThatMatter()
    {
        var accepted = new HashSet<string>(StringComparer.Ordinal);
        var rejections = new HashSet<Rejection>();
        var records = new HashSet<string>(StringComparer.Ordinal);
        int duplicates = 0;
        for (int seed = 0; seed < 50; seed++)
        {
            var scenario = Scenario.Generate(seed);
            var world = scenario.Start();
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var input in scenario.Inputs)
            {
                if (input is Scenario.Command command)
                {
                    var made = command.For(world);
                    var result = world.Apply(command.Key, made);
                    duplicates += used.Contains(command.Key) ? 1 : 0;
                    if (result.Accepted)
                    {
                        used.Add(command.Key);
                        accepted.Add(made.GetType().Name);
                    }
                    else
                    {
                        rejections.Add(result.Rejection);
                    }
                }
                else
                {
                    input.ApplyTo(world);
                }

                foreach (string line in world.Fingerprint().Split('\n'))
                {
                    records.Add(line.Split(' ')[0] + (line.StartsWith("cycle", StringComparison.Ordinal) ? $"-{world.Phase}" : string.Empty));
                }
            }
        }

        Assert.Equal(8, accepted.Count); // Every kind of command.
        Assert.Superset(new HashSet<Rejection> { Rejection.Resting, Rejection.GameDayOutOfRange, Rejection.InvalidRestSchedule }, rejections);
        Assert.Superset(
            new HashSet<string> { "cycle-Resting", "cycle-Active", "pending-schedule", "rest", "project", "unreconciled", "sites-revealed", "force-withdrawn" },
            records);
        Assert.True(duplicates > 50, $"Only {duplicates} duplicates.");
    }

    private sealed record Measures(long UtcSeconds, int Villagers, int Buildings, int Levels, int Revealed)
    {
        public static Measures Of(World world) => new(
            world.Now.UtcSeconds,
            world.Villagers,
            world.Buildings.Count,
            world.Buildings.Sum(b => b.Level),
            PrototypeIsland.Sites.Count(s => world.IsRevealed(s.Id)));

        public void AssertNotLowerThan(Measures earlier)
        {
            Assert.True(Villagers >= earlier.Villagers);
            Assert.True(Buildings >= earlier.Buildings);
            Assert.True(Levels >= earlier.Levels);
            Assert.True(Revealed >= earlier.Revealed);
        }
    }
}
