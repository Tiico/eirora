using Eirora.Simulation.Time;

namespace Eirora.Simulation.Commands
{
    /// <summary>Changes the rest schedule; it applies to rest periods starting 24 hours later or more (INV-T7).</summary>
    public sealed class ChangeRestSchedule : GameCommand
    {
        public ChangeRestSchedule(RestSchedule schedule) => Schedule = schedule;

        public RestSchedule Schedule { get; }

        public override CommandCategory Category => CommandCategory.Administrative;

        internal override Rejection Execute(World world)
        {
            if (!Schedule.IsValid)
            {
                return Rejection.InvalidRestSchedule;
            }

            world.Cycle.RequestScheduleChange(Schedule);
            return Rejection.None;
        }
    }
}
