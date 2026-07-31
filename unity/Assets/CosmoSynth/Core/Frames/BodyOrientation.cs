using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Orientation of a rotating body, in the form the IAU Working Group on Cartographic
    /// Coordinates and Rotational Elements publishes it: a pole direction that drifts slowly
    /// with precession, plus a prime meridian angle that advances with the rotation rate.
    ///
    ///     alpha = alpha0 + alphaDot * T        (T = Julian centuries past J2000, TDB)
    ///     delta = delta0 + deltaDot * T
    ///     W     = W0     + WDot     * d        (d = days past J2000, TDB)
    ///
    /// Keeping this separate from <see cref="ReferenceFrame"/> mirrors NAIF's own split
    /// between where a body is (SPK) and which way it points (PCK). Rotation only ever
    /// matters at the surface — for picking a landing site, aiming an instrument, or turning
    /// a texture the right way up — and folding it into the position tree would make every
    /// moon's orbit appear to precess with its planet's spin.
    /// </summary>
    [Serializable]
    public struct BodyOrientation
    {
        /// <summary>Pole right ascension at J2000, degrees.</summary>
        public double PoleRaDeg;

        /// <summary>Pole right ascension drift, degrees per Julian century.</summary>
        public double PoleRaRateDegPerCentury;

        /// <summary>Pole declination at J2000, degrees.</summary>
        public double PoleDecDeg;

        /// <summary>Pole declination drift, degrees per Julian century.</summary>
        public double PoleDecRateDegPerCentury;

        /// <summary>Prime meridian angle at J2000, degrees.</summary>
        public double PrimeMeridianDeg;

        /// <summary>Rotation rate, degrees per day. Negative for a retrograde rotator such as Venus.</summary>
        public double RotationRateDegPerDay;

        public BodyOrientation(double poleRaDeg, double poleDecDeg, double primeMeridianDeg,
            double rotationRateDegPerDay, double poleRaRateDegPerCentury = 0.0, double poleDecRateDegPerCentury = 0.0)
        {
            PoleRaDeg = poleRaDeg;
            PoleDecDeg = poleDecDeg;
            PrimeMeridianDeg = primeMeridianDeg;
            RotationRateDegPerDay = rotationRateDegPerDay;
            PoleRaRateDegPerCentury = poleRaRateDegPerCentury;
            PoleDecRateDegPerCentury = poleDecRateDegPerCentury;
        }

        /// <summary>Pole right ascension at an instant, radians.</summary>
        public double PoleRaAt(CosmicTime time) =>
            (PoleRaDeg + PoleRaRateDegPerCentury * time.JulianCenturiesPastJ2000) * MathD.Deg2Rad;

        /// <summary>Pole declination at an instant, radians.</summary>
        public double PoleDecAt(CosmicTime time) =>
            (PoleDecDeg + PoleDecRateDegPerCentury * time.JulianCenturiesPastJ2000) * MathD.Deg2Rad;

        /// <summary>Prime meridian angle at an instant, radians, wrapped to [0, 2pi).</summary>
        public double PrimeMeridianAt(CosmicTime time) =>
            MathD.WrapTwoPi((PrimeMeridianDeg + RotationRateDegPerDay * time.DaysPastJ2000) * MathD.Deg2Rad);

        /// <summary>Unit vector along the body's north pole, in ICRF.</summary>
        public Double3 PoleDirectionAt(CosmicTime time) =>
            StandardFrames.FromSpherical(PoleRaAt(time), PoleDecAt(time));

        /// <summary>Sidereal rotation period in seconds. Positive magnitude; sign indicates direction.</summary>
        public double SiderealPeriodSeconds =>
            RotationRateDegPerDay == 0.0
                ? double.PositiveInfinity
                : 360.0 / RotationRateDegPerDay * Constants.SecondsPerDay;

        /// <summary>True for a retrograde rotator (Venus, Uranus, Pluto).</summary>
        public bool IsRetrograde => RotationRateDegPerDay < 0.0;

        /// <summary>
        /// Rotation taking a vector from body-fixed coordinates into ICRF. The standard IAU
        /// three-rotation construction: swing to the pole's right ascension, tip to its
        /// declination, then spin by the prime meridian angle.
        /// </summary>
        public Matrix3d BodyFixedToIcrf(CosmicTime time) =>
            Matrix3d.RotationZ(PoleRaAt(time) + MathD.HalfPi) *
            Matrix3d.RotationX(MathD.HalfPi - PoleDecAt(time)) *
            Matrix3d.RotationZ(PrimeMeridianAt(time));

        /// <summary>Rotation taking a vector from ICRF into body-fixed coordinates.</summary>
        public Matrix3d IcrfToBodyFixed(CosmicTime time) => BodyFixedToIcrf(time).InverseRotation;

        // ---- Surface coordinates -------------------------------------------

        /// <summary>
        /// Body-fixed position of a point on the reference ellipsoid, from planetocentric
        /// latitude and east longitude in degrees plus an altitude above the surface.
        ///
        /// This uses planetocentric latitude (the angle at the body's centre), which is what
        /// most planetary datasets outside Earth are gridded on. Earth's own products are
        /// usually planetodetic (the angle of the surface normal), and the two differ by up to
        /// 0.19 degrees — roughly 21 km on the ground, which is more than enough to put a
        /// landing site in the wrong crater.
        /// </summary>
        public static Double3 SurfacePointCentric(double latitudeDeg, double eastLongitudeDeg,
            double equatorialRadiusKm, double polarRadiusKm, double altitudeKm = 0.0)
        {
            double lat = latitudeDeg * MathD.Deg2Rad;
            double lon = eastLongitudeDeg * MathD.Deg2Rad;

            double cosLat = System.Math.Cos(lat), sinLat = System.Math.Sin(lat);
            Double3 direction = new Double3(cosLat * System.Math.Cos(lon), cosLat * System.Math.Sin(lon), sinLat);

            // Radius of the ellipsoid along that direction.
            double a = equatorialRadiusKm, c = polarRadiusKm;
            double horizontal = cosLat / a, vertical = sinLat / c;
            double radius = 1.0 / System.Math.Sqrt(horizontal * horizontal + vertical * vertical);

            return direction * (radius + altitudeKm);
        }

        /// <summary>Planetocentric latitude, east longitude (degrees) and radius (km) of a body-fixed position.</summary>
        public static void ToSurfaceCoordinates(Double3 bodyFixedKm, out double latitudeDeg,
            out double eastLongitudeDeg, out double radiusKm)
        {
            StandardFrames.ToSpherical(bodyFixedKm, out double lon, out double lat, out radiusKm);
            latitudeDeg = lat * MathD.Rad2Deg;
            eastLongitudeDeg = MathD.WrapTwoPi(lon) * MathD.Rad2Deg;
        }

        /// <summary>
        /// Convert planetodetic (surface-normal) latitude to planetocentric latitude, both in
        /// degrees, for a body with the given flattening.
        /// </summary>
        public static double GeodeticToCentricLatitude(double geodeticLatitudeDeg, double equatorialRadiusKm,
            double polarRadiusKm)
        {
            double ratio = polarRadiusKm / equatorialRadiusKm;
            return System.Math.Atan(ratio * ratio * System.Math.Tan(geodeticLatitudeDeg * MathD.Deg2Rad)) * MathD.Rad2Deg;
        }

        /// <summary>Convert planetocentric latitude to planetodetic latitude, both in degrees.</summary>
        public static double CentricToGeodeticLatitude(double centricLatitudeDeg, double equatorialRadiusKm,
            double polarRadiusKm)
        {
            double ratio = equatorialRadiusKm / polarRadiusKm;
            return System.Math.Atan(ratio * ratio * System.Math.Tan(centricLatitudeDeg * MathD.Deg2Rad)) * MathD.Rad2Deg;
        }

        /// <summary>
        /// Sub-solar (or sub-anything) point: where the direction to <paramref name="targetIcrf"/>
        /// pierces the body's surface, in planetocentric latitude/east longitude degrees.
        /// This is what drives day/night terminators and illumination queries.
        /// </summary>
        public void SubPoint(Double3 targetIcrf, CosmicTime time, out double latitudeDeg, out double eastLongitudeDeg)
        {
            Double3 bodyFixed = IcrfToBodyFixed(time) * targetIcrf;
            ToSurfaceCoordinates(bodyFixed, out latitudeDeg, out eastLongitudeDeg, out _);
        }

        // ---- Published rotational elements ----------------------------------
        // IAU WGCCRE 2015 report (Archinal et al., Celest Mech Dyn Astr 130:22, 2018).
        // The small periodic terms some bodies carry are omitted; they are below a tenth of a
        // degree and matter for cartography, not for rendering.

        public static readonly BodyOrientation Sun = new BodyOrientation(286.13, 63.87, 84.176, 14.1844000);
        public static readonly BodyOrientation Mercury = new BodyOrientation(281.0103, 61.4155, 329.5988, 6.1385108, -0.0328, -0.0049);
        public static readonly BodyOrientation Venus = new BodyOrientation(272.76, 67.16, 160.20, -1.4813688);
        public static readonly BodyOrientation Earth = new BodyOrientation(0.00, 90.00, 190.147, 360.9856235, -0.641, -0.557);
        public static readonly BodyOrientation Moon = new BodyOrientation(269.9949, 66.5392, 38.3213, 13.17635815, 0.0031, 0.0130);
        public static readonly BodyOrientation Mars = new BodyOrientation(317.269202, 54.432516, 176.049863, 350.891982443297, -0.10927547, -0.05827105);
        public static readonly BodyOrientation Jupiter = new BodyOrientation(268.056595, 64.495303, 284.95, 870.5360000, -0.006499, 0.002413);
        public static readonly BodyOrientation Saturn = new BodyOrientation(40.589, 83.537, 38.90, 810.7939024, -0.036, -0.004);
        public static readonly BodyOrientation Uranus = new BodyOrientation(257.311, -15.175, 203.81, -501.1600928);
        public static readonly BodyOrientation Neptune = new BodyOrientation(299.36, 43.46, 253.18, 536.3128492);
        public static readonly BodyOrientation Pluto = new BodyOrientation(132.993, -6.163, 302.695, 56.3625225);
        public static readonly BodyOrientation Ceres = new BodyOrientation(291.418, 66.764, 170.650, 952.1532);
        public static readonly BodyOrientation Vesta = new BodyOrientation(309.031, 42.235, 285.39, 1617.3329428);
        public static readonly BodyOrientation Io = new BodyOrientation(268.05, 64.50, 200.39, 203.4889538, -0.009, 0.003);
        public static readonly BodyOrientation Europa = new BodyOrientation(268.08, 64.51, 36.022, 101.3747235, -0.009, 0.003);
        public static readonly BodyOrientation Ganymede = new BodyOrientation(268.20, 64.57, 44.064, 50.3176081, -0.009, 0.003);
        public static readonly BodyOrientation Callisto = new BodyOrientation(268.72, 64.83, 259.51, 21.5710715, -0.009, 0.003);
        public static readonly BodyOrientation Titan = new BodyOrientation(39.4827, 83.4279, 186.5855, 22.5769768);
        public static readonly BodyOrientation Enceladus = new BodyOrientation(40.66, 83.52, 6.32, 262.7318996, -0.036, -0.004);
        public static readonly BodyOrientation Triton = new BodyOrientation(299.36, 41.17, 296.53, -61.2572637);

        private static System.Collections.Generic.Dictionary<string, BodyOrientation> _presets;

        /// <summary>
        /// Look up a published orientation by body name.
        ///
        /// The importer records only the name, not the six numbers, so the IAU values live in
        /// exactly one place — here. Duplicating them into the generated catalogue would mean
        /// a correction to the IAU report had to be applied twice, and the two copies would
        /// eventually disagree without anything noticing.
        /// </summary>
        public static bool TryGetPreset(string name, out BodyOrientation orientation)
        {
            if (_presets == null)
            {
                _presets = new System.Collections.Generic.Dictionary<string, BodyOrientation>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    { "Sun", Sun }, { "Mercury", Mercury }, { "Venus", Venus }, { "Earth", Earth },
                    { "Moon", Moon }, { "Mars", Mars }, { "Jupiter", Jupiter }, { "Saturn", Saturn },
                    { "Uranus", Uranus }, { "Neptune", Neptune }, { "Pluto", Pluto },
                    { "Ceres", Ceres }, { "Vesta", Vesta },
                    { "Io", Io }, { "Europa", Europa }, { "Ganymede", Ganymede }, { "Callisto", Callisto },
                    { "Titan", Titan }, { "Enceladus", Enceladus }, { "Triton", Triton }
                };
            }

            if (string.IsNullOrEmpty(name))
            {
                orientation = default;
                return false;
            }
            return _presets.TryGetValue(name, out orientation);
        }

        public override string ToString() =>
            $"pole RA {PoleRaDeg:F3} deg, dec {PoleDecDeg:F3} deg, W0 {PrimeMeridianDeg:F3} deg, " +
            $"rate {RotationRateDegPerDay:F6} deg/day";
    }
}
