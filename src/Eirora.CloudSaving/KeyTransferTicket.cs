using System;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// What the new phone shows the old one, in a QR code (TASK-023b): the transfer's id on the server and the new
    /// phone's one-time public key. Nothing in it is secret; the old phone must still be signed in to the same account
    /// to deliver the key.
    /// </summary>
    public sealed class KeyTransferTicket
    {
        private const string Header = "eirora-key-transfer 1";

        public KeyTransferTicket(string id, byte[] receiverPublicKey)
        {
            if (string.IsNullOrEmpty(id) || id.Contains(' '))
            {
                throw new ArgumentException("A transfer id is non-empty and has no spaces.", nameof(id));
            }

            if (receiverPublicKey is null || receiverPublicKey.Length != TransferKeys.PublicKeyLength)
            {
                throw new ArgumentException($"A transfer public key has {TransferKeys.PublicKeyLength} bytes.", nameof(receiverPublicKey));
            }

            Id = id;
            ReceiverPublicKey = (byte[])receiverPublicKey.Clone();
        }

        public string Id { get; }

        public byte[] ReceiverPublicKey { get; }

        /// <summary>The ticket as one line of text, for the QR code.</summary>
        public string ToText() => Header + " " + Id + " " + Convert.ToBase64String(ReceiverPublicKey);

        /// <summary>The ticket <see cref="ToText"/> wrote, or null when the text is not one.</summary>
        public static KeyTransferTicket? TryParse(string? text)
        {
            if (text is null)
            {
                return null;
            }

            string[] fields = text.Trim().Split(' ');
            if (fields.Length != 4 || fields[0] + " " + fields[1] != Header)
            {
                return null;
            }

            try
            {
                byte[] publicKey = Convert.FromBase64String(fields[3]);
                return publicKey.Length == TransferKeys.PublicKeyLength ? new KeyTransferTicket(fields[2], publicKey) : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
