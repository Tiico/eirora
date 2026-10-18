using System;
using System.Collections.Generic;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;

namespace Eirora.Simulation
{
    /// <summary>
    /// A player's own world (GLOSSARY: Värld), simulated deterministically on the device (ADR-0001).
    /// Everything that changes it arrives as a command or a time reading (INV-D2).
    /// </summary>
    public sealed class World
    {
        private readonly ForceBalances _forces;
        private readonly SelfCareLedger _selfCare;
        private readonly Settlement _settlement;
        private readonly Dictionary<string, CommandResult> _results = new(StringComparer.Ordinal);

        // Self-care changes waiting for dawn (INV-T9): reconciled when the world is next active.
        private readonly SortedSet<(int Day, Habit Habit)> _unreconciled = new();
        private List<GameEvent> _events = new();

        private World(DayCycle cycle, ForceBalances forces, SelfCareLedger selfCare, Settlement settlement)
        {
            Cycle = cycle;
            _forces = forces;
            _selfCare = selfCare;
            _settlement = settlement;
        }

        public Phase Phase => Cycle.Phase;

        public int GameDay => Cycle.GameDay;

        public TimeReading Now => Cycle.Now;

        public RestSchedule RestSchedule => Cycle.Schedule;

        /// <summary>A schedule change waiting for its 24 hours to pass, if any (INV-T7).</summary>
        public RestSchedule? PendingRestSchedule => Cycle.PendingSchedule;

        /// <summary>Villagers living in the settlement (GLOSSARY: Bybor).</summary>
        public int Villagers => _settlement.Villagers;

        /// <summary>How many villagers the settlement can house.</summary>
        public int Housing => _settlement.Housing;

        /// <summary>Villagers not working in a building; they build.</summary>
        public int FreeVillagers => _settlement.FreeVillagers;

        public IReadOnlyList<Building> Buildings => _settlement.Buildings;

        /// <summary>The building queue, first in line first.</summary>
        public IReadOnlyList<Project> Projects => _settlement.Projects;

        internal DayCycle Cycle { get; }

        internal Settlement Settlement => _settlement;

        /// <summary>
        /// Creates a world at the end of the guided tour. The rest schedule applies at once, even inside the rest
        /// window (D-50), and game day 0 began at local midnight (D-51).
        /// </summary>
        public static World Create(TimeReading install, RestSchedule restSchedule)
        {
            if (!restSchedule.IsValid)
            {
                throw new ArgumentException("The rest schedule must be 6–10 hours and start within the day.", nameof(restSchedule));
            }

            var world = new World(DayCycle.Create(install, restSchedule), ForceBalances.GuidedTourStart(), new SelfCareLedger(), Settlement.Found());

            // The guided tour's Dawn Power stands in for the first game day's baseline (D-45).
            world._selfCare.MarkCredited(0, Habit.Sleep, ForceRules.DawnPowerBaseline);
            return world;
        }

        /// <summary>The whole forces available to direct.</summary>
        public int Available(Force force) => _forces.Available(force);

        /// <summary>Whole units of a resource in store.</summary>
        public long Stock(Resource resource) => Quantity.Whole(_settlement.Stock(resource));

        /// <summary>Whether a site has been found; hidden sites are under the fog (MAP-04).</summary>
        public bool IsRevealed(string siteId) => _settlement.IsRevealed(siteId);

        /// <summary>
        /// Applies a command once per idempotency key (INV-D6). Gameplay commands are rejected during rest (INV-T3).
        /// A rejected command changes nothing, not even the record of used keys (INV-D7), so it can be sent again
        /// with the same key once the reason has passed.
        /// </summary>
        public CommandResult Apply(string idempotencyKey, GameCommand command)
        {
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                throw new ArgumentException("Every command needs an idempotency key.", nameof(idempotencyKey));
            }

            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (_results.TryGetValue(idempotencyKey, out var earlier))
            {
                return earlier;
            }

            _events = new List<GameEvent>();
            var rejection = command.Category == CommandCategory.Gameplay && Phase == Phase.Resting
                ? Rejection.Resting
                : command.Execute(this);
            var result = new CommandResult(rejection, _events);
            if (result.Accepted)
            {
                _results[idempotencyKey] = result;
            }

            return result;
        }

        /// <summary>
        /// Lets simulation time pass up to <paramref name="reading"/>, whose zone and offset hold for the whole span
        /// (ADR-0005). An earlier reading changes nothing (INV-D8).
        /// </summary>
        public IReadOnlyList<GameEvent> AdvanceTo(TimeReading reading)
        {
            _events = new List<GameEvent>();
            if (reading.UtcSeconds < Now.UtcSeconds)
            {
                return _events;
            }

            // Economy ticks fall on fixed UTC quarter hours, so the same span gives the same ticks however it is split.
            long tick = NextTickAfter(Now.UtcSeconds);
            while (tick <= reading.UtcSeconds)
            {
                Cycle.AdvanceTo(new TimeReading(tick, reading.ZoneId, reading.OffsetSeconds), _events, OnDawn);
                _settlement.Tick(Phase == Phase.Active, tick, _events);
                tick += Settlement.TickSeconds;
            }

            Cycle.AdvanceTo(reading, _events, OnDawn);
            return _events;
        }

        /// <summary>
        /// The whole state in its canonical text form; equal worlds have equal fingerprints (INV-D1). It is also the
        /// state a <see cref="SimulationSnapshot"/> holds.
        /// </summary>
        public string Fingerprint()
        {
            var writer = new StateWriter();
            Write(writer);
            return writer.ToString();
        }

        /// <summary>Reads a world written by <see cref="Write"/>.</summary>
        internal static World Read(StateReader reader)
        {
            // In the order Write writes them.
            var cycle = DayCycle.Read(reader);
            var forces = ForceBalances.Read(reader);
            var settlement = Settlement.Read(reader);
            var selfCare = SelfCareLedger.Read(reader);
            var world = new World(cycle, forces, selfCare, settlement);
            while (reader.Next("unreconciled"))
            {
                world._unreconciled.Add((reader.Int(), reader.Enum<Habit>()));
            }

            while (reader.Next("command"))
            {
                string key = reader.Text();
                int count = reader.Int();
                var events = new List<GameEvent>(count >= 0 ? count : throw new FormatException("An event count is never negative."));
                for (int i = 0; i < count; i++)
                {
                    events.Add(GameEventCodec.Read(reader));
                }

                world._results.Add(key, new CommandResult(Rejection.None, events));
            }

            return world;
        }

        /// <summary>
        /// Writes the whole state: everything that can change what the world does next, and nothing derived (ADR-0006).
        /// Used keys keep their outcomes, so a command sent again after loading gets its first result (INV-D6).
        /// </summary>
        private void Write(StateWriter writer)
        {
            Cycle.Write(writer);
            _forces.Write(writer);
            _settlement.Write(writer);
            _selfCare.Write(writer);
            foreach (var (day, habit) in _unreconciled)
            {
                writer.Line("unreconciled").Long(day).Enum(habit);
            }

            var keys = new List<string>(_results.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                var result = _results[key];
                writer.Line("command").Text(key).Long(result.Events.Count);
                foreach (var gameEvent in result.Events)
                {
                    GameEventCodec.Write(writer, gameEvent);
                }
            }
        }

        /// <summary>Adds an event to the result of the command or time step being processed.</summary>
        internal void Report(GameEvent gameEvent) => _events.Add(gameEvent);

        /// <summary>Spends exactly <paramref name="cost"/> for a gameplay command, or nothing (INV-F5).</summary>
        internal bool TrySpend(Force force, int cost) => _forces.TrySpend(force, cost);

        internal void RecordSelfCare(int day, Habit habit, ContributionSource source, int total)
        {
            _selfCare.Record(day, habit, source, total);
            foreach (int affected in _selfCare.AffectedDays(day, habit, GameDay))
            {
                _unreconciled.Add((affected, habit));
            }

            if (Phase == Phase.Active)
            {
                ReconcileSelfCare(Now.UtcSeconds);
            }
        }

        private static long NextTickAfter(long utcSeconds)
        {
            long remainder = utcSeconds % Settlement.TickSeconds;
            if (remainder < 0)
            {
                remainder += Settlement.TickSeconds;
            }

            return utcSeconds - remainder + Settlement.TickSeconds;
        }

        private void OnDawn(long utcSeconds)
        {
            _settlement.Dawn(utcSeconds, _events);

            // The new day's Dawn Power starts at the baseline and is topped up when its sleep is recorded.
            _unreconciled.Add((GameDay, Habit.Sleep));
            ReconcileSelfCare(utcSeconds);
        }

        private void ReconcileSelfCare(long utcSeconds)
        {
            var changes = new List<(int Day, Habit Habit, Force Force, int Expected, int Credited)>(_unreconciled.Count);
            foreach (var (day, habit) in _unreconciled)
            {
                var (force, expected, credited) = _selfCare.Reconcile(day, habit);
                changes.Add((day, habit, force, expected, credited));
            }

            // Credit before correcting, so that a correction takes back from the highest position, where the soft cap
            // gave least, and a late contribution never makes it reach the D-62 limit.
            foreach (var change in changes)
            {
                if (change.Expected >= change.Credited)
                {
                    Settle(change.Day, change.Habit, change.Force, change.Expected, change.Credited, utcSeconds);
                }
            }

            foreach (var change in changes)
            {
                if (change.Expected < change.Credited)
                {
                    Settle(change.Day, change.Habit, change.Force, change.Expected, change.Credited, utcSeconds);
                }
            }

            _unreconciled.Clear();
        }

        private void Settle(int day, Habit habit, Force force, int expected, int credited, long utcSeconds)
        {
            long halvesBefore = _forces.Halves(force);
            if (expected > credited)
            {
                int gained = _forces.Credit(force, expected - credited);
                _events.Add(new ForceCredited(utcSeconds, force, gained));
            }
            else if (expected < credited)
            {
                // A correction takes back its credit along the soft cap (D-69), but never more than this day and habit
                // actually gave (D-62).
                int removed = _forces.Withdraw(force, credited - expected, _selfCare.GainedHalves(day, habit));
                _events.Add(new ForceWithdrawn(utcSeconds, force, removed));
            }

            _selfCare.AddGainedHalves(day, habit, _forces.Halves(force) - halvesBefore);
        }
    }
}
