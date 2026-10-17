namespace Eirora.Spike.HealthConnect
{
    /// <summary>Whether a record becomes a verified or self-reported contribution (HLT-08, D-68).</summary>
    public enum ContributionClass
    {
        /// <summary>Verifierat bidrag: recorded by the source and not marked as entered by hand, even when the method is unknown.</summary>
        Verified,

        /// <summary>Egenrapporterat bidrag: marked by the source as entered by hand.</summary>
        SelfReported,
    }

    /// <summary>Health Connect's Metadata.RECORDING_METHOD_* values.</summary>
    public static class RecordingMethods
    {
        public const int Unknown = 0;
        public const int ActivelyRecorded = 1;
        public const int AutomaticallyRecorded = 2;
        public const int ManualEntry = 3;

        public static string Name(int method) => method switch
        {
            Unknown => "UNKNOWN",
            ActivelyRecorded => "ACTIVELY_RECORDED",
            AutomaticallyRecorded => "AUTOMATICALLY_RECORDED",
            ManualEntry => "MANUAL_ENTRY",
            _ => method.ToString(),
        };

        // D-68: only what the source marks as entered by hand is self-reported. Watch apps write UNKNOWN for real sensor data.
        public static ContributionClass Classify(int method) => method == ManualEntry ? ContributionClass.SelfReported : ContributionClass.Verified;
    }
}
