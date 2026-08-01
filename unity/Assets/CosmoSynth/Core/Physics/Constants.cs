using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Physical and astronomical constants.
    ///
    /// UNIT CONVENTION FOR THE WHOLE CORE: <b>kilometres and seconds</b>.
    /// Lengths are km, velocities km/s, gravitational parameters km^3/s^2, angles radians,
    /// times seconds past J2000 TDB. This matches what SPICE and JPL Horizons emit natively,
    /// so imported data needs no unit conversion and cannot pick up conversion bugs.
    ///
    /// Precision budget in km with a 64-bit double (~1e-16 relative):
    ///   Neptune's orbit   4.5e9 km  -> 0.45 mm
    ///   Nearest star      4.0e13 km -> 4 m
    ///   Galactic centre   2.5e17 km -> 25 km
    ///
    /// So a single double is sufficient out to interstellar range and marginal at galactic
    /// range; carrying a per-sector integer offset alongside it is the standard remedy and is
    /// not yet implemented, because nothing currently rendered needs it.
    ///
    /// Only the local render frame is ever converted to Unity float metres.
    /// </summary>
    public static class Constants
    {
        // ---- Fundamental ----------------------------------------------------

        /// <summary>Newtonian constant of gravitation, km^3 kg^-1 s^-2 (CODATA 2018: 6.67430e-11 m^3 kg^-1 s^-2).</summary>
        public const double G = 6.67430e-20;

        /// <summary>Speed of light in vacuum, km/s (exact by SI definition).</summary>
        public const double SpeedOfLight = 299792.458;

        /// <summary>Stefan-Boltzmann constant, W m^-2 K^-4 (exact by SI definition).</summary>
        public const double StefanBoltzmann = 5.670374419e-8;

        // ---- Length ---------------------------------------------------------

        /// <summary>Astronomical unit in km (IAU 2012 Resolution B2, exact).</summary>
        public const double AstronomicalUnit = 149597870.700;

        /// <summary>Light year in km (Julian year * c).</summary>
        public const double LightYear = 9.4607304725808e12;

        /// <summary>Parsec in km (IAU 2015: 648000/pi AU).</summary>
        public const double Parsec = 3.0856775814913673e13;

        public const double KiloParsec = 1000.0 * Parsec;
        public const double MegaParsec = 1.0e6 * Parsec;

        // ---- Time -----------------------------------------------------------

        /// <summary>Seconds in a day (exact, SI days as used by TDB).</summary>
        public const double SecondsPerDay = 86400.0;

        /// <summary>Days in a Julian year (exact by definition).</summary>
        public const double DaysPerJulianYear = 365.25;

        /// <summary>Seconds in a Julian year.</summary>
        public const double SecondsPerJulianYear = 365.25 * 86400.0;

        /// <summary>Julian date of the J2000.0 epoch (2000-01-01 12:00:00 TT).</summary>
        public const double J2000JulianDate = 2451545.0;

        /// <summary>Days in a Julian century.</summary>
        public const double DaysPerJulianCentury = 36525.0;

        // ---- Gravitational parameters, km^3/s^2 -----------------------------
        // Planetary values are SYSTEM GM (planet + its satellites) from JPL DE440/DE441
        // except where a body-only value is named. Using system GM is correct when the
        // orbit being propagated is the barycentre of that planetary system, which is what
        // Horizons reports for the major planets by default.

        public const double GM_Sun = 1.32712440041279419e11;
        public const double GM_Mercury = 2.2031868551e4;
        public const double GM_Venus = 3.24858592e5;
        public const double GM_Earth = 3.98600435507e5;
        public const double GM_Moon = 4.902800118e3;
        public const double GM_EarthMoonSystem = GM_Earth + GM_Moon;
        public const double GM_MarsSystem = 4.2828375816e4;
        public const double GM_JupiterSystem = 1.267127641e8;
        public const double GM_SaturnSystem = 3.79405848418e7;
        public const double GM_UranusSystem = 5.7945564e6;
        public const double GM_NeptuneSystem = 6.83652710058e6;
        public const double GM_PlutoSystem = 9.755e2;
        public const double GM_Ceres = 6.26325e1;
        public const double GM_Vesta = 1.728824e1;

        // ---- Masses, kg -----------------------------------------------------

        /// <summary>Solar mass, kg (derived from GM_Sun / G).</summary>
        public const double SolarMass = GM_Sun / G;

        /// <summary>Earth mass, kg (derived from GM_Earth / G).</summary>
        public const double EarthMass = GM_Earth / G;

        /// <summary>Jupiter mass, kg (derived from system GM / G).</summary>
        public const double JupiterMass = GM_JupiterSystem / G;

        // ---- Radii, km (IAU 2015 nominal / WGCCRE 2015 report) --------------

        public const double SolarRadius = 695700.0;
        public const double EarthEquatorialRadius = 6378.1366;
        public const double EarthPolarRadius = 6356.7519;
        public const double EarthMeanRadius = 6371.0084;
        public const double JupiterEquatorialRadius = 71492.0;
        public const double LunarRadius = 1737.4;

        // ---- Photometry -----------------------------------------------------

        /// <summary>Solar luminosity, W (IAU 2015 nominal).</summary>
        public const double SolarLuminosity = 3.828e26;

        /// <summary>Solar effective temperature, K (IAU 2015 nominal).</summary>
        public const double SolarEffectiveTemperature = 5772.0;

        /// <summary>Absolute bolometric magnitude zero point (IAU 2015 B2).</summary>
        public const double AbsoluteBolometricMagnitudeZeroPoint = 4.74;

        // ---- Derived helpers ------------------------------------------------

        /// <summary>Circular orbital speed (km/s) at radius <paramref name="radiusKm"/> around a body of parameter <paramref name="gm"/>.</summary>
        public static double CircularSpeed(double gm, double radiusKm) => System.Math.Sqrt(gm / radiusKm);

        /// <summary>Escape speed (km/s) at radius <paramref name="radiusKm"/>.</summary>
        public static double EscapeSpeed(double gm, double radiusKm) => System.Math.Sqrt(2.0 * gm / radiusKm);

        /// <summary>
        /// Sphere-of-influence radius (km) of a body orbiting a primary — the Laplace radius
        /// used to decide when to switch a spacecraft's reference frame during patched-conic flight.
        /// </summary>
        public static double SphereOfInfluence(double gmBody, double gmPrimary, double semiMajorAxisKm)
        {
            if (gmPrimary <= 0.0) return double.PositiveInfinity;
            return semiMajorAxisKm * System.Math.Pow(gmBody / gmPrimary, 0.4);
        }

        /// <summary>
        /// Hill sphere radius (km) — the region where the body's gravity dominates against
        /// tidal stripping by the primary. Slightly different from the SOI and the better
        /// choice for judging long-term moon stability.
        /// </summary>
        public static double HillRadius(double gmBody, double gmPrimary, double semiMajorAxisKm, double eccentricity = 0.0)
        {
            if (gmPrimary <= 0.0) return double.PositiveInfinity;
            return semiMajorAxisKm * (1.0 - eccentricity) * System.Math.Pow(gmBody / (3.0 * gmPrimary), 1.0 / 3.0);
        }

        /// <summary>
        /// Equilibrium (black-body) temperature in K for a body at <paramref name="distanceKm"/>
        /// from a star of <paramref name="luminosityWatts"/>, given a Bond albedo. Ignores
        /// greenhouse forcing and internal heat, so it is a lower bound for atmospheric worlds.
        /// </summary>
        public static double EquilibriumTemperature(double luminosityWatts, double distanceKm, double bondAlbedo = 0.3)
        {
            double distanceMetres = distanceKm * 1000.0;
            double flux = luminosityWatts / (16.0 * System.Math.PI * distanceMetres * distanceMetres);
            return System.Math.Pow(flux * (1.0 - bondAlbedo) / StefanBoltzmann, 0.25);
        }

        /// <summary>Surface gravity in m/s^2 given GM in km^3/s^2 and radius in km.</summary>
        public static double SurfaceGravity(double gm, double radiusKm) => gm / (radiusKm * radiusKm) * 1000.0;
    }
}
