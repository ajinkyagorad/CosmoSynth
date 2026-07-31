using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace CosmoSynth.Tests
{
    /// <summary>Marks a static, parameterless method as a test case.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute
    {
        public string Description { get; }
        public TestAttribute(string description = null) { Description = description; }
    }

    /// <summary>
    /// Marks a test that reaches the network. Skipped unless COSMOSYNTH_NETWORK_TESTS=1,
    /// so an offline run still reports a clean pass rather than a misleading failure.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class NetworkTestAttribute : Attribute { }

    public sealed class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    /// <summary>Assertions phrased for numerical work: everything reports the actual margin by which it failed.</summary>
    public static class Assert
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new AssertionException(message);
        }

        public static void False(bool condition, string message) => True(!condition, message);

        public static void Equal(double expected, double actual, double tolerance, string what)
        {
            double diff = System.Math.Abs(expected - actual);
            if (double.IsNaN(actual) || diff > tolerance)
            {
                throw new AssertionException(
                    $"{what}: expected {expected:G17}, got {actual:G17} (|diff| {diff:G6} > tol {tolerance:G6})");
            }
        }

        /// <summary>Relative comparison, for quantities whose magnitude spans many orders (distances, GMs).</summary>
        public static void Relative(double expected, double actual, double relativeTolerance, string what)
        {
            double scale = System.Math.Max(System.Math.Abs(expected), 1e-300);
            double relative = System.Math.Abs(expected - actual) / scale;
            if (double.IsNaN(actual) || relative > relativeTolerance)
            {
                throw new AssertionException(
                    $"{what}: expected {expected:G17}, got {actual:G17} (relative {relative:G6} > tol {relativeTolerance:G6})");
            }
        }

        /// <summary>Angles compared on the circle, so 359.9999 deg and -0.0001 deg agree.</summary>
        public static void Angle(double expectedRad, double actualRad, double toleranceRad, string what)
        {
            double diff = System.Math.Abs(CosmoSynth.Core.MathD.WrapPi(expectedRad - actualRad));
            if (double.IsNaN(actualRad) || diff > toleranceRad)
            {
                throw new AssertionException(
                    $"{what}: expected {expectedRad * CosmoSynth.Core.MathD.Rad2Deg:F9} deg, " +
                    $"got {actualRad * CosmoSynth.Core.MathD.Rad2Deg:F9} deg " +
                    $"(|diff| {diff * CosmoSynth.Core.MathD.Rad2Deg:G6} deg > tol {toleranceRad * CosmoSynth.Core.MathD.Rad2Deg:G6} deg)");
            }
        }

        public static void VectorClose(CosmoSynth.Core.Double3 expected, CosmoSynth.Core.Double3 actual,
            double tolerance, string what)
        {
            double diff = (expected - actual).Magnitude;
            if (!actual.IsFinite || diff > tolerance)
            {
                throw new AssertionException(
                    $"{what}: expected {expected.ToString("G12")}, got {actual.ToString("G12")} " +
                    $"(|diff| {diff:G6} > tol {tolerance:G6})");
            }
        }

        public static void Throws<TException>(Action action, string what) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception e)
            {
                throw new AssertionException($"{what}: expected {typeof(TException).Name}, got {e.GetType().Name}");
            }
            throw new AssertionException($"{what}: expected {typeof(TException).Name}, but nothing was thrown");
        }
    }

    public static class TestRunner
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            string filter = args.FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));
            bool networkEnabled = Environment.GetEnvironmentVariable("COSMOSYNTH_NETWORK_TESTS") == "1";
            bool verbose = args.Contains("-v") || args.Contains("--verbose");

            var methods = Assembly.GetExecutingAssembly()
                .GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.GetCustomAttribute<TestAttribute>() != null)
                .OrderBy(m => m.DeclaringType.Name, StringComparer.Ordinal)
                .ThenBy(m => m.Name, StringComparer.Ordinal)
                .ToList();

            if (!string.IsNullOrEmpty(filter))
            {
                methods = methods
                    .Where(m => (m.DeclaringType.Name + "." + m.Name)
                        .IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            int passed = 0, failed = 0, skipped = 0;
            var failures = new List<string>();
            string currentGroup = null;
            var stopwatch = Stopwatch.StartNew();

            foreach (var method in methods)
            {
                if (currentGroup != method.DeclaringType.Name)
                {
                    currentGroup = method.DeclaringType.Name;
                    Console.WriteLine();
                    Console.WriteLine($"  {currentGroup}");
                }

                var attribute = method.GetCustomAttribute<TestAttribute>();
                string label = attribute.Description ?? Humanise(method.Name);

                if (method.GetCustomAttribute<NetworkTestAttribute>() != null && !networkEnabled)
                {
                    skipped++;
                    Console.WriteLine($"    - {label}  (skipped: set COSMOSYNTH_NETWORK_TESTS=1)");
                    continue;
                }

                var methodTimer = Stopwatch.StartNew();
                try
                {
                    method.Invoke(null, null);
                    methodTimer.Stop();
                    passed++;
                    string timing = methodTimer.ElapsedMilliseconds > 50
                        ? $"  ({methodTimer.ElapsedMilliseconds} ms)"
                        : string.Empty;
                    Console.WriteLine($"    PASS {label}{timing}");
                }
                catch (TargetInvocationException tie)
                {
                    methodTimer.Stop();
                    failed++;
                    var inner = tie.InnerException ?? tie;
                    Console.WriteLine($"    FAIL {label}");
                    Console.WriteLine($"         {inner.Message}");
                    if (verbose && !(inner is AssertionException))
                        Console.WriteLine(Indent(inner.StackTrace, "         "));
                    failures.Add($"{currentGroup}.{method.Name}: {inner.Message}");
                }
            }

            stopwatch.Stop();

            Console.WriteLine();
            Console.WriteLine(new string('-', 72));
            Console.WriteLine($"  {passed} passed, {failed} failed, {skipped} skipped   ({stopwatch.ElapsedMilliseconds} ms)");

            if (failures.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Failures:");
                foreach (var f in failures) Console.WriteLine($"    * {f}");
            }
            Console.WriteLine();

            return failed == 0 ? 0 : 1;
        }

        /// <summary>Turn <c>PropagatingEarthMatchesHorizons</c> into <c>Propagating earth matches horizons</c>.</summary>
        private static string Humanise(string name)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                {
                    sb.Append(' ');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string Indent(string text, string prefix)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return string.Join("\n", text.Split('\n').Select(l => prefix + l.TrimEnd()));
        }
    }
}
