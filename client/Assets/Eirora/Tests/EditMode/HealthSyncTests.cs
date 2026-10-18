using System;
using System.Linq;
using Eirora.Client.Health;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Forces;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>Health Connect data into the world, with a fake health store (TASK-013, NFR-11, D-68, D-70, D-71).</summary>
    public sealed class HealthSyncTests
    {
        private const long Hour = 3600;
        private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);
        private static int s_key;

        [TestCase(0, 0)]
        [TestCase(2_000, 30)]
        [TestCase(7_500, 86)]
        [TestCase(50_000, 110)]
        public void StepsFromTheStoreBecomeExploration(int steps, int exploration)
        {
            var (world, store) = Created();
            store.Add(Habit.Steps, At("2026-10-14 09:00"), At("2026-10-14 10:00"), steps);

            Apply(world, Sync(store, world));

            Assert.That(world.Available(Force.Exploration), Is.EqualTo(100 + exploration));
        }

        [Test]
        public void ReadingTheSameDataTwiceRecordsItOnce()
        {
            var (world, store) = Created();
            store.Add(Habit.Steps, At("2026-10-14 09:00"), At("2026-10-14 10:00"), 7_500);
            Apply(world, Sync(store, world));

            Assert.That(Sync(store, world), Is.Empty);
            Assert.That(world.Available(Force.Exploration), Is.EqualTo(186));
        }

        [Test]
        public void ManuallyEnteredStoreDataIsRecordedAsSelfReported()
        {
            var (world, store) = Created();
            store.Add(Habit.Steps, At("2026-10-14 09:00"), At("2026-10-14 10:00"), 5_000);
            store.Add(Habit.Steps, At("2026-10-14 11:00"), At("2026-10-14 12:00"), 2_000, manual: 2_000);

            var commands = Sync(store, world).Cast<RecordSelfCare>().ToList();

            Assert.That(commands.Select(c => (c.Source, c.Total)), Is.EquivalentTo(new[]
            {
                (ContributionSource.Verified, 5_000),
                (ContributionSource.HealthStoreManualEntry, 2_000),
            }));
        }

        [Test]
        public void ADeniedHabitIsNotReadAndCanStillBeEnteredByHand()
        {
            var (world, store) = Created(Habit.Steps);
            store.Add(Habit.Steps, At("2026-10-14 09:00"), At("2026-10-14 10:00"), 7_500);
            world.AdvanceTo(Reading("2026-10-15 08:00"));

            var commands = Sync(store, world).Cast<RecordSelfCare>().ToList();
            var byHand = world.Apply("by-hand", new RecordSelfCare(1, Habit.Sleep, ContributionSource.SelfReported, 420));

            Assert.That(commands.All(c => c.Habit == Habit.Steps), Is.True);
            Assert.That(byHand.Accepted, Is.True);
        }

        [Test]
        public void SleepCountsForTheDayItsNightEndsIn()
        {
            var (world, store) = Created();
            world.AdvanceTo(Reading("2026-10-15 08:00")); // Dawn at 07:00 began game day 1.
            store.Add(Habit.Sleep, At("2026-10-14 23:10"), At("2026-10-15 06:50"), 7 * 60);

            var sleep = Sync(store, world).Cast<RecordSelfCare>().Single(c => c.Habit == Habit.Sleep);

            Assert.That((sleep.GameDay, sleep.Total), Is.EqualTo((1, 7 * 60)));
        }

        [Test]
        public void NoSelfCareAndNoStoreChangeNothing()
        {
            var (world, store) = Created();
            var unavailable = new FakeHealthSource(Array.Empty<Habit>(), HealthAvailability.Unavailable);

            Assert.That(Sync(store, world), Is.Empty);
            Assert.That(Sync(unavailable, world), Is.Empty);
        }

        private static (World World, FakeHealthSource Store) Created(params Habit[] granted)
        {
            var world = World.Create(Reading("2026-10-14 14:00"), s_night);
            var all = new[] { Habit.Steps, Habit.Workouts, Habit.Sleep };
            var store = new FakeHealthSource(granted.Length > 0 ? granted : all);
            store.Grant(granted.Length > 0 ? granted : all);
            return (world, store);
        }

        private static GameCommand[] Sync(IHealthSource store, World world) =>
            new HealthSync(store).CommandsAsync(world).GetAwaiter().GetResult().ToArray();

        private static void Apply(World world, GameCommand[] commands)
        {
            foreach (var command in commands)
            {
                Assert.That(world.Apply($"h-{++s_key}", command).Accepted, Is.True, command.ToString());
            }
        }

        private static long At(string local) => Reading(local).UtcSeconds;

        private static TimeReading Reading(string local)
        {
            var wallClock = DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            return new TimeReading(new DateTimeOffset(wallClock, TimeSpan.FromHours(2)).ToUnixTimeSeconds(), "Europe/Stockholm", 7200);
        }
    }
}
