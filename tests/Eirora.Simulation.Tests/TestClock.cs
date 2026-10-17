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

    public static string Utc(long utcSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(utcSeconds).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
