using System;

namespace Eirora.Simulation.Time
{
    /// <summary>
    /// A moment as the device saw it: UTC time, the IANA time zone and that zone's UTC offset at that moment.
    /// The simulation never reads a clock or a time zone database itself (INV-D2, ADR-0005).
    /// </summary>
    public readonly struct TimeReading : IEquatable<TimeReading>
    {
        public TimeReading(long utcSeconds, string zoneId, int offsetSeconds)
        {
            if (string.IsNullOrEmpty(zoneId))
            {
                throw new ArgumentException("A time zone is required.", nameof(zoneId));
            }

            UtcSeconds = utcSeconds;
            ZoneId = zoneId;
            OffsetSeconds = offsetSeconds;
        }

        /// <summary>Seconds since 1970-01-01T00:00:00Z.</summary>
        public long UtcSeconds { get; }

        /// <summary>The IANA time zone, for example "Europe/Stockholm".</summary>
        public string ZoneId { get; }

        /// <summary>The zone's offset from UTC at this moment, including daylight saving time.</summary>
        public int OffsetSeconds { get; }

        /// <summary>Wall-clock seconds since 1970-01-01T00:00 in the zone.</summary>
        public long LocalSeconds => UtcSeconds + OffsetSeconds;

        public bool Equals(TimeReading other) =>
            UtcSeconds == other.UtcSeconds && OffsetSeconds == other.OffsetSeconds && string.Equals(ZoneId, other.ZoneId, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is TimeReading other && Equals(other);

        public override int GetHashCode() => UtcSeconds.GetHashCode() ^ OffsetSeconds;

        public static bool operator ==(TimeReading left, TimeReading right) => left.Equals(right);

        public static bool operator !=(TimeReading left, TimeReading right) => !left.Equals(right);
    }
}
