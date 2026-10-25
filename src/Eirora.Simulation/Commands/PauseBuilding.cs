using Eirora.Simulation.Map;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Pauses a producing building or a shipyard: it is not staffed and its villagers build instead, until it is resumed. Free of
    /// forces, like all planning (D-23).
    /// </summary>
    public sealed class PauseBuilding : GameCommand
    {
        public PauseBuilding(Hex tile) => Tile = tile;

        /// <summary>Where the building stands.</summary>
        public Hex Tile { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            var building = settlement.BuildingAt(Tile);
            if (building is null || !settlement.Content.For(building.Kind).Pausable)
            {
                return Rejection.NoBuildingThere;
            }

            if (building.Paused)
            {
                return Rejection.AlreadyDone;
            }

            settlement.SetPaused(building, true);
            return Rejection.None;
        }
    }
}
