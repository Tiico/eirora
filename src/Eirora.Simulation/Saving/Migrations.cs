using System;
using System.Collections.Generic;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// The chain of migrations from the first rules version to <see cref="RulesVersion.Current"/>: one per rules
    /// version, added in the change that bumps the rules (ADR-0006).
    /// </summary>
    internal static class Migrations
    {
        public static IReadOnlyList<StateMigration> All { get; } = new StateMigration[] { new MapArrives() };

        /// <summary>Runs the chain from <paramref name="fromRules"/> to the current rules.</summary>
        /// <exception cref="FormatException">A migration in the chain is missing.</exception>
        public static string ToCurrent(string state, int fromRules, IReadOnlyList<StateMigration> migrations)
        {
            for (int rules = fromRules; rules < RulesVersion.Current; rules++)
            {
                state = From(rules, migrations).Migrate(state);
            }

            return state;
        }

        private static StateMigration From(int rules, IReadOnlyList<StateMigration> migrations)
        {
            foreach (var migration in migrations)
            {
                if (migration.FromRules == rules)
                {
                    return migration;
                }
            }

            throw new FormatException($"There is no migration from rules version {rules}.");
        }
    }
}
