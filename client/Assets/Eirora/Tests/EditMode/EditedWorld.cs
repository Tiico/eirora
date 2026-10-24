using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Eirora.Simulation;

namespace Eirora.Client.Tests
{
    /// <summary>A world with its state edited as a test needs it, read back from a snapshot, as a save is.</summary>
    internal static class EditedWorld
    {
        public static World Edit(World world, Action<List<string>> edit)
        {
            string text = Encoding.UTF8.GetString(SimulationSnapshot.Take(world).ToBytes());
            int stateStart = text.IndexOf('\n', text.IndexOf("state-sha256 ", StringComparison.Ordinal)) + 1;
            var lines = text.Substring(stateStart).TrimEnd('\n').Split('\n').ToList();
            edit(lines);
            string state = string.Join("\n", lines) + "\n";
            var header = text.Substring(0, stateStart).TrimEnd('\n').Split('\n').ToList();
            using var sha = SHA256.Create();
            header[header.Count - 1] = "state-sha256 " + string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(state)).Select(b => b.ToString("x2")));
            return SimulationSnapshot.FromBytes(Encoding.UTF8.GetBytes(string.Join("\n", header) + "\n" + state)).Restore(world.Map);
        }

        /// <summary>The world with roads on <paramref name="tiles"/>, in map order after the queue, as the state keeps them.</summary>
        public static World WithRoads(World world, IEnumerable<Simulation.Map.Hex> tiles) => Edit(world, lines =>
        {
            lines.RemoveAll(l => l.StartsWith("road ", StringComparison.Ordinal));
            int at = lines.FindIndex(l => l.StartsWith("forest ", StringComparison.Ordinal) || l.StartsWith("fog ", StringComparison.Ordinal));
            lines.InsertRange(at, tiles.Concat(world.Roads).Distinct().OrderBy(t => t).Select(t => $"road {t.Q} {t.R}"));
        });
    }
}
