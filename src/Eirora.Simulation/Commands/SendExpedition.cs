using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Spends Exploration to send an expedition from the home jetty across the sea; it returns after 32 waking hours
    /// with new land (EXP-01, economy-v1.md, section 6). A lookout makes it a quarter cheaper.
    /// </summary>
    public sealed class SendExpedition : GameCommand
    {
        public override CommandCategory Category => CommandCategory.Gameplay;

        /// <summary>The Exploration an expedition costs in this world now.</summary>
        public static int Cost(World world) =>
            world is not null && world.Settlement.HasBuilt(BuildingKind.Lookout)
                ? PrototypeIsland.ExpeditionCost * 3 / 4
                : PrototypeIsland.ExpeditionCost;

        internal override Rejection Execute(World world)
        {
            var possible = world.Settlement.CanSendExpedition();
            if (possible != Rejection.None)
            {
                return possible;
            }

            if (!world.TrySpend(Force.Exploration, Cost(world)))
            {
                return Rejection.InsufficientForces;
            }

            world.Settlement.SendExpedition();
            return Rejection.None;
        }
    }
}
