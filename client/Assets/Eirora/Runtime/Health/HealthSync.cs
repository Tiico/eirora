using System.Collections.Generic;
using System.Threading.Tasks;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.SelfCare;

namespace Eirora.Client.Health
{
    /// <summary>
    /// Turns the health store's totals into self-care commands for the last seven game days, when the player opens the
    /// game (D-53, D-70). Only changed totals become commands, so the same data never counts twice (INV-B1).
    /// </summary>
    /// <remarks>
    /// Steps and workouts count in the game day they happen in. Sleep for a game day is the store's own sleep total
    /// over the night up to that day's dawn, that is over the previous game day (D-71); day 0 keeps the guided tour's
    /// Dawn Power, and day 1 counts sleep only from when the world was created.
    /// </remarks>
    public sealed class HealthSync
    {
        private const int DaysBack = RecordSelfCare.DaysBack;

        private readonly IHealthSource _source;

        public HealthSync(IHealthSource source) => _source = source;

        /// <summary>The commands that bring the world's self-care up to date with the health store.</summary>
        public async Task<IReadOnlyList<GameCommand>> CommandsAsync(World world)
        {
            var commands = new List<GameCommand>();
            var status = await _source.StatusAsync();
            if (status.Availability != HealthAvailability.Available || status.Granted.Count == 0)
            {
                return commands;
            }

            int today = world.GameDay;
            long now = world.Now.UtcSeconds;
            for (int day = System.Math.Max(0, today - DaysBack + 1); day <= today; day++)
            {
                if (world.GameDayStartUtc(day) is not long start)
                {
                    continue;
                }

                long end = day == today ? now : world.GameDayStartUtc(day + 1) ?? now;
                foreach (var habit in status.Granted)
                {
                    var interval = Interval(world, habit, day, start, end);
                    if (interval is (long from, long to) && to > from)
                    {
                        await Read(world, habit, day, from, to, commands);
                    }
                }
            }

            return commands;
        }

        private static (long From, long To)? Interval(World world, Habit habit, int day, long start, long end)
        {
            if (habit != Habit.Sleep)
            {
                return (start, end);
            }

            if (day == 0 || world.GameDayStartUtc(day - 1) is not long previous)
            {
                return null;
            }

            long from = day == 1 && world.CreatedUtc > previous ? world.CreatedUtc : previous;
            return (from, start);
        }

        private async Task Read(World world, Habit habit, int day, long from, long to, List<GameCommand> commands)
        {
            HealthTotal total;
            try
            {
                total = await _source.ReadAsync(habit, from, to);
            }
            catch (HealthSourceException)
            {
                // Access taken back or the store busy: keep what is recorded and let the player enter it by hand (HLT-12).
                return;
            }

            Record(world, habit, day, ContributionSource.Verified, total.Verified, commands);
            Record(world, habit, day, ContributionSource.HealthStoreManualEntry, total.Manual, commands);
        }

        private static void Record(World world, Habit habit, int day, ContributionSource source, long value, List<GameCommand> commands)
        {
            int clamped = value > int.MaxValue ? int.MaxValue : (int)value;
            int? recorded = world.RecordedSelfCare(day, habit, source);
            if (recorded == clamped || (recorded is null && clamped == 0))
            {
                return;
            }

            commands.Add(new RecordSelfCare(day, habit, source, clamped));
        }
    }
}
