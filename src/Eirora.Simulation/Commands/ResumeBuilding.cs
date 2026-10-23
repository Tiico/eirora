using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>Resumes a paused building, which is staffed again at its place in the staffing order (D-23).</summary>
    public sealed class ResumeBuilding : GameCommand
    {
        public ResumeBuilding(Hex tile) => Tile = tile;

        /// <summary>Where the building stands.</summary>
        public Hex Tile { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            var building = settlement.BuildingAt(Tile);
            if (building is null)
            {
                return Rejection.NoBuildingThere;
            }

            if (!building.Paused)
            {
                return Rejection.AlreadyDone;
            }

            settlement.SetPaused(building, false);
            return Rejection.None;
        }
    }
}
