#if UNITY_IOS && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AOT;

namespace Eirora.Client.Accounts
{
    /// <summary>Native Sign in with Apple (EiroraSignIn.mm), with Apple's authorization code for deleting the account.</summary>
    public sealed class IosAppleLogin : ILoginProvider
    {
        public string Provider => Contracts.LoginProviders.Apple;

        public async Task<ProviderSignIn> SignInAsync(string nonceHash)
        {
            var (idToken, code) = await IosSignIn.Request(id => IosSignIn.EiroraSignInWithApple(id, nonceHash));
            return new ProviderSignIn(idToken, code);
        }
    }

    /// <summary>A provider's page in ASWebAuthenticationSession (EiroraSignIn.mm).</summary>
    public sealed class IosWebAuthenticator : IWebAuthenticator
    {
        public async Task<string> AuthenticateAsync(string url, string callbackScheme) =>
            (await IosSignIn.Request(id => IosSignIn.EiroraWebAuthenticate(id, url, callbackScheme))).First;
    }

    /// <summary>The iOS keychain, this device only (EiroraSignIn.mm).</summary>
    public sealed class IosKeychain : ISecureStore
    {
        public string? Read(string key) => IosSignIn.EiroraKeychainRead(key);

        public void Write(string key, string value)
        {
            if (!IosSignIn.EiroraKeychainWrite(key, value))
            {
                throw new System.InvalidOperationException("The keychain did not keep the value.");
            }
        }

        public void Delete(string key) => IosSignIn.EiroraKeychainDelete(key);
    }

    /// <summary>
    /// The contribution key in iCloud Keychain (EiroraKeyBackup.mm); the keychain answers at once, and
    /// <see cref="KeychainStatus"/> says what its answers mean.
    /// </summary>
    public sealed class IosKeyBackup : IKeyBackup
    {
        public Task<string?> ReadAsync(string name) => Answer(() =>
        {
            string? value = EiroraKeyBackupRead(name, out int status);
            return KeychainStatus.Read(status, value);
        });

        public Task<KeyBackupReach?> KeepAsync(string name, string value) =>
            Answer(() => KeychainStatus.Kept(EiroraKeyBackupKeep(name, value, out int reach), reach));

        public Task ForgetAsync(string name) => Answer(() =>
        {
            KeychainStatus.Forgot(EiroraKeyBackupForget(name));
            return true;
        });

        private static Task<T> Answer<T>(System.Func<T> call)
        {
            try
            {
                return Task.FromResult(call());
            }
            catch (KeyBackupException e)
            {
                return Task.FromException<T>(e);
            }
        }

        [DllImport("__Internal")]
        private static extern string? EiroraKeyBackupRead(string name, out int status);

        [DllImport("__Internal")]
        private static extern int EiroraKeyBackupKeep(string name, string value, out int reach);

        [DllImport("__Internal")]
        private static extern int EiroraKeyBackupForget(string name);
    }

    /// <summary>The native calls and their answers, which arrive on the main thread.</summary>
    internal static class IosSignIn
    {
        private static readonly Dictionary<int, TaskCompletionSource<(string First, string? Second)>> s_pending = new();
        private static int s_nextId;
        private static bool s_registered;

        private delegate void Callback(int requestId, int status, string? first, string? second);

        public static Task<(string First, string? Second)> Request(System.Action<int> start)
        {
            if (!s_registered)
            {
                EiroraSignInSetCallback(Answer);
                s_registered = true;
            }

            int id = ++s_nextId;
            var answer = new TaskCompletionSource<(string, string?)>(TaskCreationOptions.RunContinuationsAsynchronously);
            s_pending[id] = answer;
            start(id);
            return answer.Task;
        }

        [MonoPInvokeCallback(typeof(Callback))]
        private static void Answer(int requestId, int status, string? first, string? second)
        {
            if (!s_pending.Remove(requestId, out var answer))
            {
                return;
            }

            switch (status)
            {
                case 0:
                    answer.SetResult((first ?? string.Empty, second));
                    break;
                case 1:
                    answer.SetException(new SignInCancelledException());
                    break;
                default:
                    answer.SetException(new LoginProviderException(first ?? "The sign-in failed"));
                    break;
            }
        }

        [DllImport("__Internal")]
        private static extern void EiroraSignInSetCallback(Callback callback);

        [DllImport("__Internal")]
        internal static extern void EiroraSignInWithApple(int requestId, string nonceHash);

        [DllImport("__Internal")]
        internal static extern void EiroraWebAuthenticate(int requestId, string url, string callbackScheme);

        [DllImport("__Internal")]
        internal static extern string? EiroraKeychainRead(string key);

        [DllImport("__Internal")]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool EiroraKeychainWrite(string key, string value);

        [DllImport("__Internal")]
        internal static extern void EiroraKeychainDelete(string key);
    }
}
#endif
