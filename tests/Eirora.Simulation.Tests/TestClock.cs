using System.Globalization;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Tests;

/// <summary>Builds time readings from local wall-clock text, as the client would from the device clock.</summary>
internal static class TestClock
{
    public const string Stockholm = "Europe/Stockholm";
    public const string NewYork = "America/New_York";

    /// <summary>Rest from 23:00 to 07:00 local time.</summary>
    public static readonly RestSchedule Night = new(23 * 60, 8 * 60);

    public static TimeReading At(string local, int offsetHours, string zone = Stockholm)
    {
        var wallClock = DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var moment = new DateTimeOffset(wallClock, TimeSpan.FromHours(offsetHours));
        return new TimeReading(moment.ToUnixTimeSeconds(), zone, offsetHours * 3600);
    }

    /// <summary>
    /// Readings that take a world in <paramref name="zone"/> from <paramref name="fromUtc"/> to <paramref name="toUtc"/>,
    /// as the device clock gives them: split wherever the zone's offset changes, so each reading's offset holds for its
    /// whole span, and also at each of <paramref name="cuts"/>.
    /// </summary>
    public static IEnumerable<TimeReading> Readings(string zone, long fromUtc, long toUtc, IEnumerable<long>? cuts = null)
    {
        const long Hour = 3600;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(zone);
        int Offset(long utc) => (int)timeZone.GetUtcOffset(DateTimeOffset.FromUnixTimeSeconds(utc)).TotalSeconds;

        var ends = new SortedSet<long>((cuts ?? []).Where(cut => cut > fromUtc && cut < toUtc)) { toUtc };
        for (long t = fromUtc + Hour; t < toUtc; t += Hour)
        {
            long low = t - Hour, high = t;
            if (Offset(high) == Offset(low))
            {
                continue;
            }

            while (high - low > 1)
            {
                long middle = low + ((high - low) / 2);
                (low, high) = Offset(middle) == Offset(low) ? (middle, high) : (low, middle);
            }

            ends.Add(high);
        }

        return ends.Select(end => new TimeReading(end, zone, Offset(end - 1))).ToList();
    }

    public static string Utc(long utcSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(utcSeconds).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
