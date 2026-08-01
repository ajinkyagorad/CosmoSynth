using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Turns a catalogued star into a whole star system, deterministically.
    ///
    /// Phase 2 of docs/roadmap-universe.md, and the property that makes a universe rather than a
    /// map: approaching any of the 119 613 real stars finds worlds around it, generated from a
    /// seed derived from the star itself, identical on every machine and every run.
    ///
    /// **The honesty problem is the whole design.** A real star with invented planets is the
    /// easiest way for this project to start lying, because the star's reality lends the planets
    /// a credibility they have not earned. Three rules keep them apart:
    ///
    /// 1. The star is **not** regenerated. Its position, magnitude and temperature come from the
    ///    catalogue, and the generator is handed the mass those imply rather than drawing one, so
    ///    the planets orbit the star that is actually there.
    /// 2. Every generated field is <see cref="ProvenanceClass.Procedural"/>, naming the seed. The
    ///    star's own fields keep the class the catalogue gave them.
    /// 3. Nothing is persisted. A generated system is a pure function of the star, so it is
    ///    rebuilt on demand rather than stored — which also means it can never drift out of step
    ///    with the catalogue it was derived from.
    ///
    /// The seed comes from the star's **position**, not its index. Index is an artefact of the
    /// file's sort order and would change every time the catalogue was rebuilt, silently giving
    /// every star in the sky a different set of planets. Position is a measured fact about the
    /// star and does not move.
    /// </summary>
    public static class StarSystemFactory
    {
        /// <summary>Absolute magnitude of the Sun, for the luminosity conversion.</summary>
        private const double SolarAbsoluteMagnitude = 4.83;

        /// <summary>
        /// A stable seed for a star, from its measured position.
        ///
        /// Quantised to a thousandth of a parsec before hashing. Without that, a rebuild of the
        /// catalogue that changed a position in the seventh decimal — a revised parallax, a
        /// different source release — would reseed the star and replace its worlds. A thousandth
        /// of a parsec is thirty times the Earth-Sun distance: far finer than any astrometric
        /// revision, far coarser than floating-point noise.
        /// </summary>
        public static long SeedFor(in CatalogStar star)
        {
            long x = (long)System.Math.Round(star.PositionParsecs.x * 1000.0);
            long y = (long)System.Math.Round(star.PositionParsecs.y * 1000.0);
            long z = (long)System.Math.Round(star.PositionParsecs.z * 1000.0);

            ulong hash = Hash.Hash3(0, x, y, z);

            // Keep it positive: the seed is printed into provenance strings and a negative one
            // reads like a bug even though it is not.
            return (long)(hash & 0x7FFFFFFFFFFFFFFFUL);
        }

        /// <summary>
        /// Luminosity in solar units, or null when the distance is unknown.
        ///
        /// Apparent magnitude alone says nothing about a star's output — a dim nearby red dwarf
        /// and a brilliant distant supergiant can share one. Without a parallax there is no
        /// luminosity, and the mass has to come from the temperature instead.
        /// </summary>
        public static double? LuminositySolar(in CatalogStar star)
        {
            double? distance = star.DistanceParsecs;
            if (!distance.HasValue || distance.Value <= 0.0) return null;

            double absolute = star.ApparentMagnitude - 5.0 * (System.Math.Log10(distance.Value) - 1.0);
            return System.Math.Pow(10.0, (SolarAbsoluteMagnitude - absolute) / 2.5);
        }

        /// <summary>
        /// Mass in solar masses implied by what is known about the star.
        ///
        /// From luminosity where a parallax exists, by inverting the same main-sequence relation
        /// the generator uses — inverted numerically rather than with a second formula, so the
        /// two cannot disagree. Otherwise from temperature, through T ∝ M^0.475, which follows
        /// from the usual main-sequence exponents (L ∝ M^3.5, R ∝ M^0.8).
        ///
        /// **This is wrong for giants**, which are luminous because they are large and old rather
        /// than massive, and HYG's bright end is full of them. The consequence is a generated
        /// system whose spacing suits a star more massive than the real one. It is recorded as
        /// Procedural like everything else generated, and it is the reason the planets are never
        /// presented as a claim about that star.
        /// </summary>
        public static double MassSolar(in CatalogStar star)
        {
            const double minimum = 0.08, maximum = 3.0;   // the generator's own clamp range

            double? luminosity = LuminositySolar(star);
            if (luminosity.HasValue && luminosity.Value > 0.0)
            {
                double low = minimum, high = maximum;
                for (int i = 0; i < 48; i++)
                {
                    double middle = 0.5 * (low + high);
                    if (PlanetGenerator.MainSequenceLuminosity(middle) < luminosity.Value) low = middle;
                    else high = middle;
                }
                return MathD.Clamp(0.5 * (low + high), minimum, maximum);
            }

            double fromTemperature = System.Math.Pow(
                System.Math.Max(star.TemperatureK, 1.0) / Constants.SolarEffectiveTemperature, 1.0 / 0.475);
            return MathD.Clamp(fromTemperature, minimum, maximum);
        }

        /// <summary>
        /// Build the system around a catalogued star.
        ///
        /// <paramref name="starId"/> must be stable and unique across the catalogue — the
        /// runtime merges these into the live catalogue, where a collision would silently drop
        /// the system.
        /// </summary>
        public static CelestialCatalog Create(in CatalogStar star, string starId, string starName,
            CosmicTime epoch)
        {
            if (string.IsNullOrWhiteSpace(starId)) throw new ArgumentException("starId is required.");

            long seed = SeedFor(star);
            double massSolar = MassSolar(star);

            CelestialCatalog catalog = PlanetGenerator.GenerateSystem(new PlanetGenerator.SystemSettings
            {
                Seed = seed,
                Name = starName ?? starId,
                StarMassSolar = massSolar,
                Epoch = epoch,
            });

            // The generator made its own star. Replace its identity and position with the real
            // one's, keeping the mass the planets were actually built around — the orbits carry
            // that GM and rewriting it here would leave every period wrong.
            // Snapshot before mutating: the catalogue keys its bodies in a dictionary, and both
            // the re-identification below and the re-parenting alter what an iterator would be
            // walking.
            var bodies = new System.Collections.Generic.List<CelestialBody>(catalog.Bodies);

            CelestialBody generatedStar = null;
            foreach (CelestialBody body in bodies)
            {
                if (body.IsStar) { generatedStar = body; break; }
            }
            if (generatedStar == null) return catalog;

            string generatedId = generatedStar.Id;

            generatedStar.Id = starId;
            generatedStar.Name = starName ?? starId;
            generatedStar.PositionKm = star.PositionParsecs * Constants.Parsec;
            generatedStar.EffectiveTemperatureK = star.TemperatureK;

            double? luminosity = LuminositySolar(star);
            if (luminosity.HasValue)
                generatedStar.LuminosityWatts = luminosity.Value * Constants.SolarLuminosity;

            generatedStar.DisplayColor = PlanetGenerator.BlackBodyColor(star.TemperatureK);

            // What is known about this star, and what is not, per field.
            var provenance = new ProvenanceRecord();
            provenance.Set("positionKm", FieldProvenance.Measured(
                "HYG star database v4.4 (Hipparcos/Yale/Gliese), CC BY-SA 4.0",
                "https://codeberg.org/astronexus/hyg"));
            provenance.Set("effectiveTemperatureK", FieldProvenance.Inferred(
                "B-V colour index via Ballesteros (2012), EPL 97 34008"));
            provenance.Set("luminosityWatts", luminosity.HasValue
                ? FieldProvenance.Inferred("apparent magnitude and HYG parallax")
                : new FieldProvenance(ProvenanceClass.Unknown, "no usable parallax"));
            provenance.Set("gravitationalParameter", FieldProvenance.Inferred(
                "main-sequence mass-luminosity relation; wrong for giants"));
            generatedStar.Provenance = provenance;

            // Re-parent the planets, which still name the id the generator gave the star.
            foreach (CelestialBody body in bodies)
            {
                if (ReferenceEquals(body, generatedStar)) continue;
                if (string.Equals(body.ParentId, generatedId, StringComparison.Ordinal))
                    body.ParentId = starId;
            }

            // Re-key the star. Setting Id on the object is not enough: the catalogue stores
            // bodies in a dictionary keyed by the id they had when they were added, and Rebuild
            // only refreshes the parent/child index. Without this the star answers to its old
            // generated id and is invisible under its real one — which the runtime looks it up
            // by, so the system would merge in and then not be findable.
            catalog.Remove(generatedId);
            catalog.Add(generatedStar);

            catalog.Name = $"{starName ?? starId} (generated)";
            catalog.Source = $"CosmoSynth procedural generator, seed {seed} from the star's " +
                             "measured position. Planets, moons and surfaces are invented.";
            catalog.Rebuild();
            return catalog;
        }
    }
}
