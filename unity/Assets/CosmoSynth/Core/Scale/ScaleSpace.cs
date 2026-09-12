using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>How a body should be drawn, chosen by apparent size rather than by raw distance.</summary>
    public enum RepresentationLevel
    {
        /// <summary>Below a pixel: a point, sprite or contribution to a star field.</summary>
        Point = 0,

        /// <summary>A few pixels: a billboard or impostor, with no real geometry.</summary>
        Impostor = 1,

        /// <summary>Resolvable: a smooth sphere with an albedo map and atmosphere shell.</summary>
        Sphere = 2,

        /// <summary>Filling a good part of the view: adaptively subdivided cube-sphere terrain.</summary>
        Terrain = 3,

        /// <summary>Close enough that only a patch is visible: streamed high-resolution tiles.</summary>
        SurfaceTiles = 4,

        /// <summary>Standing on it: local props, scatter and effects.</summary>
        LocalDetail = 5
    }

    /// <summary>
    /// Scale-space arithmetic: choosing how to draw a body, and the mapping from absolute
    /// coordinates to the universe host's local space.
    ///
    /// Distance compression and group placement live in <see cref="ScaledSpace"/>, which is the
    /// only account of distance in the codebase. The band stack that used to live here described
    /// a multi-pass compositor over a reversed floating-point depth buffer; the device has
    /// neither, and two accounts of distance is how one of them rots.
    ///
    /// Everything here is pure arithmetic on doubles so it can be tested without a renderer.
    /// The Unity layer supplies cameras, layers and materials; this decides the numbers.
    /// </summary>
    public static class ScaleSpace
    {
        // ---- Apparent size --------------------------------------------------

        /// <summary>
        /// Angular diameter of a sphere of <paramref name="radiusKm"/> seen from
        /// <paramref name="distanceKm"/> (measured to its centre), in radians.
        ///
        /// Uses the asin form, not the small-angle ratio: once you are close enough for the
        /// body to fill the sky the two disagree completely, and that is exactly the regime a
        /// planetary lander spends its time in.
        /// </summary>
        public static double AngularDiameterRad(double radiusKm, double distanceKm)
        {
            if (distanceKm <= radiusKm) return MathD.TwoPi;   // inside the body: it is everywhere
            return 2.0 * MathD.SafeAsin(radiusKm / distanceKm);
        }

        /// <summary>Apparent diameter in pixels, given the vertical field of view and framebuffer height.</summary>
        public static double ApparentDiameterPixels(double angularDiameterRad, double verticalFovRad, int verticalPixels)
        {
            if (verticalFovRad <= 0.0 || verticalPixels <= 0) return 0.0;
            return angularDiameterRad / verticalFovRad * verticalPixels;
        }

        /// <summary>
        /// Choose a representation from apparent size in pixels.
        ///
        /// Driving this from pixels rather than distance is what makes one rule work for a
        /// 2 km asteroid and a 140000 km gas giant, and what makes it adapt automatically to
        /// a headset's per-eye resolution instead of needing a separate tuning table.
        /// </summary>
        public static RepresentationLevel SelectRepresentation(double apparentDiameterPixels, bool insideBody = false)
        {
            if (insideBody) return RepresentationLevel.LocalDetail;
            if (apparentDiameterPixels < 1.5) return RepresentationLevel.Point;
            if (apparentDiameterPixels < 12.0) return RepresentationLevel.Impostor;
            if (apparentDiameterPixels < 180.0) return RepresentationLevel.Sphere;
            if (apparentDiameterPixels < 4000.0) return RepresentationLevel.Terrain;
            return RepresentationLevel.SurfaceTiles;
        }

        /// <summary>Convenience overload that works straight from physical quantities.</summary>
        public static RepresentationLevel SelectRepresentation(double radiusKm, double distanceKm,
            double verticalFovRad, int verticalPixels)
        {
            if (distanceKm <= radiusKm) return RepresentationLevel.LocalDetail;
            double angular = AngularDiameterRad(radiusKm, distanceKm);
            return SelectRepresentation(ApparentDiameterPixels(angular, verticalFovRad, verticalPixels));
        }

        /// <summary>
        /// Blend factor for cross-fading between two representations, so a planet does not pop
        /// from impostor to geometry. Returns 0 well inside the lower level and 1 well inside
        /// the upper, smoothly interpolating across a hysteresis band around the threshold.
        /// </summary>
        public static double TransitionBlend(double apparentDiameterPixels, double thresholdPixels,
            double bandWidthFraction = 0.25)
        {
            double half = thresholdPixels * bandWidthFraction;
            return MathD.SmoothStep(thresholdPixels - half, thresholdPixels + half, apparentDiameterPixels);
        }

        // ---- Floating origin -------------------------------------------------

        /// <summary>
        /// The mapping from absolute ICRF kilometres to the universe host's local metres:
        /// subtract the origin in double, swap the axes, then scale.
        ///
        /// There is no rebase. The camera stays in the room and the movers change the origin and
        /// the scale, so the subtraction is done in double before anything is cast to float and
        /// float error has nowhere to accumulate.
        /// </summary>
        public readonly struct RenderTransform
        {
            /// <summary>Absolute position, in km, that currently maps to the render origin.</summary>
            public Double3 OriginKm { get; }

            /// <summary>Render units emitted per kilometre.</summary>
            public double UnitsPerKm { get; }

            public RenderTransform(Double3 originKm, double unitsPerKm)
            {
                // Written as a positive test, not a negated one: NaN <= 0.0 is FALSE, so the
                // obvious form lets a NaN scale straight through and every ToRender after it
                // returns NaN with no exception anywhere. ScaleLimits.Clamp already tests NaN
                // explicitly; two guards on one quantity must not disagree about it.
                if (!(unitsPerKm > 0.0)) throw new ArgumentOutOfRangeException(nameof(unitsPerKm));
                OriginKm = originKm;
                UnitsPerKm = unitsPerKm;
            }

            /// <summary>
            /// The one ICRF-to-Unity axis mapping in the codebase: swap y and z.
            ///
            /// ICRF is right-handed with z toward the celestial pole; Unity is left-handed with y
            /// up. This permutation has determinant -1, which is exactly the right-to-left-handed
            /// change of basis — the identity that used to stand here IS the mirror, and a
            /// mirrored solar system orbits correctly and rotates backwards, which is easy to
            /// miss and very hard to explain later. P is symmetric and its own inverse, so the
            /// same matrix maps both ways.
            ///
            /// With the universe host at identity, Unity +y is ICRF +z (the celestial pole), +x
            /// is the vernal equinox and +z is ICRF +y. Nothing outside this struct and its Unity
            /// wrapper may reorder an axis; gate G0d counts the attempts.
            /// </summary>
            public static readonly Matrix3d AxisSwap = new Matrix3d(1, 0, 0, 0, 0, 1, 0, 1, 0);

            /// <summary>Absolute position to universe-local metres.</summary>
            public Double3 ToRender(Double3 absoluteKm) => AxisSwap * ((absoluteKm - OriginKm) * UnitsPerKm);

            /// <summary>Universe-local metres back to an absolute position.</summary>
            public Double3 ToAbsolute(Double3 renderPosition) =>
                AxisSwap * renderPosition / UnitsPerKm + OriginKm;

            /// <summary>An ICRF direction in universe-local axes. Unit length is preserved.</summary>
            public Double3 MapDirection(Double3 icrf) => AxisSwap * icrf;

            /// <summary>A universe-local direction back in ICRF axes.</summary>
            public Double3 UnmapDirection(Double3 render) => AxisSwap * render;

            /// <summary>
            /// A body-fixed-to-ICRF rotation in universe-local axes: P M P.
            ///
            /// Its determinant is (-1)(+1)(-1) = +1, so the result is a proper rotation and
            /// converts to a quaternion directly. Universe-local +y is the body's pole, which is
            /// what the sphere's UV pole assumes.
            /// </summary>
            public Matrix3d MapRotation(Matrix3d bodyToIcrf) => AxisSwap * bodyToIcrf * AxisSwap;

            /// <summary>Scale a length with no translation — for radii, offsets and velocities.</summary>
            public double ScaleLength(double km) => km * UnitsPerKm;

            public override string ToString() => $"origin {OriginKm.ToString("G6")} km at {UnitsPerKm:G6} units/km";
        }

        // ---- Navigation ------------------------------------------------------

        /// <summary>
        /// Free-flight speed appropriate to the user's surroundings, in km/s.
        ///
        /// Travel speed has to span walking pace to many light years per second, and no linear
        /// control can cover that. Tying speed to the distance to the nearest surface makes the
        /// control feel identical at every scale: the same stick deflection always crosses the
        /// visible scene in about the same number of seconds, whether the scene is a room or a
        /// galactic arm.
        ///
        /// The ceiling is 1e24 km/s because the ladder now reaches the cosmic microwave
        /// background: crossing the observable universe's 8.8e23 km in about four seconds needs
        /// 2.2e23 km/s. The old 1e14 was five decades short, which does not read as a clamp — it
        /// reads as a stick that has stopped working.
        /// </summary>
        public static double NavigationSpeed(double distanceToNearestSurfaceKm,
            double secondsToCrossScene = 4.0, double minimumKmPerSecond = 1e-6, double maximumKmPerSecond = 1e24)
        {
            if (double.IsNaN(distanceToNearestSurfaceKm) || distanceToNearestSurfaceKm <= 0.0)
                return minimumKmPerSecond;
            return MathD.Clamp(distanceToNearestSurfaceKm / secondsToCrossScene, minimumKmPerSecond, maximumKmPerSecond);
        }

        /// <summary>
        /// Map a logarithmic slider in [0,1] onto a speed between two bounds. Used for the
        /// manual speed control, where equal thumb movement should mean equal factors, not
        /// equal increments.
        /// </summary>
        public static double LogarithmicSpeed(double normalised, double minimumKmPerSecond, double maximumKmPerSecond)
        {
            double t = MathD.Clamp01(normalised);
            double logMin = MathD.SafeLog10(minimumKmPerSecond);
            double logMax = MathD.SafeLog10(maximumKmPerSecond);
            return System.Math.Pow(10.0, MathD.Lerp(logMin, logMax, t));
        }

        // ---- Presentation scaling -------------------------------------------

        /// <summary>
        /// Compress a set of radii toward each other so that wildly different bodies can be
        /// shown side by side and still both be visible.
        ///
        /// <paramref name="compression"/> of 1 is true relative scale; 0 makes everything the
        /// reference size. The intermediate values are a power law, which preserves ordering
        /// and relative ranking while pulling the extremes in — so a moon next to a gas giant
        /// stays visibly smaller without becoming a single pixel.
        ///
        /// The application must label which mode is active. A viewer who cannot tell whether
        /// they are looking at true proportions has been shown a diagram, not a measurement.
        /// </summary>
        public static double PresentationRadius(double actualRadiusKm, double referenceRadiusKm, double compression)
        {
            if (actualRadiusKm <= 0.0 || referenceRadiusKm <= 0.0) return actualRadiusKm;
            double t = MathD.Clamp01(compression);
            return referenceRadiusKm * System.Math.Pow(actualRadiusKm / referenceRadiusKm, t);
        }

        /// <summary>
        /// Scale factor that places a body of <paramref name="radiusKm"/> into a hologram of a
        /// given diameter in metres — the "hold Earth in your hands" transform.
        /// </summary>
        public static double HologramUnitsPerKm(double radiusKm, double hologramDiameterMetres)
        {
            if (radiusKm <= 0.0) return 1.0;
            return hologramDiameterMetres / (2.0 * radiusKm);
        }

        /// <summary>
        /// Number of orders of magnitude between two lengths. Used to label the scale ladder
        /// the user is climbing, and to decide how many intermediate shells to draw.
        /// </summary>
        public static double ScaleDecades(double fromKm, double toKm) =>
            MathD.SafeLog10(toKm) - MathD.SafeLog10(fromKm);
    }
}
