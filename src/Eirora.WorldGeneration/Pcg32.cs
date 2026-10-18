using System;
using System.Text;

namespace Eirora.WorldGeneration
{
    /// <summary>
    /// The generators' random numbers: PCG32 (XSH-RR, 64 bits of state) as specified at pcg-random.org, in integer
    /// arithmetic only, so the same seed gives the same numbers on .NET, Mono and IL2CPP (ADR-0010, INV-D3).
    /// </summary>
    /// <remarks>
    /// Each step of a generator draws from a stream of its own, named after the step, so adding draws to one step never
    /// changes another. Never <see cref="System.Random"/>, whose sequence is not specified and differs between runtimes.
    /// </remarks>
    public sealed class Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        /// <summary>Seeds a generator as the reference implementation's <c>pcg32_srandom_r(initstate, initseq)</c> does.</summary>
        public Pcg32(ulong initialState, ulong sequence)
        {
            unchecked
            {
                _increment = (sequence << 1) | 1UL;
                Next();
                _state += initialState;
                Next();
            }
        }

        /// <summary>The stream for one step of a generator: the world's seed, and the step's name hashed with FNV-1a.</summary>
        public static Pcg32 ForStep(ulong seed, string step) => new(seed, Fnv1a64(step));

        /// <summary>The next 32 random bits.</summary>
        public uint Next()
        {
            unchecked
            {
                ulong old = _state;
                _state = (old * Multiplier) + _increment;
                uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
                int rotation = (int)(old >> 59);
                return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
            }
        }

        /// <summary>A number from 0 to <paramref name="bound"/> - 1, without bias.</summary>
        public int Below(int bound)
        {
            if (bound <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bound), bound, "The bound is positive.");
            }

            uint limit = (uint)bound;
            uint threshold = unchecked(0u - limit) % limit;
            while (true)
            {
                uint value = Next();
                if (value >= threshold)
                {
                    return (int)(value % limit);
                }
            }
        }

        /// <summary>A number from <paramref name="minimum"/> to <paramref name="maximum"/>, both included.</summary>
        public int Between(int minimum, int maximum)
        {
            if (maximum < minimum)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "The maximum is at least the minimum.");
            }

            return minimum + Below(maximum - minimum + 1);
        }

        /// <summary>True with a chance of <paramref name="percent"/> in a hundred.</summary>
        public bool Chance(int percent) => Below(100) < percent;

        /// <summary>The 64-bit FNV-1a hash of text as UTF-8; unlike <see cref="string.GetHashCode()"/> it never changes.</summary>
        public static ulong Fnv1a64(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (byte b in Encoding.UTF8.GetBytes(text))
                {
                    hash ^= b;
                    hash *= 1099511628211UL;
                }

                return hash;
            }
        }
    }
}
