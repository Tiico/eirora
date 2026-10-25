using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Moves a project first in the building queue, so that the free villagers' work goes to it first. Free of forces,
    /// like all planning (D-23).
    /// </summary>
    public sealed class QueueFirst : GameCommand
    {
        public QueueFirst(ProjectKind kind, Hex tile)
        {
            Kind = kind;
            Tile = tile;
        }

        public ProjectKind Kind { get; }

        /// <summary>The building site, or the building an upgrade or road is for.</summary>
        public Hex Tile { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            if (settlement.Queued(Tile, Kind) is not Project project)
            {
                return Rejection.NotQueued;
            }

            if (project.ByHand)
            {
                return Rejection.BuiltByHand;
            }

            if (settlement.Projects[0] == project)
            {
                return Rejection.AlreadyDone;
            }

            settlement.MoveFirst(project);
            return Rejection.None;
        }
    }
}
