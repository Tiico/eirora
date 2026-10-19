using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Eirora.WorldGeneration.Validation
{
    /// <summary>How one rule fared over many maps: how many passed, and how its measure spread.</summary>
    public sealed class RuleSpread
    {
        public RuleSpread(string rule, string limit, int passed, long minimum, long median, long maximum)
        {
            Rule = rule;
            Limit = limit;
            Passed = passed;
            Minimum = minimum;
            Median = median;
            Maximum = maximum;
        }

        public string Rule { get; }

        public string Limit { get; }

        public int Passed { get; }

        public long Minimum { get; }

        /// <summary>The lower median, so it is always a measured value.</summary>
        public long Median { get; }

        public long Maximum { get; }
    }

    /// <summary>The summary of a bulk run: per rule, how many maps passed and how the measure spread.</summary>
    public sealed class BulkReport
    {
        public BulkReport(string generator, int version, ulong firstSeed, int maps, int passed, IReadOnlyList<RuleSpread> rules)
        {
            Generator = generator;
            Version = version;
            FirstSeed = firstSeed;
            Maps = maps;
            Passed = passed;
            Rules = rules;
        }

        public string Generator { get; }

        public int Version { get; }

        public ulong FirstSeed { get; }

        public int Maps { get; }

        /// <summary>The maps that passed every rule.</summary>
        public int Passed { get; }

        public IReadOnlyList<RuleSpread> Rules { get; }

        /// <summary>The same seeds always give the same text, byte for byte.</summary>
        public string ToText()
        {
            var text = new StringBuilder();
            text.Append(string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} seeds {2:x16}+{3} passed {4}\n",
                Generator,
                Version,
                FirstSeed,
                Maps,
                Passed));
            text.Append("rule limit passed min median max\n");
            foreach (var rule in Rules)
            {
                text.Append(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1} {2} {3} {4} {5}\n",
                    rule.Rule,
                    rule.Limit,
                    rule.Passed,
                    rule.Minimum,
                    rule.Median,
                    rule.Maximum));
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// Validates the maps a generator makes from consecutive seeds, headless (TASK-030): a small run in CI, tens of
    /// thousands locally with tools/Eirora.MapCheck.
    /// </summary>
    public static class BulkValidation
    {
        public static BulkReport Run(IWorldGenerator generator, ulong firstSeed, int count)
        {
            if (generator is null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "A bulk run validates at least one map.");
            }

            var reports = new List<MapReport>(count);
            var attempts = new List<long>(count);
            for (int i = 0; i < count; i++)
            {
                ulong seed = unchecked(firstSeed + (ulong)i);
                int tries = 1;
                var map = generator is ICountsAttempts counting ? counting.Generate(seed, out tries) : generator.Generate(seed);
                reports.Add(MapValidator.Validate(map));
                attempts.Add(tries);
            }

            var spreads = reports[0].Rules.Select((rule, index) =>
            {
                var values = reports.Select(r => r.Rules[index].Value).OrderBy(v => v).ToList();
                return new RuleSpread(rule.Rule, rule.Limit, reports.Count(r => r.Rules[index].Passed), values[0], values[(values.Count - 1) / 2], values[values.Count - 1]);
            }).ToList();

            // How many times the generator drew again before the validator passed a map (TASK-031).
            attempts.Sort();
            spreads.Add(new RuleSpread("attempts", "-", count, attempts[0], attempts[(count - 1) / 2], attempts[count - 1]));

            return new BulkReport(generator.Id, generator.Version, firstSeed, count, reports.Count(r => r.Passed), spreads);
        }
    }
}
