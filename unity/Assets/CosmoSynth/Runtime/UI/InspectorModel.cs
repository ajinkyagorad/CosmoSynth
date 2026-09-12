using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Runtime
{
    /// <summary>One line of the inspector: a label, a value, and where the value came from.</summary>
    public readonly struct InspectorField
    {
        public readonly string Label;
        public readonly string Value;
        public readonly ProvenanceClass Provenance;
        public readonly string Source;

        public InspectorField(string label, string value, ProvenanceClass provenance, string source = null)
        {
            Label = label;
            Value = value;
            Provenance = provenance;
            Source = source;
        }

        public char Glyph => CosmoTheme.ProvenanceGlyph(Provenance);
    }

    /// <summary>
    /// The numbers for whatever is selected, right now, each with its own provenance.
    ///
    /// <b>Per field, not per body.</b> A body whose radius is measured and whose eccentricity was
    /// assumed is not a measured body, and a single glyph on the card says it is. The card shows
    /// three lines and the weakest glyph; INFO -&gt; INSPECTOR shows every line with its own.
    ///
    /// A plain class, rebuilt only when the selection or a displayed value changes and at most at
    /// 2 Hz: these are strings, and formatting a dozen of them every frame for a panel nobody is
    /// reading is how a comfortable frame budget disappears into <c>string.Format</c>.
    /// </summary>
    public sealed class InspectorModel
    {
        /// <summary>The selection's name.</summary>
        public string Name { get; private set; }

        /// <summary>"Planet", "Spacecraft", "Star"...</summary>
        public string Kind { get; private set; }

        /// <summary>Parent or host, for the card's second line.</summary>
        public string Host { get; private set; }

        /// <summary>The weakest per-field class over everything shown, which is the card's glyph.</summary>
        public ProvenanceClass Weakest { get; private set; } = ProvenanceClass.Unknown;

        /// <summary>The three-or-so NOW lines: what is true at the simulation instant.</summary>
        public List<InspectorField> Now { get; } = new List<InspectorField>(8);

        /// <summary>The standing facts, with their sources.</summary>
        public List<InspectorField> Facts { get; } = new List<InspectorField>(16);

        /// <summary>A note the card shows instead of numbers, such as an ephemeris window.</summary>
        public string Notice { get; private set; }

        /// <summary>How often the model is rebuilt at most.</summary>
        public const double RefreshSeconds = 0.5;

        /// <summary>
        /// Build the inspector for a body.
        /// </summary>
        /// <param name="viewerKm">The viewer's absolute position, for the "from you" lines.</param>
        public static InspectorModel For(UniverseSimulation simulation, CelestialBodyView view,
            Double3 viewerKm, ViewOptions options)
        {
            var model = new InspectorModel();
            if (view == null || view.Body == null) { model.Name = "Nothing selected"; return model; }

            CelestialBody body = view.Body;
            model.Name = body.Name;
            model.Kind = body.Type.ToString();
            model.Host = string.IsNullOrEmpty(body.ParentId) ? null : body.ParentId;

            if (!view.IsKnown)
            {
                model.Notice = body.KnownFrom.HasValue && body.KnownUntil.HasValue
                    ? "outside its known window (" + body.KnownFrom.Value.ToUtc().Year + "-" +
                      body.KnownUntil.Value.ToUtc().Year + ")"
                    : "outside its known window";
            }

            Double3 position = view.AbsolutePositionKm;

            // ---- NOW: what is true at this instant ---------------------------

            double fromViewer = (position - viewerKm).Magnitude;
            model.Add(model.Now, "From you", Format.Distance(fromViewer), ProvenanceClass.Measured, null);

            double radiusKm = body.MeanRadiusKm ?? body.EquatorialRadiusKm ?? 0.0;
            if (radiusKm > 0.0 && fromViewer > 0.0)
            {
                model.Add(model.Now, "Angular size",
                    Format.Angle(ScaleSpace.AngularDiameterRad(radiusKm, fromViewer)),
                    ProvenanceClass.Measured, null);
            }

            if (view.SizeExaggeration > SizePresets.HonestyThreshold)
            {
                model.Add(model.Now, "Drawn at",
                    Format.Multiplier(view.SizeExaggeration) + " life size",
                    ProvenanceClass.Procedural, "presentation, not data");
            }

            Double3 sunKm = Position(simulation, "sun", out bool haveSun);
            Double3 earthKm = Position(simulation, "earth", out bool haveEarth);

            if (haveSun && body.Id != "sun")
            {
                double fromSun = (position - sunKm).Magnitude;
                model.Add(model.Now, "From the Sun",
                    Format.Distance(fromSun, DistanceUnit.AstronomicalUnits) + " · " + Format.LightTime(fromSun),
                    Class(body, "position"), Source(body, "position"));
            }

            if (haveEarth && body.Id != "earth")
            {
                double fromEarth = (position - earthKm).Magnitude;
                model.Add(model.Now, "From Earth",
                    Format.Distance(fromEarth) + " · " + Format.LightTime(fromEarth),
                    Class(body, "position"), Source(body, "position"));
            }

            double speed = view.AbsoluteVelocityKmPerSec.Magnitude;
            if (speed > 0.0)
            {
                model.Add(model.Now, "Speed", Format.Speed(speed) + " (barycentric)",
                    Class(body, "velocity"), Source(body, "velocity"));
            }

            if (haveSun && haveEarth && body.Id != "earth" && body.Id != "sun")
            {
                Double3 toSun = (sunKm - position).Normalized;
                Double3 toEarth = (earthKm - position).Normalized;
                model.Add(model.Now, "Phase angle",
                    Format.Angle(MathD.SafeAcos(Double3.Dot(toSun, toEarth))),
                    ProvenanceClass.Inferred, "Sun-body-Earth, from the ephemerides");
            }

            // ---- Facts -------------------------------------------------------

            if (body.GravitationalParameter > 0.0)
                model.Add(model.Facts, "Mass", Format.Significant(body.MassKg) + " kg",
                    Class(body, "mass"), Source(body, "mass"));

            if (body.EquatorialRadiusKm.HasValue)
                model.Add(model.Facts, "Equatorial radius", Format.Significant(body.EquatorialRadiusKm.Value) + " km",
                    Class(body, "radius"), Source(body, "radius"));

            if (body.SurfaceGravity.HasValue)
                model.Add(model.Facts, "Surface gravity",
                    Format.Significant(body.SurfaceGravity.Value * 1000.0) + " m/s2",
                    Class(body, "radius"), Source(body, "radius"));

            if (body.DensityKgPerM3.HasValue)
                model.Add(model.Facts, "Density", Format.Significant(body.DensityKgPerM3.Value) + " kg/m3",
                    Class(body, "mass"), Source(body, "mass"));

            if (body.Orbit.HasValue)
            {
                OrbitalElements orbit = body.Orbit.Value;
                model.Add(model.Facts, "Semi-major axis",
                    Format.Distance(orbit.SemiMajorAxisKm, DistanceUnit.AstronomicalUnits),
                    Class(body, "orbit"), Source(body, "orbit"));
                model.Add(model.Facts, "Eccentricity", Format.Significant(orbit.Eccentricity, 4),
                    Class(body, "orbit"), Source(body, "orbit"));
                model.Add(model.Facts, "Inclination", Format.Angle(orbit.InclinationRad),
                    Class(body, "orbit"), Source(body, "orbit"));
                if (orbit.IsClosed)
                    model.Add(model.Facts, "Period", Format.Duration(orbit.PeriodSeconds),
                        Class(body, "orbit"), Source(body, "orbit"));
            }

            if (body.Orientation.HasValue && body.Orientation.Value.RotationRateDegPerDay != 0.0)
            {
                model.Add(model.Facts, "Day length",
                    Format.Duration(System.Math.Abs(body.Orientation.Value.SiderealPeriodSeconds)) +
                    (body.Orientation.Value.IsRetrograde ? " (retrograde)" : string.Empty),
                    ProvenanceClass.Measured, "IAU rotational elements");
            }

            if (body.Atmosphere != null && body.Atmosphere.IsPresent)
            {
                model.Add(model.Facts, "Atmosphere",
                    Format.Significant(body.Atmosphere.SurfacePressureBar) + " bar at the surface",
                    Class(body, "atmosphere"), Source(body, "atmosphere"));
            }

            if (body.IsStar)
            {
                if (body.EffectiveTemperatureK.HasValue)
                    model.Add(model.Facts, "Effective temperature",
                        Format.Significant(body.EffectiveTemperatureK.Value) + " K",
                        Class(body, "temperature"), Source(body, "temperature"));
                if (!string.IsNullOrEmpty(body.SpectralType))
                    model.Add(model.Facts, "Spectral type", body.SpectralType,
                        Class(body, "spectralType"), Source(body, "spectralType"));
            }

            if (!string.IsNullOrEmpty(body.Summary))
                model.Add(model.Facts, "Summary", body.Summary,
                    body.Provenance != null ? body.Provenance.WeakestClass() : ProvenanceClass.Unknown, null);

            if (options != null && options.Sizes != SizePreset.True)
            {
                model.Add(model.Facts, "Presentation",
                    "Sizes: " + SizePresets.Name(options.Sizes) + ". Distances and directions are true.",
                    ProvenanceClass.Procedural, "CONTRACT 5.9.6");
            }

            return model;
        }

        private void Add(List<InspectorField> into, string label, string value,
            ProvenanceClass provenance, string source)
        {
            if (string.IsNullOrEmpty(value)) return;
            into.Add(new InspectorField(label, value, provenance, source));
            if (Weaker(provenance, Weakest)) Weakest = provenance;
        }

        /// <summary>Ordering by strength of claim, not by enum value: Unknown is weakest of all.</summary>
        private static bool Weaker(ProvenanceClass candidate, ProvenanceClass current) =>
            Rank(candidate) < Rank(current);

        private static int Rank(ProvenanceClass c) => c switch
        {
            ProvenanceClass.Measured => 4,
            ProvenanceClass.Inferred => 3,
            ProvenanceClass.Procedural => 2,
            ProvenanceClass.Fictional => 1,
            _ => 0
        };

        private static ProvenanceClass Class(CelestialBody body, string field) =>
            body.Provenance != null && body.Provenance.Has(field)
                ? body.Provenance.Get(field).Class
                : ProvenanceClass.Unknown;

        private static string Source(CelestialBody body, string field) =>
            body.Provenance != null && body.Provenance.Has(field) ? body.Provenance.Get(field).Source : null;

        private static Double3 Position(UniverseSimulation simulation, string id, out bool found)
        {
            found = simulation != null && simulation.Views.TryGetValue(id, out CelestialBodyView view) &&
                    view.IsKnown;
            return found ? simulation.Views[id].AbsolutePositionKm : Double3.Zero;
        }
    }
}
