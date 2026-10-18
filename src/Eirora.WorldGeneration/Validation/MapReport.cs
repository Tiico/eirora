using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Eirora.WorldGeneration.Validation
{
    /// <summary>One rule's verdict on a map: what was measured, what the limit is, and what the measure means.</summary>
    public sealed class RuleResult
    {
        public RuleResult(string rule, long value, string limit, bool passed, string detail)
        {
            Rule = rule;
            Value = value;
            Limit = limit;
            Passed = passed;
            Detail = detail;
        }

        /// <summary>The rule's stable id, such as <c>inner-sea</c>.</summary>
        public string Rule { get; }

        /// <summary>The measured value, -1 when there is nothing to measure.</summary>
        public long Value { get; }

        public string Limit { get; }

        public bool Passed { get; }

        public string Detail { get; }
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
