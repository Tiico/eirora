using System;
using System.Security.Cryptography;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// What the new phone shows the old one, in a QR code (TASK-023b): the transfer's id on the server, the new phone's
    /// one-time public key, and a secret the server never sees. The secret is what makes a sealed key the old phone's:
    /// the server stores the id and the public key, so without the secret it, or anyone with the account's token,
    /// could seal a key of its own choosing for the new phone. The old phone must still be signed in to the same account
    /// to deliver.
    /// </summary>
    public sealed class KeyTransferTicket
    {
        public const int SecretLength = 16;

        private const string Header = "eirora-key-transfer 1";

        public KeyTransferTicket(string id, byte[] receiverPublicKey, byte[] secret)
        {
            if (string.IsNullOrEmpty(id) || id.Contains(' '))
            {
                throw new ArgumentException("A transfer id is non-empty and has no spaces.", nameof(id));
            }

            if (receiverPublicKey is null || receiverPublicKey.Length != TransferKeys.PublicKeyLength)
            {
                throw new ArgumentException($"A transfer public key has {TransferKeys.PublicKeyLength} bytes.", nameof(receiverPublicKey));
            }

            if (secret is null || secret.Length != SecretLength)
            {
                throw new ArgumentException($"A ticket secret has {SecretLength} bytes.", nameof(secret));
            }

            Id = id;
            ReceiverPublicKey = (byte[])receiverPublicKey.Clone();
            Secret = (byte[])secret.Clone();
        }

        public string Id { get; }

        public byte[] ReceiverPublicKey { get; }

        /// <summary>The 128-bit secret that only the two phones hold; it travels in the QR code and never to the server.</summary>
        public byte[] Secret { get; }

        /// <summary>A new secret, from the platform's cryptographic randomness.</summary>
        public static byte[] NewSecret()
        {
            var secret = new byte[SecretLength];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(secret);
            }

            return secret;
        }

        /// <summary>The ticket as one line of text, for the QR code.</summary>
        public string ToText() => Header + " " + Id + " " + Convert.ToBase64String(ReceiverPublicKey) + " " + Convert.ToBase64String(Secret);

        /// <summary>The ticket <see cref="ToText"/> wrote, or null when the text is not one.</summary>
        public static KeyTransferTicket? TryParse(string? text)
        {
            if (text is null)
            {
                return null;
            }

            string[] fields = text.Trim().Split(' ');
            if (fields.Length != 5 || fields[0] + " " + fields[1] != Header)
            {
                return null;
            }

            try
            {
                byte[] publicKey = Convert.FromBase64String(fields[3]);
                byte[] secret = Convert.FromBase64String(fields[4]);
                return publicKey.Length == TransferKeys.PublicKeyLength && secret.Length == SecretLength
                    ? new KeyTransferTicket(fields[2], publicKey, secret)
                    : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
