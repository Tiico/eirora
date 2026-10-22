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
    /// one-time X25519 public key and the secret from the ticket, the server relays the sealed bytes, and only the new
    /// phone's private key and the secret open them. The server stores the public key but never sees the secret, so
    /// neither it nor anyone with the account's token can seal a key the new phone would keep. The account and the
    /// transfer are the associated data, so a sealed key cannot be replayed into another transfer.
    /// </summary>
    /// <remarks>
    /// A sealed key is UTF-8 text <c>eirora-transfer 1\n</c>, the sender's one-time X25519 public key (32 bytes), a
    /// random 192-bit nonce, then XChaCha20-Poly1305 over the key's text with the 128-bit tag after it. The cipher key
    /// is HKDF-SHA256 over the X25519 agreement followed by the ticket's secret, salted with both public keys.
    /// Key material is cleared from the buffers once used.
    /// </remarks>
    public static class KeyTransferCipher
    {
        private const string Header = "eirora-transfer 1\n";
        private const int NonceLength = 24;
        private const int TagLength = 16;

        private static readonly byte[] s_header = Encoding.UTF8.GetBytes(Header);
        private static readonly byte[] s_info = Encoding.UTF8.GetBytes("eirora-key-transfer");

        /// <summary>Seals <paramref name="key"/> for the phone that showed <paramref name="ticket"/>.</summary>
        /// <exception cref="ArgumentException">The ticket's public key is not usable.</exception>
        public static byte[] Seal(ContributionKey key, KeyTransferTicket ticket, Guid accountId)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (ticket is null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }

            var sender = TransferKeys.Generate();
            byte[] cipherKey = CipherKey(sender.SharedSecretWith(ticket.ReceiverPublicKey), ticket.Secret, ticket.ReceiverPublicKey, sender.PublicKey)
                ?? throw new ArgumentException("Not a usable transfer public key.", nameof(ticket));

            var nonce = new byte[NonceLength];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(nonce);
            }

            byte[] plain = Encoding.UTF8.GetBytes(key.ToText());
            try
            {
                var cipher = new XChaCha20Poly1305();
                cipher.Init(true, new AeadParameters(new KeyParameter(cipherKey), TagLength * 8, nonce, AssociatedData(accountId, ticket.Id)));
                var output = new byte[s_header.Length + TransferKeys.PublicKeyLength + NonceLength + cipher.GetOutputSize(plain.Length)];
                Buffer.BlockCopy(s_header, 0, output, 0, s_header.Length);
                Buffer.BlockCopy(sender.PublicKey, 0, output, s_header.Length, TransferKeys.PublicKeyLength);
                Buffer.BlockCopy(nonce, 0, output, s_header.Length + TransferKeys.PublicKeyLength, NonceLength);
                int at = s_header.Length + TransferKeys.PublicKeyLength + NonceLength;
                at += cipher.ProcessBytes(plain, 0, plain.Length, output, at);
                cipher.DoFinal(output, at);
                return output;
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
                Array.Clear(cipherKey, 0, cipherKey.Length);
            }
        }

        /// <summary>
        /// The key <paramref name="sealedKey"/> holds, or null when it was not sealed for <paramref name="receiver"/>
        /// with the secret of <paramref name="ticket"/> in this account's transfer, or was changed since.
        /// </summary>
        public static ContributionKey? Open(TransferKeys receiver, KeyTransferTicket ticket, Guid accountId, byte[] sealedKey)
        {
            if (receiver is null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (ticket is null)
            {
                throw new ArgumentNullException(nameof(ticket));
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
            byte[]? cipherKey = CipherKey(receiver.SharedSecretWith(senderPublicKey), ticket.Secret, receiver.PublicKey, senderPublicKey);
            if (cipherKey is null)
            {
                return null;
            }

            int offset = s_header.Length + TransferKeys.PublicKeyLength + NonceLength;
            int length = sealedKey.Length - offset;
            var cipher = new XChaCha20Poly1305();
            cipher.Init(false, new AeadParameters(new KeyParameter(cipherKey), TagLength * 8, nonce, AssociatedData(accountId, ticket.Id)));
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
            finally
            {
                Array.Clear(output, 0, output.Length);
                Array.Clear(cipherKey, 0, cipherKey.Length);
            }
        }

        internal static byte[] AssociatedData(Guid accountId, string transferId) =>
            Encoding.UTF8.GetBytes("eirora-key-transfer " + accountId.ToString("D") + " " + transferId);

        /// <summary>
        /// HKDF-SHA256 over the agreement followed by the ticket's secret, salted with the receiver's and the sender's
        /// public keys in that order.
        /// </summary>
        private static byte[]? CipherKey(byte[]? sharedSecret, byte[] ticketSecret, byte[] receiverPublicKey, byte[] senderPublicKey)
        {
            if (sharedSecret is null)
            {
                return null;
            }

            var ikm = new byte[sharedSecret.Length + ticketSecret.Length];
            Buffer.BlockCopy(sharedSecret, 0, ikm, 0, sharedSecret.Length);
            Buffer.BlockCopy(ticketSecret, 0, ikm, sharedSecret.Length, ticketSecret.Length);
            var salt = new byte[receiverPublicKey.Length + senderPublicKey.Length];
            Buffer.BlockCopy(receiverPublicKey, 0, salt, 0, receiverPublicKey.Length);
            Buffer.BlockCopy(senderPublicKey, 0, salt, receiverPublicKey.Length, senderPublicKey.Length);
            try
            {
                var hkdf = new HkdfBytesGenerator(new Sha256Digest());
                hkdf.Init(new HkdfParameters(ikm, salt, s_info));
                var key = new byte[ContributionKey.Length];
                hkdf.GenerateBytes(key, 0, key.Length);
                return key;
            }
            finally
            {
                Array.Clear(sharedSecret, 0, sharedSecret.Length);
                Array.Clear(ikm, 0, ikm.Length);
            }
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
