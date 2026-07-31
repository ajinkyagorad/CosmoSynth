using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    public enum ConicType
    {
        Elliptic,
        Parabolic,
        Hyperbolic
    }

    /// <summary>
    /// Classical (Keplerian) orbital elements for a two-body orbit, in km/s/radians.
    ///
    /// The semi-latus rectum p is stored rather than the semi-major axis a, because a is
    /// infinite for a parabolic orbit while p is finite and well behaved for every conic.
    /// <see cref="SemiMajorAxisKm"/> derives a on demand.
    ///
    /// Degenerate geometries are resolved by convention rather than left undefined:
    ///   - equatorial orbit (i ~ 0 or ~pi): the node is undefined, so RAAN is pinned to 0 and
    ///     the argument of periapsis absorbs it (becoming the longitude of periapsis);
    ///   - circular orbit (e ~ 0): periapsis is undefined, so the argument of periapsis is
    ///     pinned to 0 and the anomaly absorbs it (becoming the argument of latitude).
    /// Both conventions round-trip exactly through <see cref="ToStateVector(double)"/>, which
    /// is what the unit tests assert.
    /// </summary>
    [Serializable]
    public struct OrbitalElements
    {
        /// <summary>Semi-latus rectum p, km. Finite for every conic including the parabola.</summary>
        public double SemiLatusRectumKm;

        /// <summary>Eccentricity. 0 circular, &lt;1 elliptic, 1 parabolic, &gt;1 hyperbolic.</summary>
        public double Eccentricity;

        /// <summary>Inclination, radians, in [0, pi].</summary>
        public double InclinationRad;

        /// <summary>Right ascension / longitude of the ascending node, radians, in [0, 2pi).</summary>
        public double LongitudeOfAscendingNodeRad;

        /// <summary>Argument of periapsis, radians, in [0, 2pi).</summary>
        public double ArgumentOfPeriapsisRad;

        /// <summary>
        /// Mean anomaly at <see cref="Epoch"/>, radians. For hyperbolic orbits this is the
        /// hyperbolic mean anomaly (e sinh H - H); for parabolic orbits it is Barker's
        /// parameter D + D^3/3. In all three cases it advances linearly at <see cref="MeanMotion"/>.
        /// </summary>
        public double MeanAnomalyAtEpochRad;

        /// <summary>The instant at which <see cref="MeanAnomalyAtEpochRad"/> applies.</summary>
        public CosmicTime Epoch;

        /// <summary>Gravitational parameter of the central body, km^3/s^2.</summary>
        public double GravitationalParameter;

        // ---- Derived geometry ----------------------------------------------

        public ConicType Conic
        {
            get
            {
                if (System.Math.Abs(Eccentricity - 1.0) < KeplerSolver.ParabolicTolerance) return ConicType.Parabolic;
                return Eccentricity < 1.0 ? ConicType.Elliptic : ConicType.Hyperbolic;
            }
        }

        public bool IsClosed => Conic == ConicType.Elliptic;

        /// <summary>Semi-major axis a, km. Positive for ellipses, negative for hyperbolas, infinite for parabolas.</summary>
        public double SemiMajorAxisKm
        {
            get
            {
                if (Conic == ConicType.Parabolic) return double.PositiveInfinity;
                return SemiLatusRectumKm / (1.0 - Eccentricity * Eccentricity);
            }
        }

        /// <summary>Periapsis distance q, km. Always finite.</summary>
        public double PeriapsisKm => SemiLatusRectumKm / (1.0 + Eccentricity);

        /// <summary>Apoapsis distance, km. Infinite for open orbits.</summary>
        public double ApoapsisKm =>
            Eccentricity < 1.0 - KeplerSolver.ParabolicTolerance
                ? SemiLatusRectumKm / (1.0 - Eccentricity)
                : double.PositiveInfinity;

        /// <summary>Specific angular momentum h, km^2/s.</summary>
        public double SpecificAngularMomentum => System.Math.Sqrt(GravitationalParameter * SemiLatusRectumKm);

        /// <summary>
        /// Rate at which the mean anomaly advances, rad/s. Defined uniformly across conics so
        /// that M(t) = M0 + n*(t - epoch) holds for all three.
        /// </summary>
        public double MeanMotion
        {
            get
            {
                switch (Conic)
                {
                    case ConicType.Elliptic:
                    {
                        double a = SemiMajorAxisKm;
                        return System.Math.Sqrt(GravitationalParameter / (a * a * a));
                    }
                    case ConicType.Hyperbolic:
                    {
                        double a = -SemiMajorAxisKm;
                        return System.Math.Sqrt(GravitationalParameter / (a * a * a));
                    }
                    default:
                    {
                        double q = 0.5 * SemiLatusRectumKm;
                        return System.Math.Sqrt(GravitationalParameter / (2.0 * q * q * q));
                    }
                }
            }
        }

        /// <summary>Orbital period in seconds, or infinity for open orbits.</summary>
        public double PeriodSeconds => IsClosed ? MathD.TwoPi / MeanMotion : double.PositiveInfinity;

        /// <summary>Unit normal to the orbital plane (direction of angular momentum).</summary>
        public Double3 OrbitNormal
        {
            get
            {
                double si = System.Math.Sin(InclinationRad);
                double ci = System.Math.Cos(InclinationRad);
                double so = System.Math.Sin(LongitudeOfAscendingNodeRad);
                double co = System.Math.Cos(LongitudeOfAscendingNodeRad);
                return new Double3(so * si, -co * si, ci);
            }
        }

        /// <summary>Unit vector from the central body toward periapsis.</summary>
        public Double3 PeriapsisDirection => PerifocalBasisP();

        // ---- Construction ---------------------------------------------------

        /// <summary>Build elements from the more familiar semi-major axis form.</summary>
        public static OrbitalElements FromSemiMajorAxis(
            double semiMajorAxisKm, double eccentricity, double inclinationRad,
            double longitudeOfAscendingNodeRad, double argumentOfPeriapsisRad,
            double meanAnomalyAtEpochRad, CosmicTime epoch, double gravitationalParameter)
        {
            if (System.Math.Abs(eccentricity - 1.0) < KeplerSolver.ParabolicTolerance)
                throw new ArgumentException(
                    "A parabolic orbit has no finite semi-major axis; construct it from the semi-latus rectum instead.",
                    nameof(eccentricity));

            return new OrbitalElements
            {
                SemiLatusRectumKm = semiMajorAxisKm * (1.0 - eccentricity * eccentricity),
                Eccentricity = eccentricity,
                InclinationRad = inclinationRad,
                LongitudeOfAscendingNodeRad = MathD.WrapTwoPi(longitudeOfAscendingNodeRad),
                ArgumentOfPeriapsisRad = MathD.WrapTwoPi(argumentOfPeriapsisRad),
                MeanAnomalyAtEpochRad = meanAnomalyAtEpochRad,
                Epoch = epoch,
                GravitationalParameter = gravitationalParameter
            };
        }

        /// <summary>
        /// Recover classical elements from a Cartesian state. This is the inverse of
        /// <see cref="ToStateVector(double)"/> and the two are asserted to round-trip.
        /// </summary>
        public static OrbitalElements FromStateVector(StateVector state, double gravitationalParameter, CosmicTime epoch)
        {
            if (gravitationalParameter <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(gravitationalParameter), "GM must be positive.");

            Double3 r = state.Position;
            Double3 v = state.Velocity;
            double radius = r.Magnitude;
            if (radius <= 0.0)
                throw new ArgumentException("Cannot derive elements from a state at the central body's centre.", nameof(state));

            Double3 hVec = Double3.Cross(r, v);
            double h = hVec.Magnitude;
            if (h <= 0.0)
                throw new ArgumentException(
                    "Degenerate radial trajectory (zero angular momentum) has no conic section.", nameof(state));

            Double3 w = hVec / h;                                  // orbit normal
            double p = h * h / gravitationalParameter;             // semi-latus rectum

            Double3 eVec = state.EccentricityVector(gravitationalParameter);
            double e = eVec.Magnitude;

            // Inclination from the normal. atan2 keeps full precision for near-polar orbits
            // where acos(w.z) would be flat.
            double inclination = System.Math.Atan2(System.Math.Sqrt(w.x * w.x + w.y * w.y), w.z);

            // Node line: z_hat x h_vec. Vanishes for equatorial orbits, where we pin RAAN to 0.
            Double3 nodeVec = new Double3(-hVec.y, hVec.x, 0.0);
            double nodeMag = nodeVec.Magnitude;

            Double3 nodeDir;
            double raan;
            if (nodeMag < KeplerSolver.EquatorialTolerance * h)
            {
                nodeDir = Double3.UnitX;
                raan = 0.0;
            }
            else
            {
                nodeDir = nodeVec / nodeMag;
                raan = MathD.WrapTwoPi(System.Math.Atan2(nodeDir.y, nodeDir.x));
            }

            // Periapsis direction. Vanishes for circular orbits, where we pin argp to 0.
            Double3 periDir = e < KeplerSolver.CircularTolerance ? nodeDir : eVec / e;

            // Signed angles measured about the orbit normal, so every quadrant is handled
            // without a cascade of sign tests.
            double argp = e < KeplerSolver.CircularTolerance
                ? 0.0
                : MathD.WrapTwoPi(SignedAngleAbout(nodeDir, periDir, w));

            Double3 radialDir = r / radius;
            double trueAnomaly = SignedAngleAbout(periDir, radialDir, w);

            var elements = new OrbitalElements
            {
                SemiLatusRectumKm = p,
                Eccentricity = e,
                InclinationRad = inclination,
                LongitudeOfAscendingNodeRad = raan,
                ArgumentOfPeriapsisRad = argp,
                MeanAnomalyAtEpochRad = 0.0,
                Epoch = epoch,
                GravitationalParameter = gravitationalParameter
            };

            elements.MeanAnomalyAtEpochRad = elements.MeanAnomalyFromTrueAnomaly(trueAnomaly);
            return elements;
        }

        /// <summary>Angle from <paramref name="from"/> to <paramref name="to"/> measured right-handed about <paramref name="axis"/>.</summary>
        private static double SignedAngleAbout(Double3 from, Double3 to, Double3 axis)
        {
            double sin = Double3.Dot(Double3.Cross(axis, from), to);
            double cos = Double3.Dot(from, to);
            return System.Math.Atan2(sin, cos);
        }

        // ---- Anomaly conversions -------------------------------------------

        /// <summary>Convert a true anomaly to the mean anomaly appropriate for this conic.</summary>
        public double MeanAnomalyFromTrueAnomaly(double trueAnomalyRad)
        {
            switch (Conic)
            {
                case ConicType.Elliptic:
                {
                    double ecc = KeplerSolver.EccentricFromTrue(trueAnomalyRad, Eccentricity);
                    return KeplerSolver.MeanFromEccentric(ecc, Eccentricity);
                }
                case ConicType.Hyperbolic:
                {
                    double hyp = KeplerSolver.HyperbolicFromTrue(trueAnomalyRad, Eccentricity);
                    return KeplerSolver.MeanFromHyperbolic(hyp, Eccentricity);
                }
                default:
                    return KeplerSolver.ParabolicMeanFromTrue(trueAnomalyRad);
            }
        }

        /// <summary>Convert a mean anomaly to the true anomaly, solving Kepler's equation as needed.</summary>
        public double TrueAnomalyFromMeanAnomaly(double meanAnomalyRad)
        {
            switch (Conic)
            {
                case ConicType.Elliptic:
                {
                    double ecc = KeplerSolver.SolveEccentricAnomaly(meanAnomalyRad, Eccentricity);
                    return KeplerSolver.TrueFromEccentric(ecc, Eccentricity);
                }
                case ConicType.Hyperbolic:
                {
                    double hyp = KeplerSolver.SolveHyperbolicAnomaly(meanAnomalyRad, Eccentricity);
                    return KeplerSolver.TrueFromHyperbolic(hyp, Eccentricity);
                }
                default:
                    return KeplerSolver.TrueFromParabolicMean(meanAnomalyRad);
            }
        }

        /// <summary>Mean anomaly at an arbitrary instant.</summary>
        public double MeanAnomalyAt(CosmicTime time) =>
            MeanAnomalyAtEpochRad + MeanMotion * (time - Epoch);

        /// <summary>True anomaly at an arbitrary instant.</summary>
        public double TrueAnomalyAt(CosmicTime time) => TrueAnomalyFromMeanAnomaly(MeanAnomalyAt(time));

        // ---- Cartesian reconstruction ---------------------------------------

        /// <summary>Perifocal basis vector pointing at periapsis, expressed in the parent frame.</summary>
        public Double3 PerifocalBasisP()
        {
            double so = System.Math.Sin(LongitudeOfAscendingNodeRad), co = System.Math.Cos(LongitudeOfAscendingNodeRad);
            double sw = System.Math.Sin(ArgumentOfPeriapsisRad), cw = System.Math.Cos(ArgumentOfPeriapsisRad);
            double si = System.Math.Sin(InclinationRad), ci = System.Math.Cos(InclinationRad);
            return new Double3(
                co * cw - so * sw * ci,
                so * cw + co * sw * ci,
                sw * si);
        }

        /// <summary>Perifocal basis vector 90 degrees ahead of periapsis in the direction of motion.</summary>
        public Double3 PerifocalBasisQ()
        {
            double so = System.Math.Sin(LongitudeOfAscendingNodeRad), co = System.Math.Cos(LongitudeOfAscendingNodeRad);
            double sw = System.Math.Sin(ArgumentOfPeriapsisRad), cw = System.Math.Cos(ArgumentOfPeriapsisRad);
            double si = System.Math.Sin(InclinationRad), ci = System.Math.Cos(InclinationRad);
            return new Double3(
                -co * sw - so * cw * ci,
                -so * sw + co * cw * ci,
                cw * si);
        }

        /// <summary>Position and velocity at a given true anomaly.</summary>
        public StateVector ToStateVector(double trueAnomalyRad)
        {
            double cosNu = System.Math.Cos(trueAnomalyRad);
            double sinNu = System.Math.Sin(trueAnomalyRad);

            double denominator = 1.0 + Eccentricity * cosNu;
            double radius = SemiLatusRectumKm / denominator;

            Double3 pHat = PerifocalBasisP();
            Double3 qHat = PerifocalBasisQ();

            // This velocity form is valid for every conic — it never references the semi-major
            // axis, so it does not blow up as e approaches 1.
            double vScale = System.Math.Sqrt(GravitationalParameter / SemiLatusRectumKm);

            Double3 position = (pHat * cosNu + qHat * sinNu) * radius;
            Double3 velocity = (pHat * (-sinNu) + qHat * (Eccentricity + cosNu)) * vScale;

            return new StateVector(position, velocity);
        }

        /// <summary>Position and velocity at an arbitrary instant — the main propagation entry point.</summary>
        public StateVector Propagate(CosmicTime time) => ToStateVector(TrueAnomalyAt(time));

        /// <summary>Position only, skipping the velocity terms. Marginally cheaper for rendering-only paths.</summary>
        public Double3 PositionAt(CosmicTime time)
        {
            double nu = TrueAnomalyAt(time);
            double radius = SemiLatusRectumKm / (1.0 + Eccentricity * System.Math.Cos(nu));
            return (PerifocalBasisP() * System.Math.Cos(nu) + PerifocalBasisQ() * System.Math.Sin(nu)) * radius;
        }

        // ---- Path sampling for rendering ------------------------------------

        /// <summary>
        /// Sample points along the orbit for a line renderer.
        ///
        /// Sampling is uniform in eccentric anomaly rather than true anomaly. Uniform true
        /// anomaly starves the apoapsis half of an eccentric orbit of vertices and clusters
        /// them uselessly at periapsis; uniform eccentric anomaly distributes them by arc
        /// length instead, so a comet's orbit renders smoothly at both ends.
        /// </summary>
        public List<Double3> SampleOrbitPath(int sampleCount = 256, double hyperbolicTrueAnomalyLimit = 0.98)
        {
            if (sampleCount < 2) sampleCount = 2;
            var points = new List<Double3>(sampleCount);

            Double3 pHat = PerifocalBasisP();
            Double3 qHat = PerifocalBasisQ();

            if (Conic == ConicType.Elliptic)
            {
                double a = SemiMajorAxisKm;
                double b = a * System.Math.Sqrt(1.0 - Eccentricity * Eccentricity);
                double focusOffset = a * Eccentricity;

                // Directly parameterising the ellipse in eccentric anomaly avoids solving
                // Kepler's equation once per vertex.
                for (int i = 0; i < sampleCount; i++)
                {
                    double ecc = MathD.TwoPi * i / (sampleCount - 1);
                    double xPerifocal = a * System.Math.Cos(ecc) - focusOffset;
                    double yPerifocal = b * System.Math.Sin(ecc);
                    points.Add(pHat * xPerifocal + qHat * yPerifocal);
                }
            }
            else
            {
                // Open orbit: sweep true anomaly up to a fraction of the asymptote, since the
                // conic runs to infinity exactly at it.
                double limit = Conic == ConicType.Hyperbolic
                    ? MathD.SafeAcos(-1.0 / Eccentricity) * hyperbolicTrueAnomalyLimit
                    : MathD.Pi * hyperbolicTrueAnomalyLimit;

                for (int i = 0; i < sampleCount; i++)
                {
                    double nu = -limit + 2.0 * limit * i / (sampleCount - 1);
                    double radius = SemiLatusRectumKm / (1.0 + Eccentricity * System.Math.Cos(nu));
                    points.Add((pHat * System.Math.Cos(nu) + qHat * System.Math.Sin(nu)) * radius);
                }
            }

            return points;
        }

        public override string ToString() =>
            $"a={SemiMajorAxisKm:G6} km, e={Eccentricity:F6}, i={InclinationRad * MathD.Rad2Deg:F4}deg, " +
            $"RAAN={LongitudeOfAscendingNodeRad * MathD.Rad2Deg:F4}deg, " +
            $"argp={ArgumentOfPeriapsisRad * MathD.Rad2Deg:F4}deg, " +
            $"M0={MeanAnomalyAtEpochRad * MathD.Rad2Deg:F4}deg @ {Epoch}";
    }
}
