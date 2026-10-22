using System;
using System.Globalization;
using System.Security.Cryptography;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// The contribution key (GLOSSARY: Bidragsnyckel, ADR-0008, D-57): a random 256-bit key that only the player's own
    /// devices hold, with the key epoch it belongs to. It encrypts the cloud save's contribution part, so the server
    /// never reads the self-care per game day. Losing it costs the contribution history, never the world (D-101).
    /// </summary>
    public sealed class ContributionKey
    {
        public const int Length = 32;

        private readonly byte[] _bytes;

        public ContributionKey(int epoch, byte[] bytes)
        {
            if (epoch < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(epoch), "Key epochs start at 1.");
            }

            if (bytes is null || bytes.Length != Length)
            {
                throw new ArgumentException($"A contribution key has {Length} bytes.", nameof(bytes));
            }

            Epoch = epoch;
            _bytes = (byte[])bytes.Clone();
        }

        /// <summary>The key epoch (GLOSSARY: Nyckelepok), from 1; a device without the key begins the next one.</summary>
        public int Epoch { get; }

        /// <summary>A new random key for <paramref name="epoch"/>, from the platform's cryptographic randomness.</summary>
        public static ContributionKey Generate(int epoch)
        {
            var bytes = new byte[Length];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return new ContributionKey(epoch, bytes);
        }

        /// <summary>The key as one line for the device's secure store: the epoch and the key in base64.</summary>
        public string ToText() => Epoch.ToString(CultureInfo.InvariantCulture) + " " + Convert.ToBase64String(_bytes);

        /// <summary>The key <see cref="ToText"/> wrote, or null when the text is not one.</summary>
        public static ContributionKey? TryParse(string? text)
        {
            if (text is null)
            {
                return null;
            }

            string[] fields = text.Trim().Split(' ');
            if (fields.Length != 2 || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out int epoch) || epoch < 1)
            {
                return null;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(fields[1]);
                return bytes.Length == Length ? new ContributionKey(epoch, bytes) : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>A copy of the key's bytes, for the cipher.</summary>
        internal byte[] ToBytes() => (byte[])_bytes.Clone();
    }
}
