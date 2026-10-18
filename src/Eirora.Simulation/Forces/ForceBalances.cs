using System;
using Eirora.Simulation.Serialization;

namespace Eirora.Simulation.Forces
{
    /// <summary>
    /// The player's saved forces (D-35): they never decay, and a soft cap per force limits only new credit.
    /// </summary>
    /// <remarks>
    /// Each force keeps its position: the credit it holds before the soft cap, in half forces. The balance follows
    /// from the position: two halves per force of credit below the full threshold, one half between the full and the
    /// half threshold, and nothing above it. Credit moves the position up and a correction moves it back down, so
    /// the balance is the same whatever order and parts they come in (INV-F8, INV-B1, D-69). Spending moves the
    /// position to the lowest one that gives the balance left, so the soft cap counts from what is actually saved.
    /// </remarks>
    public sealed class ForceBalances
    {
        private const int ForceCount = 3;

        // Full credit below the first threshold, half credit up to the second (forces.md, section 5).
        private static readonly int[] s_fullCreditBelow = { 400, 600, 300 };
        private static readonly int[] s_halfCreditBelow = { 800, 1_200, 600 };

        private static readonly Force[] s_forces = { Force.Exploration, Force.BuildPower, Force.DawnPower };

        private readonly long[] _positions = new long[ForceCount];

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
        public int Available(Force force) => (int)(Halves(force) / 2);

        /// <summary>Adds credit under the soft cap and returns the whole forces actually gained.</summary>
        public int Credit(Force force, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Use Withdraw for corrections downwards.");
            }

            int before = Available(force);
            _positions[Index(force)] += 2L * amount;
            return Available(force) - before;
        }

        /// <summary>
        /// Takes back <paramref name="amount"/> of credit after a correction downwards, along the soft cap it was
        /// credited under, and never below zero (D-36). Returns the whole forces actually removed.
        /// </summary>
        public int Withdraw(Force force, int amount) => Withdraw(force, amount, long.MaxValue);

        /// <summary>
        /// Takes back <paramref name="amount"/> of credit like <see cref="Withdraw(Force, int)"/>, but lowers the
        /// balance by at most <paramref name="maxHalves"/> half forces (D-62).
        /// </summary>
        internal int Withdraw(Force force, int amount, long maxHalves)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Must not be negative.");
            }

            if (maxHalves < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHalves), maxHalves, "Must not be negative.");
            }

            int i = Index(force);
            int before = Available(force);
            long halves = Halves(force);
            long position = _positions[i] > 2L * amount ? _positions[i] - (2L * amount) : 0;
            _positions[i] = halves - BalanceAt(i, position) > maxHalves ? PositionOf(i, halves - maxHalves) : position;
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
            long halves = Halves(force);
            if (halves < 2L * cost)
            {
                return false;
            }

            _positions[i] = PositionOf(i, halves - (2L * cost));
            return true;
        }

        /// <summary>The balance in half forces, the unit credit is kept in.</summary>
        internal long Halves(Force force)
        {
            int i = Index(force);
            return BalanceAt(i, _positions[i]);
        }

        internal void Write(StateWriter writer)
        {
            foreach (var force in s_forces)
            {
                writer.Line("force").Enum(force).Long(_positions[Index(force)]);
            }
        }

        internal static ForceBalances Read(StateReader reader)
        {
            var balances = new ForceBalances();
            foreach (var force in s_forces)
            {
                reader.Expect("force");
                long position = reader.Enum<Force>() == force ? reader.Long() : throw new FormatException($"Expected {force}.");
                balances._positions[Index(force)] = position >= 0 ? position : throw new FormatException("A force is never negative.");
            }

            return balances;
        }

        // The balance in half forces that a position gives; a half force of credit straddling the full threshold
        // counts in full, so crediting from an odd balance below it matches crediting force by force.
        private static long BalanceAt(int i, long position)
        {
            long full = 2L * s_fullCreditBelow[i], half = 2L * s_halfCreditBelow[i];
            if (position <= full)
            {
                return position;
            }

            long balance = full + ((position - full + 1) / 2);
            return balance < half ? balance : half;
        }

        // The lowest position that gives a balance of at most the half-credit threshold.
        private static long PositionOf(int i, long halves)
        {
            long full = 2L * s_fullCreditBelow[i];
            return halves <= full ? halves : full + (2 * (halves - full));
        }

        private void SetWhole(Force force, int amount)
        {
            int i = Index(force);
            _positions[i] = PositionOf(i, 2L * amount);
        }

        private static int Index(Force force) => force switch
        {
            Force.Exploration => 0,
            Force.BuildPower => 1,
            Force.DawnPower => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(force), force, "Unknown force."),
        };
    }
}
