using NUnit.Framework;
using UnityEngine;

namespace Eirora.Spike.HealthConnect.Tests
{
    /// <summary>The JSON field names RecordJson.kt writes must be the ones the C# models read.</summary>
    public sealed class BridgeContractTests
    {
        // Shaped exactly like RecordJson.toJson output for a sleep session.
        private const string SleepPage = @"{""records"":[{""type"":""sleep"",""startMs"":1760396400000,""endMs"":1760421600000,
            ""hasStartOffset"":true,""startOffsetSec"":7200,""hasEndOffset"":true,""endOffsetSec"":7200,""title"":"""",
            ""stages"":[{""stage"":4,""stageName"":""LIGHT"",""startMs"":1760396400000,""endMs"":1760400000000}],
            ""id"":""5f1c"",""origin"":""com.sec.android.app.shealth"",""recordingMethod"":2,""recordingMethodName"":""AUTOMATICALLY_RECORDED"",
            ""lastModifiedMs"":1760422000000,""clientRecordId"":"""",""clientRecordVersion"":0,""hasDevice"":true,""deviceType"":1,
            ""deviceTypeName"":""WATCH"",""deviceManufacturer"":""Samsung"",""deviceModel"":""SM-R960""}],""truncated"":false}";

        [Test]
        public void ReadsARecordPage()
        {
            var record = JsonUtility.FromJson<RecordPage>(SleepPage).records[0];

            Assert.That(record.Habit, Is.EqualTo(Habit.Sleep));
            Assert.That(record.Value, Is.EqualTo(7 * 3600));
            Assert.That(record.startOffsetSec, Is.EqualTo(7200));
            Assert.That(record.stages[0].stageName, Is.EqualTo("LIGHT"));
            Assert.That(record.origin, Is.EqualTo("com.sec.android.app.shealth"));
            Assert.That(RecordingMethods.Classify(record.recordingMethod), Is.EqualTo(ContributionClass.Verified));
            Assert.That(record.deviceTypeName, Is.EqualTo("WATCH"));
        }

        [Test]
        public void ReadsStatusWithoutOptionalFields()
        {
            var status = JsonUtility.FromJson<HealthStatus>(@"{""sdkStatus"":""SDK_UNAVAILABLE"",""androidSdk"":33,""androidRelease"":""13"",""deviceModel"":""x"",""providerVersions"":[]}");

            Assert.That(status.sdkStatus, Is.EqualTo("SDK_UNAVAILABLE"));
            Assert.That(status.granted, Is.Empty);
            Assert.That(status.backgroundFeature, Is.EqualTo(""));
        }

        [TestCase(RecordingMethods.ActivelyRecorded, ContributionClass.Verified)]
        [TestCase(RecordingMethods.AutomaticallyRecorded, ContributionClass.Verified)]
        [TestCase(RecordingMethods.ManualEntry, ContributionClass.SelfReported)]
        [TestCase(RecordingMethods.Unknown, ContributionClass.Unknown)]
        public void ClassifiesRecordingMethods(int method, ContributionClass expected)
        {
            Assert.That(RecordingMethods.Classify(method), Is.EqualTo(expected));
        }
    }
}
