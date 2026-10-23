using System;
using System.Collections.Generic;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
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
        /// <summary>The timber a forest tile holds when it is full: woodcutters fell it and it grows back to this (economy-v1.md, section 3).</summary>
        public const int TimberPerForest = 40;

        private readonly ForceBalances _forces;
        private readonly SelfCareLedger _selfCare;
        private readonly Settlement _settlement;
        // Sorted by key, so the state is written in the same order on every runtime.
        private readonly SortedDictionary<string, CommandResult> _results = new(StringComparer.Ordinal);

        // Self-care changes waiting for dawn (INV-T9): reconciled when the world is next active.
        private readonly SortedSet<(int Day, Habit Habit)> _unreconciled = new();
        private List<GameEvent> _events = new();

        // While time advances: ticks of the slow processes not yet grown, and whether work changes nothing until dawn.
        private int _growing;
        private bool _idleUntilDawn;

        private World(WorldMap map, DayCycle cycle, ForceBalances forces, SelfCareLedger selfCare, Settlement settlement)
        {
            Map = map;
            Cycle = cycle;
            _forces = forces;
            _selfCare = selfCare;
            _settlement = settlement;
        }

        /// <summary>The world's map, made once and never changed (ADR-0010).</summary>
        public WorldMap Map { get; }

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

        /// <summary>The buildings in the staffing order, which is also the order they work in.</summary>
        public IReadOnlyList<Building> Buildings => _settlement.Buildings;

        /// <summary>The buildings that need staff, in the order villagers are given to them (economy-v1.md, section 4.1).</summary>
        public IReadOnlyList<Building> StaffingOrder => _settlement.StaffingOrder;

        /// <summary>The building queue, first in line first.</summary>
        public IReadOnlyList<Project> Projects => _settlement.Projects;

        /// <summary>The buildings this world knows and what they cost and produce (ADR-0006).</summary>
        public ContentCatalog Content => _settlement.Content;

        internal DayCycle Cycle { get; }

        internal Settlement Settlement => _settlement;

        /// <summary>Creates a world on the prototype's handmade map, as every world was before world generation (TASK-031).</summary>
        public static World Create(TimeReading install, RestSchedule restSchedule) => Create(PrototypeHome.Map, install, restSchedule);

        /// <summary>
        /// Creates a world on a map at the end of the guided tour, founded as the map gives (<see cref="Founding"/>). The
        /// rest schedule applies at once, even inside the rest window (D-50), and game day 0 began at local midnight (D-51).
        /// </summary>
        public static World Create(WorldMap map, TimeReading install, RestSchedule restSchedule) =>
            Create(map, install, restSchedule, ContentCatalog.Current);

        /// <summary>Creates a world whose buildings are those of <paramref name="content"/>, as tests add content.</summary>
        internal static World Create(WorldMap map, TimeReading install, RestSchedule restSchedule, ContentCatalog content)
        {
            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!restSchedule.IsValid)
            {
                throw new ArgumentException("The rest schedule must be 6–10 hours and start within the day.", nameof(restSchedule));
            }

            var world = new World(map, DayCycle.Create(install, restSchedule), ForceBalances.GuidedTourStart(), new SelfCareLedger(), Settlement.Found(map, content));

            // The guided tour's Dawn Power stands in for the first game day's baseline (D-45).
            world._selfCare.MarkCredited(0, Habit.Sleep, ForceRules.DawnPowerBaseline);
            return world;
        }

        /// <summary>The whole forces available to direct.</summary>
        public int Available(Force force) => _forces.Available(force);

        /// <summary>Whole units of a resource in store.</summary>
        public long Stock(Resource resource) => Quantity.Whole(_settlement.Stock(resource));

        /// <summary>Whole units of a resource in store on an island; goods reach home by sea route.</summary>
        public long Stock(IslandId island, Resource resource) => Quantity.Whole(_settlement.Stock(island, resource));

        /// <summary>When the world was created (D-51).</summary>
        public long CreatedUtc => Cycle.CreatedUtc;

        /// <summary>When a recent game day began, for reading health data per game day (D-40, D-71); null if too old.</summary>
        public long? GameDayStartUtc(int day) => Cycle.GameDayStartUtc(day);

        /// <summary>
        /// The last game day that takes no more self-care, because the world went on without its contribution part
        /// (D-101); null when every recent day is open.
        /// </summary>
        public int? LastClosedGameDay => _selfCare.ClosedThrough;

        /// <summary>What has been recorded for a game day, habit and source, so the client records only changes.</summary>
        public int? RecordedSelfCare(int day, Habit habit, ContributionSource source) => _selfCare.Total(day, habit, source);

        /// <summary>Hours the expedition at sea has left with the villagers awake, or 0 when none is out.</summary>
        public int ExpeditionHoursLeft => _settlement.ExpeditionHoursLeft;

        /// <summary>A tile of the island the first expedition finds, or null when the map has none worth the journey.</summary>
        public Hex? ExpeditionFinds => _settlement.Founding.ExpeditionFinds;

        /// <summary>Whether ships sail between the islands.</summary>
        public bool SeaRouteOpen => _settlement.SeaRouteOpen;

        /// <summary>The building on a tile, if any.</summary>
        public Building? BuildingAt(Hex tile) => _settlement.BuildingAt(tile);

        /// <summary>What a known tile is good for now (D-79); nothing is told about what lies under the fog (D-93).</summary>
        public TileUse UsesOf(Hex tile) => _settlement.UsesOf(tile);

        /// <summary>Why a building cannot be built on a tile right now, or <see cref="Rejection.None"/>; costs aside.</summary>
        public Rejection CheckBuild(BuildingKind building, Hex tile) => _settlement.CheckBuild(building, tile);

        /// <summary>Whole units of timber left on a forest tile, at most <see cref="TimberPerForest"/>.</summary>
        public long TimberLeft(Hex tile) => Quantity.Whole(_settlement.ForestLeft(tile));

        /// <summary>Steps over land from a tile to the nearest storehouse or jetty, or <see cref="int.MaxValue"/> when none can be reached.</summary>
        public int DepotDistance(Hex tile) => _settlement.DepotDistance(tile);

        /// <summary>
        /// Why each building produces what it does and what holds the world back (TASK-043): a reading that never changes
        /// the world or its fingerprint (INV-D2).
        /// </summary>
        public Diagnostics Diagnose() => _settlement.Diagnose();

        /// <summary>Whether the fog has lifted from a tile (TASK-032); what lies under the fog stays unknown.</summary>
        public bool IsKnown(Hex hex) => _settlement.Fog.IsKnown(hex);

        /// <summary>Whether the fog can be lifted from a tile with Exploration next (TASK-032).</summary>
        public bool CanLiftFog(Hex hex) => CheckLiftFog(hex) == Rejection.None;

        /// <summary>Why the fog cannot be lifted from a tile with Exploration, or <see cref="Rejection.None"/>.</summary>
        public Rejection CheckLiftFog(Hex hex) => _settlement.CheckLiftFog(hex);

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
            // Where only the slow processes move, the ticks are counted and grown at once (TASK-049): while the
            // villagers rest, and once a tick of their work has changed nothing, until the next dawn.
            long tick = NextTickAfter(Now.UtcSeconds);
            _idleUntilDawn = false;
            while (tick <= reading.UtcSeconds)
            {
                Cycle.AdvanceTo(new TimeReading(tick, reading.ZoneId, reading.OffsetSeconds), _events, OnDawn);
                if (Phase == Phase.Resting)
                {
                    long dawn = Cycle.RestEndUtc(reading);
                    long last = dawn <= reading.UtcSeconds ? dawn - 1 : reading.UtcSeconds;
                    long ticks = 1 + ((last - tick) / Settlement.TickSeconds);
                    tick += (ticks - 1) * Settlement.TickSeconds;
                    Cycle.AdvanceTo(new TimeReading(tick, reading.ZoneId, reading.OffsetSeconds), _events, OnDawn);
                    _growing += (int)ticks;
                }
                else if (_idleUntilDawn)
                {
                    _growing++;
                }
                else
                {
                    Grow();
                    _idleUntilDawn = !_settlement.Tick(tick, _events);
                }

                tick += Settlement.TickSeconds;
            }

            Grow();
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
            WriteWorld(writer);
            WriteContributions(writer);
            return writer.ToString();
        }

        /// <summary>
        /// The first part of the state, everything but what is kept per game day: what the save's world part holds
        /// (ADR-0008, D-100).
        /// </summary>
        internal string WorldState()
        {
            var writer = new StateWriter();
            WriteWorld(writer);
            return writer.ToString();
        }

        /// <summary>
        /// The rest of the state: self-care per game day, the force each day was credited, and the used keys with their
        /// outcomes. It follows <see cref="WorldState"/> in the fingerprint.
        /// </summary>
        internal string ContributionState()
        {
            var writer = new StateWriter();
            WriteContributions(writer);
            return writer.ToString();
        }

        /// <summary>
        /// Reads a world written by <see cref="Fingerprint"/>, on the map its state names; or, without its contributions,
        /// one written by <see cref="WorldState"/>, whose game days so far are then closed (D-101).
        /// </summary>
        internal static World Read(StateReader reader, WorldMap map, bool withContributions = true)
        {
            // In the order Write writes them.
            reader.Expect("map");
            if (!string.Equals(reader.Text(), map.Hash, StringComparison.Ordinal))
            {
                throw new FormatException("The world's state belongs to another map.");
            }

            var cycle = DayCycle.Read(reader);
            var forces = ForceBalances.Read(reader);
            var settlement = Settlement.Read(reader, map, ContentCatalog.Current);
            if (!withContributions)
            {
                // What each day was credited went with the part, so recording those days again could credit them twice.
                var closed = new SelfCareLedger();
                closed.CloseThrough(cycle.GameDay);
                return new World(map, cycle, forces, closed, settlement);
            }

            var selfCare = SelfCareLedger.Read(reader);
            var world = new World(map, cycle, forces, selfCare, settlement);
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
        /// Writes the state but what is kept per game day. Followed by <see cref="WriteContributions"/>, it is the whole
        /// state: everything that can change what the world does next, and nothing derived (ADR-0006).
        /// </summary>
        private void WriteWorld(StateWriter writer)
        {
            writer.Line("map").Text(Map.Hash);
            Cycle.Write(writer);
            _forces.Write(writer);
            _settlement.Write(writer);
        }

        /// <summary>
        /// Writes self-care per game day and the used keys. Used keys keep their outcomes, so a command sent again after
        /// loading gets its first result (INV-D6); they belong here because a self-care command's outcome tells what a
        /// day was credited.
        /// </summary>
        private void WriteContributions(StateWriter writer)
        {
            _selfCare.Write(writer);
            foreach (var (day, habit) in _unreconciled)
            {
                writer.Line("unreconciled").Long(day).Enum(habit);
            }

            foreach (var entry in _results)
            {
                var result = entry.Value;
                writer.Line("command").Text(entry.Key).Long(result.Events.Count);
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

        /// <summary>Grows the slow processes over the ticks counted since they last grew.</summary>
        private void Grow()
        {
            if (_growing > 0)
            {
                _settlement.GrowSlowProcesses(_growing);
                _growing = 0;
            }
        }

        private void OnDawn(long utcSeconds)
        {
            // The ticks before the dawn grow with the workdays that end now; after it, work can change things again.
            Grow();
            _idleUntilDawn = false;
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
            // gave least.
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
                _selfCare.NoteCredit(day, habit, _forces.Spends(force));
                int gained = _forces.Credit(force, expected - credited);
                _events.Add(new ForceCredited(utcSeconds, force, gained));
            }
            else if (expected < credited)
            {
                // A correction takes back its credit along the soft cap (D-69). Only once the force has been spent
                // since then is it limited to what this day and habit actually gave (D-62, D-74); before that the
                // position alone gives the same balance in any order, even when Build Power moves between days.
                long maxHalves = _selfCare.SpentSinceCredit(day, habit, _forces.Spends(force))
                    ? _selfCare.GainedHalves(day, habit)
                    : long.MaxValue;
                int removed = _forces.Withdraw(force, credited - expected, maxHalves);
                _events.Add(new ForceWithdrawn(utcSeconds, force, removed));
            }

            _selfCare.AddGainedHalves(day, habit, _forces.Halves(force) - halvesBefore);
            if (expected == 0)
            {
                _selfCare.ForgetCredit(day, habit);
            }
        }
    }
}
