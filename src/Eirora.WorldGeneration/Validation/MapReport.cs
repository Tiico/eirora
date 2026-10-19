using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Eirora.WorldGeneration.Validation
{
    /// <summary>One rule's verdict on a map: what was measured, what the limit is, and what the measure means.</summary>
    public sealed class RuleResult
    {
        public RuleResult(string rule, long value, string limit, bool passed, string detail, long? minimum = null, long? maximum = null)
        {
            Rule = rule;
            Value = value;
            Limit = limit;
            Passed = passed;
            Detail = detail;
            Minimum = minimum;
            Maximum = maximum;
        }

        /// <summary>The rule's stable id, such as <c>inner-sea</c>.</summary>
        public string Rule { get; }

        /// <summary>The measured value, -1 when there is nothing to measure.</summary>
        public long Value { get; }

        public string Limit { get; }

        public bool Passed { get; }

        public string Detail { get; }

        /// <summary>The lowest value that passes, if the rule has one.</summary>
        public long? Minimum { get; }

        /// <summary>The highest value that passes, if the rule has one.</summary>
        public long? Maximum { get; }

        /// <summary>
        /// How far a passing value lies from the nearest limit, so the seed tool can find the maps where the generator
        /// is weakest (TASK-034); none for a rule that only one value passes, or a failing one.
        /// </summary>
        public long? Margin
        {
            get
            {
                if (!Passed || Minimum == Maximum)
                {
                    return null;
                }

                long? fromMinimum = Minimum is long minimum ? Value - minimum : null;
                long? fromMaximum = Maximum is long maximum ? maximum - Value : null;
                return fromMinimum is long low && fromMaximum is long high ? (low < high ? low : high) : fromMinimum ?? fromMaximum;
            }
        }
    }

    /// <summary>A map's report: every rule's verdict, so a failing map says exactly what it lacks (TASK-030).</summary>
    public sealed class MapReport
    {
        public MapReport(IReadOnlyList<RuleResult> rules)
        {
            Rules = rules;
        }

        public IReadOnlyList<RuleResult> Rules { get; }

        public bool Passed => Rules.All(rule => rule.Passed);

        public IEnumerable<RuleResult> Failed => Rules.Where(rule => !rule.Passed);

        public RuleResult this[string rule] => Rules.First(result => result.Rule == rule);

        /// <summary>One line per rule: id, value, limit, verdict and detail; the same map always gives the same text.</summary>
        public string ToText()
        {
            var text = new StringBuilder();
            foreach (var rule in Rules)
            {
                text.Append(rule.Rule).Append(' ')
                    .Append(rule.Value.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(rule.Limit).Append(' ')
                    .Append(rule.Passed ? "pass" : "FAIL");
                if (rule.Detail.Length > 0)
                {
                    text.Append(" (").Append(rule.Detail).Append(')');
                }

                text.Append('\n');
            }

            return text.ToString();
        }
    }
}
