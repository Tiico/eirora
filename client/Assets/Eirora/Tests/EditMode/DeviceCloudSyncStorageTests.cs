using System;
using System.IO;
using Eirora.Client.Accounts;
using Eirora.CloudSaving;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>The contribution key in the keystore and the cloud save version beside the save (TASK-023).</summary>
    public sealed class DeviceCloudSyncStorageTests
    {
        private static readonly Guid Account = Guid.Parse("019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f");

        private string _folder = string.Empty;
        private MemorySecureStore _keystore = null!;
        private DeviceCloudSyncStorage _storage = null!;

        private string StatePath => Path.Combine(_folder, DeviceCloudSyncStorage.StateFile);

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "eirora-storage-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _keystore = new MemorySecureStore();
            _storage = new DeviceCloudSyncStorage(_keystore, StatePath);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_folder, recursive: true);

        [Test]
        public void TheKeyLivesInTheKeystoreUnderTheAccount()
        {
            var key = ContributionKey.Generate(2);

            _storage.KeepKey(Account, key);

            Assert.That(_keystore.Read("eirora.cloud.contribution-key.019a0a4e6b5c7d2e9f102a3b4c5d6e7f"), Is.EqualTo(key.ToText()));
            Assert.That(_storage.Key(Account)!.ToText(), Is.EqualTo(key.ToText()));
            Assert.That(_storage.Key(Guid.NewGuid()), Is.Null);

            _storage.ForgetKey(Account);

            Assert.That(_storage.Key(Account), Is.Null);
        }

        [Test]
        public void TheStateLivesInAFileBesideTheSaveAndIsOnlyForItsAccount()
        {
            _storage.KeepState(new CloudSyncState(Account, 7, 2));

            Assert.That(File.ReadAllText(StatePath), Is.EqualTo("eirora-cloud 1\n019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f 7 2\n"));
            var read = new DeviceCloudSyncStorage(_keystore, StatePath).State(Account)!;
            Assert.That((read.Version, read.KeyEpoch), Is.EqualTo((7L, 2)));
            Assert.That(_storage.State(Guid.NewGuid()), Is.Null);

            _storage.KeepState(new CloudSyncState(Account, 8, 2));
            Assert.That(_storage.State(Account)!.Version, Is.EqualTo(8));

            _storage.ForgetState();

            Assert.That(File.Exists(StatePath), Is.False);
            Assert.That(_storage.State(Account), Is.Null);
        }

        [Test]
        public void ADamagedStateFileCountsAsNone()
        {
            File.WriteAllText(StatePath, "eirora-cloud 1\nbroken\n");

            Assert.That(_storage.State(Account), Is.Null);
        }

        [Test]
        public void WithoutAPathTheStateStaysInMemory()
        {
            var storage = new DeviceCloudSyncStorage(_keystore, null);

            storage.KeepState(new CloudSyncState(Account, 3, 1));

            Assert.That(storage.State(Account)!.Version, Is.EqualTo(3));
            Assert.That(Directory.GetFiles(_folder), Is.Empty);
        }
    }
}
