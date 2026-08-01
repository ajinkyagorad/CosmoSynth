using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>
    /// A uniformly sampled trajectory: where something was, at a fixed cadence, relative to
    /// its parent.
    ///
    /// This is the alternative to <see cref="OrbitalElements"/> for objects a conic does not
    /// describe. Every planet in the catalogue is well served by osculating elements because
    /// its motion really is two-body plus small perturbations. A spacecraft usually is not:
    ///
    ///   * Webb holds a halo about the Earth-Sun L2 point. There is no two-body orbit anywhere
    ///     near that path — it exists only because two gravitating bodies are both significant.
    ///   * Juno's orbit is reshaped by Jupiter's oblateness on every perijove pass.
    ///   * An ion-propelled mission such as Dawn thrusts for years at a time, which is a
    ///     continuous violation of the assumption elements are derived under.
    ///
    /// Fitting a conic to any of those produces a curve that is smooth, plausible and wrong.
    /// Sampling what JPL publishes and interpolating between the samples keeps the error
    /// bounded by the sample spacing instead of by the model being incorrect.
    ///
    /// Uniform spacing is what makes the shipped file small: the sample times are implied by
    /// an epoch and a step rather than stored, which is a third of the data.
    /// </summary>
    public sealed class TrajectoryTable
    {
        private readonly StateVector[] _samples;

        /// <summary>Instant of the first sample.</summary>
        public CosmicTime Epoch { get; }

        /// <summary>Seconds between consecutive samples. Always positive.</summary>
        public double StepSeconds { get; }

        public IReadOnlyList<StateVector> Samples => _samples;
        public int Count => _samples.Length;

        public TrajectoryTable(CosmicTime epoch, double stepSeconds, IReadOnlyList<StateVector> samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (samples.Count < 2)
                throw new ArgumentException("A trajectory needs at least two samples.", nameof(samples));
            if (stepSeconds <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Sample step must be positive.");

            Epoch = epoch;
            StepSeconds = stepSeconds;

            _samples = new StateVector[samples.Count];
            for (int i = 0; i < _samples.Length; i++) _samples[i] = samples[i];
        }

        /// <summary>Instant of the first sample.</summary>
        public CosmicTime Start => Epoch;

        /// <summary>Instant of the last sample.</summary>
        public CosmicTime End => new CosmicTime(Epoch.SecondsPastJ2000 + StepSeconds * (Count - 1));

        /// <summary>Total span covered, seconds.</summary>
        public double DurationSeconds => StepSeconds * (Count - 1);

        /// <summary>
        /// Whether the table actually says anything about this instant.
        ///
        /// Callers are expected to check. The interpolator clamps to its endpoints outside the
        /// tabulated span, which for a spacecraft would park it motionless at its last known
        /// position — and a Cassini frozen above Saturn nine years after it burned up is a
        /// fabrication, however smooth it looks. The renderer hides a body whose trajectory
        /// does not cover the current time instead.
        /// </summary>
        public bool Covers(CosmicTime time)
        {
            double t = time.SecondsPastJ2000;
            return t >= Start.SecondsPastJ2000 && t <= End.SecondsPastJ2000;
        }

        /// <summary>Build the interpolating ephemeris this table feeds.</summary>
        public SampledEphemeris CreateEphemeris()
        {
            var times = new double[Count];
            for (int i = 0; i < Count; i++) times[i] = Epoch.SecondsPastJ2000 + StepSeconds * i;
            return new SampledEphemeris(times, _samples);
        }

        public override string ToString() =>
            $"{Count} samples every {StepSeconds:G6} s, {Start.SecondsPastJ2000:G9} to {End.SecondsPastJ2000:G9}";

        // ---- Serialisation --------------------------------------------------

        /// <summary>
        /// Written as one flat array of six numbers per sample rather than an array of objects.
        /// Twenty-two missions at this cadence is a quarter of a megabyte flat and about four
        /// times that with a key per component, and it all ships inside the APK.
        /// </summary>
        public JsonValue ToJson()
        {
            var json = JsonValue.Object();
            json["epochSecondsPastJ2000"] = JsonValue.Number(Epoch.SecondsPastJ2000);
            json["stepSeconds"] = JsonValue.Number(StepSeconds);

            var samples = JsonValue.Array();
            foreach (StateVector state in _samples)
            {
                samples.Add(JsonValue.Number(state.Position.x));
                samples.Add(JsonValue.Number(state.Position.y));
                samples.Add(JsonValue.Number(state.Position.z));
                samples.Add(JsonValue.Number(state.Velocity.x));
                samples.Add(JsonValue.Number(state.Velocity.y));
                samples.Add(JsonValue.Number(state.Velocity.z));
            }
            json["samples"] = samples;

            return json;
        }

        public static TrajectoryTable FromJson(JsonValue json)
        {
            if (json == null || !json.IsObject) return null;

            var flat = json["samples"];
            if (!flat.IsArray)
                throw new FormatException("A trajectory needs a 'samples' array.");

            var values = new List<double>();
            foreach (var entry in flat) values.Add(entry.AsDouble());

            if (values.Count % 6 != 0)
                throw new FormatException(
                    $"A trajectory's samples must be a multiple of six numbers (x,y,z,vx,vy,vz); " +
                    $"got {values.Count}.");

            var states = new StateVector[values.Count / 6];
            for (int i = 0; i < states.Length; i++)
            {
                int at = i * 6;
                states[i] = new StateVector(
                    new Double3(values[at], values[at + 1], values[at + 2]),
                    new Double3(values[at + 3], values[at + 4], values[at + 5]));
            }

            // Required, not defaulted. An absent or misspelt epoch key read as zero would place
            // the whole trajectory at J2000 — for Voyager, twenty-two years from where it
            // belongs — and every sample would still interpolate smoothly, so the only symptom
            // would be a spacecraft in the wrong decade. The step is already required; the
            // epoch is the more damaging of the two to get wrong.
            return new TrajectoryTable(
                new CosmicTime(json.RequireDouble("epochSecondsPastJ2000")),
                json.RequireDouble("stepSeconds"),
                states);
        }
    }
}
