using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Coherent noise for terrain, clouds and nebulae.
    ///
    /// Everything here is <b>3D</b> and sampled on the sphere's surface, never on a 2D
    /// lat/long grid. A 2D field wrapped onto a globe pinches at the poles and tears along the
    /// antimeridian; sampling a 3D field at the surface point has neither problem, needs no
    /// seam handling, and costs nothing extra because the surface point is already in hand.
    ///
    /// Gradients are derived from the integer hash rather than a permutation table, so the
    /// field is stateless and any patch of a planet can be generated in isolation — which is
    /// what makes streaming a terrain tile possible without generating its neighbours.
    /// </summary>
    public static class NoiseD
    {
        /// <summary>The 12 edge-midpoint gradients of a cube, as in Perlin's improved noise.</summary>
        private static readonly Double3[] Gradients =
        {
            new Double3(1, 1, 0), new Double3(-1, 1, 0), new Double3(1, -1, 0), new Double3(-1, -1, 0),
            new Double3(1, 0, 1), new Double3(-1, 0, 1), new Double3(1, 0, -1), new Double3(-1, 0, -1),
            new Double3(0, 1, 1), new Double3(0, -1, 1), new Double3(0, 1, -1), new Double3(0, -1, -1)
        };

        /// <summary>Quintic fade curve. Its first and second derivatives vanish at 0 and 1,
        /// which is what keeps lit terrain free of visible grid creases.</summary>
        private static double Fade(double t) => t * t * t * (t * (t * 6.0 - 15.0) + 10.0);

        private static Double3 GradientAt(long seed, long x, long y, long z) =>
            Gradients[(int)(Hash.Hash3(seed, x, y, z) % 12UL)];

        /// <summary>Gradient (Perlin) noise in three dimensions, output roughly in [-1,1].</summary>
        public static double Gradient3D(long seed, Double3 point)
        {
            long xi = (long)System.Math.Floor(point.x);
            long yi = (long)System.Math.Floor(point.y);
            long zi = (long)System.Math.Floor(point.z);

            double xf = point.x - xi, yf = point.y - yi, zf = point.z - zi;
            double u = Fade(xf), v = Fade(yf), w = Fade(zf);

            double n000 = Double3.Dot(GradientAt(seed, xi, yi, zi), new Double3(xf, yf, zf));
            double n100 = Double3.Dot(GradientAt(seed, xi + 1, yi, zi), new Double3(xf - 1, yf, zf));
            double n010 = Double3.Dot(GradientAt(seed, xi, yi + 1, zi), new Double3(xf, yf - 1, zf));
            double n110 = Double3.Dot(GradientAt(seed, xi + 1, yi + 1, zi), new Double3(xf - 1, yf - 1, zf));
            double n001 = Double3.Dot(GradientAt(seed, xi, yi, zi + 1), new Double3(xf, yf, zf - 1));
            double n101 = Double3.Dot(GradientAt(seed, xi + 1, yi, zi + 1), new Double3(xf - 1, yf, zf - 1));
            double n011 = Double3.Dot(GradientAt(seed, xi, yi + 1, zi + 1), new Double3(xf, yf - 1, zf - 1));
            double n111 = Double3.Dot(GradientAt(seed, xi + 1, yi + 1, zi + 1), new Double3(xf - 1, yf - 1, zf - 1));

            double x00 = MathD.Lerp(n000, n100, u);
            double x10 = MathD.Lerp(n010, n110, u);
            double x01 = MathD.Lerp(n001, n101, u);
            double x11 = MathD.Lerp(n011, n111, u);

            // Perlin gradient noise peaks near 1/sqrt(3) of the gradient length; scaling here
            // keeps the nominal range at [-1,1] so octave weights mean what they say.
            return MathD.Lerp(MathD.Lerp(x00, x10, v), MathD.Lerp(x01, x11, v), w) * 1.1547005383792515;
        }

        /// <summary>Parameters shared by the fractal variants.</summary>
        [Serializable]
        public struct FractalSettings
        {
            /// <summary>Number of octaves summed. Each roughly doubles the cost.</summary>
            public int Octaves;

            /// <summary>Frequency of the first octave, in cycles per unit.</summary>
            public double Frequency;

            /// <summary>Frequency multiplier between octaves. 2.0 is one octave in the musical sense.</summary>
            public double Lacunarity;

            /// <summary>Amplitude multiplier between octaves. Below 0.5 gives smooth terrain, above gives rough.</summary>
            public double Persistence;

            public static FractalSettings Default => new FractalSettings
            {
                Octaves = 8,
                Frequency = 1.0,
                Lacunarity = 2.0,
                // Slightly above 1/lacunarity, giving fractal dimension a shade under 2.5 —
                // close to what real eroded terrain measures.
                Persistence = 0.5
            };

            public FractalSettings WithOctaves(int octaves)
            {
                var copy = this;
                copy.Octaves = octaves;
                return copy;
            }
        }

        /// <summary>
        /// Fractional Brownian motion: octaves of gradient noise summed at decreasing
        /// amplitude. The general-purpose terrain and cloud primitive. Output is normalised
        /// to approximately [-1,1] regardless of octave count.
        /// </summary>
        public static double Fbm(long seed, Double3 point, FractalSettings settings)
        {
            double sum = 0.0, amplitude = 1.0, frequency = settings.Frequency, normalisation = 0.0;

            for (int i = 0; i < settings.Octaves; i++)
            {
                // Offsetting each octave decorrelates them; without it the octaves share their
                // zero crossings and the result shows a visible grid.
                Double3 offset = new Double3(i * 37.19, i * 61.73, i * 11.31);
                sum += Gradient3D(seed + i, point * frequency + offset) * amplitude;
                normalisation += amplitude;
                amplitude *= settings.Persistence;
                frequency *= settings.Lacunarity;
            }

            return normalisation > 0.0 ? sum / normalisation : 0.0;
        }

        /// <summary>
        /// Ridged multifractal: fBm with each octave folded about zero and inverted, so the
        /// extrema become sharp creases. This is what produces mountain ridges and canyon
        /// networks, which plain fBm cannot — its extrema are rounded.
        /// </summary>
        public static double Ridged(long seed, Double3 point, FractalSettings settings, double sharpness = 1.0)
        {
            double sum = 0.0, amplitude = 1.0, frequency = settings.Frequency, normalisation = 0.0;
            double weight = 1.0;

            for (int i = 0; i < settings.Octaves; i++)
            {
                Double3 offset = new Double3(i * 37.19, i * 61.73, i * 11.31);
                double signal = 1.0 - System.Math.Abs(Gradient3D(seed + i, point * frequency + offset));
                signal *= signal;

                // Weighting each octave by the previous one concentrates detail on the ridges
                // and leaves the valleys smooth, as erosion does.
                signal *= weight;
                weight = MathD.Clamp01(signal * sharpness);

                sum += signal * amplitude;
                normalisation += amplitude;
                amplitude *= settings.Persistence;
                frequency *= settings.Lacunarity;
            }

            return normalisation > 0.0 ? (sum / normalisation) * 2.0 - 1.0 : 0.0;
        }

        /// <summary>
        /// Billowy noise: absolute-valued fBm. Rounded, cloud-like lobes — the counterpart to
        /// <see cref="Ridged"/>, and the right primitive for cumulus decks and gas-giant bands.
        /// </summary>
        public static double Billow(long seed, Double3 point, FractalSettings settings)
        {
            double sum = 0.0, amplitude = 1.0, frequency = settings.Frequency, normalisation = 0.0;

            for (int i = 0; i < settings.Octaves; i++)
            {
                Double3 offset = new Double3(i * 37.19, i * 61.73, i * 11.31);
                sum += (System.Math.Abs(Gradient3D(seed + i, point * frequency + offset)) * 2.0 - 1.0) * amplitude;
                normalisation += amplitude;
                amplitude *= settings.Persistence;
                frequency *= settings.Lacunarity;
            }

            return normalisation > 0.0 ? sum / normalisation : 0.0;
        }

        /// <summary>
        /// Domain-warped fBm: the sample point is displaced by another noise field before
        /// evaluation. Cheap, and the single most effective way to stop terrain looking
        /// procedural — it introduces the curved, braided structures that plain fBm's isotropy
        /// cannot produce, and which real drainage and tectonics are full of.
        /// </summary>
        public static double WarpedFbm(long seed, Double3 point, FractalSettings settings, double warpStrength = 0.35)
        {
            var warpSettings = settings.WithOctaves(System.Math.Max(2, settings.Octaves / 2));

            Double3 warp = new Double3(
                Fbm(seed + 9001, point + new Double3(5.2, 1.3, 9.7), warpSettings),
                Fbm(seed + 9002, point + new Double3(1.7, 8.3, 2.8), warpSettings),
                Fbm(seed + 9003, point + new Double3(8.3, 2.8, 4.1), warpSettings));

            return Fbm(seed, point + warp * warpStrength, settings);
        }

        /// <summary>
        /// Worley (cellular) noise: distance to the nearest of a set of feature points, one
        /// scattered per grid cell. Returns the distance to the closest, normalised so that
        /// values are typically in [0,1]. The natural primitive for crater fields, tectonic
        /// plates and Voronoi-patterned biomes.
        /// </summary>
        public static double Worley(long seed, Double3 point, out Double3 nearestFeature)
        {
            long xi = (long)System.Math.Floor(point.x);
            long yi = (long)System.Math.Floor(point.y);
            long zi = (long)System.Math.Floor(point.z);

            double nearestSqrDistance = double.MaxValue;
            nearestFeature = Double3.Zero;

            // The nearest feature point can live in any of the 27 neighbouring cells.
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                long cx = xi + dx, cy = yi + dy, cz = zi + dz;
                ulong h = Hash.Hash3(seed, cx, cy, cz);

                Double3 feature = new Double3(
                    cx + Hash.ToUnit(h),
                    cy + Hash.ToUnit(Hash.Mix(h)),
                    cz + Hash.ToUnit(Hash.Mix(Hash.Mix(h))));

                double sqrDistance = Double3.SqrDistance(point, feature);
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearestFeature = feature;
                }
            }

            return System.Math.Sqrt(nearestSqrDistance);
        }

        public static double Worley(long seed, Double3 point) => Worley(seed, point, out _);

        /// <summary>
        /// Analytic gradient of <see cref="Fbm"/> by central differences. Needed for surface
        /// normals and for slope-dependent effects such as material layering and scree.
        /// </summary>
        public static Double3 FbmGradient(long seed, Double3 point, FractalSettings settings, double epsilon = 1e-4)
        {
            double dx = Fbm(seed, point + new Double3(epsilon, 0, 0), settings)
                        - Fbm(seed, point - new Double3(epsilon, 0, 0), settings);
            double dy = Fbm(seed, point + new Double3(0, epsilon, 0), settings)
                        - Fbm(seed, point - new Double3(0, epsilon, 0), settings);
            double dz = Fbm(seed, point + new Double3(0, 0, epsilon), settings)
                        - Fbm(seed, point - new Double3(0, 0, epsilon), settings);

            return new Double3(dx, dy, dz) / (2.0 * epsilon);
        }
    }
}
