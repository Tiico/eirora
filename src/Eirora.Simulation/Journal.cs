using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
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

        private readonly StringBuilder _text = new();
        private TimeReading _lastRecorded;

        private Journal(World world, TimeReading install)
        {
            World = world;
            _lastRecorded = install;
        }

        public World World { get; }

        /// <summary>Creates a new world and starts its journal.</summary>
        public static Journal Start(TimeReading install, RestSchedule restSchedule)
        {
            var journal = new Journal(World.Create(install, restSchedule), install);
            journal.Line($"{Header} {FormatVersion} rules {RulesVersion.Current}");
            journal.Line($"create {Reading(install)} {restSchedule.StartMinuteOfDay} {restSchedule.DurationMinutes}");
            return journal;
        }

        /// <summary>Rebuilds a world by replaying a saved journal.</summary>
        /// <exception cref="FormatException">The text is not a journal this version can read.</exception>
        public static Journal Load(string text)
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

            if (header[3] != Text(RulesVersion.Current))
            {
                throw new FormatException($"The journal uses rules version {header[3]}; this build runs {RulesVersion.Current}.");
            }

            var create = Fields(lines[1]);
            Expect(create, "create", 6);
            var journal = Start(ParseReading(create, 1), new RestSchedule(Int(create[4]), Int(create[5])));

            for (int i = 2; i < lines.Length; i++)
            {
                var fields = Fields(lines[i]);
                switch (fields[0])
                {
                    case "time":
                        Expect(fields, "time", 4);
                        journal.AdvanceTo(ParseReading(fields, 1));
                        break;
                    case "cmd":
                        if (fields.Length < 3)
                        {
                            throw new FormatException($"A command needs a key and a type: '{lines[i]}'.");
                        }

                        journal.Apply(fields[1], ParseCommand(fields));
                        break;
                    default:
                        throw new FormatException($"Unknown journal line: '{lines[i]}'.");
                }
            }

            return journal;
        }

        /// <summary>The journal as text, one input per line.</summary>
        public string ToText() => _text.ToString();

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

        private void Line(string line) => _text.Append(line).Append('\n');

        private static string Reading(TimeReading reading) =>
            $"{Text(reading.UtcSeconds)} {reading.ZoneId} {Text(reading.OffsetSeconds)}";

        private static string Encode(GameCommand command) => command switch
        {
            ChangeRestSchedule c => $"schedule {Text(c.Schedule.StartMinuteOfDay)} {Text(c.Schedule.DurationMinutes)}",
            RecordSelfCare c => $"selfcare {Text(c.GameDay)} {c.Habit} {c.Source} {Text(c.Total)}",
            Build c => $"build {c.Building} {c.SiteId}",
            Upgrade c => $"upgrade {c.SiteId}",
            BuildRoad c => $"road {c.SiteId}",
            Survey => "survey",
            Workday c => $"workday {c.SiteId}",
            Rush => "rush",
            _ => throw new ArgumentException($"{command.GetType().Name} cannot be journalled.", nameof(command)),
        };

        private static GameCommand ParseCommand(string[] f) => f[2] switch
        {
            "schedule" when f.Length == 5 => new ChangeRestSchedule(new RestSchedule(Int(f[3]), Int(f[4]))),
            "selfcare" when f.Length == 7 => new RecordSelfCare(Int(f[3]), Enum<Habit>(f[4]), Enum<ContributionSource>(f[5]), Int(f[6])),
            "build" when f.Length == 5 => new Build(Enum<BuildingKind>(f[3]), f[4]),
            "upgrade" when f.Length == 4 => new Upgrade(f[3]),
            "road" when f.Length == 4 => new BuildRoad(f[3]),
            "survey" when f.Length == 3 => new Survey(),
            "workday" when f.Length == 4 => new Workday(f[3]),
            "rush" when f.Length == 3 => new Rush(),
            _ => throw new FormatException($"Unknown command: '{string.Join(" ", f)}'."),
        };

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
    }
}
