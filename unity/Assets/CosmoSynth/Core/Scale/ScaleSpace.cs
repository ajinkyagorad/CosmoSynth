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
    /// A rendering band. The universe cannot be drawn in one pass because no depth buffer
    /// spans metres to light years — at 32 bits of float depth, a single frustum reaching from
    /// a handrail to a nebula has no precision left for either. The standard answer, used by
    /// every seamless space renderer, is several passes: each band compresses its slice of the
    /// world by its own factor and draws with its own near/far planes, and the results are
    /// composited back to front.
    /// </summary>
    public readonly struct ScaleBand
    {
        public string Name { get; }

        /// <summary>Nearest absolute distance this band is responsible for, km.</summary>
        public double NearKm { get; }

        /// <summary>Farthest absolute distance this band is responsible for, km.</summary>
        public double FarKm { get; }

        /// <summary>
        /// Render units emitted per kilometre of real distance. Distant bands compress
        /// heavily so that a whole galaxy lands inside a workable frustum.
        /// </summary>
        public double RenderUnitsPerKm { get; }

        public ScaleBand(string name, double nearKm, double farKm, double renderUnitsPerKm)
        {
            if (farKm <= nearKm)
                throw new ArgumentException($"Band '{name}' must have FarKm > NearKm.", nameof(farKm));
            if (renderUnitsPerKm <= 0.0)
                throw new ArgumentException($"Band '{name}' must have a positive scale.", nameof(renderUnitsPerKm));

            Name = name;
            NearKm = nearKm;
            FarKm = farKm;
            RenderUnitsPerKm = renderUnitsPerKm;
        }

        public double NearPlaneRenderUnits => NearKm * RenderUnitsPerKm;
        public double FarPlaneRenderUnits => FarKm * RenderUnitsPerKm;

        /// <summary>
        /// Ratio of far to near plane. This is the number that actually governs depth
        /// precision, and keeping it under about 1e5 per band is what makes the scheme work.
        /// </summary>
        public double DepthRange => FarPlaneRenderUnits / NearPlaneRenderUnits;

        public bool Contains(double distanceKm) => distanceKm >= NearKm && distanceKm < FarKm;

        public override string ToString() =>
            $"{Name}: {NearKm:G3}-{FarKm:G3} km at {RenderUnitsPerKm:G3} units/km (depth range {DepthRange:G3})";
    }

    /// <summary>
    /// Scale-space arithmetic: choosing how to draw a body, compressing the universe into
    /// renderable bands, and keeping the camera near the origin.
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

        // ---- Bands -----------------------------------------------------------

        /// <summary>
        /// The default band stack, from a handrail to the observable universe.
        ///
        /// Each band's far/near ratio is kept near 1e4-1e5, which a reversed floating-point
        /// depth buffer handles comfortably. The scales are chosen so consecutive bands
        /// overlap slightly in world space, giving the compositor somewhere to cross-fade.
        /// </summary>
        public static IReadOnlyList<ScaleBand> DefaultBands { get; } = new[]
        {
            // 1 render unit = 1 metre here: this is the band the XR rig lives in, and hand
            // tracking is only meaningful at true scale.
            new ScaleBand("Local",          1e-4,   1e2,   1000.0),
            new ScaleBand("Orbital",        1e1,    1e6,   1.0),
            new ScaleBand("Interplanetary", 1e5,    1e11,  1e-5),
            new ScaleBand("Interstellar",   1e10,   1e16,  1e-10),
            new ScaleBand("Galactic",       1e15,   1e22,  1e-16),
        };

        /// <summary>Pick the band that should own an object at the given distance.</summary>
        public static ScaleBand SelectBand(double distanceKm, IReadOnlyList<ScaleBand> bands = null)
        {
            bands = bands ?? DefaultBands;
            for (int i = 0; i < bands.Count; i++)
            {
                if (bands[i].Contains(distanceKm)) return bands[i];
            }
            return distanceKm < bands[0].NearKm ? bands[0] : bands[bands.Count - 1];
        }

        // ---- Floating origin -------------------------------------------------

        /// <summary>
        /// Distance from the origin, in render units, past which the origin should be rebased.
        ///
        /// A 32-bit float holds about 7 significant decimal digits, so at 10 km from the
        /// origin the spacing between representable positions is already around a millimetre —
        /// enough to make a hand-held hologram visibly jitter. Rebasing well before that keeps
        /// everything the user can actually see in the precise part of the float range.
        /// </summary>
        public const double DefaultRebaseThresholdRenderUnits = 4000.0;

        /// <summary>
        /// The current mapping from absolute universe coordinates to renderable coordinates:
        /// subtract the origin, then scale.
        /// </summary>
        public readonly struct RenderTransform
        {
            /// <summary>Absolute position, in km, that currently maps to the render origin.</summary>
            public Double3 OriginKm { get; }

            /// <summary>Render units emitted per kilometre.</summary>
            public double UnitsPerKm { get; }

            public RenderTransform(Double3 originKm, double unitsPerKm)
            {
                if (unitsPerKm <= 0.0) throw new ArgumentOutOfRangeException(nameof(unitsPerKm));
                OriginKm = originKm;
                UnitsPerKm = unitsPerKm;
            }

            /// <summary>Absolute position to render position.</summary>
            public Double3 ToRender(Double3 absoluteKm) => (absoluteKm - OriginKm) * UnitsPerKm;

            /// <summary>Render position back to absolute position.</summary>
            public Double3 ToAbsolute(Double3 renderPosition) => renderPosition / UnitsPerKm + OriginKm;

            /// <summary>Scale a length with no translation — for radii, offsets and velocities.</summary>
            public double ScaleLength(double km) => km * UnitsPerKm;

            /// <summary>Move the origin, returning the shift the scene graph must be offset by.</summary>
            public RenderTransform Rebased(Double3 newOriginKm) => new RenderTransform(newOriginKm, UnitsPerKm);

            public override string ToString() => $"origin {OriginKm.ToString("G6")} km at {UnitsPerKm:G6} units/km";
        }

        /// <summary>
        /// Whether the origin should be rebased, given where the camera has drifted to.
        /// </summary>
        public static bool ShouldRebase(Double3 cameraRenderPosition,
            double thresholdRenderUnits = DefaultRebaseThresholdRenderUnits) =>
            cameraRenderPosition.SqrMagnitude > thresholdRenderUnits * thresholdRenderUnits;

        // ---- Navigation ------------------------------------------------------

        /// <summary>
        /// Free-flight speed appropriate to the user's surroundings, in km/s.
        ///
        /// Travel speed has to span walking pace to many light years per second, and no linear
        /// control can cover that. Tying speed to the distance to the nearest surface makes the
        /// control feel identical at every scale: the same stick deflection always crosses the
        /// visible scene in about the same number of seconds, whether the scene is a room or a
        /// galactic arm.
        /// </summary>
        public static double NavigationSpeed(double distanceToNearestSurfaceKm,
            double secondsToCrossScene = 4.0, double minimumKmPerSecond = 1e-6, double maximumKmPerSecond = 1e14)
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
