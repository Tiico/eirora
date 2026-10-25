using System;
using System.Collections.Generic;
using System.Globalization;
using Eirora.Simulation;

namespace Eirora.Client.App
{
    /// <summary>A system that the guide introduces when it first reaches the player (epoch-1.md, section 3).</summary>
    public enum Introduction
    {
        /// <summary>6a: a settlement has 20 villagers, and crofters can become farmers (economy-v2.md, section 5).</summary>
        Farmers,
    }

    /// <summary>
    /// Which introductions the player has seen (PRG-02): each once, in epoch 1's order, at most one a game day, and one
    /// that waits comes the next time. What has been seen is the device's, not the world's, and is kept in a small file
    /// beside the save so that it survives a restart.
    /// </summary>
    public sealed class Introductions
    {
        public const string File = "introductions.txt";

        // In epoch 1's order, each with the world it waits for.
        private static readonly (Introduction Introduction, Func<World, bool> Due)[] s_order =
        {
            (Introduction.Farmers, world => world.Step >= 1),
        };

        private readonly string? _path;
        private readonly HashSet<Introduction> _seen = new();
        private int? _lastDay; // The game day the last introduction was shown.

        /// <param name="path">Where what has been seen is kept, or null to keep it only while the app runs.</param>
        public Introductions(string? path)
        {
            _path = path;
            if (path is null || !System.IO.File.Exists(path))
            {
                return;
            }

            // One line per introduction seen and one with the day of the last; an unknown line is from a later build.
            foreach (string line in System.IO.File.ReadAllLines(path))
            {
                if (line.StartsWith("day ", StringComparison.Ordinal) && int.TryParse(line.Substring(4), NumberStyles.None, CultureInfo.InvariantCulture, out int day))
                {
                    _lastDay = day;
                }
                else if (Enum.TryParse(line, out Introduction seen))
                {
                    _seen.Add(seen);
                }
            }
        }

        /// <summary>The introduction to show now, or null when none waits or one has been shown today.</summary>
        public Introduction? Due(World world)
        {
            if (_lastDay == world.GameDay)
            {
                return null;
            }

            foreach (var (introduction, due) in s_order)
            {
                if (!_seen.Contains(introduction) && due(world))
                {
                    return introduction;
                }
            }

            return null;
        }

        /// <summary>Notes that an introduction was shown today, so that it is not shown again and none other is today.</summary>
        public void Shown(Introduction introduction, World world)
        {
            _seen.Add(introduction);
            _lastDay = world.GameDay;
            if (_path is null)
            {
                return;
            }

            var lines = new List<string> { $"day {world.GameDay.ToString(CultureInfo.InvariantCulture)}" };
            foreach (var (known, _) in s_order)
            {
                if (_seen.Contains(known))
                {
                    lines.Add(known.ToString());
                }
            }

            string temporary = _path + ".tmp";
            System.IO.File.WriteAllLines(temporary, lines);
            if (System.IO.File.Exists(_path))
            {
                System.IO.File.Delete(_path);
            }

            System.IO.File.Move(temporary, _path);
        }
    }
}
