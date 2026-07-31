using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Deterministic hashing and seed derivation.
    ///
    /// The entire procedural universe rests on one property: the same coordinate must always
    /// produce the same world, on every machine, in every session, for every player. That
    /// rules out <see cref="Random"/> (whose algorithm is not contractually stable across
    /// runtimes) and anything that depends on iteration order or floating-point rounding.
    /// What is needed is a pure integer hash — no state, no sequence, just coordinate in and
    /// value out — so that a body can be generated on demand without generating its
    /// neighbours first.
    ///
    /// The mixer is SplitMix64, chosen because it is short, passes the standard statistical
    /// batteries, and is exactly specified in integer arithmetic with no platform latitude.
    /// </summary>
    public static class Hash
    {
        /// <summary>SplitMix64 finaliser. Avalanches a single bit change across the whole word.</summary>
        public static ulong Mix(ulong value)
        {
            unchecked
            {
                value += 0x9E3779B97F4A7C15UL;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        public static ulong Combine(ulong seed, ulong value)
        {
            unchecked
            {
                // Golden-ratio constant, as in boost::hash_combine: decorrelates the inputs so
                // that Combine(a,b) and Combine(b,a) do not collide.
                return Mix(seed ^ (value + 0x9E3779B97F4A7C15UL + (seed << 6) + (seed >> 2)));
            }
        }

        public static ulong Hash1(long seed, long x) => Mix((ulong)seed ^ Mix((ulong)x));

        public static ulong Hash2(long seed, long x, long y) => Combine(Hash1(seed, x), Mix((ulong)y));

        public static ulong Hash3(long seed, long x, long y, long z) => Combine(Hash2(seed, x, y), Mix((ulong)z));

        public static ulong Hash4(long seed, long x, long y, long z, long w) =>
            Combine(Hash3(seed, x, y, z), Mix((ulong)w));

        /// <summary>Hash a string into a seed. Stable across runtimes, unlike <see cref="string.GetHashCode"/>.</summary>
        public static long HashString(string text, long seed = 0)
        {
            if (string.IsNullOrEmpty(text)) return seed;
            unchecked
            {
                ulong result = (ulong)seed ^ 0xCBF29CE484222325UL;   // FNV-1a offset basis
                foreach (char c in text)
                {
                    result ^= c;
                    result *= 0x100000001B3UL;                        // FNV-1a prime
                }
                return (long)Mix(result);
            }
        }

        /// <summary>Uniform double in [0,1) from a hash value.</summary>
        public static double ToUnit(ulong hash)
        {
            // Take the top 53 bits: exactly the mantissa width of a double, so every
            // representable value in the range is reachable and none is favoured.
            return (hash >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>Uniform double in [-1,1) from a hash value.</summary>
        public static double ToSigned(ulong hash) => ToUnit(hash) * 2.0 - 1.0;

        /// <summary>Uniform double in [min,max).</summary>
        public static double Range(ulong hash, double min, double max) => min + ToUnit(hash) * (max - min);

        /// <summary>Uniform integer in [min,max).</summary>
        public static int RangeInt(ulong hash, int min, int max) =>
            max <= min ? min : min + (int)(hash % (ulong)(max - min));

        /// <summary>
        /// Normally distributed value with the given mean and standard deviation, by the
        /// Box-Muller transform. Physical parameters — stellar masses, crater sizes, orbital
        /// inclinations — cluster around a value rather than spreading uniformly, and using a
        /// uniform draw for them is what makes a procedural system feel arbitrary.
        /// </summary>
        public static double Normal(ulong hash, double mean = 0.0, double standardDeviation = 1.0)
        {
            double u1 = ToUnit(Mix(hash));
            double u2 = ToUnit(Mix(hash ^ 0xA5A5A5A5A5A5A5A5UL));
            if (u1 <= double.Epsilon) u1 = double.Epsilon;
            double magnitude = System.Math.Sqrt(-2.0 * System.Math.Log(u1));
            return mean + standardDeviation * magnitude * System.Math.Cos(MathD.TwoPi * u2);
        }

        /// <summary>
        /// Draw from a power law with the given exponent over [min,max]. The size distribution
        /// of craters, asteroids and stellar masses all follow one, and a uniform draw over
        /// the same range produces far too many large objects.
        /// </summary>
        public static double PowerLaw(ulong hash, double min, double max, double exponent)
        {
            double u = ToUnit(hash);
            if (System.Math.Abs(exponent + 1.0) < 1e-9)
            {
                // The exponent -1 case integrates to a logarithm rather than a power.
                return min * System.Math.Pow(max / min, u);
            }
            double p = exponent + 1.0;
            return System.Math.Pow(u * (System.Math.Pow(max, p) - System.Math.Pow(min, p)) + System.Math.Pow(min, p), 1.0 / p);
        }

        /// <summary>
        /// Derive a seed for a named sub-feature of a parent seed, e.g. the third moon of a
        /// planet. Lets each generator work from its own coordinate without any shared state,
        /// which is what allows a body to be generated in isolation.
        /// </summary>
        public static long Derive(long parentSeed, string channel, int index = 0) =>
            (long)Combine(Combine((ulong)parentSeed, (ulong)HashString(channel)), (ulong)index);

        /// <summary>
        /// Seed for a point in space, quantised to a grid. This is what makes "the same
        /// coordinate always generates the same world" literally true: any position inside a
        /// cell maps to the cell's seed.
        /// </summary>
        public static long SeedForPosition(Double3 positionKm, double cellSizeKm, long universeSeed)
        {
            long x = (long)System.Math.Floor(positionKm.x / cellSizeKm);
            long y = (long)System.Math.Floor(positionKm.y / cellSizeKm);
            long z = (long)System.Math.Floor(positionKm.z / cellSizeKm);
            return (long)Hash3(universeSeed, x, y, z);
        }
    }

    /// <summary>
    /// A stateless random stream derived from a seed. Convenience wrapper over <see cref="Hash"/>
    /// for generators that want a sequence, while keeping full determinism: the nth draw
    /// depends only on the seed and n, never on how many draws came before.
    /// </summary>
    public struct DeterministicRandom
    {
        private readonly long _seed;
        private int _counter;

        public DeterministicRandom(long seed)
        {
            _seed = seed;
            _counter = 0;
        }

        public long Seed => _seed;

        private ulong Next() => Hash.Hash1(_seed, _counter++);

        /// <summary>Uniform double in [0,1).</summary>
        public double Unit() => Hash.ToUnit(Next());

        /// <summary>Uniform double in [min,max).</summary>
        public double Range(double min, double max) => Hash.Range(Next(), min, max);

        /// <summary>Uniform integer in [min,max).</summary>
        public int RangeInt(int min, int max) => Hash.RangeInt(Next(), min, max);

        public bool Chance(double probability) => Unit() < probability;

        public double Normal(double mean = 0.0, double standardDeviation = 1.0) =>
            Hash.Normal(Next(), mean, standardDeviation);

        public double PowerLaw(double min, double max, double exponent) =>
            Hash.PowerLaw(Next(), min, max, exponent);

        /// <summary>A direction uniformly distributed over the sphere.</summary>
        public Double3 OnUnitSphere()
        {
            // Sampling z uniformly and then the azimuth gives an even distribution over area;
            // drawing two angles uniformly instead would bunch points at the poles.
            double z = Range(-1.0, 1.0);
            double azimuth = Range(0.0, MathD.TwoPi);
            double radius = System.Math.Sqrt(System.Math.Max(0.0, 1.0 - z * z));
            return new Double3(radius * System.Math.Cos(azimuth), radius * System.Math.Sin(azimuth), z);
        }

        /// <summary>Pick an element from a list.</summary>
        public T Pick<T>(System.Collections.Generic.IReadOnlyList<T> items) =>
            items == null || items.Count == 0 ? default : items[RangeInt(0, items.Count)];

        /// <summary>A named sub-stream, so a generator can branch without disturbing its own sequence.</summary>
        public DeterministicRandom Branch(string channel, int index = 0) =>
            new DeterministicRandom(Hash.Derive(_seed, channel, index));
    }
}
