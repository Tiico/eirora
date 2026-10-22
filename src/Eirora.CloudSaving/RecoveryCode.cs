using System;
using System.Security.Cryptography;
using System.Text;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// The contribution key as text a player can write down and type in on a new phone (GLOSSARY: Återställningskod,
    /// ADR-0008, TASK-023b): 64 characters of Crockford base32 in eight groups of eight, which hold the key, its epoch
    /// and a check that catches a wrong character. It is one of the key's copies, so it is shown only on request.
    /// </summary>
    public static class RecoveryCode
    {
        public const int Length = 64;
        public const int GroupLength = 8;

        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const byte Version = 1;
        private const int EpochBytes = 2;
        private const int CheckBytes = 5;
        private const int Bytes = 1 + EpochBytes + ContributionKey.Length + CheckBytes; // 40 bytes, exactly 64 characters.

        /// <summary>The code of <paramref name="key"/>, grouped with hyphens.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The epoch is too large for the code.</exception>
        public static string Of(ContributionKey key)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Epoch > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(key), "The key epoch does not fit the recovery code.");
            }

            var bytes = new byte[Bytes];
            bytes[0] = Version;
            bytes[1] = (byte)(key.Epoch >> 8);
            bytes[2] = (byte)key.Epoch;
            Buffer.BlockCopy(key.ToBytes(), 0, bytes, 1 + EpochBytes, ContributionKey.Length);
            byte[] check = Check(bytes);
            Buffer.BlockCopy(check, 0, bytes, Bytes - CheckBytes, CheckBytes);

            var code = new StringBuilder(Length + (Length / GroupLength));
            string encoded = Encode(bytes);
            for (int i = 0; i < encoded.Length; i += GroupLength)
            {
                if (i > 0)
                {
                    code.Append('-');
                }

                code.Append(encoded, i, GroupLength);
            }

            return code.ToString();
        }

        /// <summary>
        /// The key a typed code holds, or null when the text is not a code or a character is wrong. Case, hyphens and
        /// spaces do not matter, and the letters O, I and L read as 0, 1 and 1.
        /// </summary>
        public static ContributionKey? TryParse(string? text)
        {
            if (text is null)
            {
                return null;
            }

            var normalized = new StringBuilder(Length);
            foreach (char c in text.ToUpperInvariant())
            {
                switch (c)
                {
                    case 'O':
                        normalized.Append('0');
                        break;
                    case 'I':
                    case 'L':
                        normalized.Append('1');
                        break;
                    case '-':
                    case ' ':
                        break;
                    default:
                        normalized.Append(c);
                        break;
                }
            }

            if (normalized.Length != Length)
            {
                return null;
            }

            byte[]? bytes = Decode(normalized.ToString());
            if (bytes is null || bytes[0] != Version)
            {
                return null;
            }

            byte[] check = Check(bytes);
            for (int i = 0; i < CheckBytes; i++)
            {
                if (bytes[Bytes - CheckBytes + i] != check[i])
                {
                    return null;
                }
            }

            int epoch = (bytes[1] << 8) | bytes[2];
            if (epoch < 1)
            {
                return null;
            }

            var key = new byte[ContributionKey.Length];
            Buffer.BlockCopy(bytes, 1 + EpochBytes, key, 0, ContributionKey.Length);
            return new ContributionKey(epoch, key);
        }

        /// <summary>The first bytes of SHA-256 over the version, the epoch and the key.</summary>
        private static byte[] Check(byte[] bytes)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(bytes, 0, Bytes - CheckBytes);
        }

        private static string Encode(byte[] bytes)
        {
            var text = new StringBuilder(Length);
            int buffer = 0, bits = 0;
            foreach (byte b in bytes)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    text.Append(Alphabet[(buffer >> bits) & 31]);
                }
            }

            return text.ToString();
        }

        private static byte[]? Decode(string text)
        {
            var bytes = new byte[Bytes];
            int buffer = 0, bits = 0, at = 0;
            foreach (char c in text)
            {
                int value = Alphabet.IndexOf(c);
                if (value < 0)
                {
                    return null;
                }

                buffer = (buffer << 5) | value;
                bits += 5;
                if (bits >= 8)
                {
                    bits -= 8;
                    bytes[at++] = (byte)((buffer >> bits) & 255);
                }
            }

            return at == Bytes ? bytes : null;
        }
    }
}
