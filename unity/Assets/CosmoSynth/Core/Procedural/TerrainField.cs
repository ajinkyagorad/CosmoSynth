using System;

namespace CosmoSynth.Core
{
    /// <summary>Broad surface classes, which select which terrain layers are active.</summary>
    public enum SurfaceClass
    {
        /// <summary>Airless, crater-dominated: the Moon, Mercury, most small bodies.</summary>
        Cratered,

        /// <summary>Tectonic and eroded: Earth, Mars, Venus.</summary>
        Continental,

        /// <summary>Water or hydrocarbon ocean over a shallow floor.</summary>
        Oceanic,

        /// <summary>Ice shell over a subsurface ocean: Europa, Enceladus.</summary>
        IceShell,

        /// <summary>Molten or heavily volcanic: Io, a young rocky world.</summary>
        Volcanic,

        /// <summary>No surface. Cloud decks only.</summary>
        GasGiant
    }

    /// <summary>
    /// Elevation as a function of direction on a body — the procedural counterpart to a real
    /// digital elevation model, used where no measured one exists.
    ///
    /// This is deliberately a <b>plausible-looking</b> surface, not a geological simulation.
    /// It composes a handful of layers whose visual signatures happen to match the processes
    /// that dominate real planetary surfaces — broad tectonic provinces, ridge networks,
    /// impact craters, a sea level — because reproducing the appearance is achievable now and
    /// simulating the mechanism is not. Anything it produces is marked
    /// <see cref="ProvenanceClass.Procedural"/> so it can never be mistaken for measurement.
    /// </summary>
    [Serializable]
    public sealed class TerrainField
    {
        public long Seed;
        public SurfaceClass Class = SurfaceClass.Continental;

        /// <summary>Body radius, km. Sets the physical scale of every feature.</summary>
        public double BodyRadiusKm = 6371.0;

        /// <summary>Peak-to-trough relief, km. Earth is about 20 (Everest to the Mariana Trench).</summary>
        public double ReliefKm = 20.0;

        /// <summary>Fraction of the surface below sea level. Zero means no ocean.</summary>
        public double OceanFraction = 0.0;

        /// <summary>Fraction of the surface covered by continental crust rather than basin.</summary>
        public double ContinentFraction = 0.3;

        /// <summary>Relative strength of ridge networks against broad relief.</summary>
        public double MountainStrength = 0.55;

        /// <summary>Crater density multiplier. High for an ancient airless surface, near zero for a young one.</summary>
        public double CraterDensity = 0.0;

        /// <summary>How strongly the terrain is smoothed toward its local mean, standing in for erosion.</summary>
        public double ErosionStrength = 0.35;

        private NoiseD.FractalSettings _continentSettings = new NoiseD.FractalSettings
        { Octaves = 5, Frequency = 1.1, Lacunarity = 2.0, Persistence = 0.55 };

        private NoiseD.FractalSettings _mountainSettings = new NoiseD.FractalSettings
        { Octaves = 9, Frequency = 3.4, Lacunarity = 2.1, Persistence = 0.5 };

        private NoiseD.FractalSettings _detailSettings = new NoiseD.FractalSettings
        { Octaves = 6, Frequency = 22.0, Lacunarity = 2.2, Persistence = 0.48 };

        /// <summary>
        /// Elevation above the reference sphere, in km, for a unit direction from the body's
        /// centre. Negative values are below the datum; below <see cref="SeaLevelKm"/> they are
        /// under water when the body has an ocean.
        /// </summary>
        public double ElevationKm(Double3 direction)
        {
            if (Class == SurfaceClass.GasGiant) return 0.0;

            Double3 point = direction.Normalized;
            double elevation;

            switch (Class)
            {
                case SurfaceClass.Cratered:
                    elevation = CrateredSurface(point);
                    break;
                case SurfaceClass.IceShell:
                    elevation = IceShellSurface(point);
                    break;
                case SurfaceClass.Volcanic:
                    elevation = VolcanicSurface(point);
                    break;
                default:
                    elevation = ContinentalSurface(point);
                    break;
            }

            return elevation * ReliefKm * 0.5;
        }

        private double _cachedSeaLevelKm;
        private bool _seaLevelValid;

        /// <summary>
        /// Sea level relative to the reference sphere, km. Negative infinity when the body has
        /// no ocean.
        ///
        /// This is found by sampling the elevation field and taking the quantile at
        /// <see cref="OceanFraction"/>, not by interpolating between the extremes. Summed
        /// octaves of noise pile up near their mean rather than spreading evenly, so a linear
        /// guess is badly wrong: asking for 70% ocean on a 20 km relief field and placing sea
        /// level 2 km up puts 97% of the surface under water. Inverting the actual distribution
        /// makes the parameter mean what it says.
        ///
        /// The sample is deterministic and computed once, so a body's coastline never shifts
        /// between sessions.
        /// </summary>
        public double SeaLevelKm
        {
            get
            {
                if (OceanFraction <= 0.0) return double.NegativeInfinity;
                if (OceanFraction >= 1.0) return double.PositiveInfinity;
                if (_seaLevelValid) return _cachedSeaLevelKm;

                const int sampleCount = 2048;
                var elevations = new double[sampleCount];
                var random = new DeterministicRandom(Hash.Derive(Seed, "sea-level"));

                for (int i = 0; i < sampleCount; i++) elevations[i] = ElevationKm(random.OnUnitSphere());

                Array.Sort(elevations);
                int index = (int)MathD.Clamp(System.Math.Floor(OceanFraction * sampleCount), 0, sampleCount - 1);

                _cachedSeaLevelKm = elevations[index];
                _seaLevelValid = true;
                return _cachedSeaLevelKm;
            }
        }

        /// <summary>Drop the cached sea level after changing any field that affects elevation.</summary>
        public void Invalidate() => _seaLevelValid = false;

        public bool HasOcean => OceanFraction > 0.0;

        /// <summary>Whether a point lies under the ocean.</summary>
        public bool IsSubmerged(Double3 direction) => HasOcean && ElevationKm(direction) < SeaLevelKm;

        /// <summary>Depth below sea level in km, or 0 on land.</summary>
        public double OceanDepthKm(Double3 direction)
        {
            if (!HasOcean) return 0.0;
            double elevation = ElevationKm(direction);
            return elevation < SeaLevelKm ? SeaLevelKm - elevation : 0.0;
        }

        /// <summary>Radius from the body's centre to the solid surface, km.</summary>
        public double SurfaceRadiusKm(Double3 direction) => BodyRadiusKm + ElevationKm(direction);

        /// <summary>Radius to the visible surface — the ocean top where submerged, km.</summary>
        public double VisibleRadiusKm(Double3 direction)
        {
            double elevation = ElevationKm(direction);
            if (HasOcean && elevation < SeaLevelKm) return BodyRadiusKm + SeaLevelKm;
            return BodyRadiusKm + elevation;
        }

        /// <summary>
        /// Outward surface normal at a point, by finite differences along two tangents.
        /// </summary>
        public Double3 SurfaceNormal(Double3 direction, double sampleOffsetKm = 0.5)
        {
            Double3 up = direction.Normalized;

            // Any vector not parallel to up gives a usable tangent basis.
            Double3 reference = System.Math.Abs(up.z) < 0.9 ? Double3.UnitZ : Double3.UnitX;
            Double3 east = Double3.Cross(reference, up).Normalized;
            Double3 north = Double3.Cross(up, east);

            double delta = sampleOffsetKm / BodyRadiusKm;

            double heightEast = ElevationKm((up + east * delta).Normalized);
            double heightWest = ElevationKm((up - east * delta).Normalized);
            double heightNorth = ElevationKm((up + north * delta).Normalized);
            double heightSouth = ElevationKm((up - north * delta).Normalized);

            double slopeEast = (heightEast - heightWest) / (2.0 * sampleOffsetKm);
            double slopeNorth = (heightNorth - heightSouth) / (2.0 * sampleOffsetKm);

            return (up - east * slopeEast - north * slopeNorth).Normalized;
        }

        /// <summary>Surface slope in radians, for scree, vegetation masks and landing-site checks.</summary>
        public double SlopeRad(Double3 direction, double sampleOffsetKm = 0.5) =>
            Double3.Angle(direction.Normalized, SurfaceNormal(direction, sampleOffsetKm));

        // ---- Layer composition ----------------------------------------------

        private double ContinentalSurface(Double3 point)
        {
            // Broad provinces, domain-warped so coastlines meander instead of running in the
            // straight-ish lines that unwarped fBm produces.
            double continents = NoiseD.WarpedFbm(Seed, point, _continentSettings, 0.4);

            // Bias toward the requested land fraction.
            continents += (ContinentFraction - 0.5) * 0.8;

            // Ridges, masked to the uplifted regions: mountains belong at plate boundaries,
            // not scattered evenly over ocean floor.
            double uplift = MathD.Clamp01((continents + 0.2) * 1.4);
            double ridges = NoiseD.Ridged(Seed + 101, point, _mountainSettings) * MountainStrength * uplift;

            double detail = NoiseD.Fbm(Seed + 202, point, _detailSettings) * 0.08;

            double elevation = continents + ridges + detail;

            // Erosion proxy: pull the surface toward its local mean, which flattens peaks and
            // fills basins the way transport does, without simulating any of it.
            if (ErosionStrength > 0.0)
            {
                double smoothed = NoiseD.Fbm(Seed, point, _continentSettings.WithOctaves(3));
                elevation = MathD.Lerp(elevation, smoothed, ErosionStrength * 0.5);
            }

            return MathD.Clamp(elevation, -1.0, 1.0);
        }

        private double CrateredSurface(Double3 point)
        {
            double basins = NoiseD.Fbm(Seed, point, _continentSettings.WithOctaves(4)) * 0.5;
            double craters = CraterLayer(point);
            double roughness = NoiseD.Fbm(Seed + 303, point, _detailSettings) * 0.1;
            return MathD.Clamp(basins + craters + roughness, -1.0, 1.0);
        }

        private double IceShellSurface(Double3 point)
        {
            // Fracture networks: ridged noise at low amplitude over an almost flat shell, which
            // is what gives Europa its characteristic lineae.
            double shell = NoiseD.Fbm(Seed, point, _continentSettings.WithOctaves(3)) * 0.15;
            double fractures = -System.Math.Abs(NoiseD.Ridged(Seed + 404, point, _mountainSettings)) * 0.35;
            double chaos = NoiseD.Billow(Seed + 505, point, _detailSettings) * 0.08;
            return MathD.Clamp(shell + fractures + chaos, -1.0, 1.0);
        }

        private double VolcanicSurface(Double3 point)
        {
            double plains = NoiseD.Fbm(Seed, point, _continentSettings) * 0.35;

            // Isolated shields rather than continuous ranges: take only the strongest peaks of
            // a cellular field.
            double cellular = NoiseD.Worley(Seed + 606, point * 6.0);
            double shields = System.Math.Max(0.0, 1.0 - cellular * 1.8) * 0.7;

            double flows = NoiseD.Billow(Seed + 707, point, _detailSettings) * 0.12;
            return MathD.Clamp(plains + shields + flows, -1.0, 1.0);
        }

        /// <summary>
        /// Impact craters from a cellular field, with the rim/floor profile of a real simple
        /// crater: a raised rim, a flat floor, and an ejecta blanket falling off outside.
        /// </summary>
        private double CraterLayer(Double3 point)
        {
            if (CraterDensity <= 0.0) return 0.0;

            double total = 0.0;

            // Several size classes, following the power-law size distribution real crater
            // populations obey: many small, few large.
            for (int scaleIndex = 0; scaleIndex < 4; scaleIndex++)
            {
                double frequency = 3.0 * System.Math.Pow(2.6, scaleIndex);
                double amplitude = CraterDensity * 0.45 / System.Math.Pow(2.0, scaleIndex);

                double distance = NoiseD.Worley(Seed + 800 + scaleIndex, point * frequency);
                total += CraterProfile(distance) * amplitude;
            }

            return total;
        }

        /// <summary>Radial profile of one crater, as a function of normalised distance from its centre.</summary>
        private static double CraterProfile(double normalisedDistance)
        {
            const double floorRadius = 0.30;
            const double rimRadius = 0.42;
            const double ejectaRadius = 0.85;

            if (normalisedDistance < floorRadius)
            {
                // Flat-ish floor, slightly bowl-shaped.
                double t = normalisedDistance / floorRadius;
                return -0.6 + 0.15 * t * t;
            }
            if (normalisedDistance < rimRadius)
            {
                // Inner wall climbing to the rim crest.
                double t = (normalisedDistance - floorRadius) / (rimRadius - floorRadius);
                return MathD.Lerp(-0.45, 0.35, MathD.SmoothStep(0.0, 1.0, t));
            }
            if (normalisedDistance < ejectaRadius)
            {
                // Ejecta blanket decaying outward.
                double t = (normalisedDistance - rimRadius) / (ejectaRadius - rimRadius);
                return 0.35 * System.Math.Pow(1.0 - t, 2.5);
            }
            return 0.0;
        }

        // ---- Climate proxies ------------------------------------------------

        /// <summary>
        /// Surface temperature estimate, K. Combines the body's equilibrium temperature with a
        /// latitude gradient and a lapse rate with altitude. Enough to drive ice caps and
        /// biome selection; nowhere near a climate model.
        /// </summary>
        public double TemperatureK(Double3 direction, double equilibriumTemperatureK, double axialTiltRad = 0.0,
            double lapseRateKPerKm = 6.5)
        {
            Double3 point = direction.Normalized;

            // Latitude relative to the rotation axis, tilted by the obliquity.
            double latitude = MathD.SafeAsin(MathD.Clamp(point.z, -1.0, 1.0));
            double poleFactor = System.Math.Cos(latitude);

            // Equator-to-pole contrast, roughly Earth-like at 40%.
            double baseTemperature = equilibriumTemperatureK * (0.75 + 0.4 * poleFactor);

            // Cooling with altitude above the datum (or sea level, when there is one).
            double datum = HasOcean ? SeaLevelKm : 0.0;
            double altitude = System.Math.Max(0.0, ElevationKm(point) - datum);

            return baseTemperature - altitude * lapseRateKPerKm;
        }

        /// <summary>Relative humidity proxy in [0,1], higher near oceans and at low latitude.</summary>
        public double Moisture(Double3 direction)
        {
            if (!HasOcean) return 0.0;

            Double3 point = direction.Normalized;
            double latitude = MathD.SafeAsin(MathD.Clamp(point.z, -1.0, 1.0));
            double banding = 0.5 + 0.5 * System.Math.Cos(latitude * 3.0);   // stand-in for Hadley cells
            double variation = NoiseD.Fbm(Seed + 909, point, _continentSettings.WithOctaves(4)) * 0.5 + 0.5;

            double altitude = System.Math.Max(0.0, ElevationKm(point) - SeaLevelKm);
            double dryness = MathD.Clamp01(altitude / System.Math.Max(1e-6, ReliefKm * 0.25));

            return MathD.Clamp01(banding * 0.5 + variation * 0.5 - dryness * 0.4);
        }
    }
}
