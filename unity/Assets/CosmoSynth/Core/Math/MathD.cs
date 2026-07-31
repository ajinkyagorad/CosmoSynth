using System;

namespace CosmoSynth.Core
{
    /// <summary>Double-precision scalar helpers shared across the core.</summary>
    public static class MathD
    {
        public const double Pi = System.Math.PI;
        public const double TwoPi = 2.0 * System.Math.PI;
        public const double HalfPi = 0.5 * System.Math.PI;
        public const double Deg2Rad = System.Math.PI / 180.0;
        public const double Rad2Deg = 180.0 / System.Math.PI;

        public static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);

        public static double Clamp01(double v) => Clamp(v, 0.0, 1.0);

        public static double Lerp(double a, double b, double t) => a + (b - a) * t;

        public static double InverseLerp(double a, double b, double v)
        {
            if (a == b) return 0.0;
            return (v - a) / (b - a);
        }

        /// <summary>Smoothstep with clamped edges; used for LOD and scale-band cross-fades.</summary>
        public static double SmoothStep(double edge0, double edge1, double x)
        {
            double t = Clamp01(InverseLerp(edge0, edge1, x));
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>Wrap an angle into [0, 2pi).</summary>
        public static double WrapTwoPi(double radians)
        {
            double r = radians % TwoPi;
            if (r < 0.0) r += TwoPi;
            return r;
        }

        /// <summary>Wrap an angle into (-pi, pi].</summary>
        public static double WrapPi(double radians)
        {
            double r = WrapTwoPi(radians);
            if (r > Pi) r -= TwoPi;
            return r;
        }

        /// <summary>Shortest signed angular difference a-b, in (-pi, pi].</summary>
        public static double DeltaAngle(double a, double b) => WrapPi(a - b);

        /// <summary>acos that tolerates arguments pushed marginally outside [-1,1] by rounding.</summary>
        public static double SafeAcos(double x) => System.Math.Acos(Clamp(x, -1.0, 1.0));

        /// <summary>asin that tolerates arguments pushed marginally outside [-1,1] by rounding.</summary>
        public static double SafeAsin(double x) => System.Math.Asin(Clamp(x, -1.0, 1.0));

        public static double Sign(double x) => x < 0.0 ? -1.0 : (x > 0.0 ? 1.0 : 0.0);

        /// <summary>Base-10 log guarded against zero/negative input, for logarithmic scale mapping.</summary>
        public static double SafeLog10(double x, double floor = 1e-300) =>
            System.Math.Log10(System.Math.Max(x, floor));

        public static bool Approximately(double a, double b, double epsilon = 1e-12)
        {
            double diff = System.Math.Abs(a - b);
            if (diff <= epsilon) return true;
            double scale = System.Math.Max(System.Math.Abs(a), System.Math.Abs(b));
            return diff <= epsilon * scale;
        }
    }
}
