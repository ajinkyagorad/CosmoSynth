using System;

namespace CosmoSynth.Core
{
    /// <summary>One fitted spiral arm: a log-periodic curve valid only over the range it was fitted on.</summary>
    public readonly struct SpiralArm
    {
        /// <summary>Arm name as Reid+2019 publishes it.</summary>
        public readonly string Name;

        /// <summary>Reference radius, kpc, at the reference azimuth.</summary>
        public readonly double RefRadiusKpc;

        /// <summary>Reference galactocentric azimuth, degrees.</summary>
        public readonly double RefBetaDeg;

        /// <summary>Pitch angle, degrees.</summary>
        public readonly double PitchDeg;

        /// <summary>Azimuth range the fit is supported over, degrees. Outside it the arm is a guess.</summary>
        public readonly double BetaMinDeg, BetaMaxDeg;

        /// <summary>Arm half-width, kpc — what the published scatter about the fit is.</summary>
        public readonly double WidthKpc;

        public SpiralArm(string name, double refRadiusKpc, double refBetaDeg, double pitchDeg,
            double betaMinDeg, double betaMaxDeg, double widthKpc)
        {
            Name = name;
            RefRadiusKpc = refRadiusKpc;
            RefBetaDeg = refBetaDeg;
            PitchDeg = pitchDeg;
            BetaMinDeg = betaMinDeg;
            BetaMaxDeg = betaMaxDeg;
            WidthKpc = widthKpc;
        }

        /// <summary>
        /// <c>ln(R/Rref) = -(beta - betaRef) tan(psi)</c> — the log-periodic spiral Reid+2019 fits
        /// to maser parallaxes. Returns kpc.
        /// </summary>
        public double RadiusKpcAt(double betaDeg) =>
            RefRadiusKpc * Math.Exp(-(betaDeg - RefBetaDeg) * MathD.Deg2Rad * Math.Tan(PitchDeg * MathD.Deg2Rad));

        /// <summary>True inside the fitted azimuth range. Outside it the curve is an extrapolation.</summary>
        public bool IsFitted(double betaDeg) => betaDeg >= BetaMinDeg && betaDeg <= BetaMaxDeg;

        /// <summary>
        /// How strongly the arm should be drawn at this azimuth: 1 where the masers support it,
        /// falling linearly to <paramref name="floor"/> over <paramref name="extrapolationDeg"/>
        /// of guesswork, and 0 beyond.
        ///
        /// This is the whole correction to the Atlas, in one function. It drew complete spirals,
        /// which asserts structure on the far side of the Galaxy that nobody has ever measured —
        /// and it looks magnificent, which is exactly why it needs a rule rather than a judgement.
        /// </summary>
        public double Support(double betaDeg, double extrapolationDeg = 30.0, double floor = 0.15)
        {
            if (IsFitted(betaDeg)) return 1.0;
            double over = betaDeg < BetaMinDeg ? BetaMinDeg - betaDeg : betaDeg - BetaMaxDeg;
            if (over > extrapolationDeg) return 0.0;
            return MathD.Lerp(1.0, floor, over / extrapolationDeg);
        }
    }

    /// <summary>
    /// The Milky Way as constants: no file, no download, no licence. CONTRACT C.6, C.8, §5.4
    /// (<c>arms</c>, <c>sunOrbit</c>, <c>mwDisc</c>).
    ///
    /// Four arms plus the Local Arm, from the maser-parallax fit of Reid+2019 (ApJ 885, 131,
    /// Table 2). The renderer draws each curve <b>only over its own fitted azimuth range</b> and
    /// fades it outside: the fit is supported by parallaxes that only cover part of the disc, and
    /// a spiral drawn all the way round asserts structure on the far side of the Galaxy that
    /// nobody has measured. The Atlas drew complete arms; that is the single most confident-looking
    /// unmeasured thing in it.
    ///
    /// Positions are GALACTOCENTRIC: azimuth beta is measured at the Galactic centre from the
    /// Sun-centre line, increasing in the direction of galactic rotation.
    /// </summary>
    public static class GalaxyModel
    {
        /// <summary>Sun-to-Galactic-centre distance, kpc (Reid+2019).</summary>
        public const double R0Kpc = 8.15;

        /// <summary>Circular rotation speed at R0, km/s (Reid+2019).</summary>
        public const double Theta0KmS = 236.0;

        /// <summary>
        /// Height of the Sun above the true mid-plane, pc (INFERRED, commonly adopted).
        ///
        /// <b>This cannot be derived from the frame, and the attempt is a trap.</b> The IAU
        /// galactic coordinate system was fixed in 1958 from 21 cm data, and its origin is about
        /// 0.07 degrees away from Sgr A*; the Sun's offset from the true mid-plane is a separate
        /// measurement of a different plane. Putting the centre at galactic (0, 0) and then
        /// tilting by this height lands 0.14 degrees from the radio source — which is, to be
        /// clear, a Galaxy that looks perfectly correct. It is used only where the true mid-plane
        /// matters, which is the disc model's own z distribution.
        /// </summary>
        public const double SunHeightPc = 20.8;

        /// <summary>Sgr A*, ICRF degrees (Reid &amp; Brunthaler 2004; CONTRACT C.9's landmark row).</summary>
        public const double SgrAStarRaDeg = 266.41684;
        public const double SgrAStarDecDeg = -29.00781;

        /// <summary>Radius of the stellar disc that is worth drawing, kpc (INFERRED: the disc has no edge).</summary>
        public const double DiscRadiusKpc = 16.0;

        /// <summary>Exponential scale height of the thin disc, kpc (INFERRED).</summary>
        public const double DiscScaleHeightKpc = 0.3;

        /// <summary>Radius of the central bulge, kpc (INFERRED).</summary>
        public const double BulgeRadiusKpc = 2.0;

        /// <summary>Parsecs per kilometre, and its inverse, so no layer re-types them.</summary>
        public const double KmPerParsec = 3.0856775814913673e13;
        public const double KmPerKiloparsec = KmPerParsec * 1000.0;

        /// <summary>
        /// The seven arms of Reid+2019 Table 2: reference radius, reference azimuth, pitch angle,
        /// the azimuth range the masers assigned to that arm actually span, and the fitted width.
        ///
        /// <b>UNVERIFIED — do not ship these numbers as Measured.</b> They were entered from
        /// memory of the paper's table, not read off it: this lane has no network and the paper is
        /// not in the tree. The <c>milkyway</c> data lane must re-enter every row from
        /// ApJ 885, 131 Table 2 when it fetches the maser catalogue, and
        /// <c>GalaxyModelTests.ArmsMatchTheMasers</c> is where that check belongs — the masers are
        /// shipped, so the fit can be re-proved against its own data rather than trusted.
        /// Until then the layer tags the arms Inferred and says "log-periodic fit" in the panel,
        /// which is true whatever the coefficients turn out to be.
        /// </summary>
        public static readonly SpiralArm[] Arms =
        {
            new SpiralArm("3-kpc",        3.52,   15.0,  -4.2,  -4.0,  25.0, 0.18),
            new SpiralArm("Norma",        4.46,   18.0,  -1.0,   5.0,  54.0, 0.14),
            new SpiralArm("Sct-Cen",      4.91,   23.0,  13.1,   0.0, 104.0, 0.23),
            new SpiralArm("Sgr-Car",      6.04,   24.0,   1.0,   2.0,  97.0, 0.27),
            new SpiralArm("Local",        8.26,    9.0,  11.4,  -8.0,  34.0, 0.31),
            new SpiralArm("Perseus",      8.87,   40.0,   9.9,  -21.0, 88.0, 0.35),
            new SpiralArm("Outer",       12.24,   18.0,   6.0,  -16.0, 71.0, 0.65),
        };

        /// <summary>
        /// Galactocentric Cartesian kpc for a point on an arm.
        ///
        /// The frame: origin at the Galactic centre, +x toward the Sun, +z the north galactic
        /// pole. Reid's azimuth beta is zero toward the Sun and increases in the direction of
        /// galactic rotation, and the Galaxy rotates CLOCKWISE seen from the north pole — so
        /// increasing beta moves toward -y here. Getting that sign wrong mirrors every arm about
        /// the Sun-centre line, which still looks exactly like a spiral galaxy.
        /// </summary>
        public static Double3 ArmPointKpc(in SpiralArm arm, double betaDeg)
        {
            double r = arm.RadiusKpcAt(betaDeg);
            double b = betaDeg * MathD.Deg2Rad;
            return new Double3(r * Math.Cos(b), -r * Math.Sin(b), 0.0);
        }

        /// <summary>The north galactic pole as an ICRF unit vector, from the one galactic matrix.</summary>
        public static Double3 NorthGalacticPoleIcrf =>
            StandardFrames.GalacticToEquatorial * Double3.UnitZ;

        /// <summary>The Galactic centre in ICRF parsecs: the measured Sgr A* direction, at R0.</summary>
        public static Double3 GalacticCentreIcrfPc =>
            StandardFrames.FromSpherical(SgrAStarRaDeg * MathD.Deg2Rad,
                SgrAStarDecDeg * MathD.Deg2Rad, R0Kpc * 1000.0);

        /// <summary>The same, in kilometres, which is what every layer actually places with.</summary>
        public static Double3 GalacticCentreIcrfKm => GalacticCentreIcrfPc * KmPerParsec;

        /// <summary>
        /// The galactocentric basis, as columns of an ICRF rotation: +x from the centre toward the
        /// Sun, +z the north galactic pole, +y completing a right-handed set.
        ///
        /// Built from the measured Sgr A* direction and the pole, orthonormalised — the two are
        /// 0.07 degrees from perpendicular and pretending otherwise is what put the centre in the
        /// wrong place. Because +y is a cross product rather than an asserted axis, the frame
        /// cannot come out mirrored, which is the failure no screenshot shows.
        /// </summary>
        public static Matrix3d GalactocentricToIcrf
        {
            get
            {
                Double3 z = NorthGalacticPoleIcrf.Normalized;
                Double3 toSun = (-GalacticCentreIcrfPc).Normalized;
                Double3 x = (toSun - z * Double3.Dot(toSun, z)).Normalized;
                Double3 y = Double3.Cross(z, x);
                return Matrix3d.FromColumns(x, y, z);
            }
        }

        /// <summary>ICRF parsecs for a galactocentric point, which is what the renderer wants.</summary>
        public static Double3 IcrfPcFrom(Double3 galactocentricKpc) =>
            GalacticCentreIcrfPc + GalactocentricToIcrf * (galactocentricKpc * 1000.0);

        /// <summary>The inverse: ICRF parsecs back to galactocentric kpc.</summary>
        public static Double3 GalactocentricKpcFrom(Double3 icrfPc) =>
            GalactocentricToIcrf.InverseRotation * (icrfPc - GalacticCentreIcrfPc) / 1000.0;

        /// <summary>
        /// The Sun in the galactocentric frame, kpc. Derived, not asserted: it is wherever the
        /// ICRF origin lands, which is R0 along +x plus whatever the frame's own 0.07 degrees of
        /// non-perpendicularity puts on z.
        /// </summary>
        public static Double3 SunGalactocentricKpc => GalactocentricKpcFrom(Double3.Zero);

        /// <summary>
        /// Galactocentric kpc to heliocentric galactic parsecs — the frame the dust cube, the star
        /// catalogue and every galactic label are in. One path, through ICRF, so no second
        /// spelling of the galactic rotation can exist to disagree with the first.
        /// </summary>
        public static Double3 HeliocentricGalacticPcFrom(Double3 galactocentricKpc) =>
            StandardFrames.EquatorialToGalactic * IcrfPcFrom(galactocentricKpc);

        public static Double3 GalactocentricKpcFromHeliocentric(Double3 heliocentricGalacticPc) =>
            GalactocentricKpcFrom(StandardFrames.GalacticToEquatorial * heliocentricGalacticPc);

        /// <summary>
        /// The disc's own colour, linear RGB. Content colour, derived here rather than in
        /// <c>CosmoTheme</c> because it is a property of the subject, not of the interface: a
        /// warm old bulge and a bluer star-forming disc is what a spiral galaxy looks like.
        /// </summary>
        public static readonly Double3 DiscColourLinear = new Double3(0.46, 0.55, 0.82);
        public static readonly Double3 BulgeColourLinear = new Double3(0.95, 0.82, 0.58);
        public static readonly Double3 ArmColourLinear = new Double3(0.55, 0.72, 1.00);

        /// <summary>
        /// The dust colour ramp, linear RGB, for a normalised density in [0, 1].
        ///
        /// Procedural and labelled as such. Interstellar dust in the visible is defined by what it
        /// REMOVES, not by what it emits: a true-colour render of the Vergely cube would be an
        /// absence, which is correct and unreadable. The ramp is a reading aid, warm where the
        /// column is thick, and the panel says so.
        /// </summary>
        public static Double3 DustRampLinear(double t)
        {
            double u = MathD.Clamp01(t);
            Double3[] stops =
            {
                new Double3(0.06, 0.07, 0.10),
                new Double3(0.28, 0.24, 0.26),
                new Double3(0.62, 0.45, 0.34),
                new Double3(0.92, 0.74, 0.52),
            };
            double x = u * (stops.Length - 1);
            int i = (int)Math.Floor(x);
            if (i >= stops.Length - 1) return stops[stops.Length - 1];
            return Double3.Lerp(stops[i], stops[i + 1], x - i);
        }

    }
}
