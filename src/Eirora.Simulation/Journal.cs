using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;

namespace Eirora.Simulation
{
    /// <summary>
    /// A world together with every input that made it, as versioned text the client saves (INV-D5).
    /// Loading replays the inputs, which gives the identical world because the simulation is deterministic (INV-D1).
    /// </summary>
    /// <remarks>
    /// Time is recorded only before each command and when the zone or offset changes: by INV-D4 the spans in between
    /// need no readings of their own. The simulation does no I/O; the client stores the text.
    /// </remarks>
    public sealed class Journal
    {
        /// <summary>The journal format. Bumped when the text changes shape.</summary>
        public const int FormatVersion = 1;

        private const string Header = "eirora-journal";

        private static readonly char[] s_lineBreak = { '\n' };

        // The prototype journal's header and create line; empty for a journal that continues from a snapshot.
        private readonly string _start;
        private readonly StringBuilder _inputs = new();
        private TimeReading _lastRecorded;

        private Journal(World world, string start)
        {
            World = world;
            _start = start;
            _lastRecorded = world.Now;
        }

        public World World { get; }

        /// <summary>The inputs since the journal began, one per line, without the prototype journal's start.</summary>
        internal string Inputs => _inputs.ToString();

        /// <summary>The first line of a journal written by this build.</summary>
        internal static string CurrentHeader => $"{Header} {Text(FormatVersion)} rules {Text(RulesVersion.Current)}";

        /// <summary>Creates a new world and starts its journal.</summary>
        public static Journal Start(TimeReading install, RestSchedule restSchedule) =>
            new(
                World.Create(install, restSchedule),
                CurrentHeader + "\n"
                + $"create {Reading(install)} {Text(restSchedule.StartMinuteOfDay)} {Text(restSchedule.DurationMinutes)}\n");

        /// <summary>Rebuilds a world by replaying a saved prototype journal.</summary>
        /// <exception cref="FormatException">The text is not a journal this version can read.</exception>
        public static Journal Load(string text) => Load(text, out _);

        /// <summary>
        /// Rebuilds a world from a prototype journal like <see cref="Load(string)"/>, and gives the world as it was
        /// created, before any input, as <paramref name="start"/>. <paramref name="played"/> hears each replayed
        /// command's key and result.
        /// </summary>
        internal static Journal Load(string text, out SimulationSnapshot start, Action<string, CommandResult>? played = null)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var lines = text.Split(s_lineBreak, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                throw new FormatException("A journal has a header and a create line.");
            }

            var header = Fields(lines[0]);
            if (header.Length != 4 || header[0] != Header || header[1] != Text(FormatVersion) || header[2] != "rules")
            {
                throw new FormatException($"Not a journal of format {FormatVersion}: '{lines[0]}'.");
            }

            if (!int.TryParse(header[3], NumberStyles.None, CultureInfo.InvariantCulture, out int rules)
                || rules < RulesVersion.ReplaysJournalsFrom || rules > RulesVersion.Current)
            {
                string message = $"The journal uses rules version {header[3]}; this build replays {RulesVersion.ReplaysJournalsFrom} to {RulesVersion.Current}.";
                throw rules > 0 && rules < RulesVersion.ReplaysJournalsFrom ? new JournalFromOlderRulesException(message) : new FormatException(message);
            }

            var create = Fields(lines[1]);
            Expect(create, "create", 6);
            var schedule = new RestSchedule(Int(create[4]), Int(create[5]));
            if (!schedule.IsValid)
            {
                throw new FormatException($"Not a valid rest schedule: '{lines[1]}'.");
            }

            var journal = Start(ParseReading(create, 1), schedule);
            start = SimulationSnapshot.Take(journal.World);
            journal.Replay(lines, 2, played);
            return journal;
        }

        /// <summary>Starts recording the inputs to a world from where it is now, such as a restored snapshot.</summary>
        internal static Journal Continue(World world) => new(world, string.Empty);

        /// <summary>
        /// Goes on recording after <paramref name="inputs"/>, which brought a world to where <paramref name="world"/> is
        /// now and end with its time (<see cref="RecordNow"/>). Nothing is replayed.
        /// </summary>
        internal static Journal Resume(World world, string inputs)
        {
            var journal = Continue(world);
            journal._inputs.Append(inputs);
            return journal;
        }

        /// <summary>
        /// Continues from <paramref name="start"/> by replaying <paramref name="inputs"/>, which a journal continued from
        /// the same world recorded; the journal then holds the same inputs.
        /// </summary>
        /// <exception cref="FormatException">The inputs are not journal lines this version can read.</exception>
        internal static Journal Continue(World start, string inputs)
        {
            var journal = Continue(start);
            journal.Replay(inputs.Split(s_lineBreak, StringSplitOptions.RemoveEmptyEntries), 0);
            return journal;
        }

        /// <summary>The prototype journal as text: its start, then one input per line.</summary>
        public string ToText() => _start + _inputs;

        /// <summary>Records the world's current time, so that replaying the inputs reaches exactly where it is now.</summary>
        internal void RecordNow() => Record(World.Now);

        /// <summary>Advances the world, recording the reading only when the zone or offset changes.</summary>
        public IReadOnlyList<GameEvent> AdvanceTo(TimeReading reading)
        {
            if (reading.UtcSeconds < World.Now.UtcSeconds)
            {
                return Array.Empty<GameEvent>();
            }

            if (reading.OffsetSeconds != _lastRecorded.OffsetSeconds || reading.ZoneId != _lastRecorded.ZoneId)
            {
                // The offset held until the previous reading; record where the old span ended and the new one begins.
                Record(World.Now);
                Record(reading);
            }

            return World.AdvanceTo(reading);
        }

        /// <summary>Applies a command at the world's current time and records it.</summary>
        public CommandResult Apply(string idempotencyKey, GameCommand command)
        {
            if (string.IsNullOrEmpty(idempotencyKey) || idempotencyKey.Contains(' '))
            {
                throw new ArgumentException("A key is non-empty and has no spaces.", nameof(idempotencyKey));
            }

            Record(World.Now);
            Line($"cmd {idempotencyKey} {Encode(command)}");
            return World.Apply(idempotencyKey, command);
        }

        private void Record(TimeReading reading)
        {
            if (reading == _lastRecorded)
            {
                return;
            }

            Line($"time {Reading(reading)}");
            _lastRecorded = reading;
        }

        private void Line(string line) => _inputs.Append(line).Append('\n');

        private void Replay(string[] lines, int first, Action<string, CommandResult>? played = null)
        {
            for (int i = first; i < lines.Length; i++)
            {
                var fields = Fields(lines[i]);
                switch (fields[0])
                {
                    case "time":
                        Expect(fields, "time", 4);
                        var reading = ParseReading(fields, 1);
                        AdvanceTo(reading);
                        Record(reading);
                        break;
                    case "cmd":
                        if (fields.Length < 3)
                        {
                            throw new FormatException($"A command needs a key and a type: '{lines[i]}'.");
                        }

                        var result = Apply(fields[1], ParseCommand(fields));
                        played?.Invoke(fields[1], result);
                        break;
                    default:
                        throw new FormatException($"Unknown journal line: '{lines[i]}'.");
                }
            }
        }

        private static string Reading(TimeReading reading) =>
            $"{Text(reading.UtcSeconds)} {reading.ZoneId} {Text(reading.OffsetSeconds)}";

        private static string Encode(GameCommand command) => command switch
        {
            ChangeRestSchedule c => $"schedule {Text(c.Schedule.StartMinuteOfDay)} {Text(c.Schedule.DurationMinutes)}",
            RecordSelfCare c => $"selfcare {Text(c.GameDay)} {c.Habit} {c.Source} {Text(c.Total)}",
            Build c => $"build {c.Building} {Text(c.Tile)}",
            Upgrade c => $"upgrade {Text(c.Tile)}",
            BuildRoad c => $"road {Text(c.Tile)}",
            LayRoad c => $"lay {Text(c.Tile)}",
            SetCarrierCap c => $"carriers {Text(c.Tile)} {Text(c.Cap)}",
            Survey => "survey",
            Workday c => $"workday {Text(c.Tile)}",
            Rush => "rush",
            PauseBuilding c => $"pause {Text(c.Tile)}",
            ResumeBuilding c => $"resume {Text(c.Tile)}",
            Prioritize c => $"prioritize {Text(c.Tile)} {Text(c.Place)}",
            QueueFirst c => $"first {c.Kind} {Text(c.Tile)}",
            SendExpedition => "expedition",
            LiftFog c => $"fog {Text(c.Q)} {Text(c.R)}",
            _ => throw new ArgumentException($"{command.GetType().Name} cannot be journalled.", nameof(command)),
        };

        private static GameCommand ParseCommand(string[] f) => f[2] switch
        {
            "schedule" when f.Length == 5 => new ChangeRestSchedule(new RestSchedule(Int(f[3]), Int(f[4]))),
            "selfcare" when f.Length == 7 => new RecordSelfCare(Int(f[3]), Enum<Habit>(f[4]), Enum<ContributionSource>(f[5]), Int(f[6])),
            "build" when f.Length == 6 => new Build(Enum<BuildingKind>(f[3]), Tile(f, 4)),
            "upgrade" when f.Length == 5 => new Upgrade(Tile(f, 3)),
            "road" when f.Length == 5 => new BuildRoad(Tile(f, 3)),
            "lay" when f.Length == 5 => new LayRoad(Tile(f, 3)),
            "carriers" when f.Length == 6 => new SetCarrierCap(Tile(f, 3), Int(f[5])),
            "survey" when f.Length == 3 => new Survey(),
            "workday" when f.Length == 5 => new Workday(Tile(f, 3)),
            "rush" when f.Length == 3 => new Rush(),
            "pause" when f.Length == 5 => new PauseBuilding(Tile(f, 3)),
            "resume" when f.Length == 5 => new ResumeBuilding(Tile(f, 3)),
            "prioritize" when f.Length == 6 => new Prioritize(Tile(f, 3), Int(f[5])),
            "first" when f.Length == 6 => new QueueFirst(Enum<ProjectKind>(f[3]), Tile(f, 4)),
            "expedition" when f.Length == 3 => new SendExpedition(),
            "fog" when f.Length == 5 => new LiftFog(Int(f[3]), Int(f[4])),
            _ => throw new FormatException($"Unknown command: '{string.Join(" ", f)}'."),
        };

        private static Hex Tile(string[] fields, int start) => new(Int(fields[start]), Int(fields[start + 1]));

        private static TimeReading ParseReading(string[] fields, int start) =>
            new(Long(fields[start]), fields[start + 1], Int(fields[start + 2]));

        private static string[] Fields(string line) => line.TrimEnd('\r').Split(' ');

        private static void Expect(string[] fields, string kind, int count)
        {
            if (fields[0] != kind || fields.Length != count)
            {
                throw new FormatException($"Expected a '{kind}' line with {count} fields: '{string.Join(" ", fields)}'.");
            }
        }

        private static T Enum<T>(string text)
            where T : struct =>
            System.Enum.TryParse<T>(text, ignoreCase: false, out var value) && System.Enum.IsDefined(typeof(T), value)
                ? value
                : throw new FormatException($"'{text}' is not a {typeof(T).Name}.");

        private static int Int(string text) =>
            int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)
                ? value
                : throw new FormatException($"'{text}' is not a whole number.");

        private static long Long(string text) =>
            long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)
                ? value
                : throw new FormatException($"'{text}' is not a whole number.");

        private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(Hex tile) => $"{Text(tile.Q)} {Text(tile.R)}";
    }
}
