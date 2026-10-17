using Eirora.Simulation.SelfCare;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Records a game day's total for one habit from one source (GLOSSARY: Bidrag). The client normalises and
    /// de-duplicates health data into daily totals before recording (PRD 7.4); raw records never reach the
    /// simulation (INV-B4). Recording is allowed during rest, and the force is credited at dawn (D-54, INV-T9).
    /// </summary>
    public sealed class RecordSelfCare : GameCommand
    {
        /// <summary>How many game days back self-care can be recorded, including today (D-53).</summary>
        public const int DaysBack = 7;

        public RecordSelfCare(int gameDay, Habit habit, ContributionSource source, int total)
        {
            GameDay = gameDay;
            Habit = habit;
            Source = source;
            Total = total;
        }

        public int GameDay { get; }

        public Habit Habit { get; }

        public ContributionSource Source { get; }

        /// <summary>Steps, workout sessions or sleep minutes for the whole game day.</summary>
        public int Total { get; }

        public override CommandCategory Category => CommandCategory.Administrative;

        internal override Rejection Execute(World world)
        {
            if (GameDay > world.GameDay || GameDay <= world.GameDay - DaysBack)
            {
                return Rejection.GameDayOutOfRange;
            }

            if (Total < 0 || Total > Limit(Habit, Source))
            {
                return Rejection.AmountOutOfRange;
            }

            world.RecordSelfCare(GameDay, Habit, Source, Total);
            return Rejection.None;
        }

        /// <summary>Plausibility limits per game day (forces.md, section 6); the curves cap the force long before.</summary>
        private static int Limit(Habit habit, ContributionSource source) => (habit, source) switch
        {
            (Habit.Steps, ContributionSource.SelfReported) => 60_000,
            (Habit.Steps, _) => 200_000,
            (Habit.Workouts, ContributionSource.SelfReported) => 6,
            (Habit.Workouts, _) => 20,
            (_, ContributionSource.SelfReported) => 16 * 60,
            _ => 24 * 60,
        };
    }
}
