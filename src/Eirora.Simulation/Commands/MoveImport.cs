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
        /// <exception cref="System.ArgumentException">
        /// The island is not a map's id, which is one word, or the resource is not one; a journal could not keep it.
        /// </exception>
        public MoveImport(string island, Resource resource, int place)
        {
            Island = IslandArguments.Id(island, nameof(island));
            Resource = IslandArguments.Defined(resource, nameof(resource));
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
            if (Island == settlement.HomeIsland || !settlement.HasStore(Island))
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
