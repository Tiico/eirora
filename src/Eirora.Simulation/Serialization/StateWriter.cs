using System;
using System.Globalization;
using System.Text;

namespace Eirora.Simulation.Serialization
{
    /// <summary>
    /// Writes state in its canonical text form: one record per line, a keyword and space-separated fields.
    /// The same state always gives the same text, which is both the fingerprint and the body of a snapshot (INV-D1).
    /// </summary>
    /// <remarks>
    /// Numbers are written in invariant culture, enums by name and content by its stable id (ADR-0006).
    /// Text fields escape control characters, spaces and '%' as %XX, so that any idempotency key fits in a field.
    /// </remarks>
    internal sealed class StateWriter
    {
        private readonly StringBuilder _text = new();

        /// <summary>Starts a record.</summary>
        public StateWriter Line(string keyword)
        {
            if (_text.Length > 0)
            {
                _text.Append('\n');
            }

            _text.Append(keyword);
            return this;
        }

        public StateWriter Long(long value)
        {
            _text.Append(' ').Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public StateWriter Bool(bool value) => Long(value ? 1 : 0);

        public StateWriter Enum<T>(T value)
            where T : struct, Enum
        {
            _text.Append(' ').Append(value.ToString());
            return this;
        }

        public StateWriter Text(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("A text field is never empty.", nameof(value));
            }

            _text.Append(' ');
            foreach (char c in value)
            {
                if (c <= ' ' || c == '%' || c == '\u007F')
                {
                    _text.Append('%').Append(((int)c).ToString("X2", CultureInfo.InvariantCulture));
                }
                else
                {
                    _text.Append(c);
                }
            }

            return this;
        }

        /// <summary>The records written so far, each ending in a line break.</summary>
        public override string ToString() => _text.Length == 0 ? string.Empty : _text.ToString() + "\n";
    }
}
