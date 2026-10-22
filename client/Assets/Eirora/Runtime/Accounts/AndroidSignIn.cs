#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// Google sign-in on Android with Credential Manager's "Sign in with Google" (SignInBridge.kt in SignIn.androidlib):
    /// the ID token is issued to the app's Web client id, with the nonce hash in its <c>nonce</c> claim.
    /// </summary>
    public sealed class AndroidGoogleLogin : ILoginProvider
    {
        private readonly string _webClientId;

        public AndroidGoogleLogin(string webClientId) => _webClientId = webClientId;

        public string Provider => Contracts.LoginProviders.Google;

        public async Task<ProviderSignIn> SignInAsync(string nonceHash) =>
            new ProviderSignIn(await AndroidSignInBridge.Call("signInWithGoogle", AndroidSignInBridge.Activity, _webClientId, nonceHash));
    }

    /// <summary>A provider's page in a Custom Tab, answered through the <c>eirora-signin</c> scheme (SignInBridge.kt).</summary>
    public sealed class AndroidWebAuthenticator : IWebAuthenticator
    {
        public Task<string> AuthenticateAsync(string url, string callbackScheme) =>
            AndroidSignInBridge.Call("authenticate", AndroidSignInBridge.Activity, url);
    }

    /// <summary>
    /// Values encrypted with an AES key in the Android Keystore (SecureStore.kt). Auto Backup may copy the ciphertext to a
    /// new phone, but the key never leaves this one, so there the value reads as missing and the player signs in again.
    /// </summary>
    public sealed class AndroidKeystore : ISecureStore
    {
        private const string StoreClass = "com.eirora.signin.SecureStore";

        private readonly AndroidJavaClass _store = new(StoreClass);

        public string? Read(string key) => _store.CallStatic<string?>("read", AndroidSignInBridge.Activity, key);

        public void Write(string key, string value) => _store.CallStatic("write", AndroidSignInBridge.Activity, key, value);

        public void Delete(string key) => _store.CallStatic("delete", AndroidSignInBridge.Activity, key);
    }

    /// <summary>
    /// The contribution key in Block Store (KeyBackup.kt), backed up end to end where the phone has a screen lock.
    /// </summary>
    public sealed class AndroidKeyBackup : IKeyBackup
    {
        private const string BackupClass = "com.eirora.signin.KeyBackup";

        public async Task<string?> ReadAsync(string name)
        {
            string value = await CallAsync("read", name);
            return value.Length == 0 ? null : value;
        }

        public async Task<KeyBackupReach> KeepAsync(string name, string value) =>
            await CallAsync("keep", name, value) == "end-to-end" ? KeyBackupReach.EndToEnd : KeyBackupReach.ThisPhone;

        public Task ForgetAsync(string name) => CallAsync("forget", name);

        private static async Task<string> CallAsync(string method, params object[] arguments)
        {
            var withContext = new object[arguments.Length + 1];
            withContext[0] = AndroidSignInBridge.Activity;
            arguments.CopyTo(withContext, 1);
            try
            {
                return await AndroidSignInBridge.CallOn(BackupClass, method, withContext);
            }
            catch (LoginProviderException e)
            {
                throw new KeyBackupException(e.Message);
            }
        }
    }

    /// <summary>Calls SignInBridge.kt and KeyBackup.kt; each call answers once through a callback on a Java thread.</summary>
    internal static class AndroidSignInBridge
    {
        private const string BridgeClass = "com.eirora.signin.SignInBridge";
        private const string CallbackInterface = "com.eirora.signin.SignInCallback";

        public static AndroidJavaObject Activity
        {
            get
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                return player.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }

        public static Task<string> Call(string method, params object[] arguments) => CallOn(BridgeClass, method, arguments);

        /// <summary>Calls a static method of <paramref name="javaClass"/> that takes a SignInCallback last.</summary>
        public static Task<string> CallOn(string javaClass, string method, params object[] arguments)
        {
            var callback = new Callback();
            var withCallback = new object[arguments.Length + 1];
            arguments.CopyTo(withCallback, 0);
            withCallback[arguments.Length] = callback;
            using var bridge = new AndroidJavaClass(javaClass);
            bridge.CallStatic(method, withCallback);
            return callback.Result;
        }

        /// <summary>Implements SignInCallback. Kept alive until Java has answered.</summary>
        private sealed class Callback : AndroidJavaProxy
        {
            private static readonly HashSet<Callback> s_pending = new();

            private readonly TaskCompletionSource<string> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Callback()
                : base(CallbackInterface)
            {
                lock (s_pending)
                {
                    s_pending.Add(this);
                }
            }

            public Task<string> Result => _result.Task;

            // Dispatching here instead of by reflection keeps IL2CPP stripping from removing the callback methods.
            public override AndroidJavaObject? Invoke(string methodName, object[] args)
            {
                switch (methodName)
                {
                    case "onSuccess":
                        Release();
                        _result.TrySetResult((string)args[0]);
                        return null;
                    case "onCancel":
                        Release();
                        _result.TrySetException(new SignInCancelledException());
                        return null;
                    case "onError":
                        Release();
                        _result.TrySetException(new LoginProviderException((string)args[0]));
                        return null;
                    default:
                        return base.Invoke(methodName, args);
                }
            }

            private void Release()
            {
                lock (s_pending)
                {
                    s_pending.Remove(this);
                }
            }
        }
    }
}
#endif
