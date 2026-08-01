using System;
using System.Collections.Generic;
using System.IO;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The spacecraft layer: sampled trajectories, surface-fixed landing sites, coverage
    /// windows, and the catalogue that actually ships.
    ///
    /// The properties worth testing here are not "does it interpolate" — that is
    /// <see cref="SampledEphemeris"/>, already covered — but the ones that decide whether the
    /// application tells the truth: that a landing site rotates with its planet at the right
    /// rate, and that nothing claims to know where a spacecraft is outside the span its data
    /// covers.
    /// </summary>
    public static class SpacecraftTests
    {
        // ---- Surface-fixed placement ----------------------------------------

        [Test("a landing site rides its planet's rotation at the right speed")]
        public static void SurfaceSiteRotatesWithItsPlanet()
        {
            // Jezero Crater, where Perseverance is.
            var site = new SurfaceSite(18.4447, 77.4508, 0.0, "Jezero Crater");
            const double equatorial = 3396.19, polar = 3376.20;

            Double3 bodyFixed = site.BodyFixedPositionKm(equatorial, polar);
            var ephemeris = new SurfaceFixedEphemeris(BodyOrientation.Mars, bodyFixed);

            // Over one Martian sidereal day the site must return to where it started.
            double day = System.Math.Abs(BodyOrientation.Mars.SiderealPeriodSeconds);
            var start = new CosmicTime(0.0);
            var later = new CosmicTime(day);

            Double3 first = ephemeris.GetStateInParent(start).Position;
            Double3 second = ephemeris.GetStateInParent(later).Position;

            Assert.True((first - second).Magnitude < 1.0,
                $"after one sidereal day the site moved {(first - second).Magnitude:F3} km; it should return.");

            // Half a day later it must be on the other side of the axis. Comparing against the
            // full distance across catches a rotation applied at half or double rate, which a
            // round-trip test on its own would not.
            Double3 half = ephemeris.GetStateInParent(new CosmicTime(day * 0.5)).Position;
            double across = (first - half).Magnitude;
            double expectedAcross = 2.0 * bodyFixed.Magnitude * System.Math.Cos(18.4447 * MathD.Deg2Rad);
            Assert.Relative(expectedAcross, across, 1e-3, "distance across the rotation axis after half a day");

            // The radius from the spin axis is invariant under rotation about it.
            Assert.Relative(first.Magnitude, half.Magnitude, 1e-9, "distance from the planet's centre");
        }

        [Test("a landing site's velocity is the rotation it is actually carried by")]
        public static void SurfaceSiteVelocityMatchesRotation()
        {
            // On the equator, where the answer is a number that can be checked by hand:
            // v = 2 pi r / T.
            var site = new SurfaceSite(0.0, 0.0);
            const double equatorial = 3396.19, polar = 3376.20;

            Double3 bodyFixed = site.BodyFixedPositionKm(equatorial, polar);
            var ephemeris = new SurfaceFixedEphemeris(BodyOrientation.Mars, bodyFixed);

            StateVector state = ephemeris.GetStateInParent(new CosmicTime(0.0));

            double period = System.Math.Abs(BodyOrientation.Mars.SiderealPeriodSeconds);
            double expected = MathD.TwoPi * equatorial / period;

            Assert.Relative(expected, state.Velocity.Magnitude, 1e-6,
                "equatorial ground speed on Mars (should be about 0.24 km/s)");

            // Rigid rotation means the velocity is perpendicular to the position.
            Assert.True(System.Math.Abs(Double3.Dot(state.Position.Normalized, state.Velocity.Normalized)) < 1e-9,
                "a rotating site's velocity must be perpendicular to its radius");
        }

        [Test("a site placed at the pole barely moves, and one at the equator moves fastest")]
        public static void SurfaceSiteSpeedVariesWithLatitude()
        {
            const double equatorial = 3396.19, polar = 3376.20;

            double SpeedAt(double latitude)
            {
                var site = new SurfaceSite(latitude, 0.0);
                var ephemeris = new SurfaceFixedEphemeris(
                    BodyOrientation.Mars, site.BodyFixedPositionKm(equatorial, polar));
                return ephemeris.GetStateInParent(new CosmicTime(0.0)).Velocity.Magnitude;
            }

            double atEquator = SpeedAt(0.0);
            double atMidLatitude = SpeedAt(45.0);
            double atPole = SpeedAt(90.0);

            Assert.True(atEquator > atMidLatitude && atMidLatitude > atPole,
                $"ground speed should fall with latitude, got {atEquator:F4} / {atMidLatitude:F4} / {atPole:F4} km/s");
            Assert.True(atPole < 1e-9, $"a site on the pole should be stationary, got {atPole:G4} km/s");
        }

        // ---- Trajectory tables ----------------------------------------------

        [Test("a trajectory table reproduces its own samples exactly")]
        public static void TrajectoryTableIsExactAtItsKnots()
        {
            const double step = 3600.0;
            var samples = new List<StateVector>();
            for (int i = 0; i < 12; i++)
            {
                double t = i * step;
                samples.Add(new StateVector(
                    new Double3(1000.0 + t * 0.5, 2000.0 - t * 0.25, t * 0.1),
                    new Double3(0.5, -0.25, 0.1)));
            }

            var table = new TrajectoryTable(new CosmicTime(0.0), step, samples);
            IEphemerisSource ephemeris = table.CreateEphemeris();

            for (int i = 0; i < samples.Count; i++)
            {
                StateVector actual = ephemeris.GetStateInParent(new CosmicTime(i * step));
                Assert.VectorClose(samples[i].Position, actual.Position, 1e-6,
                    $"position at sample {i}");
            }

            Assert.Equal(0.0, table.Start.SecondsPastJ2000, 1e-9, "table start");
            Assert.Equal(11 * step, table.End.SecondsPastJ2000, 1e-9, "table end");
        }

        [Test("a trajectory table round-trips through JSON")]
        public static void TrajectoryTableRoundTrips()
        {
            var samples = new List<StateVector>
            {
                new StateVector(new Double3(1.0, 2.0, 3.0), new Double3(0.1, 0.2, 0.3)),
                new StateVector(new Double3(4.0, 5.0, 6.0), new Double3(0.4, 0.5, 0.6)),
                new StateVector(new Double3(7.0, 8.0, 9.0), new Double3(0.7, 0.8, 0.9)),
            };

            var original = new TrajectoryTable(new CosmicTime(12345.0), 600.0, samples);
            TrajectoryTable restored = TrajectoryTable.FromJson(JsonValue.Parse(original.ToJson().ToString(false)));

            Assert.True(restored.Count == original.Count, "sample count survives the round trip");
            Assert.Equal(original.StepSeconds, restored.StepSeconds, 1e-9, "step");
            Assert.Equal(original.Epoch.SecondsPastJ2000, restored.Epoch.SecondsPastJ2000, 1e-9, "epoch");

            for (int i = 0; i < original.Count; i++)
            {
                Assert.VectorClose(original.Samples[i].Position, restored.Samples[i].Position, 1e-12,
                    $"sample {i} position");
                Assert.VectorClose(original.Samples[i].Velocity, restored.Samples[i].Velocity, 1e-12,
                    $"sample {i} velocity");
            }
        }

        [Test("a malformed trajectory is rejected rather than silently truncated")]
        public static void TrajectoryRejectsRaggedSamples()
        {
            // Five numbers is not a whole number of six-component samples. Accepting it would
            // drop the remainder and shift every subsequent sample by one component, producing
            // a trajectory that parses and is nonsense.
            var json = JsonValue.Parse(
                "{\"epochSecondsPastJ2000\":0,\"stepSeconds\":60,\"samples\":[1,2,3,4,5]}");

            Assert.Throws<FormatException>(() => TrajectoryTable.FromJson(json),
                "a samples array that is not a multiple of six");
        }

        // ---- Coverage --------------------------------------------------------

        [Test("nothing claims to know where a spacecraft is outside its data")]
        public static void CoverageWindowBoundsWhatIsKnown()
        {
            var body = new CelestialBody { Id = "probe", Name = "Probe", Type = BodyType.Spacecraft };

            // With nothing declared, a body is always known — which is right for a planet.
            Assert.True(body.KnownAt(new CosmicTime(0.0)), "a body with no window is always known");

            body.KnownFrom = new CosmicTime(100.0);
            body.KnownUntil = new CosmicTime(200.0);

            Assert.False(body.KnownAt(new CosmicTime(99.0)), "before the window");
            Assert.True(body.KnownAt(new CosmicTime(150.0)), "inside the window");
            Assert.False(body.KnownAt(new CosmicTime(201.0)), "after the window");

            // A sampled trajectory bounds it too, independently of the declared window. This is
            // the case that matters: the interpolator clamps outside its span rather than
            // failing, so without this check a spacecraft would be drawn parked at its last
            // known position, looking exactly like one still flying.
            var samples = new List<StateVector>
            {
                new StateVector(new Double3(1.0, 0.0, 0.0), Double3.Zero),
                new StateVector(new Double3(2.0, 0.0, 0.0), Double3.Zero),
            };
            var withTable = new CelestialBody
            {
                Id = "probe2",
                Trajectory = new TrajectoryTable(new CosmicTime(0.0), 10.0, samples)
            };

            Assert.True(withTable.KnownAt(new CosmicTime(5.0)), "inside the table");
            Assert.False(withTable.KnownAt(new CosmicTime(11.0)), "past the end of the table");
            Assert.False(withTable.KnownAt(new CosmicTime(-1.0)), "before the start of the table");
        }

        [Test("a catalogue rejects a window that claims more than its data")]
        public static void ValidateCatchesAnOverstatedWindow()
        {
            var samples = new List<StateVector>
            {
                new StateVector(new Double3(1000.0, 0.0, 0.0), new Double3(0.0, 1.0, 0.0)),
                new StateVector(new Double3(1000.0, 10.0, 0.0), new Double3(0.0, 1.0, 0.0)),
            };

            // The window runs a day past the last sample. Nothing is drawn wrongly — KnownAt
            // takes the intersection of the two — which is precisely why this went unnoticed on
            // all eight shipped sampled missions, overrunning by up to 148 days. A file that
            // overstates its own extent is only harmless until something believes it.
            var overstated = new CelestialBody
            {
                Id = "probe",
                Type = BodyType.Spacecraft,
                ParentId = "earth",
                Trajectory = new TrajectoryTable(new CosmicTime(0.0), 10.0, samples),
                KnownFrom = new CosmicTime(0.0),
                KnownUntil = new CosmicTime(86400.0)
            };
            Assert.True(overstated.Validate().Count > 0,
                "a window running past the end of the trajectory must be reported");

            overstated.KnownUntil = new CosmicTime(10.0);
            Assert.True(overstated.Validate().Count == 0,
                $"a window matching the trajectory must pass: {string.Join("; ", overstated.Validate())}");

            // Backwards, which draws nothing at any epoch and looks like a missing mesh.
            var backwards = new CelestialBody
            {
                Id = "probe2",
                Type = BodyType.Spacecraft,
                ParentId = "earth",
                Site = new SurfaceSite(0.0, 0.0),
                KnownFrom = new CosmicTime(200.0),
                KnownUntil = new CosmicTime(100.0)
            };
            Assert.True(backwards.Validate().Count > 0, "a window that ends before it starts must be reported");
        }

        [Test("a polar orbiter is not reported as being inside an oblate planet")]
        public static void PeriapsisIsCheckedWhereItActuallyOccurs()
        {
            // Juno's perijove radius is 71 491 km against Jupiter's equatorial 71 492, and it is
            // 3385 km above the cloud tops — because perijove happens at +57 degrees latitude,
            // where an oblate Jupiter's surface is 68 106 km rather than 71 492. Comparing a
            // periapsis radius against the equatorial radius only asks the right question for an
            // equatorial orbit, and it started failing the moment the catalogue stopped modelling
            // the giants as spheres. The check was wrong, not the data.
            var catalog = new CelestialCatalog { Name = "test" };
            catalog.Add(new CelestialBody
            {
                Id = "giant", Type = BodyType.Planet, GravitationalParameter = 1.26686e8,
                EquatorialRadiusKm = 71492.0, PolarRadiusKm = 66854.0,
                Orientation = BodyOrientation.Jupiter,
                Orbit = new OrbitalElements
                {
                    SemiLatusRectumKm = 7.76e8, Eccentricity = 0.0481,
                    GravitationalParameter = 1.32839e11, Epoch = new CosmicTime(0.0)
                },
                ParentId = "star",
            });
            catalog.Add(new CelestialBody
            {
                Id = "star", Type = BodyType.Star, GravitationalParameter = 1.32712e11,
                LuminosityWatts = 3.828e26, EffectiveTemperatureK = 5772.0,
            });
            catalog.Add(new CelestialBody
            {
                Id = "polar", Type = BodyType.Spacecraft, ParentId = "giant",
                Orbit = new OrbitalElements
                {
                    SemiLatusRectumKm = 141250.188844, Eccentricity = 0.975775,
                    InclinationRad = 106.315274 * MathD.Deg2Rad,
                    LongitudeOfAscendingNodeRad = 282.999089 * MathD.Deg2Rad,
                    ArgumentOfPeriapsisRad = 96.721315 * MathD.Deg2Rad,
                    GravitationalParameter = 1.26686e8, Epoch = new CosmicTime(0.0)
                },
            });
            catalog.Rebuild();

            foreach (string problem in catalog.Validate())
            {
                Assert.True(!problem.Contains("periapsis"),
                    $"a high-latitude periapsis well clear of the surface was reported: {problem}");
            }

            // The check must still catch a genuine one: same orbit brought down to a radius
            // below the surface at every latitude.
            CelestialBody probe = catalog.Get("polar");
            OrbitalElements grazing = probe.Orbit.Value;
            grazing.SemiLatusRectumKm = 60000.0 * (1.0 + grazing.Eccentricity);
            probe.Orbit = grazing;

            bool caught = false;
            foreach (string problem in catalog.Validate())
            {
                if (problem.Contains("periapsis")) caught = true;
            }
            Assert.True(caught, "an orbit whose periapsis is inside the planet must still be reported");
        }

        [Test("a spacecraft with no placement at all is rejected")]
        public static void ValidateCatchesAPlacelessSpacecraft()
        {
            // It would still build a frame — FixedEphemeris at its parent's centre — and be
            // drawn inside the body it was meant to be orbiting, which reads as a missing model
            // rather than as missing data.
            var nowhere = new CelestialBody { Id = "ghost", Type = BodyType.Spacecraft, ParentId = "earth" };
            Assert.True(nowhere.Validate().Count > 0, "a spacecraft with no placement must be reported");
        }

        [Test("a lander whose host has no rotation model is reported, not buried")]
        public static void ValidateCatchesALanderWithNoGroundToStandOn()
        {
            // Without the host's pole and prime meridian there is no way to turn a latitude
            // into a position, so the lander ends up at the centre of the planet. That is
            // invisible: a body inside another body simply does not appear, exactly like one
            // whose mesh failed to load.
            var catalog = new CelestialCatalog { Name = "test" };
            catalog.Add(new CelestialBody
            {
                Id = "rock", Name = "Rock", Type = BodyType.DwarfPlanet,
                GravitationalParameter = 100.0, EquatorialRadiusKm = 500.0,
            });
            catalog.Add(new CelestialBody
            {
                Id = "lander", Type = BodyType.Spacecraft, ParentId = "rock",
                Site = new SurfaceSite(10.0, 20.0),
            });
            catalog.Rebuild();

            bool reported = false;
            foreach (string problem in catalog.Validate())
            {
                if (problem.Contains("rotation model")) reported = true;
            }
            Assert.True(reported, "a site on a body with no rotation model must be reported");

            // And with one, it really is placed on the surface rather than at the centre.
            catalog.Get("rock").Orientation = new BodyOrientation(0.0, 90.0, 0.0, 100.0);
            ReferenceFrame root = catalog.BuildFrameTree();
            double distance = root.Find("lander").StateInParent(new CosmicTime(0.0)).Position.Magnitude;
            Assert.Relative(500.0, distance, 1e-9, "the lander sits on the surface once the host can rotate");
        }

        [Test("a trajectory with no epoch is rejected rather than parked at J2000")]
        public static void TrajectoryRequiresItsEpoch()
        {
            // Read as a default of zero, an absent epoch key puts the whole trajectory at
            // J2000 — for Voyager, twenty-two years from where it belongs — and every sample
            // still interpolates smoothly, so the only symptom is a spacecraft in the wrong
            // decade. The step was already required; the epoch matters more.
            var json = JsonValue.Parse("{\"stepSeconds\":60,\"samples\":[1,2,3,4,5,6,7,8,9,10,11,12]}");
            Assert.Throws<FormatException>(() => TrajectoryTable.FromJson(json),
                "a trajectory with no epoch");
        }

        // ---- Frame construction ---------------------------------------------

        [Test("the catalogue builds the right kind of ephemeris for each placement")]
        public static void CatalogChoosesEphemerisByPlacement()
        {
            var catalog = new CelestialCatalog { Name = "test" };

            catalog.Add(new CelestialBody
            {
                Id = "mars",
                Name = "Mars",
                Type = BodyType.Planet,
                GravitationalParameter = 42828.375,
                EquatorialRadiusKm = 3396.19,
                PolarRadiusKm = 3376.20,
                Orientation = BodyOrientation.Mars,
                Orbit = new OrbitalElements
                {
                    SemiLatusRectumKm = 2.2e8,
                    Eccentricity = 0.0934,
                    GravitationalParameter = 1.327124400e11,
                    Epoch = new CosmicTime(0.0)
                },
                ParentId = "sun",
            });
            catalog.Add(new CelestialBody
            {
                Id = "sun", Name = "Sun", Type = BodyType.Star,
                GravitationalParameter = 1.327124400e11,
            });
            catalog.Add(new CelestialBody
            {
                Id = "rover", Name = "Rover", Type = BodyType.Spacecraft, ParentId = "mars",
                Site = new SurfaceSite(18.4447, 77.4508, 0.0, "Jezero Crater"),
            });

            catalog.Rebuild();
            ReferenceFrame root = catalog.BuildFrameTree();

            ReferenceFrame rover = root.Find("rover");
            Assert.True(rover != null, "the rover has a frame");
            Assert.True(rover.Ephemeris is SurfaceFixedEphemeris,
                $"a landed body must get a surface-fixed ephemeris, got {rover.Ephemeris?.GetType().Name}");

            // The rover must sit on Mars's surface, not at its centre.
            Double3 relative = rover.StateInParent(new CosmicTime(0.0)).Position;
            Assert.True(relative.Magnitude > 3300.0 && relative.Magnitude < 3400.0,
                $"the rover is {relative.Magnitude:F1} km from Mars's centre; it should be on the surface.");

            ReferenceFrame mars = root.Find("mars");
            Assert.True(mars.Ephemeris is KeplerEphemeris, "a body with elements gets a Kepler ephemeris");
        }

        // ---- The shipped artefact -------------------------------------------

        private static CelestialCatalog _shipped;

        private static CelestialCatalog Shipped
        {
            get
            {
                if (_shipped == null)
                {
                    string path = FindRepoFile(Path.Combine(
                        "unity", "Assets", "StreamingAssets", "CosmoSynth", "catalog", "spacecraft.json"));

                    if (path == null)
                        throw new FileNotFoundException(
                            "spacecraft.json not found. Build it with " +
                            "'python3 tools/cosmodata/build_spacecraft.py'.");

                    // Merged with the Solar System, because that is how it is loaded: a
                    // spacecraft's parent is a planet, and on its own the fragment is a set of
                    // dangling references.
                    string solar = FindRepoFile(Path.Combine(
                        "unity", "Assets", "StreamingAssets", "CosmoSynth", "catalog", "solar_system.json"));

                    _shipped = CelestialCatalog.Parse(File.ReadAllText(solar));
                    _shipped.MergeFrom(CelestialCatalog.Parse(File.ReadAllText(path)));
                }
                return _shipped;
            }
        }

        private static string FindRepoFile(string relative)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 10 && directory != null; depth++)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            return null;
        }

        [Test("the shipped spacecraft catalogue parses, validates and hangs off real bodies")]
        public static void ShippedSpacecraftCatalogIsValid()
        {
            var problems = Shipped.Validate();
            Assert.True(problems.Count == 0,
                $"the merged catalogue has {problems.Count} problems:\n  " + string.Join("\n  ", problems));

            int spacecraft = 0;
            foreach (CelestialBody body in Shipped.Bodies)
            {
                if (body.Type != BodyType.Spacecraft) continue;
                spacecraft++;

                Assert.True(Shipped.Contains(body.ParentId),
                    $"{body.Id} names parent '{body.ParentId}', which is not in the catalogue");

                // Every spacecraft must be placed by exactly one mechanism. Two would let one
                // silently win; none is a claim with no content behind it.
                int placements = (body.Orbit.HasValue ? 1 : 0)
                                 + (body.Trajectory != null ? 1 : 0)
                                 + (body.Site.HasValue ? 1 : 0);
                Assert.True(placements == 1,
                    $"{body.Id} has {placements} placements; it must have exactly one");

                // ...and the derived property must name the one the frame builder will use.
                Assert.True(body.Placement != CelestialBody.PlacementKind.None
                            && body.Placement != CelestialBody.PlacementKind.Fixed,
                    $"{body.Id} reports placement {body.Placement}");
                Assert.True(!string.IsNullOrEmpty(body.DescribeCoverage()),
                    $"{body.Id} cannot say what span its position is known over");

                Assert.True(body.KnownFrom.HasValue && body.KnownUntil.HasValue,
                    $"{body.Id} does not say when its position is known");
                Assert.True(body.KnownUntil.Value.SecondsPastJ2000 > body.KnownFrom.Value.SecondsPastJ2000,
                    $"{body.Id} has a coverage window that ends before it starts");

                Assert.True(!string.IsNullOrEmpty(body.Summary), $"{body.Id} has no summary");
            }

            Assert.True(spacecraft >= 20, $"expected at least 20 spacecraft, found {spacecraft}");
        }

        [Test("every shipped spacecraft names a model that exists and is a real mesh")]
        public static void ShippedSpacecraftModelsExist()
        {
            int checkedModels = 0;

            foreach (CelestialBody body in Shipped.Bodies)
            {
                if (body.Type != BodyType.Spacecraft || body.Model == null) continue;

                ModelReference model = body.Model.Value;
                Assert.True(model.IsPresent, $"{body.Id} has an empty model path");

                string path = FindRepoFile(Path.Combine("unity", "Assets", "StreamingAssets",
                    model.Path.Replace('/', Path.DirectorySeparatorChar)));

                Assert.True(path != null, $"{body.Id} names model '{model.Path}', which does not exist");

                // A magic-number check, not a size check. The whole reason this format has a
                // magic number is that a wrong path can return something that looks fine.
                byte[] header = new byte[CsMeshHeaderBytes];
                using (var stream = File.OpenRead(path))
                {
                    Assert.True(stream.Length >= CsMeshHeaderBytes,
                        $"{body.Id}'s mesh is {stream.Length} bytes, too short to be a .csmesh");
                    stream.Read(header, 0, header.Length);
                }

                Assert.True(header[0] == 'C' && header[1] == 'S' && header[2] == 'M' && header[3] == '1',
                    $"{body.Id}'s mesh does not begin with 'CSM1'");

                // The declared size must match the file, which is what catches a truncated
                // write or a packer/reader disagreement.
                uint flags = BitConverter.ToUInt32(header, 4);
                long vertices = BitConverter.ToUInt32(header, 8);
                long indices = BitConverter.ToUInt32(header, 12);
                long submeshes = BitConverter.ToUInt32(header, 16);
                long expected = CsMeshHeaderBytes + vertices * 24
                                + indices * ((flags & 1u) != 0 ? 4 : 2) + submeshes * 32;

                long actual = new FileInfo(path).Length;
                Assert.True(actual == expected,
                    $"{body.Id}'s mesh declares {vertices} vertices/{indices} indices/{submeshes} " +
                    $"submeshes, needing {expected} bytes, but the file is {actual}");

                Assert.True(model.LengthMetres > 0.1 && model.LengthMetres < 200.0,
                    $"{body.Id} is {model.LengthMetres} m long, which is not a plausible spacecraft");

                checkedModels++;
            }

            Assert.True(checkedModels >= 20, $"expected at least 20 models, checked {checkedModels}");
        }

        private const int CsMeshHeaderBytes = 48;

        // ---- The shipped meshes ---------------------------------------------

        /// <summary>
        /// A .csmesh decoded far enough to check the things a header cannot show.
        ///
        /// Deliberately a second, independent reader rather than a call into
        /// <c>Runtime.CsMesh</c>: that one needs UnityEngine, and the whole point of this suite
        /// is that it runs without it. Two readers agreeing on a binary format is also a
        /// stronger statement than one reader round-tripping against itself.
        /// </summary>
        private sealed class Mesh
        {
            public double[] Px, Py, Pz, Nx, Ny, Nz;
            public int[] Indices;
            public int[] SubmeshStart, SubmeshCount;
            public double[] Alpha, Metallic;
            public float DeclaredLengthMetres;
        }

        private static Mesh ReadMesh(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            var mesh = new Mesh();

            uint flags = BitConverter.ToUInt32(data, 4);
            int vertices = (int)BitConverter.ToUInt32(data, 8);
            int indices = (int)BitConverter.ToUInt32(data, 12);
            int submeshes = (int)BitConverter.ToUInt32(data, 16);
            mesh.DeclaredLengthMetres = BitConverter.ToSingle(data, 44);

            bool wide = (flags & 1u) != 0;
            mesh.Px = new double[vertices]; mesh.Py = new double[vertices]; mesh.Pz = new double[vertices];
            mesh.Nx = new double[vertices]; mesh.Ny = new double[vertices]; mesh.Nz = new double[vertices];

            int at = CsMeshHeaderBytes;
            for (int i = 0; i < vertices; i++, at += 24)
            {
                mesh.Px[i] = BitConverter.ToSingle(data, at);
                mesh.Py[i] = BitConverter.ToSingle(data, at + 4);
                mesh.Pz[i] = BitConverter.ToSingle(data, at + 8);
                mesh.Nx[i] = BitConverter.ToSingle(data, at + 12);
                mesh.Ny[i] = BitConverter.ToSingle(data, at + 16);
                mesh.Nz[i] = BitConverter.ToSingle(data, at + 20);
            }

            mesh.Indices = new int[indices];
            for (int i = 0; i < indices; i++, at += wide ? 4 : 2)
                mesh.Indices[i] = wide ? (int)BitConverter.ToUInt32(data, at) : BitConverter.ToUInt16(data, at);

            mesh.SubmeshStart = new int[submeshes];
            mesh.SubmeshCount = new int[submeshes];
            mesh.Alpha = new double[submeshes];
            mesh.Metallic = new double[submeshes];
            for (int i = 0; i < submeshes; i++, at += 32)
            {
                mesh.SubmeshStart[i] = (int)BitConverter.ToUInt32(data, at);
                mesh.SubmeshCount[i] = (int)BitConverter.ToUInt32(data, at + 4);
                mesh.Alpha[i] = BitConverter.ToSingle(data, at + 20);
                mesh.Metallic[i] = BitConverter.ToSingle(data, at + 24);
            }

            return mesh;
        }

        private static IEnumerable<(CelestialBody Body, Mesh Mesh)> ShippedMeshes()
        {
            foreach (CelestialBody body in Shipped.Bodies)
            {
                if (body.Type != BodyType.Spacecraft || body.Model == null) continue;
                string path = FindRepoFile(Path.Combine("unity", "Assets", "StreamingAssets",
                    body.Model.Value.Path.Replace('/', Path.DirectorySeparatorChar)));
                if (path == null) continue;
                yield return (body, ReadMesh(path));
            }
        }

        [Test("a shipped mesh is the size it says it is")]
        public static void ShippedMeshesAreTheDeclaredSize()
        {
            // Every vertex must belong to a triangle, and the drawn extent must be the declared
            // one. These are the same assertion from two directions, because the failure that
            // motivated them was one bug: decimation left vertices behind that no triangle
            // referenced, the bounding box was taken over the whole array, and both the scale
            // applied to the model and the length written into its header came from geometry
            // that is never drawn. Ulysses shipped at 1.656 m against a declared 3.200 m.
            //
            // Nothing about the file is wrong when this happens — it parses, the sizes match,
            // the triangle count is right — so a header check cannot see it.
            int checkedMeshes = 0;

            foreach (var (body, mesh) in ShippedMeshes())
            {
                var referenced = new HashSet<int>(mesh.Indices);
                Assert.True(referenced.Count == mesh.Px.Length,
                    $"{body.Id}: {mesh.Px.Length - referenced.Count} of {mesh.Px.Length} vertices are " +
                    "referenced by no triangle, so they are invisible and still set the bounds");

                double longest = 0.0;
                foreach (double[] axis in new[] { mesh.Px, mesh.Py, mesh.Pz })
                {
                    double low = double.MaxValue, high = double.MinValue;
                    foreach (int index in referenced)
                    {
                        low = System.Math.Min(low, axis[index]);
                        high = System.Math.Max(high, axis[index]);
                    }
                    longest = System.Math.Max(longest, high - low);
                }

                Assert.Relative(mesh.DeclaredLengthMetres, longest, 1e-4,
                    $"{body.Id}: longest drawn axis against the length in its header");
                Assert.Relative(body.Model.Value.LengthMetres, longest, 1e-4,
                    $"{body.Id}: longest drawn axis against the length in the catalogue");

                checkedMeshes++;
            }

            Assert.True(checkedMeshes >= 20, $"expected at least 20 models, checked {checkedMeshes}");
        }

        [Test("shipped meshes are wound the right way out")]
        public static void ShippedMeshesFaceOutwards()
        {
            // glTF fixes both conventions -- counter-clockwise front faces, normals pointing
            // away from them -- and so does Unity, in the opposite handedness. The importer
            // converts by negating one axis, which is a mirror, so it has to reverse the
            // winding too. It did that for its own axis conversion and not for mirroring node
            // transforms inside the model, which is how SOHO shipped with 2% of its triangles
            // agreeing with their own normals and rendered as a shell seen from inside.
            //
            // Nothing counts differently when this happens: same triangles, same vertices, same
            // bounding box, same file size. The only cheap test is whether the geometry and the
            // normals agree about which side is out.
            foreach (var (body, mesh) in ShippedMeshes())
            {
                int agreeing = 0, total = 0;

                for (int i = 0; i + 2 < mesh.Indices.Length; i += 3)
                {
                    int a = mesh.Indices[i], b = mesh.Indices[i + 1], c = mesh.Indices[i + 2];

                    double ux = mesh.Px[b] - mesh.Px[a], uy = mesh.Py[b] - mesh.Py[a], uz = mesh.Pz[b] - mesh.Pz[a];
                    double vx = mesh.Px[c] - mesh.Px[a], vy = mesh.Py[c] - mesh.Py[a], vz = mesh.Pz[c] - mesh.Pz[a];

                    double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
                    double nx = mesh.Nx[a] + mesh.Nx[b] + mesh.Nx[c];
                    double ny = mesh.Ny[a] + mesh.Ny[b] + mesh.Ny[c];
                    double nz = mesh.Nz[a] + mesh.Nz[b] + mesh.Nz[c];

                    total++;
                    if (cx * nx + cy * ny + cz * nz > 0.0) agreeing++;
                }

                double fraction = total > 0 ? (double)agreeing / total : 1.0;

                // Half, not most. Real CAD exports carry open shells, coincident two-sided
                // panels and a scattering of genuinely broken faces, so the good models here
                // score 63 to 97 per cent. A mirrored one scores 2. The populations are nowhere
                // near each other, so the threshold only has to land in the gap.
                Assert.True(fraction > 0.5,
                    $"{body.Id}: only {fraction:P1} of triangles are wound to match their normals, " +
                    "so the model is inside out");
            }
        }

        [Test("shipped materials are ones this renderer can actually draw")]
        public static void ShippedMaterialsAreDrawable()
        {
            // Two properties survive the glTF import into a Standard material that the Built-in
            // pipeline cannot honour as written, and both are silent.
            //
            // Alpha: Standard is opaque by default and ignores the channel, so a material the
            // author made fully transparent arrives as solid geometry. Juno shipped a
            // (1, 1, 1, 0.00) submesh drawn as a solid white panel across the spacecraft.
            //
            // Metallic: glTF's default metallicFactor is 1, which is right under image-based
            // lighting and wrong here -- a Standard material at metallic 1 has no diffuse term
            // and this scene has flat 0.05 ambient and no reflection probe, so it draws as a
            // silhouette. Perseverance carried it on four of six submeshes.
            foreach (var (body, mesh) in ShippedMeshes())
            {
                for (int i = 0; i < mesh.Alpha.Length; i++)
                {
                    Assert.True(mesh.Alpha[i] >= 0.02,
                        $"{body.Id} submesh {i} has alpha {mesh.Alpha[i]:F3}: invisible in the source " +
                        "and opaque once drawn");
                    Assert.True(mesh.Metallic[i] <= 0.35 + 1e-6,
                        $"{body.Id} submesh {i} is metallic {mesh.Metallic[i]:F3}, which has no diffuse " +
                        "term to light in a scene with no reflection probe");
                }
            }
        }

        [Test("a shipped mesh's submeshes tile its index buffer exactly")]
        public static void ShippedSubmeshesTileTheIndexBuffer()
        {
            // The runtime slices the index buffer by (start, count) per submesh. A gap draws
            // nothing; an overlap draws the same triangles twice with two materials; a short
            // last submesh silently drops the end of the model.
            foreach (var (body, mesh) in ShippedMeshes())
            {
                int expected = 0;
                for (int i = 0; i < mesh.SubmeshStart.Length; i++)
                {
                    Assert.True(mesh.SubmeshStart[i] == expected,
                        $"{body.Id} submesh {i} starts at {mesh.SubmeshStart[i]}, not {expected}");
                    Assert.True(mesh.SubmeshCount[i] % 3 == 0,
                        $"{body.Id} submesh {i} spans {mesh.SubmeshCount[i]} indices, not a whole number " +
                        "of triangles");
                    expected += mesh.SubmeshCount[i];
                }
                Assert.True(expected == mesh.Indices.Length,
                    $"{body.Id}: submeshes cover {expected} of {mesh.Indices.Length} indices");

                foreach (int index in mesh.Indices)
                {
                    Assert.True(index >= 0 && index < mesh.Px.Length,
                        $"{body.Id}: index {index} is out of range for {mesh.Px.Length} vertices");
                }
            }
        }

        [Test("shipped spacecraft are where JPL says, not inside the bodies they orbit")]
        public static void ShippedSpacecraftArePlacedSensibly()
        {
            ReferenceFrame root = Shipped.BuildFrameTree();

            foreach (CelestialBody body in Shipped.Bodies)
            {
                if (body.Type != BodyType.Spacecraft) continue;

                // Evaluate in the middle of the window, where every placement is valid.
                var midpoint = new CosmicTime(
                    (body.KnownFrom.Value.SecondsPastJ2000 + body.KnownUntil.Value.SecondsPastJ2000) * 0.5);

                Assert.True(body.KnownAt(midpoint), $"{body.Id} is not known at the middle of its own window");

                ReferenceFrame frame = root.Find(body.Id);
                Assert.True(frame != null, $"{body.Id} has no frame");

                Double3 relative = frame.StateInParent(midpoint).Position;
                double distance = relative.Magnitude;

                Assert.True(!double.IsNaN(distance) && !double.IsInfinity(distance),
                    $"{body.Id} is at a non-finite distance from {body.ParentId}");

                CelestialBody parent = Shipped.Get(body.ParentId);
                double parentRadius = parent?.EquatorialRadiusKm ?? 0.0;

                if (body.Site.HasValue)
                {
                    // A lander sits on the surface, so its distance is the local radius.
                    Assert.True(System.Math.Abs(distance - parentRadius) < parentRadius * 0.01,
                        $"{body.Id} is {distance:F1} km from {body.ParentId}'s centre, but the " +
                        $"surface is at about {parentRadius:F1} km");
                }
                else
                {
                    // Everything else must be outside the body it is measured against. A
                    // spacecraft inside its own primary means the placement is wrong.
                    Assert.True(distance > parentRadius,
                        $"{body.Id} is {distance:F1} km from {body.ParentId}'s centre, inside its " +
                        $"radius of {parentRadius:F1} km");
                }
            }
        }

        [Test("a spacecraft that has ended is not drawn as though it were still flying")]
        public static void EndedMissionsStopBeingKnown()
        {
            CelestialBody cassini = Shipped.Get("cassini");
            Assert.True(cassini != null, "Cassini is in the shipped catalogue");

            // Cassini entered Saturn's atmosphere on 15 September 2017. A conic propagates
            // happily forever, so the only thing stopping the application drawing an intact
            // Cassini orbiting Saturn today is the coverage window.
            var today = new CosmicTime((2026.5 - 2000.0) * 365.25 * 86400.0);
            Assert.False(cassini.KnownAt(today), "Cassini must not be known in 2026");

            var during = new CosmicTime((2010.0 - 2000.0) * 365.25 * 86400.0);
            Assert.True(cassini.KnownAt(during), "Cassini must be known in 2010, during its mission");

            Assert.True(!string.IsNullOrEmpty(cassini.Epitaph),
                "an ended mission should say how it ended");
        }
    }
}
