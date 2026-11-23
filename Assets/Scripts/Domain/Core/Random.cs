namespace FootballBoard.Domain
{
    /// <summary>All randomness in the rules engine goes through this interface.</summary>
    public interface IRandomSource
    {
        /// <summary>Uniform value in [0, 1).</summary>
        double NextDouble();

        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        int NextInt(int maxExclusive);
    }

    public static class RandomExtensions
    {
        /// <summary>
        /// True with probability <paramref name="probability"/>. Certain outcomes (p &lt;= 0 or p &gt;= 1)
        /// do not consume a random value, so replays stay aligned when rules change odds to 0 or 1.
        /// </summary>
        public static bool Chance(this IRandomSource random, double probability)
        {
            if (probability >= 1.0) return true;
            if (probability <= 0.0) return false;
            return random.NextDouble() < probability;
        }
    }

    /// <summary>
    /// Deterministic xorshift64* generator. Its whole state is one ulong, so a match
    /// can be saved, replayed or reproduced in tests on any platform.
    /// </summary>
    public sealed class SeededRandom : IRandomSource
    {
        private ulong _state;

        public SeededRandom(ulong seed)
        {
            State = SplitMix(seed);
        }

        public ulong State
        {
            get => _state;
            set => _state = value == 0 ? 0x9E3779B97F4A7C15UL : value;
        }

        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        private ulong NextULong()
        {
            var x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 2685821657736338717UL;
        }

        private static ulong SplitMix(ulong seed)
        {
            var z = seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
