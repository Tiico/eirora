using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Spends Exploration to lift the fog from one tile at the edge of what is known (TASK-032, MAP-04): it costs 10
    /// Exploration (epoch-1.md), and the tile stays known for ever. Tiles a survey reveals and islands an expedition
    /// finds are not lifted this way.
    /// </summary>
    public sealed class LiftFog : GameCommand
    {
        /// <summary>The Exploration one tile costs (epoch-1.md, section 5).</summary>
        public const int Cost = 10;

        public LiftFog(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int Q { get; }

        public int R { get; }

        public Hex Tile => new(Q, R);

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var rejection = world.Settlement.CheckLiftFog(Tile);
            if (rejection != Rejection.None)
            {
                return rejection;
            }

            if (!world.TrySpend(Force.Exploration, Cost))
            {
                return Rejection.InsufficientForces;
            }

            world.Settlement.Fog.Lift(Tile);
            world.Report(new FogLifted(world.Now.UtcSeconds, Q, R));
            return Rejection.None;
        }
    }
}
