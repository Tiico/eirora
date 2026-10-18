using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eirora.Simulation.SelfCare;

namespace Eirora.Client.Health
{
    /// <summary>
    /// A health store in memory for tests and the editor (NFR-11): steps and workouts per UTC day, and sleep per
    /// night, with a manual share. Grants exactly the habits it was told to.
    /// </summary>
    public sealed class FakeHealthSource : IHealthSource
    {
        private readonly List<(Habit Habit, long FromUtc, long ToUtc, long Total, long Manual)> _entries = new();
        private readonly HashSet<Habit> _grantable;
        private readonly HashSet<Habit> _granted = new();

        public FakeHealthSource(IEnumerable<Habit> grantable, HealthAvailability availability = HealthAvailability.Available)
        {
            _grantable = new HashSet<Habit>(grantable);
            Availability = availability;
        }

        public HealthAvailability Availability { get; set; }

        public int Reads { get; private set; }

        /// <summary>Adds a total that lies wholly inside [fromUtc, toUtc); a read counts it if the interval covers its start.</summary>
        public void Add(Habit habit, long fromUtc, long toUtc, long total, long manual = 0) =>
            _entries.Add((habit, fromUtc, toUtc, total, manual));

        public void Grant(params Habit[] habits) => _granted.UnionWith(habits.Where(_grantable.Contains));

        public void Revoke(params Habit[] habits) => _granted.ExceptWith(habits);

        public Task<HealthStatus> StatusAsync() =>
            Task.FromResult(Availability == HealthAvailability.Available
                ? new HealthStatus(Availability, _granted.ToArray())
                : new HealthStatus(Availability, new Habit[0]));

        public Task<IReadOnlyCollection<Habit>> RequestAccessAsync(IReadOnlyCollection<Habit> habits)
        {
            Grant(habits.ToArray());
            return Task.FromResult<IReadOnlyCollection<Habit>>(_granted.ToArray());
        }

        public Task<HealthTotal> ReadAsync(Habit habit, long fromUtc, long toUtc)
        {
            Reads++;
            if (!_granted.Contains(habit))
            {
                throw new HealthSourceException("permission_denied", $"{habit} is not granted");
            }

            var inside = _entries.Where(e => e.Habit == habit && e.FromUtc >= fromUtc && e.FromUtc < toUtc).ToList();
            return Task.FromResult(new HealthTotal(inside.Sum(e => e.Total), inside.Sum(e => e.Manual)));
        }

        public bool OpenSettings() => true;
    }
}
