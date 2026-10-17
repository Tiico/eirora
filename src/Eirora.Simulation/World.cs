using System;
using System.Collections.Generic;
using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;

namespace Eirora.Simulation
{
    /// <summary>
    /// A player's own world (GLOSSARY: Värld), simulated deterministically on the device (ADR-0001).
    /// Everything that changes it arrives as a command or a time reading (INV-D2).
    /// </summary>
    public sealed class World
    {
        private readonly ForceBalances _forces = ForceBalances.GuidedTourStart();
        private readonly SelfCareLedger _selfCare = new();
        private readonly Dictionary<string, CommandResult> _results = new(StringComparer.Ordinal);

        // Self-care changes waiting for dawn (INV-T9): reconciled when the world is next active.
        private readonly SortedSet<(int Day, Habit Habit)> _unreconciled = new();
        private List<GameEvent> _events = new();

        private World(TimeReading install, RestSchedule restSchedule)
        {
            Cycle = DayCycle.Create(install, restSchedule);

            // The guided tour's Dawn Power stands in for the first game day's baseline (D-45).
            _selfCare.MarkCredited(0, Habit.Sleep, ForceRules.DawnPowerBaseline);
        }

        public Phase Phase => Cycle.Phase;

        public int GameDay => Cycle.GameDay;

        public TimeReading Now => Cycle.Now;

        public RestSchedule RestSchedule => Cycle.Schedule;

        /// <summary>A schedule change waiting for its 24 hours to pass, if any (INV-T7).</summary>
        public RestSchedule? PendingRestSchedule => Cycle.PendingSchedule;

        internal DayCycle Cycle { get; }

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

            return new World(install, restSchedule);
        }

        /// <summary>The whole forces available to direct.</summary>
        public int Available(Force force) => _forces.Available(force);

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
            Cycle.AdvanceTo(reading, _events, OnDawn);
            return _events;
        }

        /// <summary>A canonical text form of the whole state; equal worlds have equal fingerprints (INV-D1).</summary>
        public string Fingerprint()
        {
            var text = new StringBuilder();
            Cycle.AppendFingerprint(text);
            _forces.AppendFingerprint(text);
            _selfCare.AppendFingerprint(text);
            text.Append("unreconciled:");
            foreach (var (day, habit) in _unreconciled)
            {
                text.Append(day).Append('/').Append((int)habit).Append(',');
            }

            text.Append(";commands:");
            var keys = new List<string>(_results.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                text.Append(key).Append(',');
            }

            return text.ToString();
        }

        /// <summary>Spends exactly <paramref name="cost"/> for a gameplay command, or nothing (INV-F5).</summary>
        internal bool TrySpend(Force force, int cost) => _forces.TrySpend(force, cost);

        internal void RecordSelfCare(int day, Habit habit, ContributionSource source, int total)
        {
            _selfCare.Record(day, habit, source, total);
            foreach (int affected in SelfCareLedger.AffectedDays(day, habit, GameDay))
            {
                _unreconciled.Add((affected, habit));
            }

            if (Phase == Phase.Active)
            {
                ReconcileSelfCare(Now.UtcSeconds);
            }
        }

        private void OnDawn(long utcSeconds)
        {
            // The new day's Dawn Power starts at the baseline and is topped up when its sleep is recorded.
            _unreconciled.Add((GameDay, Habit.Sleep));
            ReconcileSelfCare(utcSeconds);
        }

        private void ReconcileSelfCare(long utcSeconds)
        {
            foreach (var (day, habit) in _unreconciled)
            {
                var (force, expected, credited) = _selfCare.Reconcile(day, habit);
                if (expected > credited)
                {
                    int gained = _forces.Credit(force, expected - credited);
                    _events.Add(new ForceCredited(utcSeconds, force, gained));
                }
                else if (expected < credited)
                {
                    int removed = _forces.Withdraw(force, credited - expected);
                    _events.Add(new ForceWithdrawn(utcSeconds, force, removed));
                }
            }

            _unreconciled.Clear();
        }
    }
}
