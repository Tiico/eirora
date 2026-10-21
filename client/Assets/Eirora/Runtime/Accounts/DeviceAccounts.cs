using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// The app's sign-in configuration, <c>Resources/Accounts/AccountConfig.json</c>: the server and the client ids the
    /// product owner creates at Apple and Google (docs/development.md#inloggning). None of them is a secret.
    /// An empty value turns its part off, and without a server the account is not offered at all.
    /// </summary>
    [Serializable]
    public sealed class AccountConfig
    {
#pragma warning disable CS0649 // Filled in by JsonUtility.
        /// <summary>The server's HTTPS address, such as <c>https://api.example.se</c>.</summary>
        public string server = string.Empty;

        /// <summary>Apple's Services ID for Sign in with Apple on Android, whose return URL is the server's callback.</summary>
        public string appleServicesId = string.Empty;

        /// <summary>Google's OAuth client of type Web, which Credential Manager on Android issues ID tokens to.</summary>
        public string googleWebClientId = string.Empty;

        /// <summary>Google's OAuth client of type iOS.</summary>
        public string googleIosClientId = string.Empty;
#pragma warning restore CS0649

        public bool HasServer => server.StartsWith("https://", StringComparison.Ordinal);

        public static AccountConfig Load()
        {
            var file = Resources.Load<TextAsset>("Accounts/AccountConfig");
            return file is null ? new AccountConfig() : JsonUtility.FromJson<AccountConfig>(file.text) ?? new AccountConfig();
        }
    }

    /// <summary>Puts the account together for the platform the game runs on.</summary>
    public static class DeviceAccounts
    {
        /// <summary>
        /// The account session with this device's providers and keystore, or null where accounts are not offered: in the
        /// editor and desktop players, and in a build without a server or without any provider configured.
        /// </summary>
        public static AccountSession? Create(AccountConfig config)
        {
            if (!config.HasServer)
            {
                return null;
            }

            var http = new UnityHttpTransport();
            var providers = new List<ILoginProvider>();
            ISecureStore store;
#if UNITY_IOS && !UNITY_EDITOR
            // Sign in with Apple first: App Store Review Guideline 4.8 wants it at least as prominent as Google.
            store = new IosKeychain();
            providers.Add(new IosAppleLogin());
            if (config.googleIosClientId.Length > 0)
            {
                providers.Add(new GoogleWebLogin(config.googleIosClientId, new IosWebAuthenticator(), http));
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            store = new AndroidKeystore();
            if (config.googleWebClientId.Length > 0)
            {
                providers.Add(new AndroidGoogleLogin(config.googleWebClientId));
            }

            if (config.appleServicesId.Length > 0)
            {
                providers.Add(new AppleWebLogin(config.appleServicesId, config.server, new AndroidWebAuthenticator()));
            }
#else
            store = new MemorySecureStore();
#endif
            return providers.Count == 0 ? null : new AccountSession(config.server, http, store, providers);
        }
    }
}
