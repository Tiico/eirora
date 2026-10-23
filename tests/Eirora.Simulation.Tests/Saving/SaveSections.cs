using System.Globalization;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>Reads a save's text by its framing (Save.cs): the magic line, the hash line, then named sections.</summary>
internal static class SaveSections
{
    /// <summary>The text of a section.</summary>
    public static string Text(string save, string name)
    {
        int at = save.IndexOf('\n', save.IndexOf('\n') + 1) + 1;
        while (at < save.Length)
        {
            int lineEnd = save.IndexOf('\n', at);
            string[] header = save[at..lineEnd].Split(' ');
            int length = int.Parse(header[1], CultureInfo.InvariantCulture);
            if (header[0] == name)
            {
                return save.Substring(lineEnd + 1, length);
            }

            at = lineEnd + 1 + length;
        }

        throw new InvalidOperationException($"The save has no '{name}' section.");
    }

    /// <summary>A section as it is written: its name and length, then its text.</summary>
    public static string Written(string save, string name)
    {
        string text = Text(save, name);
        return $"{name} {text.Length.ToString(CultureInfo.InvariantCulture)}\n{text}";
    }
}
