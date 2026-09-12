using System;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The distance map, the group solver and the depth arithmetic.
    ///
    /// Every assertion here is arithmetic, so it either holds or it does not — no screenshot, no
    /// headset, no eyeballing. That is the whole reason the placement maths lives in Core.
    /// </summary>
    public static class ScaledSpaceTests
    {
        private const double D0 = ScaledSpace.IdentityRadiusMetres;

        // Solve() works in rendered metres, D = |anchor - eye| * unitsPerKm. Driving it at
        // 1 render metre per catalogue kilometre makes every figure below read directly as the
        // rendered metres the design tables are written in, with no conversion to get wrong.
        private const double Upk = 1.0;

        private static GroupNode Leaf(double distanceMetres, double radiusMetres) => new GroupNode
        {
            Parent = -1,
            AnchorKm = new Double3(0, 0, distanceMetres),
            RadiusKm = radiusMetres,
            LeafRadiusKm = radiusMetres,
            HasChildren = false,
            HasOpaque = true
        };

        private static GroupNode Parent(double distanceMetres, double subtreeRadiusMetres,
            double ownRadiusMetres) => new GroupNode
        {
            Parent = -1,
            AnchorKm = new Double3(0, 0, distanceMetres),
            RadiusKm = subtreeRadiusMetres,
            LeafRadiusKm = ownRadiusMetres,
            HasChildren = true,
            HasOpaque = true
        };

        private static GroupNode Child(double distanceMetres, double radiusMetres) => new GroupNode
        {
            Parent = 0,
            AnchorKm = new Double3(0, 0, distanceMetres),
            RadiusKm = radiusMetres,
            LeafRadiusKm = radiusMetres,
            HasChildren = false,
            HasOpaque = true
        };

        // ---- The map --------------------------------------------------------

        [Test("f is the identity inside d0, exactly")]
        public static void CompressIsIdentityNear()
        {
            foreach (double d in new[] { 0.05, 1.0, 9.999, D0 })
            {
                // Same branch, no arithmetic at all: this must be bit-exact or the room moves.
                Assert.True(ScaledSpace.Compress(d) == d, $"f({d}) must be exactly {d}");
                Assert.True(ScaledSpace.GroupScale(d) == 1.0, $"s({d}) must be exactly 1");
            }
        }

        [Test("f matches its published table to 1e-12 relative")]
        public static void CompressKnownAnswers()
        {
            // Relative 1e-12: Math.Log is within an ulp (~1e-16), and the margin lets an
            // algebraically equal form such as log(d) - log(d0) pass unchanged.
            var rows = new[]
            {
                new[] { 0.05, 0.05 },
                new[] { 10.0, 10.0 },
                new[] { 11.0, 10.953101798043 },
                new[] { 20.0, 16.931471805599 },
                new[] { 100.0, 33.025850929940 },
                new[] { 1e3, 56.051701859881 },
                new[] { 1e6, 125.129254649702 },
                new[] { 1e9, 194.206807439524 },
                new[] { 1e12, 263.284360229345 },
                new[] { 1e18, 401.439465808988 },
                new[] { 4.4e26, 600.462318657753 },
                new[] { 1e27, 608.672124178452 },
                new[] { 4.4e29, 669.539871447575 },
            };

            foreach (var row in rows)
            {
                Assert.Relative(row[1], ScaledSpace.Compress(row[0]), 1e-12, $"f({row[0]:G3})");
                Assert.Relative(row[1] / row[0], ScaledSpace.GroupScale(row[0]), 1e-12, $"s({row[0]:G3})");
            }
        }

        [Test("f is C-1 at the join, so walking across d0 shows no crease")]
        public static void CompressIsSmoothAtJoin()
        {
            // A one-sided difference quotient truncates at O(h/d0), so 1e-6 is the error the
            // method has, not a bound chosen to make the test pass.
            const double h = 1e-6 * D0;
            double below = (ScaledSpace.Compress(D0) - ScaledSpace.Compress(D0 - h)) / h;
            double above = (ScaledSpace.Compress(D0 + h) - ScaledSpace.Compress(D0)) / h;

            Assert.Equal(1.0, below, 1e-6, "slope just inside d0");
            Assert.Equal(1.0, above, 1e-6, "slope just outside d0");
            Assert.True(ScaledSpace.Compress(D0) == D0, "f is continuous at d0");
        }

        [Test("f is strictly increasing over 33 decades and stays inside the far plane")]
        public static void CompressIsMonotonicAndBounded()
        {
            // Strict monotonicity is what guarantees depth order along a ray. If it ever fails,
            // two surfaces swap, which is exactly the artefact the whole design is built to
            // avoid — so this is an exact comparison, not a tolerance.
            double previous = double.NegativeInfinity;
            for (int i = 0; i <= 2000; i++)
            {
                double d = Math.Pow(10.0, -3.0 + 33.0 * i / 2000.0);
                double f = ScaledSpace.Compress(d);
                Assert.True(f > previous, $"f stopped increasing at d = {d:G6}");
                previous = f;
            }

            Assert.True(ScaledSpace.Compress(1e30) + ScaledSpace.RigidLeaveMetres
                        < 0.5 * ScaledSpace.FarPlaneMetres,
                "the farthest placeable surface must sit well inside half the far plane");
        }

        [Test("f inverts to 1e-12 relative everywhere it is used")]
        public static void CompressInverts()
        {
            for (int i = 0; i <= 2000; i++)
            {
                double d = Math.Pow(10.0, -3.0 + 33.0 * i / 2000.0);
                Assert.Relative(d, ScaledSpace.Decompress(ScaledSpace.Compress(d)), 1e-12,
                    $"f^-1(f({d:G6}))");
            }
        }

        [Test("a similarity about the eye preserves angular size exactly")]
        public static void AngularSizeIsInvariantUnderTheSimilarity()
        {
            // asin(sR / sD) == asin(R/D) is algebraically exact; 1e-12 rad leaves four orders of
            // margin over double rounding (~1e-16) and is far tighter than any real scaling bug.
            var random = new Random(20260911);
            for (int i = 0; i < 1000; i++)
            {
                double d = Math.Pow(10.0, random.NextDouble() * 27.0 - 1.0);
                double r = d * random.NextDouble() * 0.9;
                double s = ScaledSpace.GroupScale(d);

                Assert.Equal(MathD.SafeAsin(r / d), MathD.SafeAsin(s * r / (s * d)), 1e-12,
                    $"angular radius at D = {d:G6}, R = {r:G6}");
            }
        }

        [Test("CompressRelative keeps direction exact and maps only the distance")]
        public static void CompressRelativeKeepsDirection()
        {
            var v = new Double3(3.0, -4.0, 12.0);          // |v| = 13
            Double3 c = ScaledSpace.CompressRelative(v);

            Assert.Equal(0.0, Double3.Angle(v, c), 1e-15, "direction is untouched");
            Assert.Relative(ScaledSpace.Compress(13.0), c.Magnitude, 1e-12, "distance is f(|v|)");

            Double3 near = new Double3(0.0, 0.5, 0.0);
            Assert.True(ScaledSpace.CompressRelative(near) == near, "inside d0 the vector is returned unchanged");
        }

        // ---- Depth ----------------------------------------------------------

        [Test("the depth quantum is z^2 / (near * 2^bits)")]
        public static void DepthQuantumIsTheUnormForm()
        {
            // 600.462318658^2 / (0.05 * 2^24) computed to 15 digits. NOTE: Section A and the
            // CONTRACT both print 0.429823 for this row; that figure is arithmetically wrong by
            // 3.4e-5 relative. The formula in both documents is right, the printed value is a
            // typo, and the code follows the formula.
            Assert.Relative(0.429815049324204, ScaledSpace.DepthQuantum(600.462318658), 1e-12,
                "quantum at the CMB's rendered distance");
            // 20/2^24 exactly. Section 2.4 prints this as "1.2 um", which is the same number to
            // two significant figures and 0.7 % away from it.
            Assert.Relative(1.1920928955078125e-6, ScaledSpace.DepthQuantum(1.0), 1e-12, "quantum at 1 m");
            Assert.True(ScaledSpace.DepthQuantum(0.0) == 0.0, "a degenerate distance has no quantum");

            // 16-bit is 256x coarser. The device's format is logged for exactly this reason.
            Assert.Relative(256.0,
                ScaledSpace.DepthQuantum(100.0, 16) / ScaledSpace.DepthQuantum(100.0, 24), 1e-12,
                "D16 against D24");
        }

        [Test("depth stays separable inside a rigid group from 1 m to 1e27 m")]
        public static void DepthIsSeparableAcrossTheLadder()
        {
            // The claim being tested: two surfaces inside one rigid group whose TRUE separation
            // along the ray exceeds 8.0e-4 of their distance land on different depth values, at
            // every scale from a metre to the observable universe.
            //
            // Inside a group everything is scaled by one s, so a true separation delta renders as
            // s*delta at rendered distance f(D); it resolves when s*delta > dz(f(D)), i.e. when
            // delta/D > dz(f)/f. So the whole claim is the single inequality below.
            const double claimed = 8.0e-4;

            double worst = 0.0, worstAt = 0.0;
            for (int i = 0; i <= 2700; i++)
            {
                double d = Math.Pow(10.0, i / 100.0);           // 1 m .. 1e27 m
                double f = ScaledSpace.Compress(d);
                double relative = ScaledSpace.DepthQuantum(f) / f;
                if (relative > worst) { worst = relative; worstAt = d; }
            }

            Assert.True(worst < claimed,
                $"worst relative resolution {worst:G4} at d = {worstAt:G4} m exceeds the claimed {claimed:G4}");

            // And it must not be vacuous: the same bound on an uncompressed scene fails by orders
            // of magnitude, which is what makes the compression load-bearing rather than cosmetic.
            double linear = ScaledSpace.DepthQuantum(1e6) / 1e6;
            Assert.True(linear > claimed * 100.0,
                "without compression the same test should fail badly; if it passes the bound is meaningless");
        }

        // ---- The solver -----------------------------------------------------

        private static GroupPlacement[] Solve(params GroupNode[] nodes)
        {
            var result = new GroupPlacement[nodes.Length];
            ScaledSpace.Solve(nodes, Double3.Zero, Upk, null, result);
            return result;
        }

        private static int SolveCounting(GroupNode[] nodes, out GroupPlacement[] result)
        {
            result = new GroupPlacement[nodes.Length];
            return ScaledSpace.Solve(nodes, Double3.Zero, Upk, null, result);
        }

        [Test("T1: a near leaf is placed exactly, with no scaling at all")]
        public static void T1NearLeafIsExact()
        {
            GroupPlacement p = Solve(Leaf(1.0, 0.3))[0];

            Assert.True(p.Mode == GroupMode.Rigid, "a near leaf is rigid");
            Assert.True(p.Scale == 1.0, "and exactly unscaled");
            Assert.Equal(0.7, p.RenderedNearMetres, 1e-12, "near point");
            Assert.Equal(1.3, p.RenderedFarMetres, 1e-12, "far point");
        }

        [Test("T2: a leaf a million kilometres out is rigid at s = f(D)/D")]
        public static void T2FarLeafIsRigid()
        {
            GroupPlacement p = Solve(Leaf(1e9, 1e6))[0];

            Assert.True(p.Mode == GroupMode.Rigid, "still rigid: its rendered radius is 0.19 m");
            Assert.Relative(1.94206807439524e-7, p.Scale, 1e-12, "s at 1e9 m");
            Assert.Relative(0.194206807439524, p.Scale * 1e6, 1e-12, "rendered radius");
        }

        [Test("T3: standing off a true-scale Earth is Envelope, not a 138 m sphere in your face")]
        public static void T3CloseBodyIsEnvelope()
        {
            // Earth plus atmosphere seen from low orbit at 1:1. The similarity would give a
            // rendered radius of 137.9 m, which is 14 times the cap; a rigid placement would put
            // the ground micrometres from the eye, inside the near plane.
            GroupPlacement p = Solve(Leaf(6.771e6, 6.471e6))[0];

            Assert.True(p.Mode == GroupMode.Envelope, "the eye is near something big: Envelope");
            Assert.True(p.Scale == 1.0, "Envelope draws at true scale and compresses per vertex");
            Assert.True(p.RigidOwner == -1, "an Envelope node owns no similarity");
        }

        [Test("T4: a planetary system owns its subtree far away and dissolves as you approach")]
        public static void T4SystemCut()
        {
            var far = new[] { Parent(1e9, 4e7, 6.4e6), Child(1.03e9, 1e6) };
            GroupPlacement[] p = Solve(far);

            Assert.True(p[0].Mode == GroupMode.Rigid && p[0].OwnsSubtree,
                "at 1e9 m the whole system is one rigid group (rendered radius 7.77 m)");
            Assert.True(p[1].Mode == GroupMode.Collapsed, "the moon rides the parent's similarity");
            Assert.True(p[1].Scale == p[0].Scale, "and shares it exactly — this is what keeps a transit correct");
            Assert.True(p[1].RigidOwner == 0, "the moon names its owner");
            Assert.Relative(7.768272297581, p[0].Scale * 4e7, 1e-12, "the system's rendered radius");

            GroupPlacement[] q = Solve(Parent(1e8, 4e7, 6.4e6));

            // 68.5 m of rendered radius: too deep to sit among groups decades away, so the system
            // dissolves. Its own body is then 10.96 m, still over the 9 m cap, so it is Envelope.
            Assert.True(q[0].Mode == GroupMode.Envelope,
                "at 1e8 m the system dissolves and the planet itself is too big to be rigid");
        }

        [Test("T5: a transiting moon stays in front of its planet, which per-body placement breaks")]
        public static void T5TheTransitTrap()
        {
            // Planet radius 100 at 1000 m; moon radius 10 at 850 m on the same ray, in front.
            var nodes = new[] { Parent(1000.0, 160.0, 100.0), Child(850.0, 10.0) };
            GroupPlacement[] p = Solve(nodes);

            Assert.True(p[0].Mode == GroupMode.Rigid && p[0].OwnsSubtree,
                "rendered radius 8.97 m is inside the 9 m cap, so one group owns both");

            double s = p[0].Scale;
            double moonBack = s * 860.0;
            double planetFront = s * 900.0;
            Assert.Relative(48.20446359949766, moonBack, 1e-12, "the moon's far side, rendered");
            Assert.Relative(50.44653167389290, planetFront, 1e-12, "the planet's near side, rendered");
            Assert.True(moonBack < planetFront, "the moon must render entirely in front of the planet");

            // The counterexample, which is why the similarity is per GROUP and never per body:
            // scaling the moon by its own f(d)/d puts its far side 4 m BEHIND the planet's near
            // face, and the moon vanishes mid-transit.
            Assert.True(ScaledSpace.Compress(860.0) > planetFront,
                "per-body placement would hide the moon behind the planet it is in front of");
        }

        [Test("T6: overlapping groups are pushed apart by two depth quanta")]
        public static void T6Push()
        {
            var nodes = new[] { Leaf(1000.0, 100.0), Leaf(1500.0, 100.0) };

            var before = new GroupPlacement[1];
            ScaledSpace.Solve(new[] { nodes[0] }, Double3.Zero, Upk, null, before);
            Assert.Relative(50.44653167389290, before[0].RenderedNearMetres, 1e-12, "A's unpushed near point");
            Assert.Relative(61.65687204586910, before[0].RenderedFarMetres, 1e-12, "A's unpushed far point");

            int conflicts = SolveCounting(nodes, out GroupPlacement[] p);

            Assert.True(conflicts == 0, "both groups can be ordered without reaching the floor");
            Assert.Relative(0.050992508553, p[0].Scale, 1e-9, "A's pushed scale");
            Assert.Relative(45.893258, p[0].RenderedNearMetres, 1e-7, "A's pushed near point");
            Assert.Relative(56.091759, p[0].RenderedFarMetres, 1e-7, "A's pushed far point");
            Assert.True(p[0].Pushed, "A reports that it was moved");

            Assert.True(p[0].RenderedFarMetres < p[1].RenderedNearMetres,
                "A's far point must clear B's near point");
            Assert.True(p[1].RenderedNearMetres - p[0].RenderedFarMetres >=
                        2.0 * ScaledSpace.DepthQuantum(p[1].RenderedNearMetres) * 0.999,
                "and clear it by the two quanta the margin promises");

            // The push is depth only. Angular size is what the viewer sees, and it must not move.
            Assert.Equal(MathD.SafeAsin(100.0 / 1000.0),
                MathD.SafeAsin(p[0].Scale * 100.0 / (p[0].Scale * 1000.0)), 1e-12,
                "A's angular radius is unchanged by the push");
        }

        [Test("T7: a push is refused rather than dragging a group into the room, and says so")]
        public static void T7NearPointFloor()
        {
            var nodes = new[] { Leaf(11.0, 5.0), Leaf(13.0, 1.0) };
            int conflicts = SolveCounting(nodes, out GroupPlacement[] p);

            // Clearing B would put A's near point at 4.37 m, inside the room. Refusing leaves the
            // pair possibly mis-ordered, so the count is the honest report; the play-mode gate
            // requires it to be zero at every fixed view.
            Assert.True(conflicts == 1, "the refusal is counted");
            Assert.Relative(5.0 / 6.0, p[0].Scale, 1e-12, "A is clamped to put its near point at exactly 5 m");
            Assert.Equal(ScaledSpace.NearPointFloorMetres, p[0].RenderedNearMetres, 1e-12,
                "and lands on the floor, not through it");
        }

        [Test("T8: a volume containing the eye is Enclosing at true scale")]
        public static void T8Enclosing()
        {
            GroupNode volume = Leaf(0.7, 173.0);
            volume.MayEnclose = true;
            GroupPlacement p = Solve(volume)[0];

            Assert.True(p.Mode == GroupMode.Enclosing, "a MayEnclose group that contains the eye encloses");
            Assert.True(p.Scale == 1.0, "the eye stays at the same object-space point, so the raymarch is unaffected");
        }

        [Test("T9: hysteresis keeps a group on the cut from flipping mode every frame")]
        public static void T9Hysteresis()
        {
            // Rendered radius exactly 10 m: above the 9 m entry threshold, below the 11 m exit.
            const double distance = 1e6;
            double radius = 10.0 / ScaledSpace.GroupScale(distance);
            GroupNode node = Parent(distance, radius, 1.0);

            GroupPlacement[] cold = Solve(node);
            Assert.False(cold[0].OwnsSubtree, "with no history, 10 m is over the entry threshold: it dissolves");

            var was = new[] { new GroupPlacement { Mode = GroupMode.Rigid, OwnsSubtree = true, RigidOwner = 0, Scale = 1 } };
            var warm = new GroupPlacement[1];
            ScaledSpace.Solve(new[] { node }, Double3.Zero, Upk, was, warm);
            Assert.True(warm[0].OwnsSubtree, "a group that already owned its subtree keeps it up to 11 m");
        }

        [Test("room content is never pushed, however much is behind it")]
        public static void RoomContentIsNotPushed()
        {
            // A hologram at 0.7 m with a group far behind it on the same ray. The far group is
            // pushed; the near one must not move, or the thing in your hands jumps.
            var nodes = new[] { Leaf(0.7, 0.3), Leaf(4.0, 1.0), Leaf(1e6, 5e4) };
            SolveCounting(nodes, out GroupPlacement[] p);

            Assert.True(p[0].Scale == 1.0 && !p[0].Pushed, "the hologram is untouched");
            Assert.True(p[1].Scale == 1.0 && !p[1].Pushed, "so is room-scale content behind it");
        }
    }
}
