using Eirora.Simulation.Forces;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Spends Dawn Power to put eight extra hours of work into the first project in the queue that the free villagers work
    /// on; one built by hand stands at its dawn whatever is spent (D-65).
    /// </summary>
    public sealed class Rush : GameCommand
    {
        public const int Cost = 20;
        public const int Hours = 8;

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            if (world.Settlement.FirstWorked is null)
            {
                return Rejection.NothingToRush;
            }

            if (!world.TrySpend(Force.DawnPower, Cost))
            {
                return Rejection.InsufficientForces;
            }

            world.Settlement.Rush(Hours);
            return Rejection.None;
        }
    }
}
