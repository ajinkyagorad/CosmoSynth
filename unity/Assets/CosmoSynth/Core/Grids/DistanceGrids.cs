using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>Which distance regime a grid belongs to. CONTRACT §5.4 <c>cylGrids</c>.</summary>
    public enum GridRegime : byte { Stellar, Galactic, LocalGroup }

    /// <summary>One ring of a distance grid, with everything needed to label it.</summary>
    public readonly struct GridRing
    {
        public readonly double RadiusKm;
        public readonly string DistanceLabel;

        /// <summary>Light-travel time across the radius, years. This is the honest second reading.</summary>
        public readonly double LightYears;

        public GridRing(double radiusKm, string distanceLabel, double lightYears)
        {
            RadiusKm = radiusKm;
            DistanceLabel = distanceLabel;
            LightYears = lightYears;
        }
    }

    /// <summary>
    /// The three cylindrical distance grids, as radii and labels. CONTRACT §5.4, Atlas MWA-25.
    ///
    /// A grid is the only honest way to answer "how far is that" in a scene with no horizon and
    /// no familiar objects. Each regime is centred on something the viewer can name — the Sun,
    /// the Galactic centre, the Local Group — and each ring carries BOTH a distance and a
    /// light-travel time, because at these scales the second number is the interesting one: the
    /// edge of the 60 kpc galactic grid is 196,000 years of light, which is a fact about looking
    /// rather than a fact about geometry.
    ///
    /// <b>Legibility, not scale, decides whether a ring is drawn.</b> A ring smaller than 8 cm in
    /// the room is a blob on the centre and a ring larger than 3 m is a band across the whole
    /// view, and both are worse than nothing. That is the same rule the orbit lines already
    /// follow, and it is why this is arithmetic in Core rather than a magic number in a shader.
    /// </summary>
    public static class DistanceGrids
    {
        /// <summary>A ring below this room radius is a blob on the centre.</summary>
        public const double MinRoomMetres = 0.08;

        /// <summary>A ring above this room radius is a band across the view.</summary>
        public const double MaxRoomMetres = 3.0;

        /// <summary>Kilometres in a light year (IAU).</summary>
        public const double KmPerLightYear = 9.4607304725808e12;

        /// <summary>Segments used to draw one ring. 128 keeps a 3 m circle under a pixel of chord error.</summary>
        public const int RingSegments = 128;

        /// <summary>Radial spokes per regime, for reading azimuth off the grid.</summary>
        public const int Spokes = 12;

        private const double Pc = GalaxyModel.KmPerParsec;
        private const double Kpc = GalaxyModel.KmPerKiloparsec;
        private const double Mpc = Kpc * 1000.0;

        /// <summary>
        /// Ring radii for a regime, in kilometres, with their labels.
        ///
        /// Stellar: 10 / 25 / 50 / 100 / 250 / 500 pc — the solar neighbourhood, out to where the
        /// dust map ends. Galactic: 2 / 5 / 8.15 / 10 / 15 / 20 kpc — the last of those is beyond
        /// the stellar disc, and 8.15 is the Sun's own orbit, which is why it is not a round
        /// number. Local Group: 0.5 / 1 / 2 / 5 / 10 Mpc.
        /// </summary>
        public static IReadOnlyList<GridRing> Rings(GridRegime regime)
        {
            switch (regime)
            {
                case GridRegime.Stellar:
                    return new[]
                    {
                        Ring(10 * Pc, "10 pc"), Ring(25 * Pc, "25 pc"), Ring(50 * Pc, "50 pc"),
                        Ring(100 * Pc, "100 pc"), Ring(250 * Pc, "250 pc"), Ring(500 * Pc, "500 pc"),
                    };
                case GridRegime.Galactic:
                    return new[]
                    {
                        Ring(2 * Kpc, "2 kpc"), Ring(5 * Kpc, "5 kpc"),
                        Ring(GalaxyModel.R0Kpc * Kpc, "8.15 kpc (the Sun)"),
                        Ring(10 * Kpc, "10 kpc"), Ring(15 * Kpc, "15 kpc"), Ring(20 * Kpc, "20 kpc"),
                    };
                default:
                    return new[]
                    {
                        Ring(0.5 * Mpc, "0.5 Mpc"), Ring(1 * Mpc, "1 Mpc"), Ring(2 * Mpc, "2 Mpc"),
                        Ring(5 * Mpc, "5 Mpc"), Ring(10 * Mpc, "10 Mpc"),
                    };
            }
        }

        private static GridRing Ring(double km, string label) =>
            new GridRing(km, label, km / KmPerLightYear);

        /// <summary>
        /// Is this ring worth drawing at this scale? Both bounds, never just the lower one — the
        /// project has already shipped a minimum with no maximum and put a lander the size of a
        /// planet in front of the camera.
        /// </summary>
        public static bool IsLegible(double radiusKm, double unitsPerKm)
        {
            double room = radiusKm * unitsPerKm;
            return room >= MinRoomMetres && room <= MaxRoomMetres;
        }

        /// <summary>How many rings of a regime are legible at this scale. 0 means draw nothing.</summary>
        public static int LegibleCount(GridRegime regime, double unitsPerKm)
        {
            IReadOnlyList<GridRing> rings = Rings(regime);
            int n = 0;
            for (int i = 0; i < rings.Count; i++) if (IsLegible(rings[i].RadiusKm, unitsPerKm)) n++;
            return n;
        }

        /// <summary>
        /// The regime whose rings are most legible at this scale, or null when none is. This is
        /// what replaces "which grid is on" as a user setting: the right grid for a scale is a
        /// fact about the scale.
        /// </summary>
        public static GridRegime? Best(double unitsPerKm)
        {
            GridRegime? best = null;
            int bestCount = 0;
            foreach (GridRegime regime in new[] { GridRegime.Stellar, GridRegime.Galactic, GridRegime.LocalGroup })
            {
                int n = LegibleCount(regime, unitsPerKm);
                if (n > bestCount) { bestCount = n; best = regime; }
            }
            return best;
        }

        /// <summary>The centre a regime's rings are drawn about, in absolute ICRF kilometres.</summary>
        public static Double3 CentreKm(GridRegime regime) => regime switch
        {
            GridRegime.Stellar => Double3.Zero,                       // the Sun
            GridRegime.Galactic => GalaxyModel.GalacticCentreIcrfKm,  // Sgr A*
            _ => Double3.Zero,                                        // the Local Group: the Sun, honestly
        };

        /// <summary>
        /// The plane normal a regime's rings lie in, as an ICRF unit vector. Grids keep their own
        /// native frame rather than being resampled into a common one (CONTRACT §3.4): a galactic
        /// grid rotated into supergalactic coordinates measures nothing anybody published.
        /// </summary>
        public static Double3 PlaneNormalIcrf(GridRegime regime) => regime switch
        {
            GridRegime.LocalGroup => StandardFrames.SupergalacticToEquatorial * Double3.UnitZ,
            _ => StandardFrames.GalacticToEquatorial * Double3.UnitZ,
        };
    }
}
