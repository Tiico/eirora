namespace Eirora.Client.Accounts
{
    /// <summary>
    /// What the iOS keychain's answers mean (EiroraKeyBackup.mm), apart from the native calls so that the tests can check
    /// them. A failure carries the keychain's OSStatus, which holds nothing of the key, so that it can be told apart on a
    /// device.
    /// </summary>
    public static class KeychainStatus
    {
        public const int Success = 0;
        public const int ItemNotFound = -25300;

        /// <summary>The value read, or null when there is none.</summary>
        /// <exception cref="KeyBackupException">The keychain failed, or said it succeeded without a value.</exception>
        public static string? Read(int status, string? value) => status switch
        {
            Success => value ?? throw new KeyBackupException("The keychain answered without a value"),
            ItemNotFound => null,
            _ => throw Failed(status),
        };

        /// <summary>
        /// How far a kept key reaches: the native reach is 1 with a passcode, 0 without one, and anything else when the
        /// phone cannot tell.
        /// </summary>
        /// <exception cref="KeyBackupException">The keychain did not keep it.</exception>
        public static KeyBackupReach? Kept(int status, int reach)
        {
            if (status != Success)
            {
                throw Failed(status);
            }

            return reach switch
            {
                1 => KeyBackupReach.EndToEnd,
                0 => KeyBackupReach.ThisPhone,
                _ => null,
            };
        }

        /// <summary>Checks a removal; nothing to remove counts as removed.</summary>
        /// <exception cref="KeyBackupException">The keychain did not remove it.</exception>
        public static void Forgot(int status)
        {
            if (status is not (Success or ItemNotFound))
            {
                throw Failed(status);
            }
        }

        private static KeyBackupException Failed(int status) => new($"Keychain error {status}");
    }

    /// <summary>What Block Store's answers to a keep mean (the constants in KeyBackup.kt), apart from JNI so that the tests can check them.</summary>
    public static class BlockStoreAnswer
    {
        /// <summary>How far a kept key reaches; null when Block Store could not say whether it encrypts end to end.</summary>
        /// <exception cref="KeyBackupException">An answer KeyBackup.kt never gives, so that a gap between the two shows.</exception>
        public static KeyBackupReach? Kept(string answer) => answer switch
        {
            "end-to-end" => KeyBackupReach.EndToEnd,
            "this-phone" => KeyBackupReach.ThisPhone,
            "unknown" => null,
            _ => throw new KeyBackupException($"Block Store answered \"{answer}\" to a keep"),
        };
    }
}
