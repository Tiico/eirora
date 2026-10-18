using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Eirora.Simulation.Serialization
{
    /// <summary>The SHA-256 hash of text as UTF-8, in lower-case hex: how snapshots, saves and maps detect a changed byte.</summary>
    internal static class Sha256
    {
        public static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        public static string Of(string text)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Utf8.GetBytes(text));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }
    }
}
