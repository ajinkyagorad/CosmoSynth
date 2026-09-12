using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>One log-periodic spiral-arm segment, as Reid et al. 2019 fitted it.</summary>
    public readonly struct SpiralArm
    {
        public string Name { get; }

        /// <summary>Galactocentric azimuth covered by the parallax data, degrees.</summary>
        public double BetaMinDeg { get; }

        public double BetaMaxDeg { get; }

        /// <summary>Azimuth of the kink, where the pitch angle changes, degrees.</summary>
        public double BetaKinkDeg { get; }

        /// <summary>Galactocentric radius at the kink, kpc, and its published uncertainty.</summary>
        public double RadiusKinkKpc { get; }

        public double RadiusKinkErrKpc { get; }

        /// <summary>Pitch angle for azimuths at or below the kink, degrees.</summary>
        public double PitchLowDeg { get; }

        /// <summary>Pitch angle above the kink, degrees. Equal to the low one where only a
        /// single pitch angle was solved for.</summary>
        public double PitchHighDeg { get; }

        /// <summary>Gaussian one-sigma intrinsic arm width at the kink radius, kpc.</summary>
        public double WidthKpc { get; }

        /// <summary>Number of maser parallaxes assigned to this arm.</summary>
        public int Parallaxes { get; }

        public SpiralArm(string name, double betaMin, double betaMax, double betaKink,
            double radiusKink, double radiusKinkErr, double pitchLow, double pitchHigh,
            double width, int parallaxes)
        {
            Name = name;
            BetaMinDeg = betaMin;
            BetaMaxDeg = betaMax;
            BetaKinkDeg = betaKink;
            RadiusKinkKpc = radiusKink;
            RadiusKinkErrKpc = radiusKinkErr;
            PitchLowDeg = pitchLow;
            PitchHighDeg = pitchHigh;
            WidthKpc = width;
            Parallaxes = parallaxes;
        }

        public bool HasKink => Math.Abs(PitchHighDeg - PitchLowDeg) > 1e-9;

        /// <summary>True where this arm was actually fitted, and so where it may be drawn.</summary>
        public bool Covers(double betaDeg) => betaDeg >= BetaMinDeg && betaDeg <= BetaMaxDeg;

        /// <summary>
        /// Galactocentric radius of the arm at an azimuth, kpc, or NaN outside the fitted range.
        ///
        /// NaN rather than an extrapolated number on purpose. Every one of these fits covers at
        /// most about 12 kpc of arm, and a log spiral extrapolated past its data winds into the
        /// centre or off the disc while still looking entirely like a spiral arm.
        /// </summary>
        public double RadiusKpc(double betaDeg)
        {
            if (!Covers(betaDeg)) return double.NaN;
            double pitch = betaDeg <= BetaKinkDeg ? PitchLowDeg : PitchHighDeg;
            double dBeta = (betaDeg - BetaKinkDeg) * MathD.Deg2Rad;
            return RadiusKinkKpc * Math.Exp(-dBeta * Math.Tan(pitch * MathD.Deg2Rad));
        }
    }

    /// <summary>
    /// The Milky Way's own geometry: where the Sun is, how the disc is shaped, where the bar
    /// points, and where the spiral arms run.
    ///
    /// <b>This is code, not data, and that is a licence decision as much as a design one.</b>
    /// The VizieR copy of Reid et al. 2019's maser table may not be redistributed
    /// (<c>docs/DATA-LICENCE-AMENDMENT.md</c> evidence E5); the fitted arm parameters are
    /// measurements of the Galaxy, and a measurement is a fact. So the model is implemented from
    /// the published fit and nothing is redistributed but this file.
    ///
    /// Every number below was transcribed from the papers themselves — Reid et al. 2019 Table 2
    /// read from the arXiv e-print of ApJ 885, 131, and Bland-Hawthorn &amp; Gerhard 2016's
    /// summary entries from the e-print of ARA&amp;A 54, 529 — not from a catalogue scrape and
    /// not from memory. The staged source notes say why that distinction is worth the trouble:
    /// "that is how a wrong R0 ships looking confident".
    ///
    /// <b>Frames.</b> Galactocentric cartesian here shares its axes with the heliocentric
    /// galactic frame the dust cube uses — +x toward l = 0, +y toward l = 90, +z toward the
    /// north galactic pole — translated so the Galactic Centre is the origin. The Sun is then at
    /// (-R0, 0, 0). Galactocentric azimuth beta follows Reid: zero toward the Sun, increasing in
    /// the direction of Galactic rotation.
    /// </summary>
    public static class GalaxyModel
    {
        // ---- The Sun's place ------------------------------------------------------------

        /// <summary>
        /// Distance from the Sun to the Galactic Centre, kpc: 8.15 +/- 0.15.
        /// Reid et al. 2019, ApJ 885, 131, fit B. The arm model assumes this same value, so the
        /// arms and the Galactic Centre cannot disagree.
        /// </summary>
        public const double R0Kpc = 8.15;

        public const double R0ErrKpc = 0.15;

        public const double R0Pc = R0Kpc * 1000.0;

        /// <summary>Circular rotation speed at the Sun, km/s: 236 +/- 7 (Reid et al. 2019).</summary>
        public const double Theta0KmS = 236.0;

        public const double Theta0ErrKmS = 7.0;

        /// <summary>
        /// The Sun's height above the plane is taken as zero here, and that is a definition
        /// rather than a measurement: every catalogue this layer ships is heliocentric with
        /// galactic latitude measured from the Sun, so the Sun is on the b = 0 plane by
        /// construction. Published values are of order 20 pc; adopting one would move the Sun
        /// relative to its own data.
        /// </summary>
        public const double SunHeightPc = 0.0;

        /// <summary>The Galactic Centre in heliocentric galactic parsecs.</summary>
        public static Double3 GalacticCentrePc => new Double3(R0Pc, 0.0, 0.0);

        /// <summary>
        /// Unit vector from the Sun toward the Galactic Centre, in ICRF.
        /// Equal to the direction of Sgr A* (RA 266.41684, Dec -29.00781) to better than a
        /// tenth of a degree, which is what the test asserts.
        /// </summary>
        public static Double3 GalacticCentreDirectionIcrf =>
            (StandardFrames.GalacticToEquatorial * Double3.UnitX).Normalized;

        /// <summary>Heliocentric galactic parsecs to galactocentric parsecs.</summary>
        public static Double3 ToGalactocentricPc(Double3 galacticHelioPc) =>
            galacticHelioPc - GalacticCentrePc;

        public static Double3 ToHeliocentricPc(Double3 galactocentricPc) =>
            galactocentricPc + GalacticCentrePc;

        /// <summary>
        /// Galactocentric azimuth of a point, degrees in (-180, 180]: zero toward the Sun,
        /// increasing in the direction of Galactic rotation. This is Reid's beta.
        /// </summary>
        public static double AzimuthDeg(Double3 galactocentricPc) =>
            Math.Atan2(galactocentricPc.y, -galactocentricPc.x) * MathD.Rad2Deg;

        /// <summary>Galactocentric radius in the plane, kpc.</summary>
        public static double RadiusKpc(Double3 galactocentricPc) =>
            Math.Sqrt(galactocentricPc.x * galactocentricPc.x +
                      galactocentricPc.y * galactocentricPc.y) / 1000.0;

        // ---- The disc -------------------------------------------------------------------

        /// <summary>Thin-disc radial scale length, kpc: 2.6 +/- 0.5 (Bland-Hawthorn &amp;
        /// Gerhard 2016, ARA&amp;A 54, 529).</summary>
        public const double ThinDiscScaleLengthKpc = 2.6;

        /// <summary>Thin-disc vertical scale height at R0, pc: 300 +/- 50 (BH&amp;G 2016).</summary>
        public const double ThinDiscScaleHeightPc = 300.0;

        /// <summary>Thick-disc radial scale length, kpc: 2.0 +/- 0.2 (BH&amp;G 2016).</summary>
        public const double ThickDiscScaleLengthKpc = 2.0;

        /// <summary>Thick-disc vertical scale height at R0, pc: 900 +/- 180 (BH&amp;G 2016).</summary>
        public const double ThickDiscScaleHeightPc = 900.0;

        /// <summary>Thick/thin local density ratio at R0: 4% +/- 2% (BH&amp;G 2016).</summary>
        public const double ThickToThinDensityRatio = 0.04;

        /// <summary>
        /// Relative stellar density of the two exponential discs at a galactocentric position,
        /// normalised to 1 at the Sun. Procedural: it is the standard double-exponential form
        /// evaluated with measured scale lengths, not a reconstruction of the real disc.
        /// </summary>
        public static double DiscDensity(Double3 galactocentricPc)
        {
            double rKpc = RadiusKpc(galactocentricPc);
            double zPc = Math.Abs(galactocentricPc.z);
            double thin = Math.Exp(-(rKpc - R0Kpc) / ThinDiscScaleLengthKpc) *
                          Math.Exp(-zPc / ThinDiscScaleHeightPc);
            double thick = ThickToThinDensityRatio *
                           Math.Exp(-(rKpc - R0Kpc) / ThickDiscScaleLengthKpc) *
                           Math.Exp(-zPc / ThickDiscScaleHeightPc);
            return thin + thick;
        }

        // ---- The bar --------------------------------------------------------------------

        /// <summary>
        /// Angle between the bar's major axis and the Sun-Centre line, degrees: 27 +/- 2 for the
        /// boxy/peanut bulge (Bland-Hawthorn &amp; Gerhard 2016, entry phi_bar). The near end is
        /// in the first Galactic quadrant, so it leans toward positive l.
        /// </summary>
        public const double BarAngleDeg = 27.0;

        public const double BarAngleErrDeg = 2.0;

        /// <summary>Long-bar angle, degrees; BH&amp;G quote a range of 28 to 33.</summary>
        public const double LongBarAngleDeg = 30.5;

        /// <summary>Half-length of the long bar, kpc: 5.0 +/- 0.2 (BH&amp;G 2016).</summary>
        public const double BarHalfLengthKpc = 5.0;

        public const double BarHalfLengthErrKpc = 0.2;

        /// <summary>
        /// Unit vector along the bar's major axis in galactocentric coordinates, near end first.
        /// The near end lies in the first Galactic quadrant (0 &lt; l &lt; 90), which in this
        /// frame means it points back toward the Sun and to positive y.
        /// </summary>
        public static Double3 BarAxis
        {
            get
            {
                double a = BarAngleDeg * MathD.Deg2Rad;
                return new Double3(-Math.Cos(a), Math.Sin(a), 0.0);
            }
        }

        // ---- The arms -------------------------------------------------------------------

        /// <summary>
        /// Reid et al. 2019, ApJ 885, 131, Table 2 ("Spiral Arm Characteristics"), transcribed
        /// from the paper. Columns: arm, N, beta range, beta_kink, R_kink, psi_&lt;, psi_&gt;,
        /// width. The fits assume R0 = 8.15 kpc, which is <see cref="R0Kpc"/>.
        ///
        /// The Sagittarius-Carina and Norma arms genuinely change pitch angle at their kinks
        /// (17.1 to 1.0 degrees and -1.0 to 19.5); the Local and 3-kpc arms have a single fitted
        /// pitch angle, and their two entries are equal on purpose.
        /// </summary>
        public static readonly SpiralArm[] Arms =
        {
            //             name          beta range     kink  R_kink  +/-   psi<   psi>  width  N
            new SpiralArm("3-kpc",        15,  18,        15,   3.52, 0.26,  -4.2,  -4.2, 0.18,  3),
            new SpiralArm("Norma",         5,  54,        18,   4.46, 0.19,  -1.0,  19.5, 0.14, 11),
            new SpiralArm("Scutum-Centaurus", 0, 104,     23,   4.91, 0.09,  14.1,  12.1, 0.23, 36),
            new SpiralArm("Sagittarius-Carina", 2, 97,    24,   6.04, 0.09,  17.1,   1.0, 0.27, 35),
            new SpiralArm("Local",        -8,  34,         9,   8.26, 0.05,  11.4,  11.4, 0.31, 28),
            new SpiralArm("Perseus",     -23, 115,        40,   8.87, 0.13,  10.3,   8.7, 0.35, 41),
            new SpiralArm("Outer",       -16,  71,        18,  12.24, 0.36,   3.0,   9.4, 0.65, 11),
        };

        /// <summary>
        /// Gaussian one-sigma arm width in the plane at a galactocentric radius, parsecs.
        /// Reid et al. 2019: w(R) = 336 + 36 (R_kpc - 8.15) pc, fitted across all seven arms.
        /// </summary>
        public static double ArmWidthPc(double radiusKpc) => 336.0 + 36.0 * (radiusKpc - R0Kpc);

        public static SpiralArm Arm(string name)
        {
            for (int i = 0; i < Arms.Length; i++)
                if (string.Equals(Arms[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return Arms[i];
            throw new ArgumentException("no arm named '" + name + "'", nameof(name));
        }

        /// <summary>
        /// Points along one arm in galactocentric parsecs, z = 0, sampled over its fitted
        /// azimuth range only. An arm is never sampled outside the data it was fitted to.
        /// </summary>
        public static Double3[] ArmPoints(SpiralArm arm, double stepDeg = 1.0)
        {
            if (stepDeg <= 0.0) throw new ArgumentOutOfRangeException(nameof(stepDeg));
            int steps = Math.Max(1, (int)Math.Ceiling((arm.BetaMaxDeg - arm.BetaMinDeg) / stepDeg));
            var points = new Double3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                double beta = arm.BetaMinDeg +
                              (arm.BetaMaxDeg - arm.BetaMinDeg) * i / (double)steps;
                double r = arm.RadiusKpc(beta) * 1000.0;
                double a = beta * MathD.Deg2Rad;
                // beta is measured from the Sun direction, which is -x, toward +y.
                points[i] = new Double3(-r * Math.Cos(a), r * Math.Sin(a), 0.0);
            }
            return points;
        }

        /// <summary>
        /// The nearest arm to a galactocentric position and how far off it is, measured in
        /// intrinsic arm widths. Returns null when no arm was fitted at that azimuth, which is
        /// the honest answer over most of the far side of the Galaxy.
        /// </summary>
        public static bool TryNearestArm(Double3 galactocentricPc, out SpiralArm nearest,
            out double offsetInWidths)
        {
            double beta = AzimuthDeg(galactocentricPc);
            double r = RadiusKpc(galactocentricPc);
            bool found = false;
            nearest = default(SpiralArm);
            offsetInWidths = double.NaN;
            for (int i = 0; i < Arms.Length; i++)
            {
                double armR = Arms[i].RadiusKpc(beta);
                if (double.IsNaN(armR)) continue;
                double widthKpc = ArmWidthPc(armR) / 1000.0;
                if (widthKpc <= 0.0) continue;
                double offset = Math.Abs(r - armR) / widthKpc;
                if (!found || offset < offsetInWidths)
                {
                    found = true;
                    nearest = Arms[i];
                    offsetInWidths = offset;
                }
            }
            return found;
        }

        /// <summary>The citation every use of this model owes, per the licence amendment.</summary>
        public const string Citation =
            "Spiral-arm geometry after Reid et al. (2019), ApJ 885, 131 (BeSSeL Survey maser " +
            "parallaxes). Model implemented from the published fit parameters; no catalogue " +
            "data redistributed. Disc and bar parameters from Bland-Hawthorn & Gerhard (2016), " +
            "ARA&A 54, 529.";

        /// <summary>Provenance of the model's own numbers, for the panel that shows it.</summary>
        public static IReadOnlyDictionary<string, FieldProvenance> Provenance { get; } =
            new Dictionary<string, FieldProvenance>(StringComparer.Ordinal)
            {
                ["R0"] = new FieldProvenance(ProvenanceClass.Measured, "reid2019",
                    "8.15 kpc, Reid et al. 2019 fit B", R0ErrKpc),
                ["theta0"] = new FieldProvenance(ProvenanceClass.Measured, "reid2019",
                    "236 km/s, Reid et al. 2019", Theta0ErrKmS),
                ["arms"] = new FieldProvenance(ProvenanceClass.Inferred, "reid2019",
                    "log-periodic fit to maser parallaxes; drawn only over the fitted azimuth " +
                    "range of each arm"),
                ["disc"] = new FieldProvenance(ProvenanceClass.Inferred, "bhg2016",
                    "double-exponential disc with measured scale lengths; a model, not a map"),
                ["bar"] = new FieldProvenance(ProvenanceClass.Measured, "bhg2016",
                    "27 +/- 2 degree bar angle, 5.0 +/- 0.2 kpc long-bar half-length",
                    BarAngleErrDeg),
            };
    }
}
