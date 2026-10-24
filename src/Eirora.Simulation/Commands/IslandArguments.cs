using System;
using Eirora.Simulation.Economy;

namespace Eirora.Simulation.Commands
{
    /// <summary>
    /// Checks the arguments of the commands that name an island and a resource (TASK-046), so that every such command can
    /// be written to a journal and read back: a map's ids are one word (WorldMapBuilder).
    /// </summary>
    internal static class IslandArguments
    {
        private static readonly char[] s_spaces = { ' ', '\t', '\n', '\r' };

        public static string Id(string island, string parameter) =>
            !string.IsNullOrEmpty(island) && island.IndexOfAny(s_spaces) < 0 ? island : throw new ArgumentException($"'{island}' is not an island's id.", parameter);

        public static Resource Defined(Resource resource, string parameter) =>
            Enum.IsDefined(typeof(Resource), resource) ? resource : throw new ArgumentException($"{(int)resource} is not a resource.", parameter);
    }
}
