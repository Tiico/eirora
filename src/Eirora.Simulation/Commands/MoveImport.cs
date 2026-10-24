using Eirora.Simulation.Economy;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Moves a resource to another place in the order the ships take an island's goods home in (GLOSSARY:
    /// Importprioritet, LOG-06), so that, say, stone from a vein is not crowded out by wool. It applies from the next
    /// quarter hour and can be set before the sea route opens. Free of forces, like all planning (D-23).
    /// </summary>
    public sealed class MoveImport : GameCommand
    {
        public MoveImport(string island, Resource resource, int place)
        {
            Island = island;
            Resource = resource;
            Place = place;
        }

        /// <summary>The map's id of the island the sea route sails from.</summary>
        public string Island { get; }

        public Resource Resource { get; }

        /// <summary>Its new place in <see cref="World.ImportOrder"/>, counted from 0.</summary>
        public int Place { get; }

        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            var settlement = world.Settlement;
            if (Island == settlement.HomeIsland || !settlement.HasStore(Island) || !System.Enum.IsDefined(typeof(Resource), Resource))
            {
                return Rejection.NoSuchIsland;
            }

            var order = settlement.ImportOrder(Island);
            if (Place < 0 || Place >= order.Count)
            {
                return Rejection.NoSuchPlace;
            }

            if (order[Place] == Resource)
            {
                return Rejection.AlreadyDone;
            }

            settlement.MoveImport(Island, Resource, Place);
            return Rejection.None;
        }
    }
}
