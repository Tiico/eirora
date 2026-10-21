using System;
using System.Security.Cryptography;
using System.Text;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// The secret a sign-in is bound to (ADR-0008, threat model: replay): the provider gets its SHA-256 as lowercase hex
    /// and puts that in the ID token, and the server only accepts the token together with the nonce itself.
    /// </summary>
    public static class Nonce
    {
        /// <summary>256 random bits, base64url without padding (43 characters).</summary>
        public static string Create() => Random(32);

        /// <summary>The SHA-256 of <paramref name="nonce"/> as lowercase hex, which the provider is given.</summary>
        public static string Hash(string nonce)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(nonce));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                hex.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }

        /// <summary>Random bytes as base64url without padding, for nonces, states and PKCE verifiers.</summary>
        public static string Random(int bytes)
        {
            var buffer = new byte[bytes];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(buffer);
            }

            return Base64Url(buffer);
        }

        public static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
