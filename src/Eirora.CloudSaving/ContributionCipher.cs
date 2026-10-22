using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// Seals the cloud save's contribution part on the device with XChaCha20-Poly1305 (ADR-0008, D-57), so that the
    /// server stores ciphertext it cannot read. The account id, the key epoch and the version the upload becomes are
    /// the associated data, so the server, or whoever reaches its database, cannot move a part between players, key
    /// epochs or versions without the device noticing.
    /// </summary>
    /// <remarks>
    /// A sealed part is UTF-8 text <c>eirora-sealed 1\n</c>, then the random 192-bit nonce, then the ciphertext with
    /// the 128-bit tag after it. A random nonce of that size is safe for any number of parts under one key, which is
    /// why XChaCha20 rather than ChaCha20 or AES-GCM. The cipher comes from BouncyCastle, which runs the same under
    /// .NET and Unity's IL2CPP.
    /// </remarks>
    public static class ContributionCipher
    {
        public const int NonceLength = 24;
        public const int TagLength = 16;

        private const string Header = "eirora-sealed 1\n";

        private static readonly byte[] s_header = Encoding.UTF8.GetBytes(Header);

        /// <summary>Seals <paramref name="contributions"/> for <paramref name="version"/> of the account's cloud save.</summary>
        public static byte[] Seal(ContributionKey key, Guid accountId, long version, byte[] contributions)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (contributions is null)
            {
                throw new ArgumentNullException(nameof(contributions));
            }

            var nonce = new byte[NonceLength];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(nonce);
            }

            var cipher = new XChaCha20Poly1305();
            cipher.Init(true, new AeadParameters(new KeyParameter(key.ToBytes()), TagLength * 8, nonce, AssociatedData(accountId, key.Epoch, version)));
            var output = new byte[s_header.Length + NonceLength + cipher.GetOutputSize(contributions.Length)];
            Buffer.BlockCopy(s_header, 0, output, 0, s_header.Length);
            Buffer.BlockCopy(nonce, 0, output, s_header.Length, NonceLength);
            int at = s_header.Length + NonceLength;
            at += cipher.ProcessBytes(contributions, 0, contributions.Length, output, at);
            cipher.DoFinal(output, at);
            return output;
        }

        /// <summary>
        /// The contribution part <paramref name="sealedContributions"/> holds, or null when it was not sealed with this
        /// key for this account and version, or was changed since: such a part is never used (threat model).
        /// </summary>
        public static byte[]? Open(ContributionKey key, Guid accountId, long version, byte[] sealedContributions)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (sealedContributions is null)
            {
                throw new ArgumentNullException(nameof(sealedContributions));
            }

            int least = s_header.Length + NonceLength + TagLength;
            if (sealedContributions.Length < least || !StartsWithHeader(sealedContributions))
            {
                return null;
            }

            var nonce = new byte[NonceLength];
            Buffer.BlockCopy(sealedContributions, s_header.Length, nonce, 0, NonceLength);
            int offset = s_header.Length + NonceLength;
            int length = sealedContributions.Length - offset;
            var cipher = new XChaCha20Poly1305();
            cipher.Init(false, new AeadParameters(new KeyParameter(key.ToBytes()), TagLength * 8, nonce, AssociatedData(accountId, key.Epoch, version)));
            var output = new byte[cipher.GetOutputSize(length)];
            try
            {
                int at = cipher.ProcessBytes(sealedContributions, offset, length, output, 0);
                at += cipher.DoFinal(output, at);
                if (at != output.Length)
                {
                    Array.Resize(ref output, at);
                }

                return output;
            }
            catch (InvalidCipherTextException)
            {
                return null;
            }
        }

        /// <summary>The associated data: what the part must stay bound to, in the order ADR-0008 names them.</summary>
        internal static byte[] AssociatedData(Guid accountId, int keyEpoch, long version) => Encoding.UTF8.GetBytes(
            "eirora-contributions " + accountId.ToString("D") + " " + keyEpoch.ToString(CultureInfo.InvariantCulture) + " " + version.ToString(CultureInfo.InvariantCulture));

        private static bool StartsWithHeader(byte[] bytes)
        {
            for (int i = 0; i < s_header.Length; i++)
            {
                if (bytes[i] != s_header[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
