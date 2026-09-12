using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// The fixed rotations between the inertial frames CosmoSynth ingests data in.
    ///
    /// The canonical internal frame is <b>ICRF / J2000 equatorial</b>. That choice follows the
    /// data: SPICE kernels, Gaia astrometry, SIMBAD, NED and Horizons' default vector output
    /// all live there, so making it the internal frame means the common path involves no
    /// rotation at all and therefore no opportunity to apply one twice or backwards.
    ///
    /// The ecliptic frame is used for presentation — planetary inclinations of a few degrees
    /// are meaningful to a viewer in a way that "everything is tilted 23.4 degrees" is not.
    /// </summary>
    public static class StandardFrames
    {
        /// <summary>Arcseconds to radians.</summary>
        public const double ArcsecToRad = MathD.Pi / (180.0 * 3600.0);

        /// <summary>
        /// Mean obliquity of the ecliptic at J2000 per IAU 2006: 84381.406 arcseconds.
        /// This is the current standard and the one SOFA/ERFA use.
        /// </summary>
        public const double ObliquityJ2000Iau2006Arcsec = 84381.406;

        /// <summary>
        /// Mean obliquity at J2000 per IAU 1976: 84381.448 arcseconds. Retained because the
        /// JPL DE ephemerides and Horizons' ecliptic output are built on it, so comparing our
        /// elements against theirs requires this value, not the 2006 one. The two differ by
        /// 42 milliarcseconds — negligible for rendering, but enough to show up as a
        /// systematic residual when checking inclinations against JPL at full precision.
        /// </summary>
        public const double ObliquityJ2000Iau1976Arcsec = 84381.448;

        public const double ObliquityJ2000Iau2006Rad = ObliquityJ2000Iau2006Arcsec * ArcsecToRad;
        public const double ObliquityJ2000Iau1976Rad = ObliquityJ2000Iau1976Arcsec * ArcsecToRad;

        /// <summary>
        /// Rotation taking a vector from the ICRF equatorial frame into the ecliptic frame of
        /// J2000. The two frames share the x axis (the equinox direction) and differ by the
        /// obliquity about it.
        ///
        /// Note the sign. Tilting the *frame* by +epsilon transforms the *components* of a
        /// vector by -epsilon, and <see cref="Matrix3d.RotationX"/> rotates vectors. Getting
        /// this backwards does not produce an obviously broken result — it doubles the tilt to
        /// 46.9 degrees, which still looks like a plausible solar system until you check an
        /// inclination against a catalogue.
        /// </summary>
        public static Matrix3d EquatorialToEcliptic(double obliquityRad = ObliquityJ2000Iau1976Rad) =>
            Matrix3d.RotationX(-obliquityRad);

        /// <summary>Rotation taking a vector from the ecliptic frame of J2000 into ICRF equatorial.</summary>
        public static Matrix3d EclipticToEquatorial(double obliquityRad = ObliquityJ2000Iau1976Rad) =>
            Matrix3d.RotationX(obliquityRad);

        /// <summary>Convenience: rotate a single position or velocity into the ecliptic frame.</summary>
        public static Double3 ToEcliptic(Double3 equatorial, double obliquityRad = ObliquityJ2000Iau1976Rad) =>
            EquatorialToEcliptic(obliquityRad) * equatorial;

        /// <summary>Convenience: rotate a single position or velocity into the equatorial frame.</summary>
        public static Double3 ToEquatorial(Double3 ecliptic, double obliquityRad = ObliquityJ2000Iau1976Rad) =>
            EclipticToEquatorial(obliquityRad) * ecliptic;

        /// <summary>Rotate a whole state vector into the ecliptic frame.</summary>
        public static StateVector ToEcliptic(StateVector equatorial, double obliquityRad = ObliquityJ2000Iau1976Rad)
        {
            Matrix3d r = EquatorialToEcliptic(obliquityRad);
            return new StateVector(r * equatorial.Position, r * equatorial.Velocity);
        }

        /// <summary>Rotate a whole state vector into the equatorial frame.</summary>
        public static StateVector ToEquatorial(StateVector ecliptic, double obliquityRad = ObliquityJ2000Iau1976Rad)
        {
            Matrix3d r = EclipticToEquatorial(obliquityRad);
            return new StateVector(r * ecliptic.Position, r * ecliptic.Velocity);
        }

        // ---- Galactic ------------------------------------------------------

        /// <summary>ICRS right ascension of the north galactic pole, degrees.</summary>
        public const double GalacticPoleRaDeg = 192.85948;

        /// <summary>ICRS declination of the north galactic pole, degrees.</summary>
        public const double GalacticPoleDecDeg = 27.12825;

        /// <summary>Galactic longitude of the ascending node of the galactic plane on the ICRS equator, degrees.</summary>
        public const double GalacticAscendingNodeLongitudeDeg = 32.93192;

        private static Matrix3d? _equatorialToGalactic;

        /// <summary>
        /// Rotation from ICRF equatorial into galactic coordinates, built from the IAU-defined
        /// pole and node angles. Needed to lay Gaia's astrometry onto a Milky Way model, whose
        /// structure is naturally described in galactic longitude and latitude.
        /// </summary>
        public static Matrix3d EquatorialToGalactic
        {
            get
            {
                if (_equatorialToGalactic == null)
                {
                    // Standard three-rotation construction: swing the x axis round to the node,
                    // tip the pole down to the z axis, then rotate to the galactic longitude
                    // origin.
                    //
                    // Every sign here was wrong until 2026-09-12, and nothing caught it because
                    // the matrix had no test. Matrix3d's rotations are ACTIVE — they rotate a
                    // vector, not the frame — so mapping components INTO a frame turned by +a
                    // needs RotationZ(-a), and the shipped code had each of the three the other
                    // way round. The north galactic pole came out at (-0.346, 0.176, 0.922)
                    // instead of (0, 0, 1): a 45-degree error that still produces a plausible
                    // band of stars across a plausible sky. Checked against the Hipparcos matrix
                    // element by element, now to 4.9e-11.
                    _equatorialToGalactic =
                        Matrix3d.RotationZ(+GalacticAscendingNodeLongitudeDeg * MathD.Deg2Rad) *
                        Matrix3d.RotationX(-(90.0 - GalacticPoleDecDeg) * MathD.Deg2Rad) *
                        Matrix3d.RotationZ(-(90.0 + GalacticPoleRaDeg) * MathD.Deg2Rad);
                }
                return _equatorialToGalactic.Value;
            }
        }

        public static Matrix3d GalacticToEquatorial => EquatorialToGalactic.InverseRotation;

        // ---- Supergalactic -------------------------------------------------

        /// <summary>Galactic longitude of the north supergalactic pole, degrees (de Vaucouleurs).</summary>
        public const double SupergalacticPoleGalacticLonDeg = 47.37;

        /// <summary>Galactic latitude of the north supergalactic pole, degrees.</summary>
        public const double SupergalacticPoleGalacticLatDeg = 6.32;

        /// <summary>Galactic longitude where supergalactic longitude is zero, degrees.</summary>
        public const double SupergalacticOriginGalacticLonDeg = 137.37;

        private static Matrix3d? _galacticToSupergalactic;

        /// <summary>
        /// Rotation from galactic into supergalactic coordinates.
        ///
        /// Built from the two defining directions rather than from a copied table: the pole is
        /// the z row, the SGL = 0 direction orthonormalised against it is the x row, and y is
        /// their cross product. Agrees with the published matrix to 3.5e-11, and with the CF4
        /// catalogue's own supergalactic columns to a median of 0.17 arcsec.
        ///
        /// This is the frame the local cosmic web is described in — Laniakea, the Great
        /// Attractor, the flow basins — so it is the frame those layers are levelled to.
        /// </summary>
        public static Matrix3d GalacticToSupergalactic
        {
            get
            {
                if (_galacticToSupergalactic == null)
                {
                    Double3 z = FromSpherical(SupergalacticPoleGalacticLonDeg * MathD.Deg2Rad,
                                              SupergalacticPoleGalacticLatDeg * MathD.Deg2Rad);
                    Double3 seed = FromSpherical(SupergalacticOriginGalacticLonDeg * MathD.Deg2Rad, 0.0);
                    Double3 x = (seed - z * Double3.Dot(seed, z)).Normalized;
                    Double3 y = Double3.Cross(z, x);
                    _galacticToSupergalactic = Matrix3d.FromRows(x, y, z);
                }
                return _galacticToSupergalactic.Value;
            }
        }

        public static Matrix3d SupergalacticToGalactic => GalacticToSupergalactic.InverseRotation;

        /// <summary>Rotation from ICRF equatorial straight into supergalactic coordinates.</summary>
        public static Matrix3d EquatorialToSupergalactic => GalacticToSupergalactic * EquatorialToGalactic;

        public static Matrix3d SupergalacticToEquatorial => EquatorialToSupergalactic.InverseRotation;

        // ---- Spherical <-> Cartesian ---------------------------------------

        /// <summary>
        /// Unit vector from a right ascension and declination (or any longitude/latitude pair
        /// on a sphere), both in radians.
        /// </summary>
        public static Double3 FromSpherical(double longitudeRad, double latitudeRad)
        {
            double cosLat = System.Math.Cos(latitudeRad);
            return new Double3(
                cosLat * System.Math.Cos(longitudeRad),
                cosLat * System.Math.Sin(longitudeRad),
                System.Math.Sin(latitudeRad));
        }

        /// <summary>Position from spherical coordinates and a distance.</summary>
        public static Double3 FromSpherical(double longitudeRad, double latitudeRad, double distance) =>
            FromSpherical(longitudeRad, latitudeRad) * distance;

        /// <summary>
        /// Longitude (in [0, 2pi)), latitude (in [-pi/2, pi/2]) and distance of a position.
        /// </summary>
        public static void ToSpherical(Double3 position, out double longitudeRad, out double latitudeRad,
            out double distance)
        {
            distance = position.Magnitude;
            if (distance <= 0.0)
            {
                longitudeRad = 0.0;
                latitudeRad = 0.0;
                return;
            }
            longitudeRad = MathD.WrapTwoPi(System.Math.Atan2(position.y, position.x));
            latitudeRad = MathD.SafeAsin(position.z / distance);
        }

        /// <summary>
        /// Distance in km implied by a parallax in milliarcseconds.
        ///
        /// The naive inversion d = 1/parallax is only valid when the parallax is measured far
        /// more precisely than its own value. Gaia publishes many sources with parallax
        /// uncertainties comparable to (or larger than) the parallax itself, and a few with
        /// negative parallaxes, which are physically meaningful measurements but obviously not
        /// invertible. Callers must filter on the fractional uncertainty first; this method
        /// returns NaN rather than a fabricated distance for non-positive input.
        /// </summary>
        public static double DistanceFromParallaxMas(double parallaxMilliarcsec)
        {
            if (parallaxMilliarcsec <= 0.0 || double.IsNaN(parallaxMilliarcsec)) return double.NaN;
            // 1 arcsec of parallax = 1 parsec, so 1 mas = 1000 pc.
            return (1000.0 / parallaxMilliarcsec) * Constants.Parsec;
        }
    }
}
