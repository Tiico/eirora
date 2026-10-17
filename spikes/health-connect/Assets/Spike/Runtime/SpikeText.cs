using System;
using System.Globalization;

namespace Eirora.Spike.HealthConnect
{
    /// <summary>Swedish labels and formatting for the spike's screens and report.</summary>
    public static class SpikeText
    {
        private static readonly CultureInfo Swedish = SwedishOrInvariant();

        public static string HabitName(Habit habit) => habit switch
        {
            Habit.Steps => "Steg",
            Habit.Exercise => "Träningspass",
            Habit.Sleep => "Sömn",
            _ => habit.ToString(),
        };

        public static string PermissionName(ReadPermission permission) => permission switch
        {
            ReadPermission.Steps => "steg",
            ReadPermission.Exercise => "träningspass",
            ReadPermission.Sleep => "sömn",
            ReadPermission.Background => "bakgrundsläsning",
            ReadPermission.History => "historik",
            _ => permission.ToString(),
        };

        public static string ClassName(ContributionClass contributionClass) => contributionClass switch
        {
            ContributionClass.Verified => "verifierat",
            ContributionClass.SelfReported => "egenrapporterat",
            _ => "oklassat",
        };

        public static string SdkStatus(string status) => status switch
        {
            "SDK_AVAILABLE" => "Tillgänglig",
            "SDK_UNAVAILABLE" => "Finns inte på telefonen",
            "SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED" => "Behöver installeras eller uppdateras",
            _ => status,
        };

        public static string Value(HealthRecord record) => record.Habit == Habit.Steps
            ? $"{Number(record.count)} steg"
            : Duration(TimeSpan.FromMilliseconds(record.endMs - record.startMs));

        public static string Value(Habit habit, long value) => habit == Habit.Steps ? Number(value) : Duration(TimeSpan.FromSeconds(value));

        public static string Number(long value) => (value < 0 ? "−" : "") + Math.Abs(value).ToString("#,0", Swedish);

        public static string Duration(TimeSpan duration)
        {
            var sign = duration < TimeSpan.Zero ? "−" : "";
            duration = duration.Duration();
            if (duration.TotalDays >= 1)
            {
                return $"{sign}{(int)duration.TotalDays} d {duration.Hours} h";
            }

            return duration.TotalHours >= 1 ? $"{sign}{(int)duration.TotalHours} h {duration.Minutes:00} min" : $"{sign}{duration.Minutes} min";
        }

        /// <summary>The interval in the record's own zone offset when it has one, otherwise the device's.</summary>
        public static string Interval(HealthRecord record, TimeZoneInfo zone)
        {
            var start = record.hasStartOffset ? record.Start.ToOffset(TimeSpan.FromSeconds(record.startOffsetSec)) : TimeZoneInfo.ConvertTime(record.Start, zone);
            var end = record.hasEndOffset ? record.End.ToOffset(TimeSpan.FromSeconds(record.endOffsetSec)) : TimeZoneInfo.ConvertTime(record.End, zone);
            var endFormat = end.Date == start.Date ? "HH:mm" : "d MMM HH:mm";
            var offset = record.hasStartOffset ? $" (UTC{Offset(start.Offset)})" : " (ingen tidszon)";
            return $"{start.ToString("d MMM HH:mm", Swedish)}–{end.ToString(endFormat, Swedish)}{offset}";
        }

        public static string Timestamp(long epochMs, TimeZoneInfo zone) =>
            TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(epochMs), zone).ToString("d MMM HH:mm", Swedish);

        public static string Day(string isoDay) =>
            DateTime.TryParseExact(isoDay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day.ToString("ddd d MMM", Swedish) : isoDay;

        private static CultureInfo SwedishOrInvariant()
        {
            try
            {
                return CultureInfo.GetCultureInfo("sv-SE");
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }

        private static string Offset(TimeSpan offset) => (offset < TimeSpan.Zero ? "−" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }
}
