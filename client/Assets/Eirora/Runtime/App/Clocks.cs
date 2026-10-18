using System;
using System.Collections.Generic;
using Eirora.Simulation.Time;

namespace Eirora.Client.App
{
    /// <summary>Where the client gets time from (ADR-0005): the device, or a fixed clock in tests and screenshots.</summary>
    public interface IClock
    {
        TimeReading Now();

        /// <summary>
        /// Readings that take the world from <paramref name="fromUtc"/> to now, split wherever the zone's offset
        /// changes, so each reading's offset holds for its whole span.
        /// </summary>
        IEnumerable<TimeReading> ReadingsSince(long fromUtc);
    }

    /// <summary>The phone's clock and time zone.</summary>
    public sealed class DeviceClock : IClock
    {
        private const long Hour = 3600;

        public TimeReading Now() => At(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        public IEnumerable<TimeReading> ReadingsSince(long fromUtc)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int offset = Offset(fromUtc);
            for (long t = fromUtc + Hour; t < now; t += Hour)
            {
                if (Offset(t) != offset)
                {
                    long change = FirstWithNewOffset(t - Hour, t, offset);
                    yield return new TimeReading(change, Zone, offset);
                    offset = Offset(change);
                }
            }

            yield return At(now);
        }

        private static string Zone => TimeZoneInfo.Local.Id;

        private static TimeReading At(long utc) => new(utc, Zone, Offset(utc));

        private static int Offset(long utc) =>
            (int)TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.FromUnixTimeSeconds(utc)).TotalSeconds;

        private static long FirstWithNewOffset(long low, long high, int oldOffset)
        {
            while (high - low > 1)
            {
                long middle = low + ((high - low) / 2);
                if (Offset(middle) == oldOffset)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return high;
        }
    }

    /// <summary>A clock that only moves when told to, for screenshots and tests.</summary>
    public sealed class FixedClock : IClock
    {
        public FixedClock(TimeReading now) => Current = now;

        public TimeReading Current { get; set; }

        public TimeReading Now() => Current;

        public IEnumerable<TimeReading> ReadingsSince(long fromUtc)
        {
            yield return Current;
        }
    }
}
