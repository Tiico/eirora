namespace Eirora.Spike.HealthConnect
{
    /// <summary>The spike's proposal for how a record becomes a verified or self-reported contribution (HLT-08).</summary>
    public enum ContributionClass
    {
        /// <summary>Verifierat bidrag: recorded by the source automatically or during an active session.</summary>
        Verified,

        /// <summary>Egenrapporterat bidrag: entered by hand in the source app.</summary>
        SelfReported,

        /// <summary>The source did not say. How to classify it is an open question (see the ADR).</summary>
        Unknown,
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

        public static ContributionClass Classify(int method) => method switch
        {
            ActivelyRecorded or AutomaticallyRecorded => ContributionClass.Verified,
            ManualEntry => ContributionClass.SelfReported,
            _ => ContributionClass.Unknown,
        };
    }
}
