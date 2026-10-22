using System;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// Seals the contribution key for one other phone (ADR-0008, TASK-023b): the old phone seals it to the new phone's
    /// one-time X25519 public key from the ticket, the server relays the sealed bytes, and only the new phone's private
    /// key opens them. The account and the transfer are the associated data, so a sealed key cannot be replayed into
    /// another transfer.
    /// </summary>
    /// <remarks>
    /// A sealed key is UTF-8 text <c>eirora-transfer 1\n</c>, the sender's one-time X25519 public key (32 bytes), a
    /// random 192-bit nonce, then XChaCha20-Poly1305 over the key's text with the 128-bit tag after it. The cipher key
    /// is HKDF-SHA256 of the X25519 agreement, salted with both public keys.
    /// </remarks>
    public static class KeyTransferCipher
    {
        private const string Header = "eirora-transfer 1\n";
        private const int NonceLength = 24;
        private const int TagLength = 16;

        private static readonly byte[] s_header = Encoding.UTF8.GetBytes(Header);
        private static readonly byte[] s_info = Encoding.UTF8.GetBytes("eirora-key-transfer");

        /// <summary>Seals <paramref name="key"/> for the phone that holds the private half of <paramref name="receiverPublicKey"/>.</summary>
        /// <exception cref="ArgumentException">The public key is not one.</exception>
        public static byte[] Seal(ContributionKey key, byte[] receiverPublicKey, Guid accountId, string transferId)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var sender = TransferKeys.Generate();
            byte[] cipherKey = CipherKey(sender.SharedSecretWith(receiverPublicKey), receiverPublicKey, sender.PublicKey)
                ?? throw new ArgumentException("Not a usable transfer public key.", nameof(receiverPublicKey));

            var nonce = new byte[NonceLength];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(nonce);
            }

            byte[] plain = Encoding.UTF8.GetBytes(key.ToText());
            var cipher = new XChaCha20Poly1305();
            cipher.Init(true, new AeadParameters(new KeyParameter(cipherKey), TagLength * 8, nonce, AssociatedData(accountId, transferId)));
            var output = new byte[s_header.Length + TransferKeys.PublicKeyLength + NonceLength + cipher.GetOutputSize(plain.Length)];
            Buffer.BlockCopy(s_header, 0, output, 0, s_header.Length);
            Buffer.BlockCopy(sender.PublicKey, 0, output, s_header.Length, TransferKeys.PublicKeyLength);
            Buffer.BlockCopy(nonce, 0, output, s_header.Length + TransferKeys.PublicKeyLength, NonceLength);
            int at = s_header.Length + TransferKeys.PublicKeyLength + NonceLength;
            at += cipher.ProcessBytes(plain, 0, plain.Length, output, at);
            cipher.DoFinal(output, at);
            return output;
        }

        /// <summary>
        /// The key <paramref name="sealedKey"/> holds, or null when it was not sealed for <paramref name="receiver"/> in
        /// this account's transfer, or was changed since.
        /// </summary>
        public static ContributionKey? Open(TransferKeys receiver, Guid accountId, string transferId, byte[] sealedKey)
        {
            if (receiver is null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (sealedKey is null)
            {
                throw new ArgumentNullException(nameof(sealedKey));
            }

            int least = s_header.Length + TransferKeys.PublicKeyLength + NonceLength + TagLength;
            if (sealedKey.Length < least || !StartsWithHeader(sealedKey))
            {
                return null;
            }

            var senderPublicKey = new byte[TransferKeys.PublicKeyLength];
            Buffer.BlockCopy(sealedKey, s_header.Length, senderPublicKey, 0, TransferKeys.PublicKeyLength);
            var nonce = new byte[NonceLength];
            Buffer.BlockCopy(sealedKey, s_header.Length + TransferKeys.PublicKeyLength, nonce, 0, NonceLength);
            byte[]? cipherKey = CipherKey(receiver.SharedSecretWith(senderPublicKey), receiver.PublicKey, senderPublicKey);
            if (cipherKey is null)
            {
                return null;
            }

            int offset = s_header.Length + TransferKeys.PublicKeyLength + NonceLength;
            int length = sealedKey.Length - offset;
            var cipher = new XChaCha20Poly1305();
            cipher.Init(false, new AeadParameters(new KeyParameter(cipherKey), TagLength * 8, nonce, AssociatedData(accountId, transferId)));
            var output = new byte[cipher.GetOutputSize(length)];
            try
            {
                int at = cipher.ProcessBytes(sealedKey, offset, length, output, 0);
                at += cipher.DoFinal(output, at);
                return ContributionKey.TryParse(Encoding.UTF8.GetString(output, 0, at));
            }
            catch (InvalidCipherTextException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null; // Not UTF-8.
            }
        }

        internal static byte[] AssociatedData(Guid accountId, string transferId) =>
            Encoding.UTF8.GetBytes("eirora-key-transfer " + accountId.ToString("D") + " " + transferId);

        /// <summary>HKDF-SHA256 of the agreement, salted with the receiver's and the sender's public keys in that order.</summary>
        private static byte[]? CipherKey(byte[]? sharedSecret, byte[] receiverPublicKey, byte[] senderPublicKey)
        {
            if (sharedSecret is null)
            {
                return null;
            }

            var salt = new byte[receiverPublicKey.Length + senderPublicKey.Length];
            Buffer.BlockCopy(receiverPublicKey, 0, salt, 0, receiverPublicKey.Length);
            Buffer.BlockCopy(senderPublicKey, 0, salt, receiverPublicKey.Length, senderPublicKey.Length);
            var hkdf = new HkdfBytesGenerator(new Sha256Digest());
            hkdf.Init(new HkdfParameters(sharedSecret, salt, s_info));
            var key = new byte[ContributionKey.Length];
            hkdf.GenerateBytes(key, 0, key.Length);
            return key;
        }

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
