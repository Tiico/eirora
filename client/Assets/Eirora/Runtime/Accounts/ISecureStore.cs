using System.Collections.Generic;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// Secrets on the device, in the platform's keystore: the iOS keychain, and on Android values encrypted with a key
    /// in the Android Keystore. Neither is synced or restored to another device, so a moved phone signs in again.
    /// </summary>
    public interface ISecureStore
    {
        /// <summary>The value, or null if there is none or it can no longer be decrypted (for example after a restore).</summary>
        string? Read(string key);

        void Write(string key, string value);

        void Delete(string key);
    }

    /// <summary>A store that forgets on restart: the editor, desktop players and tests.</summary>
    public sealed class MemorySecureStore : ISecureStore
    {
        private readonly Dictionary<string, string> _values = new();

        public string? Read(string key) => _values.TryGetValue(key, out var value) ? value : null;

        public void Write(string key, string value) => _values[key] = value;

        public void Delete(string key) => _values.Remove(key);
    }
}
