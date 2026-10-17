using System;
using System.Text;

namespace Eirora.Simulation.Forces
{
    /// <summary>
    /// The player's saved forces (D-35): they never decay, and a soft cap per force limits only new credit.
    /// </summary>
    /// <remarks>
    /// Balances are kept in half forces. Each whole force of credit adds two halves below the full threshold,
    /// one half between the full and the half threshold, and nothing above it. Because credit is defined
    /// one force at a time, crediting in parts gives the same balance as crediting at once (INV-F8).
    /// </remarks>
    public sealed class ForceBalances
    {
        private const int ForceCount = 3;

        // Full credit below the first threshold, half credit up to the second (forces.md, section 5).
        private static readonly int[] s_fullCreditBelow = { 400, 600, 300 };
        private static readonly int[] s_halfCreditBelow = { 800, 1_200, 600 };

        private readonly long[] _halves = new long[ForceCount];

        /// <summary>Starting forces from the guided tour (D-45).</summary>
        public static ForceBalances GuidedTourStart()
        {
            var balances = new ForceBalances();
            balances.SetWhole(Force.Exploration, 100);
            balances.SetWhole(Force.BuildPower, 100);
            balances.SetWhole(Force.DawnPower, 40);
            return balances;
        }

        /// <summary>The whole forces available to spend.</summary>
        public int Available(Force force) => (int)(_halves[Index(force)] / 2);

        /// <summary>Adds credit under the soft cap and returns the whole forces actually gained.</summary>
        public int Credit(Force force, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Use Withdraw for corrections downwards.");
            }

            int i = Index(force);
            int before = Available(force);
            long full = 2L * s_fullCreditBelow[i], half = 2L * s_halfCreditBelow[i];
            long halves = _halves[i];
            long remaining = amount;

            if (halves < full && remaining > 0)
            {
                long units = (full - halves + 1) / 2;
                long used = units < remaining ? units : remaining;
                halves += 2 * used;
                remaining -= used;
            }

            if (halves < half && remaining > 0)
            {
                long used = half - halves < remaining ? half - halves : remaining;
                halves += used;
            }

            _halves[i] = halves;
            return Available(force) - before;
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> after a correction downwards; never below zero (D-36).
        /// Returns the whole forces actually removed.
        /// </summary>
        public int Withdraw(Force force, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Must not be negative.");
            }

            return WithdrawHalves(force, 2L * amount);
        }

        /// <summary>The balance in half forces, the unit credit is kept in.</summary>
        internal long Halves(Force force) => _halves[Index(force)];

        /// <summary>Removes up to <paramref name="halves"/> half forces, never below zero; returns the whole forces removed.</summary>
        internal int WithdrawHalves(Force force, long halves)
        {
            int i = Index(force);
            int before = Available(force);
            _halves[i] -= halves < _halves[i] ? halves : _halves[i];
            return before - Available(force);
        }

        /// <summary>Spends exactly <paramref name="cost"/> if the balance covers it (INV-F5); otherwise changes nothing.</summary>
        public bool TrySpend(Force force, int cost)
        {
            if (cost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cost), cost, "Must not be negative.");
            }

            int i = Index(force);
            if (_halves[i] < 2L * cost)
            {
                return false;
            }

            _halves[i] -= 2L * cost;
            return true;
        }

        internal void AppendFingerprint(StringBuilder text) =>
            text.Append("forces:").Append(_halves[0]).Append(',').Append(_halves[1]).Append(',').Append(_halves[2]).Append(';');

        private void SetWhole(Force force, int amount) => _halves[Index(force)] = 2L * amount;

        private static int Index(Force force) => force switch
        {
            Force.Exploration => 0,
            Force.BuildPower => 1,
            Force.DawnPower => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(force), force, "Unknown force."),
        };
    }
}
