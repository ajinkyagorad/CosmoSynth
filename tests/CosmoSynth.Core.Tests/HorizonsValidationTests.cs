using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// Validation against real JPL Horizons ephemerides.
    ///
    /// The other test files check that the code is self-consistent. These check it against
    /// the solar system, using 260 state vectors fetched from NASA and committed under
    /// fixtures/. Two separate questions are asked, and conflating them would make both
    /// answers meaningless:
    ///
    ///   * <see cref="RecoveredElementsMatchJpl"/> holds the physics fixed and tests the
    ///     arithmetic. Given the same state vector JPL used, do we derive the same orbital
    ///     elements they publish? Any disagreement here is a bug in our code.
    ///
    ///   * <see cref="TwoBodyDriftAgainstRealEphemeris"/> holds the arithmetic fixed and
    ///     measures the model. How far does an unperturbed two-body conic wander from the
    ///     real, perturbed solar system over five years? Disagreement here is physics, not a
    ///     defect — but it is exactly the number the application's provenance display owes
    ///     the user, so it is measured rather than assumed.
    /// </summary>
    public static class HorizonsValidationTests
    {
        private static JsonValue _fixture;

        private static JsonValue Fixture
        {
            get
            {
                if (_fixture == null)
                {
                    string path = LocateFixture("horizons_validation.json");
                    _fixture = JsonValue.Parse(File.ReadAllText(path));
                }
                return _fixture;
            }
        }

        /// <summary>
        /// Find a fixture whether we are running from the build output (where the csproj copies
        /// it) or straight from the source tree.
        /// </summary>
        private static string LocateFixture(string fileName)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 8 && directory != null; depth++)
            {
                string candidate = Path.Combine(directory.FullName, "fixtures", fileName);
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new FileNotFoundException(
                $"Could not locate fixtures/{fileName}. Regenerate it with " +
                "'python3 tools/cosmodata/fetch_validation_fixture.py'.");
        }

        private static StateVector ReadState(JsonValue vector) => new StateVector(
            vector["positionKm"].AsVector(),
            vector["velocityKmPerSec"].AsVector());

        [Test("orbital elements recovered from real state vectors match JPL's own")]
        public static void RecoveredElementsMatchJpl()
        {
            // JPL derives the published osculating elements from the very state vector we are
            // given, so this isolates our state-to-elements conversion completely. The only
            // shared assumption is the obliquity used to rotate into the ecliptic frame, and
            // Horizons' ecliptic output is built on the IAU 1976 value.
            var report = new List<string>();
            double worstAngleArcsec = 0.0;
            double worstRelative = 0.0;

            foreach (var body in Fixture["bodies"])
            {
                string name = body["name"].AsString();
                double gm = body["gravitationalParameter"].AsDouble();
                var jpl = body["elementsEcliptic"]["values"];

                var equatorialState = ReadState(body["vectors"][0]);
                var epoch = CosmicTime.FromJulianDateTdb(body["vectors"][0]["jdTdb"].AsDouble());

                // Horizons reports elements in the ecliptic frame, so rotate before converting.
                var eclipticState = StandardFrames.ToEcliptic(equatorialState);
                var elements = OrbitalElements.FromStateVector(eclipticState, gm, epoch);

                double aError = System.Math.Abs(elements.SemiMajorAxisKm - jpl["A"].AsDouble())
                                / System.Math.Abs(jpl["A"].AsDouble());
                double eError = System.Math.Abs(elements.Eccentricity - jpl["EC"].AsDouble());
                double qError = System.Math.Abs(elements.PeriapsisKm - jpl["QR"].AsDouble()) / jpl["QR"].AsDouble();

                double inclinationArcsec = System.Math.Abs(MathD.WrapPi(
                    elements.InclinationRad - jpl["IN"].AsDouble() * MathD.Deg2Rad)) / StandardFrames.ArcsecToRad;
                double raanArcsec = System.Math.Abs(MathD.WrapPi(
                    elements.LongitudeOfAscendingNodeRad - jpl["OM"].AsDouble() * MathD.Deg2Rad)) / StandardFrames.ArcsecToRad;
                double argpArcsec = System.Math.Abs(MathD.WrapPi(
                    elements.ArgumentOfPeriapsisRad - jpl["W"].AsDouble() * MathD.Deg2Rad)) / StandardFrames.ArcsecToRad;
                double meanAnomalyArcsec = System.Math.Abs(MathD.WrapPi(
                    elements.MeanAnomalyAtEpochRad - jpl["MA"].AsDouble() * MathD.Deg2Rad)) / StandardFrames.ArcsecToRad;

                // Period is an independent consequence of a and mu, so it catches an error in
                // either that happens to cancel in the geometry.
                double periodError = System.Math.Abs(elements.PeriodSeconds - jpl["PR"].AsDouble()) / jpl["PR"].AsDouble();

                worstRelative = System.Math.Max(worstRelative, System.Math.Max(aError, System.Math.Max(qError, periodError)));
                worstAngleArcsec = System.Math.Max(worstAngleArcsec,
                    System.Math.Max(inclinationArcsec, System.Math.Max(raanArcsec,
                        System.Math.Max(argpArcsec, meanAnomalyArcsec))));

                report.Add($"      {name,-22} a {aError:E2}  e {eError:E2}  i {inclinationArcsec:F5}\"  " +
                           $"RAAN {raanArcsec:F5}\"  argp {argpArcsec:F5}\"  M {meanAnomalyArcsec:F5}\"");

                Assert.Relative(jpl["A"].AsDouble(), elements.SemiMajorAxisKm, 1e-9, $"[{name}] semi-major axis");
                Assert.Relative(jpl["QR"].AsDouble(), elements.PeriapsisKm, 1e-9, $"[{name}] periapsis distance");

                // Eccentricity is checked through its geometric consequence, the focus offset
                // a*e, rather than as a bare number of digits.
                //
                // For a nearly circular orbit e is intrinsically ill-conditioned: the
                // eccentricity vector is built from (v^2 - mu/r), a difference of two large and
                // nearly equal quantities, so several digits cancel before the result is even
                // formed. That is why equinoctial elements exist. At Neptune the residual
                // against JPL sits around 2e-11 in e, which sounds alarming and means a
                // displacement of the orbit's focus by about a hundred metres across four and
                // a half billion kilometres. Asserting on a*e says what we actually care about
                // and stays meaningful for both a comet and a near-circular gas giant.
                double focusOffsetError = System.Math.Abs(elements.Eccentricity - jpl["EC"].AsDouble())
                                          * System.Math.Abs(jpl["A"].AsDouble());
                Assert.True(focusOffsetError < 1.0,
                    $"[{name}] eccentricity differs by {eError:E3}, displacing the orbital focus by " +
                    $"{focusOffsetError:F3} km");
                Assert.Relative(jpl["PR"].AsDouble(), elements.PeriodSeconds, 1e-9, $"[{name}] orbital period");

                // One arcsecond is a very tight bound on an angle: it is the apparent size of
                // a coin at four kilometres. Residuals live at the milliarcsecond level and
                // come from the obliquity constant, not from the conversion.
                Assert.True(inclinationArcsec < 1.0, $"[{name}] inclination differs by {inclinationArcsec:F4} arcsec");
                Assert.True(raanArcsec < 1.0, $"[{name}] RAAN differs by {raanArcsec:F4} arcsec");
                Assert.True(argpArcsec < 1.0, $"[{name}] argument of periapsis differs by {argpArcsec:F4} arcsec");
                Assert.True(meanAnomalyArcsec < 1.0, $"[{name}] mean anomaly differs by {meanAnomalyArcsec:F4} arcsec");
            }

            Console.WriteLine("      --- element residuals against JPL ---");
            foreach (var line in report) Console.WriteLine(line);
            Console.WriteLine($"      worst relative {worstRelative:E2}, worst angle {worstAngleArcsec:F5} arcsec");
        }

        [Test("propagated states reproduce JPL vectors, and the two-body drift is quantified")]
        public static void TwoBodyDriftAgainstRealEphemeris()
        {
            // Expected drift after five years, as a fraction of orbital radius. These bounds
            // are empirical: they record what an unperturbed conic actually achieves, so a
            // regression that broke the propagator would blow through them, while the honest
            // limits of the model stay documented rather than hidden.
            var expectedRelativeDrift = new Dictionary<string, double>
            {
                { "Mercury", 5e-4 },
                { "Venus", 5e-4 },
                { "EarthMoonBarycenter", 5e-4 },
                { "MarsBarycenter", 5e-3 },
                { "JupiterBarycenter", 5e-3 },
                { "SaturnBarycenter", 2e-2 },
                { "UranusBarycenter", 5e-3 },
                { "NeptuneBarycenter", 5e-3 },
                { "Ceres", 2e-2 },
                // The Moon is the deliberate failure case: the Sun perturbs the lunar orbit so
                // strongly that a Kepler conic is simply the wrong model over months.
                { "Moon", 1.0 },
            };

            Console.WriteLine("      --- two-body Kepler drift vs real ephemeris ---");
            Console.WriteLine("      body                    after 1 day    after 1 year    after 5 years");

            foreach (var body in Fixture["bodies"])
            {
                string name = body["name"].AsString();
                double gm = body["gravitationalParameter"].AsDouble();

                var vectors = body["vectors"];
                var referenceState = ReadState(vectors[0]);
                double referenceJd = vectors[0]["jdTdb"].AsDouble();
                var epoch = CosmicTime.FromJulianDateTdb(referenceJd);

                var elements = OrbitalElements.FromStateVector(referenceState, gm, epoch);

                // At zero elapsed time the propagator must reproduce the input exactly; this is
                // the sanity check that the drift below is physics and not an offset bug.
                var atEpoch = elements.Propagate(epoch);
                Assert.VectorClose(referenceState.Position, atEpoch.Position,
                    referenceState.Position.Magnitude * 1e-12, $"[{name}] state at the reference epoch");

                double driftAfterOneDay = 0.0, driftAfterOneYear = 0.0, finalDrift = 0.0;
                double finalDays = 0.0;

                for (int i = 1; i < vectors.Count; i++)
                {
                    double jd = vectors[i]["jdTdb"].AsDouble();
                    double elapsedDays = jd - referenceJd;
                    var truth = ReadState(vectors[i]);

                    var predicted = elements.Propagate(epoch + elapsedDays * Constants.SecondsPerDay);
                    double error = (predicted.Position - truth.Position).Magnitude;
                    double relative = error / truth.Position.Magnitude;

                    if (elapsedDays <= 80.0) driftAfterOneDay = System.Math.Max(driftAfterOneDay, relative);
                    if (elapsedDays <= 370.0) driftAfterOneYear = System.Math.Max(driftAfterOneYear, relative);
                    finalDrift = relative;
                    finalDays = elapsedDays;

                    // The universal-variable path must agree with the classical one on real
                    // data too, not just on synthetic orbits.
                    var universal = UniversalKepler.Propagate(referenceState, gm, elapsedDays * Constants.SecondsPerDay);
                    double methodDisagreement = (predicted.Position - universal.Position).Magnitude
                                                / predicted.Position.Magnitude;
                    Assert.True(methodDisagreement < 1e-9,
                        $"[{name}] the two propagators disagree by {methodDisagreement:E3} at {elapsedDays:F0} days");
                }

                Console.WriteLine($"      {name,-22} {driftAfterOneDay:E2}       {driftAfterOneYear:E2}        " +
                                  $"{finalDrift:E2}   ({finalDays:F0} d)");

                double bound = expectedRelativeDrift.TryGetValue(name, out double b) ? b : 1e-2;
                Assert.True(finalDrift < bound,
                    $"[{name}] two-body drift reached {finalDrift:E3} of orbital radius after {finalDays:F0} days, " +
                    $"beyond the expected {bound:E1}. Either the propagator regressed or this bound needs revisiting.");
            }
        }

        [Test("Earth's orbit is flat in the ecliptic frame, which is what defines that frame")]
        public static void EarthOrbitDefinesTheEcliptic()
        {
            // The ecliptic *is* the plane of the Earth-Moon barycentre's orbit, so a correct
            // obliquity rotation must drive its inclination to nearly zero. Getting the sign
            // of the rotation backwards would instead double it to ~47 degrees, which makes
            // this a decisive check on a mistake that is otherwise easy to miss.
            foreach (var body in Fixture["bodies"])
            {
                if (body["name"].AsString() != "EarthMoonBarycenter") continue;

                var equatorial = ReadState(body["vectors"][0]);
                var epoch = CosmicTime.FromJulianDateTdb(body["vectors"][0]["jdTdb"].AsDouble());
                double gm = body["gravitationalParameter"].AsDouble();

                var inEquatorial = OrbitalElements.FromStateVector(equatorial, gm, epoch);
                var inEcliptic = OrbitalElements.FromStateVector(StandardFrames.ToEcliptic(equatorial), gm, epoch);

                Assert.Equal(23.44, inEquatorial.InclinationRad * MathD.Rad2Deg, 0.05,
                    "in the equatorial frame Earth's orbit is inclined by the obliquity");
                Assert.True(inEcliptic.InclinationRad * MathD.Rad2Deg < 0.01,
                    $"in the ecliptic frame it should be flat, got {inEcliptic.InclinationRad * MathD.Rad2Deg:F5} deg");

                // And the rotation must be reversible.
                var roundTrip = StandardFrames.ToEquatorial(StandardFrames.ToEcliptic(equatorial));
                Assert.VectorClose(equatorial.Position, roundTrip.Position,
                    equatorial.Position.Magnitude * 1e-14, "equatorial -> ecliptic -> equatorial");
                return;
            }

            throw new AssertionException("fixture is missing the EarthMoonBarycenter entry");
        }

        [Test("the fixture itself is well formed")]
        public static void FixtureIsWellFormed()
        {
            var bodies = Fixture["bodies"];
            Assert.True(bodies.Count >= 10, $"expected at least 10 bodies in the fixture, found {bodies.Count}");

            foreach (var body in bodies)
            {
                string name = body["name"].AsString();
                Assert.True(!string.IsNullOrEmpty(name), "every fixture body needs a name");
                Assert.True(body["vectors"].Count >= 20, $"[{name}] expected at least 20 samples");
                Assert.True(body["gravitationalParameter"].AsDouble() > 0.0, $"[{name}] needs a positive GM");
                Assert.True(!body["elementsEcliptic"].IsNull, $"[{name}] needs JPL reference elements");

                foreach (var vector in body["vectors"])
                {
                    var state = ReadState(vector);
                    Assert.True(state.IsFinite, $"[{name}] every sample must be finite");
                    Assert.True(state.Radius > 0.0, $"[{name}] every sample must have positive radius");
                }
            }
        }

        [Test("catalogue GM constants agree with the values JPL publishes")]
        public static void ConstantsAgreeWithJpl()
        {
            // The fixture records the GM used for each body; those came from DE440, the same
            // source as Constants. This catches a typo in a constant, which would otherwise
            // show up only as a slow, plausible-looking drift.
            var expected = new Dictionary<string, double>
            {
                { "Mercury", Constants.GM_Sun + Constants.GM_Mercury },
                { "Venus", Constants.GM_Sun + Constants.GM_Venus },
                { "EarthMoonBarycenter", Constants.GM_Sun + Constants.GM_EarthMoonSystem },
                { "MarsBarycenter", Constants.GM_Sun + Constants.GM_MarsSystem },
                { "JupiterBarycenter", Constants.GM_Sun + Constants.GM_JupiterSystem },
                { "SaturnBarycenter", Constants.GM_Sun + Constants.GM_SaturnSystem },
                { "UranusBarycenter", Constants.GM_Sun + Constants.GM_UranusSystem },
                { "NeptuneBarycenter", Constants.GM_Sun + Constants.GM_NeptuneSystem },
                { "Moon", Constants.GM_Earth + Constants.GM_Moon },
                { "Ceres", Constants.GM_Sun + Constants.GM_Ceres },
            };

            foreach (var body in Fixture["bodies"])
            {
                string name = body["name"].AsString();
                if (!expected.TryGetValue(name, out double value)) continue;
                Assert.Relative(body["gravitationalParameter"].AsDouble(), value, 1e-12,
                    $"[{name}] GM constant matches the fixture");
            }
        }
    }
}
