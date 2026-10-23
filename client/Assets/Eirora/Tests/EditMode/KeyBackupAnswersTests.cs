using Eirora.Client.Accounts;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// What the iOS keychain's and Block Store's answers mean (TASK-023b): the part of the platform backups that can be
    /// checked without a phone.
    /// </summary>
    public sealed class KeyBackupAnswersTests
    {
        // The OSStatus values of Security/SecBase.h, written out so that a wrong constant in KeychainStatus fails here.
        private const int ErrSecSuccess = 0;
        private const int ErrSecItemNotFound = -25300;
        private const int InteractionNotAllowed = -25308;
        private const int MissingEntitlement = -34018;
        private const int DuplicateItem = -25299;

        [Test]
        public void OnlyAMissingItemReadsAsNoKey()
        {
            Assert.That(KeychainStatus.Read(ErrSecSuccess, "key"), Is.EqualTo("key"));
            Assert.That(KeychainStatus.Read(ErrSecItemNotFound, null), Is.Null);

            Assert.That(() => KeychainStatus.Read(InteractionNotAllowed, null), Throws.TypeOf<KeyBackupException>().With.Message.EqualTo("Keychain error -25308"));
            Assert.That(() => KeychainStatus.Read(MissingEntitlement, null), Throws.TypeOf<KeyBackupException>().With.Message.EqualTo("Keychain error -34018"));
            Assert.That(() => KeychainStatus.Read(ErrSecSuccess, null), Throws.TypeOf<KeyBackupException>(), "success without a value is not \"no key\"");
        }

        [Test]
        public void AKeptKeyReachesAsFarAsThePasscodeAllowsOrIsUnknown()
        {
            Assert.That(KeychainStatus.Kept(ErrSecSuccess, 1), Is.EqualTo(KeyBackupReach.EndToEnd));
            Assert.That(KeychainStatus.Kept(ErrSecSuccess, 0), Is.EqualTo(KeyBackupReach.ThisPhone));
            Assert.That(KeychainStatus.Kept(ErrSecSuccess, -1), Is.Null, "the phone could not tell whether it has a passcode");

            Assert.That(() => KeychainStatus.Kept(DuplicateItem, 1), Throws.TypeOf<KeyBackupException>().With.Message.EqualTo("Keychain error -25299"));
        }

        [Test]
        public void NothingToRemoveCountsAsRemoved()
        {
            Assert.That(() => KeychainStatus.Forgot(ErrSecSuccess), Throws.Nothing);
            Assert.That(() => KeychainStatus.Forgot(ErrSecItemNotFound), Throws.Nothing);

            Assert.That(() => KeychainStatus.Forgot(InteractionNotAllowed), Throws.TypeOf<KeyBackupException>().With.Message.EqualTo("Keychain error -25308"));
        }

        [Test]
        public void BlockStoresAnswersAreItsConstantsAndNothingElse()
        {
            Assert.That(BlockStoreAnswer.Kept("end-to-end"), Is.EqualTo(KeyBackupReach.EndToEnd));
            Assert.That(BlockStoreAnswer.Kept("this-phone"), Is.EqualTo(KeyBackupReach.ThisPhone));
            Assert.That(BlockStoreAnswer.Kept("unknown"), Is.Null);

            Assert.That(() => BlockStoreAnswer.Kept(""), Throws.TypeOf<KeyBackupException>());
            Assert.That(() => BlockStoreAnswer.Kept("END_TO_END"), Throws.TypeOf<KeyBackupException>(), "a renamed constant in KeyBackup.kt shows instead of silencing the sheet");
        }
    }
}
