using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Eirora.Contracts;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Time;

namespace Eirora.Server.Tests;

/// <summary>Cloud saves as the client uploads them: a real save's world part and stand-ins for the encrypted contributions.</summary>
public static class TestSaves
{
    private static readonly TimeReading s_install = new(1_780_304_400, "Europe/Stockholm", 7200);

    public static Save NewSave() => Save.Begin(s_install, new RestSchedule(22 * 60, 8 * 60));

    public static byte[] World() => NewSave().ToParts().World;

    /// <summary>Random bytes, like ciphertext: the server never reads them.</summary>
    public static byte[] Contributions(int length = 4096) => RandomNumberGenerator.GetBytes(length);

    public static CloudSaveUpload Upload(long baseVersion, int keyEpoch = 1, byte[]? contributions = null) =>
        new(baseVersion, keyEpoch, World(), contributions ?? Contributions());

    /// <summary>
    /// The part with <paramref name="edit"/> applied to each section's text and sealed again with a matching hash, as a
    /// player who crafts an upload can; <paramref name="format"/> replaces the save format in the header.
    /// </summary>
    public static byte[] Resealed(byte[] part, Func<string, string, string> edit, int? format = null)
    {
        string text = Encoding.UTF8.GetString(part);
        int magicEnd = text.IndexOf('\n', StringComparison.Ordinal);
        int hashEnd = text.IndexOf('\n', magicEnd + 1);
        string header = format is { } replaced ? $"eirora-save {replaced}\n" : text[..(magicEnd + 1)];
        var body = new StringBuilder();
        for (int at = hashEnd + 1; at < text.Length;)
        {
            int lineEnd = text.IndexOf('\n', at);
            string[] fields = text[at..lineEnd].Split(' ');
            int length = int.Parse(fields[1], CultureInfo.InvariantCulture);
            string section = edit(fields[0], text.Substring(lineEnd + 1, length));
            body.Append(CultureInfo.InvariantCulture, $"{fields[0]} {section.Length}\n").Append(section);
            at = lineEnd + 1 + length;
        }

        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(header + body)));
        return Encoding.UTF8.GetBytes($"{header}sha256 {hash}\n{body}");
    }
}
