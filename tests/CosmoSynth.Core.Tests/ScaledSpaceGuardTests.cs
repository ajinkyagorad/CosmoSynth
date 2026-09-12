using System;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The cases the placement suite was green on because no test exercised two features at once.
    ///
    /// T4/T5 have children but no push; T6/T7 have a push but no children. So the one combination
    /// where a pushed owner has to carry its collapsed descendants was never covered, and the
    /// suite reported 174 passed on an inconsistency it was built to catch.
    /// </summary>
    public static class ScaledSpaceGuardTests
    {
        private const double Upk = 1.0;

        private static GroupNode Node(double distanceMetres, double subtreeRadiusMetres,
            double leafRadiusMetres, int parent, bool hasChildren) => new GroupNode
        {
            Parent = parent,
            AnchorKm = new Double3(0, 0, distanceMetres),
            RadiusKm = subtreeRadiusMetres,
            LeafRadiusKm = leafRadiusMetres,
            HasChildren = hasChildren,
            HasOpaque = true
        };

        [Test("a pushed owner's collapsed child reports the depth it is actually drawn at")]
        public static void PushedOwnerCarriesItsDescendants()
        {
            // A planet at 1e9 m owning a moon at 1.03e9 m, with a third opaque group behind them
            // on the same ray to force the planet to be pushed. Nothing else in the suite has both
            // a subtree and a push.
            var nodes = new[]
            {
                Node(1.0e9, 4.0e7, 6.4e6, -1, true),    // 0: the owner
                Node(1.03e9, 1.0e6, 1.0e6, 0, false),   // 1: collapsed into it
                Node(2.0e9, 8.0e7, 8.0e7, -1, false),   // 2: behind, and overlapping on screen
            };

            var result = new GroupPlacement[nodes.Length];
            ScaledSpace.Solve(nodes, Double3.Zero, Upk, null, result);

            Assert.True(result[0].Mode == GroupMode.Rigid && result[0].OwnsSubtree,
                "the planet owns its subtree, or this test is not exercising the case it names");
            Assert.True(result[1].Mode == GroupMode.Collapsed,
                "the moon rides the planet's similarity");
            Assert.True(result[0].Pushed,
                "the planet is pushed, or this test is not exercising the case it names");

            // The defect: Push propagated the new Scale to the descendants and left their
            // RenderedNear/Far at the pre-push values. ScaleRenderer folds exactly this field into
            // the far-plane headroom the render line prints, for exactly these nodes.
            double s = result[1].Scale;
            double d = 1.03e9 * Upk;
            double r = 1.0e6 * Upk;

            Assert.True(s == result[0].Scale, "the moon's scale is the planet's");
            Assert.Relative(s * (d + r), result[1].RenderedFarMetres, 1e-12,
                "the moon's far point must agree with the scale it is drawn at");
            Assert.Relative(s * (d - r), result[1].RenderedNearMetres, 1e-12,
                "and so must its near point");
        }

        [Test("a short result array throws rather than reporting a clean frame")]
        public static void ShortResultArrayThrows()
        {
            var nodes = new[]
            {
                Node(1.0e6, 1.0e4, 1.0e4, -1, false),
                Node(2.0e6, 1.0e4, 1.0e4, -1, false),
            };

            // Zero is the value that means "no conflicts", and both RenderDiagnostics.IsHealthy
            // and the play-mode `push` assertion key off exactly that — so returning it here meant
            // a caller that mis-sized the array silently re-used last frame's placements and was
            // reported healthy.
            Assert.Throws<ArgumentException>(
                () => ScaledSpace.Solve(nodes, Double3.Zero, Upk, null, new GroupPlacement[1]),
                "a result array shorter than the node list");
            Assert.Throws<ArgumentException>(
                () => ScaledSpace.Solve(nodes, Double3.Zero, Upk, null, null),
                "a null result array");
        }

        [Test("a NaN scale is refused by the render transform, as ScaleLimits already refuses it")]
        public static void NaNScaleIsRefused()
        {
            // NaN <= 0.0 is FALSE, so the obvious guard lets NaN through and every ToRender after
            // it returns NaN with no exception anywhere — a scene of NaN transforms. The two
            // guards on this one quantity must agree about it.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ScaleSpace.RenderTransform(Double3.Zero, double.NaN),
                "a NaN units-per-km");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ScaleSpace.RenderTransform(Double3.Zero, 0.0),
                "a zero units-per-km");

            Assert.True(ScaleLimits.Clamp(double.NaN) == ScaleLimits.MinUnitsPerKm,
                "and ScaleLimits still answers the same way it always did");
        }
    }
}
