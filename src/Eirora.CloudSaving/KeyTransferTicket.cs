using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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

        /// <summary>The longest id a ticket carries; the server's are 22 characters of base64url.</summary>
        public const int MaxIdLength = 64;

        private const string Header = "eirora-key-transfer 1";

        public KeyTransferTicket(string id, byte[] receiverPublicKey, byte[] secret)
        {
            if (!IsId(id))
            {
                throw new ArgumentException($"A transfer id is 1 to {MaxIdLength} letters, digits, '-' or '_'.", nameof(id));
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

        /// <summary>
        /// Six digits from the ticket, shown on both phones so that the player can see that the code the old phone read
        /// is the one the new phone shows, before the key is sent. Anyone who can create a transfer for the account could
        /// otherwise collect the key by getting a code of their own in front of the camera.
        /// </summary>
        public string CheckCode
        {
            get
            {
                byte[] label = Encoding.ASCII.GetBytes("eirora-key-transfer-check " + Id + " ");
                var material = new byte[label.Length + ReceiverPublicKey.Length + Secret.Length];
                label.CopyTo(material, 0);
                ReceiverPublicKey.CopyTo(material, label.Length);
                Secret.CopyTo(material, label.Length + ReceiverPublicKey.Length);
                using var sha = SHA256.Create();
                byte[] hash = sha.ComputeHash(material);
                uint value = ((uint)hash[0] << 24) | ((uint)hash[1] << 16) | ((uint)hash[2] << 8) | hash[3];
                string digits = (value % 1_000_000).ToString("000000", CultureInfo.InvariantCulture);
                return digits.Substring(0, 3) + " " + digits.Substring(3);
            }
        }

        /// <summary>Whether <paramref name="id"/> can be a transfer id: it goes into a request path, so only plain characters.</summary>
        public static bool IsId(string? id)
        {
            if (string.IsNullOrEmpty(id) || id!.Length > MaxIdLength)
            {
                return false;
            }

            foreach (char c in id)
            {
                bool plain = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!plain)
                {
                    return false;
                }
            }

            return true;
        }

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

        /// <summary>
        /// The ticket <see cref="ToText"/> wrote, or null when the text is not one. The text comes from a camera, so
        /// nothing in it is trusted: this never throws.
        /// </summary>
        public static KeyTransferTicket? TryParse(string? text)
        {
            if (text is null)
            {
                return null;
            }

            string[] fields = text.Trim().Split(' ');
            if (fields.Length != 5 || fields[0] + " " + fields[1] != Header || !IsId(fields[2]))
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
