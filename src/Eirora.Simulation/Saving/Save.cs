using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// A world's stored form (GLOSSARY: Sparning, ADR-0006): the world's map (ADR-0010), the snapshot from when the world
    /// got its current rules version, the journal since then and the latest snapshot, plus the previous segment until
    /// the next rules change. Loading restores the latest snapshot; the journal lets a migration be redone and
    /// conflicts between devices be resolved (TASK-023).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bytes are UTF-8 text: a header line with the format, a line with the SHA-256 hash of the header and the body,
    /// then the body of named sections, each with its length in UTF-16 characters. This frame of two lines stays the same
    /// in every format, so any build can tell a damaged save from a newer one. A changed or missing byte is caught, so a
    /// save cut off mid-write is never mistaken for a world. The simulation does no I/O; the client writes the bytes
    /// atomically.
    /// </para>
    /// <para>
    /// The cloud save keeps it as two parts in the same frame (ADR-0008, D-100): the world part, with the map and the
    /// latest snapshot of everything but what is kept per game day, and the contribution part, with the rest of the
    /// latest state, the segments and the journals, which the client encrypts. The device keeps both in one file.
    /// </para>
    /// </remarks>
    public sealed class Save
    {
        /// <summary>The save container's format. Bumped when the sections or their encoding change.</summary>
        /// <remarks>
        /// Format 2 added the map (TASK-029); a format 1 save is a world on the prototype's map. Format 3 split the
        /// latest snapshot into the world part and the contributions (TASK-023).
        /// </remarks>
        public const int FormatVersion = 3;

        private const string Magic = "eirora-save";

        private static readonly UTF8Encoding s_utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private static readonly string[] s_worldSections = { "map", "latest" };

        private static readonly string[] s_contributionSections = { "contributions", "start", "inputs", "previous-start", "previous-inputs" };

        private static readonly char[] s_lineBreak = { '\n' };

        private readonly WorldMap _map;
        private readonly SimulationSnapshot _start;
        private readonly Journal _journal;
        private readonly Segment? _previous;

        private Save(WorldMap map, SimulationSnapshot start, Journal journal, Segment? previous, bool changedOnLoad)
        {
            _map = map;
            _start = start;
            _journal = journal;
            _previous = previous;
            ChangedOnLoad = changedOnLoad;
        }

        public World World => _journal.World;

        /// <summary>
        /// Whether loading changed the save, by a migration to new rules or by taking over a prototype journal, so it
        /// should be written at once (ADR-0006).
        /// </summary>
        public bool ChangedOnLoad { get; }

        /// <summary>Whether a segment under earlier rules is kept, so a faulty migration can be redone.</summary>
        internal bool HasPreviousSegment => _previous is not null;

        /// <summary>Creates a new world on the prototype's map, and its save.</summary>
        public static Save Begin(TimeReading install, RestSchedule restSchedule) => Begin(PrototypeHome.Map, install, restSchedule);

        /// <summary>Creates a new world on <paramref name="map"/>, and its save.</summary>
        public static Save Begin(WorldMap map, TimeReading install, RestSchedule restSchedule)
        {
            var world = World.Create(map, install, restSchedule);
            return new Save(world.Map, SimulationSnapshot.Take(world), Journal.Continue(world), null, changedOnLoad: false);
        }

        /// <summary>
        /// Takes over a prototype journal from M1: it is replayed once, and the created world and the journal become the
        /// save's first segment (ADR-0006).
        /// </summary>
        /// <exception cref="JournalFromOlderRulesException">The journal is from rules this version no longer replays.</exception>
        /// <exception cref="FormatException">The text is not a prototype journal this version can replay.</exception>
        public static Save FromPrototypeJournal(string text)
        {
            var prototype = Journal.Load(text, out var start);
            return new Save(prototype.World.Map, start, Journal.Resume(prototype.World, prototype.Inputs), null, changedOnLoad: true);
        }

        /// <summary>Restores the world from its latest snapshot, migrating it to the current rules if needed.</summary>
        /// <exception cref="SavedByNewerVersionException">A newer app wrote the save.</exception>
        /// <exception cref="FormatException">The save is damaged or incomplete.</exception>
        public static Save Load(byte[] bytes) => Load(bytes, Migrations.All);

        internal static Save Load(byte[] bytes, IReadOnlyList<StateMigration> migrations) =>
            Load(Sections(bytes, out int format), format, migrations);

        /// <summary>
        /// Restores the world from a cloud save's two parts (<see cref="ToParts"/>), migrating it to the current rules if
        /// needed. Without the contribution part, when its key is lost, the world goes on in a new segment with its game
        /// days so far closed (ADR-0008, D-101).
        /// </summary>
        /// <exception cref="SavedByNewerVersionException">A newer app wrote the save.</exception>
        /// <exception cref="FormatException">
        /// A part is damaged or incomplete, is not the part it is passed as, or the parts are of different saves.
        /// </exception>
        public static Save FromParts(byte[] world, byte[]? contributions) => FromParts(world, contributions, Migrations.All);

        internal static Save FromParts(byte[] world, byte[]? contributions, IReadOnlyList<StateMigration> migrations)
        {
            var sections = Sections(world, out int format);
            if (format < 3 || !HasOnly(sections, s_worldSections))
            {
                throw new FormatException("Not a save's world part.");
            }

            if (contributions is null)
            {
                var map = WorldMap.Parse(Section(sections, "map"));
                var latest = Snapshot(sections, "latest");
                ThrowIfNewer(latest, start: null);
                var alone = latest.RestoreWorld(map, null, migrations);
                return new Save(map, SimulationSnapshot.Take(alone), Journal.Continue(alone), null, changedOnLoad: true);
            }

            var rest = Sections(contributions, out int restFormat);
            if (restFormat != format || !HasOnly(rest, s_contributionSections))
            {
                throw new FormatException("Not the save's contribution part.");
            }

            foreach (string name in s_contributionSections)
            {
                if (rest.TryGetValue(name, out string? text))
                {
                    sections.Add(name, text);
                }
            }

            return Load(sections, format, migrations);
        }

        private static Save Load(Dictionary<string, string> sections, int format, IReadOnlyList<StateMigration> migrations)
        {
            var map = format > 1 ? WorldMap.Parse(Section(sections, "map")) : PrototypeHome.Map;
            var start = Snapshot(sections, "start");
            string inputs = Section(sections, "inputs");
            var latest = Snapshot(sections, "latest");
            Segment? previous = sections.ContainsKey("previous-start")
                ? new Segment(Snapshot(sections, "previous-start"), Section(sections, "previous-inputs"))
                : null;

            ThrowIfNewer(latest, start);
            var world = format > 2
                ? latest.RestoreWorld(map, Contributions(sections, latest), migrations)
                : latest.Restore(map, migrations);
            if (latest.Rules == RulesVersion.Current)
            {
                return new Save(map, start, Journal.Resume(world, inputs), previous, changedOnLoad: format != FormatVersion);
            }

            // New rules begin a new segment at the migrated snapshot; the one that ended is kept (ADR-0006).
            return new Save(map, SimulationSnapshot.Take(world), Journal.Continue(world), new Segment(start, inputs), changedOnLoad: true);
        }

        /// <summary>Lets simulation time pass and records it as the journal does.</summary>
        public IReadOnlyList<GameEvent> AdvanceTo(TimeReading reading) => _journal.AdvanceTo(reading);

        /// <summary>Applies a command and records it; a key has no spaces.</summary>
        public CommandResult Apply(string idempotencyKey, GameCommand command) => _journal.Apply(idempotencyKey, command);

        /// <summary>
        /// The save as bytes, with a new latest snapshot. The journal records the current time first, so that the
        /// segment's start and journal always lead exactly to the latest snapshot.
        /// </summary>
        public byte[] ToBytes()
        {
            var (world, contributions) = Bodies();
            return Sealed(world + contributions);
        }

        /// <summary>
        /// The save as the cloud save's two parts, with a new latest snapshot: the world part, which holds nothing per game
        /// day, and the contribution part, which the client encrypts (ADR-0008, D-100). <see cref="ToBytes"/> holds both.
        /// </summary>
        public (byte[] World, byte[] Contributions) ToParts()
        {
            var (world, contributions) = Bodies();
            return (Sealed(world), Sealed(contributions));
        }

        /// <summary>The world the current segment's start and journal lead to, for checking that they do.</summary>
        internal World Replay() => Journal.Continue(_start.Restore(_map), _journal.Inputs).World;

        /// <summary>
        /// Merges two saves of the same world that were played apart, as two devices that were offline (D-99, ACC-03):
        /// every input of both, from the latest segment start they share, is replayed in time order under the current
        /// rules. The idempotency keys keep a command that both hold from counting twice, and a command that no longer
        /// goes is rejected as a replayed one is. The result is the same whichever save is <paramref name="mine"/>.
        /// </summary>
        /// <returns>The merged save, or null when the saves share no segment start, which makes them different worlds.</returns>
        /// <exception cref="FormatException">A journal holds an input this version cannot replay.</exception>
        /// <remarks>
        /// When the shared start is from earlier rules, the merged segment begins at that start migrated to the current
        /// rules (ADR-0006), and no earlier segment is kept: the merged journal already holds everything since it.
        /// </remarks>
        public static Save? Merge(Save mine, Save theirs)
        {
            if (mine is null)
            {
                throw new ArgumentNullException(nameof(mine));
            }

            if (theirs is null)
            {
                throw new ArgumentNullException(nameof(theirs));
            }

            if (!string.Equals(mine._map.ToText(), theirs._map.ToText(), StringComparison.Ordinal))
            {
                return null;
            }

            // The latest start both share: the current segments first, then one side's previous segment, then both.
            foreach (var (ours, others) in new[] { (false, false), (true, false), (false, true), (true, true) })
            {
                var start = mine.StartOfSegment(fromPrevious: ours);
                var theirStart = theirs.StartOfSegment(fromPrevious: others);
                if (start is null || theirStart is null || !start.SameAs(theirStart))
                {
                    continue;
                }

                string inputs = MergedInputs(start.UtcSeconds, mine.InputsSince(fromPrevious: ours), theirs.InputsSince(fromPrevious: others));
                var world = start.Restore(mine._map, Migrations.All);
                var segmentStart = start.Rules == RulesVersion.Current ? start : SimulationSnapshot.Take(world);
                return new Save(mine._map, segmentStart, Journal.Continue(world, inputs), null, changedOnLoad: true);
            }

            return null;
        }

        private SimulationSnapshot? StartOfSegment(bool fromPrevious) => fromPrevious ? _previous?.Start : _start;

        /// <summary>The inputs since a segment's start: the previous segment's and then the current one's.</summary>
        private string InputsSince(bool fromPrevious) => fromPrevious ? _previous!.Value.Inputs + _journal.Inputs : _journal.Inputs;

        /// <summary>
        /// Both journals' lines in time order, each line once: the two are walked side by side, and at each step the
        /// earlier line goes first. A command's time is the reading before it, or the segment's start. At the same
        /// second, readings go before commands and otherwise the line that is first in ordinal order, so that the
        /// result does not depend on which journal is which; a line that is next in both journals, as in their shared
        /// beginning, is taken once. Each journal's own order is kept, so what one device did in one second stays in
        /// that order.
        /// </summary>
        private static string MergedInputs(long startUtcSeconds, string mine, string theirs)
        {
            var a = Collect(startUtcSeconds, mine);
            var b = Collect(startUtcSeconds, theirs);
            var merged = new StringBuilder();
            int i = 0, j = 0;
            while (i < a.Count || j < b.Count)
            {
                string line;
                if (j >= b.Count)
                {
                    line = a[i++].Line;
                }
                else if (i >= a.Count)
                {
                    line = b[j++].Line;
                }
                else
                {
                    int order = a[i].Utc.CompareTo(b[j].Utc);
                    if (order == 0)
                    {
                        order = a[i].Rank.CompareTo(b[j].Rank);
                    }

                    if (order == 0)
                    {
                        order = string.CompareOrdinal(a[i].Line, b[j].Line);
                    }

                    line = order <= 0 ? a[i++].Line : b[j++].Line;
                    if (order == 0)
                    {
                        j++;
                    }
                }

                merged.Append(line).Append('\n');
            }

            return merged.ToString();
        }

        /// <summary>A journal's lines with the time each one happened and whether it is a reading (0) or a command (1).</summary>
        private static List<(long Utc, int Rank, string Line)> Collect(long startUtcSeconds, string inputs)
        {
            var lines = new List<(long Utc, int Rank, string Line)>();
            long utc = startUtcSeconds;
            foreach (string line in inputs.Split(s_lineBreak, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = line.Split(' ');
                switch (fields[0])
                {
                    case "time" when fields.Length == 4 && long.TryParse(fields[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long at):
                        utc = at;
                        lines.Add((utc, 0, line));
                        break;
                    case "cmd" when fields.Length >= 3:
                        lines.Add((utc, 1, line));
                        break;
                    default:
                        throw new FormatException($"Unknown journal line: '{line}'.");
                }
            }

            return lines;
        }

        /// <summary>
        /// The world part's sections and the contribution part's, after recording the current time so that the segment's
        /// start and journal always lead exactly to the latest snapshot. The contributions name the snapshot they complete.
        /// </summary>
        private (string World, string Contributions) Bodies()
        {
            _journal.RecordNow();
            var latest = SimulationSnapshot.TakeWorld(World);
            var world = new StringBuilder();
            AppendSection(world, "map", _map.ToText());
            AppendSection(world, "latest", Text(latest));

            var contributions = new StringBuilder();
            AppendSection(contributions, "contributions", $"of {latest.StateHash}\n{World.ContributionState()}");
            AppendSection(contributions, "start", Text(_start));
            AppendSection(contributions, "inputs", _journal.Inputs);
            if (_previous is Segment previous)
            {
                AppendSection(contributions, "previous-start", Text(previous.Start));
                AppendSection(contributions, "previous-inputs", previous.Inputs);
            }

            return (world.ToString(), contributions.ToString());
        }

        private static byte[] Sealed(string body)
        {
            string header = $"{Magic} {FormatVersion.ToString(CultureInfo.InvariantCulture)}\n";
            return s_utf8.GetBytes($"{header}sha256 {Hash(header + body)}\n{body}");
        }

        /// <summary>The contribution state that completes the latest snapshot, checked to be the one it belongs to.</summary>
        private static string Contributions(Dictionary<string, string> sections, SimulationSnapshot latest)
        {
            string text = Section(sections, "contributions");
            string of = $"of {latest.StateHash}\n";
            if (!text.StartsWith(of, StringComparison.Ordinal))
            {
                throw new FormatException("The contributions belong to another snapshot.");
            }

            return text.Substring(of.Length);
        }

        private static void ThrowIfNewer(SimulationSnapshot latest, SimulationSnapshot? start)
        {
            if (latest.IsFromNewerVersion || start?.IsFromNewerVersion == true)
            {
                throw new SavedByNewerVersionException(
                    $"The save has rules version {latest.Rules} and content version {latest.Content}; this build has {RulesVersion.Current} and {ContentVersion.Current}.");
            }
        }

        private static bool HasOnly(Dictionary<string, string> sections, string[] names)
        {
            int known = 0;
            foreach (string name in names)
            {
                if (sections.ContainsKey(name))
                {
                    known++;
                }
            }

            return known == sections.Count;
        }

        private static Dictionary<string, string> Sections(byte[] bytes, out int format)
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            string text;
            try
            {
                text = s_utf8.GetString(bytes);
            }
            catch (ArgumentException e)
            {
                throw new FormatException("A save is UTF-8 text.", e);
            }

            int magicEnd = text.IndexOf('\n');
            int hashEnd = magicEnd < 0 ? -1 : text.IndexOf('\n', magicEnd + 1);
            if (hashEnd < 0 || !text.StartsWith(Magic + " ", StringComparison.Ordinal))
            {
                throw new FormatException("Not a save.");
            }

            // The hash is checked before the format is read, so a damaged version is never taken for a newer app's.
            string header = text.Substring(0, magicEnd + 1);
            string body = text.Substring(hashEnd + 1);
            if (!string.Equals(text.Substring(magicEnd + 1, hashEnd - magicEnd - 1), "sha256 " + Hash(header + body), StringComparison.Ordinal))
            {
                throw new FormatException("The save does not match its hash; it is damaged or incomplete.");
            }

            string version = text.Substring(Magic.Length + 1, magicEnd - Magic.Length - 1);
            if (!int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out format) || format < 1)
            {
                throw new FormatException($"'{version}' is not a save format.");
            }

            if (format > FormatVersion)
            {
                throw new SavedByNewerVersionException($"The save has format {format}; this build reads format {FormatVersion}.");
            }

            var sections = new Dictionary<string, string>(StringComparer.Ordinal);
            int at = 0;
            while (at < body.Length)
            {
                int lineEnd = body.IndexOf('\n', at);
                string[] fields = lineEnd < 0 ? Array.Empty<string>() : body.Substring(at, lineEnd - at).Split(' ');
                if (fields.Length != 2
                    || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int length)
                    || length > body.Length - lineEnd - 1
                    || sections.ContainsKey(fields[0]))
                {
                    throw new FormatException("The save's sections are broken.");
                }

                sections.Add(fields[0], body.Substring(lineEnd + 1, length));
                at = lineEnd + 1 + length;
            }

            return sections;
        }

        private static string Section(Dictionary<string, string> sections, string name) =>
            sections.TryGetValue(name, out string? text) ? text : throw new FormatException($"The save has no '{name}' section.");

        private static SimulationSnapshot Snapshot(Dictionary<string, string> sections, string name) =>
            SimulationSnapshot.FromBytes(s_utf8.GetBytes(Section(sections, name)));

        private static void AppendSection(StringBuilder body, string name, string text) =>
            body.Append(name).Append(' ').Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append('\n').Append(text);

        private static string Text(SimulationSnapshot snapshot) => s_utf8.GetString(snapshot.ToBytes());

        private static string Hash(string text) => Sha256.Of(text);

        /// <summary>A snapshot and the inputs recorded after it under the same rules.</summary>
        private readonly struct Segment
        {
            public Segment(SimulationSnapshot start, string inputs)
            {
                Start = start;
                Inputs = inputs;
            }

            public SimulationSnapshot Start { get; }

            public string Inputs { get; }
        }
    }
}
