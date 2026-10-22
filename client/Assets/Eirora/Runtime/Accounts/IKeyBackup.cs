using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Eirora.Client.Accounts
{
    /// <summary>How far a contribution key kept in the platform's backup reaches.</summary>
    public enum KeyBackupReach
    {
        /// <summary>
        /// To the player's other phones on the same platform, end-to-end encrypted: the phone has a screen lock, which
        /// iCloud Keychain and Block Store's end-to-end encrypted backup both need.
        /// </summary>
        EndToEnd,

        /// <summary>
        /// This phone only, without a screen lock: a new phone needs a key transfer or the recovery code (ADR-0008).
        /// </summary>
        ThisPhone,
    }

    /// <summary>
    /// The platform's end-to-end encrypted backup of the contribution key (ADR-0008, TASK-023b): iCloud Keychain on iOS
    /// and Block Store on Android. A new phone on the same platform finds the key there and reads the contribution part
    /// without asking the player. The key never goes anywhere else in the clear.
    /// </summary>
    public interface IKeyBackup
    {
        /// <summary>The value kept under <paramref name="name"/>, or null when there is none.</summary>
        /// <exception cref="KeyBackupException">The platform's backup did not answer.</exception>
        Task<string?> ReadAsync(string name);

        /// <summary>Keeps <paramref name="value"/> under <paramref name="name"/>, replacing what was there, and says how far it reaches.</summary>
        /// <exception cref="KeyBackupException">The platform's backup did not keep it.</exception>
        Task<KeyBackupReach> KeepAsync(string name, string value);

        /// <summary>Removes what is kept under <paramref name="name"/>, on this phone and in the backup.</summary>
        /// <exception cref="KeyBackupException">The platform's backup did not answer.</exception>
        Task ForgetAsync(string name);
    }

    /// <summary>The platform's backup failed; the reason only, never a key.</summary>
    public sealed class KeyBackupException : Exception
    {
        public KeyBackupException(string reason)
            : base(reason)
        {
        }
    }

    public static class KeyBackups
    {
        /// <summary>
        /// The account's contribution key's name in the backup. Only letters, digits and hyphens, which Block Store's
        /// keys allow, and the same on both platforms.
        /// </summary>
        public static string Name(Guid accountId) => "eirora-contribution-key-" + accountId.ToString("N");
    }

    /// <summary>
    /// A backup in memory, shared by the phones of one test as iCloud Keychain or Block Store is shared by one player's
    /// phones. <see cref="Reach"/> is what the phone's screen lock allows.
    /// </summary>
    public sealed class MemoryKeyBackup : IKeyBackup
    {
        private readonly Dictionary<string, string> _values = new();

        public KeyBackupReach Reach { get; set; } = KeyBackupReach.EndToEnd;

        /// <summary>Makes every call fail, as when Google Play services or the keychain does not answer.</summary>
        public bool Failing { get; set; }

        public string? this[string name] => _values.TryGetValue(name, out var value) ? value : null;

        public Task<string?> ReadAsync(string name)
        {
            Fail();
            return Task.FromResult(this[name]);
        }

        public Task<KeyBackupReach> KeepAsync(string name, string value)
        {
            Fail();
            _values[name] = value;
            return Task.FromResult(Reach);
        }

        public Task ForgetAsync(string name)
        {
            Fail();
            _values.Remove(name);
            return Task.CompletedTask;
        }

        private void Fail()
        {
            if (Failing)
            {
                throw new KeyBackupException("The backup does not answer");
            }
        }
    }
}
