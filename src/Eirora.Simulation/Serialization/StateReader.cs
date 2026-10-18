using System;
using System.Globalization;
using System.Text;

namespace Eirora.Simulation.Serialization
{
    /// <summary>
    /// Reads records written by <see cref="StateWriter"/> in order, and throws <see cref="FormatException"/> for
    /// anything else. It checks the shape of each record; whether the text is canonical is checked by writing the
    /// restored state again and comparing.
    /// </summary>
    internal sealed class StateReader
    {
        private readonly string[] _lines;
        private int _next;
        private string[] _fields = Array.Empty<string>();
        private int _field;

        public StateReader(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.Length > 0 && text[text.Length - 1] != '\n')
            {
                throw new FormatException("State text ends with a line break.");
            }

            _lines = text.Length == 0 ? Array.Empty<string>() : text.Substring(0, text.Length - 1).Split('\n');
        }

        /// <summary>The keyword of the next record, or null at the end.</summary>
        public string? Peek => _next < _lines.Length ? Keyword(_lines[_next]) : null;

        /// <summary>Whether the current record has fields left.</summary>
        public bool HasField => _field < _fields.Length;

        /// <summary>Moves to the next record if it has <paramref name="keyword"/>.</summary>
        public bool Next(string keyword)
        {
            EndOfRecord();
            if (Peek != keyword)
            {
                return false;
            }

            _fields = _lines[_next].Split(' ');
            _field = 1;
            _next++;
            return true;
        }

        /// <summary>Moves to the next record, which must have <paramref name="keyword"/>.</summary>
        public void Expect(string keyword)
        {
            if (!Next(keyword))
            {
                throw new FormatException($"Expected a '{keyword}' record, found '{Peek ?? "the end"}'.");
            }
        }

        /// <summary>Checks that every record has been read in full.</summary>
        public void End()
        {
            EndOfRecord();
            if (_next < _lines.Length)
            {
                throw new FormatException($"Unexpected '{Peek}' record.");
            }
        }

        public long Long()
        {
            string field = Field();
            return long.TryParse(field, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)
                ? value
                : throw new FormatException($"'{field}' is not a whole number.");
        }

        public int Int()
        {
            long value = Long();
            return value >= int.MinValue && value <= int.MaxValue ? (int)value : throw new FormatException($"{value} is out of range.");
        }

        public bool Bool() => Long() switch
        {
            0 => false,
            1 => true,
            long other => throw new FormatException($"{other} is not 0 or 1."),
        };

        public T Enum<T>()
            where T : struct, Enum
        {
            string field = Field();
            return System.Enum.TryParse<T>(field, ignoreCase: false, out var value) && System.Enum.IsDefined(typeof(T), value)
                ? value
                : throw new FormatException($"'{field}' is not a {typeof(T).Name}.");
        }

        public string Text()
        {
            string field = Field();
            if (field.Length == 0)
            {
                throw new FormatException("A text field is never empty.");
            }

            if (field.IndexOf('%') < 0)
            {
                return field;
            }

            var text = new StringBuilder(field.Length);
            for (int i = 0; i < field.Length; i++)
            {
                if (field[i] != '%')
                {
                    text.Append(field[i]);
                }
                else if (i + 2 < field.Length
                    && int.TryParse(field.AsSpan(i + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                {
                    text.Append((char)code);
                    i += 2;
                }
                else
                {
                    throw new FormatException($"'{field}' has a broken escape.");
                }
            }

            return text.ToString();
        }

        private string Field() =>
            _field < _fields.Length ? _fields[_field++] : throw new FormatException($"The '{_fields[0]}' record has too few fields.");

        private void EndOfRecord()
        {
            if (_field < _fields.Length)
            {
                throw new FormatException($"The '{_fields[0]}' record has too many fields.");
            }
        }

        private static string Keyword(string line)
        {
            int space = line.IndexOf(' ');
            return space < 0 ? line : line.Substring(0, space);
        }
    }
}
