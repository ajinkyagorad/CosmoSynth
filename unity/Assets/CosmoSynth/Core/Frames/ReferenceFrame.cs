using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>Supplies the state of a frame's origin relative to its parent frame.</summary>
    public interface IEphemerisSource
    {
        StateVector GetStateInParent(CosmicTime time);
    }

    /// <summary>An origin that never moves relative to its parent.</summary>
    public sealed class FixedEphemeris : IEphemerisSource
    {
        private readonly StateVector _state;

        public FixedEphemeris(Double3 offsetKm) => _state = new StateVector(offsetKm, Double3.Zero);
        public FixedEphemeris(StateVector state) => _state = state;

        public StateVector GetStateInParent(CosmicTime time) => _state;
    }

    /// <summary>An origin on a Keplerian orbit about its parent.</summary>
    public sealed class KeplerEphemeris : IEphemerisSource
    {
        public OrbitalElements Elements { get; }

        public KeplerEphemeris(OrbitalElements elements) => Elements = elements;

        public StateVector GetStateInParent(CosmicTime time) => Elements.Propagate(time);
    }

    /// <summary>
    /// An origin driven by a table of sampled states, as produced by the Horizons/SPICE
    /// importers, with Hermite interpolation between samples.
    ///
    /// Cubic Hermite is the right interpolant here rather than a spline through positions
    /// alone, because the samples carry velocity as well: matching both value and derivative
    /// at each knot reproduces the underlying trajectory to far higher accuracy for the same
    /// sample density, and guarantees the interpolated velocity is consistent with the
    /// interpolated position instead of being a separate approximation that can disagree.
    /// </summary>
    public sealed class SampledEphemeris : IEphemerisSource
    {
        private readonly double[] _times;          // seconds past J2000 TDB, strictly ascending
        private readonly StateVector[] _samples;

        public SampledEphemeris(IReadOnlyList<double> timesSecondsPastJ2000, IReadOnlyList<StateVector> samples)
        {
            if (timesSecondsPastJ2000 == null) throw new ArgumentNullException(nameof(timesSecondsPastJ2000));
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (timesSecondsPastJ2000.Count != samples.Count)
                throw new ArgumentException("Times and samples must be the same length.");
            if (timesSecondsPastJ2000.Count < 2)
                throw new ArgumentException("A sampled ephemeris needs at least two samples.");

            _times = new double[timesSecondsPastJ2000.Count];
            _samples = new StateVector[samples.Count];
            for (int i = 0; i < _times.Length; i++)
            {
                _times[i] = timesSecondsPastJ2000[i];
                _samples[i] = samples[i];
                if (i > 0 && _times[i] <= _times[i - 1])
                    throw new ArgumentException($"Sample times must strictly ascend; index {i} does not.");
            }
        }

        public double StartSeconds => _times[0];
        public double EndSeconds => _times[_times.Length - 1];
        public int SampleCount => _times.Length;

        /// <summary>True if the requested instant lies inside the tabulated span.</summary>
        public bool Covers(CosmicTime time) =>
            time.SecondsPastJ2000 >= StartSeconds && time.SecondsPastJ2000 <= EndSeconds;

        public StateVector GetStateInParent(CosmicTime time)
        {
            double t = time.SecondsPastJ2000;

            // Outside the table, hold the endpoint rather than extrapolating. A Hermite
            // extrapolation diverges cubically and would fling a body across the system;
            // clamping is visibly wrong in a way a user can notice and report.
            if (t <= StartSeconds) return _samples[0];
            if (t >= EndSeconds) return _samples[_samples.Length - 1];

            int index = FindInterval(t);
            double t0 = _times[index], t1 = _times[index + 1];
            double span = t1 - t0;
            double u = (t - t0) / span;

            StateVector a = _samples[index], b = _samples[index + 1];

            // Standard cubic Hermite basis.
            double u2 = u * u, u3 = u2 * u;
            double h00 = 2 * u3 - 3 * u2 + 1;
            double h10 = u3 - 2 * u2 + u;
            double h01 = -2 * u3 + 3 * u2;
            double h11 = u3 - u2;

            Double3 position = a.Position * h00 + a.Velocity * (h10 * span)
                             + b.Position * h01 + b.Velocity * (h11 * span);

            // Derivative of the same basis, so velocity is exactly d(position)/dt.
            double d00 = 6 * u2 - 6 * u;
            double d10 = 3 * u2 - 4 * u + 1;
            double d01 = -6 * u2 + 6 * u;
            double d11 = 3 * u2 - 2 * u;

            Double3 velocity = (a.Position * d00 + b.Position * d01) / span
                             + a.Velocity * d10 + b.Velocity * d11;

            return new StateVector(position, velocity);
        }

        private int FindInterval(double t)
        {
            int low = 0, high = _times.Length - 1;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (_times[mid] <= t) low = mid;
                else high = mid;
            }
            return low;
        }
    }

    /// <summary>
    /// An origin bolted to a rotating body's surface: a landing site, a rover, a base.
    ///
    /// A lander does not orbit anything, and it is not stationary either — it is carried round
    /// once per day by the body it sits on. Storing that as a sampled trajectory would need
    /// dozens of points per rotation to interpolate a circle accurately, forever. Storing it as
    /// a latitude and a longitude and rotating it through the same IAU model the planet itself
    /// is drawn with costs six numbers, is exact at every epoch, and cannot drift out of step
    /// with the planet's own rotation, because it *is* the planet's rotation.
    ///
    /// Note that this frame's origin translates but, like every frame in this tree, stays
    /// inertially oriented. Which way the lander points is a rendering question, answered from
    /// the same rotation matrix.
    /// </summary>
    public sealed class SurfaceFixedEphemeris : IEphemerisSource
    {
        private readonly BodyOrientation _orientation;
        private readonly Double3 _bodyFixedKm;
        private readonly double _angularRateRadPerSec;

        /// <summary>Position in the body's rotating frame, km from its centre.</summary>
        public Double3 BodyFixedPositionKm => _bodyFixedKm;

        public SurfaceFixedEphemeris(BodyOrientation orientation, Double3 bodyFixedKm)
        {
            _orientation = orientation;
            _bodyFixedKm = bodyFixedKm;
            _angularRateRadPerSec =
                orientation.RotationRateDegPerDay * MathD.Deg2Rad / Constants.SecondsPerDay;
        }

        public StateVector GetStateInParent(CosmicTime time)
        {
            Double3 position = _orientation.BodyFixedToIcrf(time) * _bodyFixedKm;

            // Rigid rotation: v = omega x r, with omega along the pole. Reporting zero velocity
            // instead would be wrong by 0.24 km/s at Mars's equator, and anything that computes
            // a relative velocity — a rendezvous readout, an entry model — would quietly
            // inherit the error.
            Double3 omega = _orientation.PoleDirectionAt(time) * _angularRateRadPerSec;

            return new StateVector(position, Double3.Cross(omega, position));
        }
    }

    /// <summary>
    /// A node in the nested reference-frame tree: solar-system barycentre, a planet's centre,
    /// a moon's centre, a landing site, and so on.
    ///
    /// Every frame here is <b>inertially oriented</b> — parallel to ICRF/J2000 — and differs
    /// from its parent only by a translation. Body rotation is deliberately kept out of this
    /// tree and handled by <see cref="BodyOrientation"/>, mirroring the split NAIF makes
    /// between SPK (where things are) and PCK (which way they point). Mixing the two produces
    /// a tree where a moon's orbit appears to precess because its parent planet is spinning.
    ///
    /// The tree is what makes precision tractable. A spacecraft near Enceladus is stored
    /// relative to Enceladus, where its coordinates are of order 100 km, rather than relative
    /// to the solar system barycentre, where they would be 1.4e9 km and every metre of
    /// manoeuvring would be lost in the mantissa.
    /// </summary>
    public sealed class ReferenceFrame
    {
        public string Id { get; }
        public ReferenceFrame Parent { get; }
        public IEphemerisSource Ephemeris { get; }

        /// <summary>Number of hops to the root frame; the root itself is 0.</summary>
        public int Depth { get; }

        private readonly List<ReferenceFrame> _children = new List<ReferenceFrame>();
        public IReadOnlyList<ReferenceFrame> Children => _children;

        /// <summary>Single-entry memo of the last absolute state, since consecutive queries in
        /// a frame nearly always share a timestamp within one simulation tick.</summary>
        private double _cachedTime = double.NaN;
        private StateVector _cachedAbsolute;

        private ReferenceFrame(string id, ReferenceFrame parent, IEphemerisSource ephemeris)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Parent = parent;
            Ephemeris = ephemeris;
            Depth = parent == null ? 0 : parent.Depth + 1;
            parent?._children.Add(this);
        }

        /// <summary>Create a root frame. In CosmoSynth this is the solar system barycentre in ICRF.</summary>
        public static ReferenceFrame CreateRoot(string id) => new ReferenceFrame(id, null, null);

        /// <summary>Attach a child frame whose origin follows the given ephemeris.</summary>
        public ReferenceFrame CreateChild(string id, IEphemerisSource ephemeris)
        {
            if (ephemeris == null) throw new ArgumentNullException(nameof(ephemeris));
            return new ReferenceFrame(id, this, ephemeris);
        }

        public bool IsRoot => Parent == null;

        public ReferenceFrame Root
        {
            get
            {
                var frame = this;
                while (frame.Parent != null) frame = frame.Parent;
                return frame;
            }
        }

        /// <summary>State of this frame's origin relative to its parent.</summary>
        public StateVector StateInParent(CosmicTime time) =>
            Ephemeris == null ? default : Ephemeris.GetStateInParent(time);

        /// <summary>
        /// State of this frame's origin in the root frame. Accumulated by walking up the tree;
        /// this is the quantity whose magnitude the frame hierarchy exists to keep out of
        /// local calculations, so prefer <see cref="StateRelativeTo"/> where possible.
        /// </summary>
        public StateVector AbsoluteState(CosmicTime time)
        {
            if (Parent == null) return default;

            if (_cachedTime == time.SecondsPastJ2000) return _cachedAbsolute;

            StateVector result = StateInParent(time) + Parent.AbsoluteState(time);
            _cachedTime = time.SecondsPastJ2000;
            _cachedAbsolute = result;
            return result;
        }

        /// <summary>
        /// State of this frame's origin as seen from <paramref name="observer"/>.
        ///
        /// Walks only as far as the nearest common ancestor rather than all the way to the
        /// root, so a query between two moons of Jupiter never forms the 7.8e8 km heliocentric
        /// coordinates it would have to subtract back off again.
        /// </summary>
        public StateVector StateRelativeTo(ReferenceFrame observer, CosmicTime time)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (ReferenceEquals(this, observer)) return default;

            ReferenceFrame ancestor = CommonAncestor(this, observer);
            if (ancestor == null)
                throw new InvalidOperationException(
                    $"Frames '{Id}' and '{observer.Id}' belong to different trees and cannot be related.");

            return StateRelativeToAncestor(this, ancestor, time) - StateRelativeToAncestor(observer, ancestor, time);
        }

        /// <summary>Convert a position expressed in this frame into <paramref name="target"/>'s frame.</summary>
        public Double3 TransformPosition(Double3 positionInThisFrame, ReferenceFrame target, CosmicTime time) =>
            positionInThisFrame + StateRelativeTo(target, time).Position;

        /// <summary>Convert a full state expressed in this frame into <paramref name="target"/>'s frame.</summary>
        public StateVector TransformState(StateVector stateInThisFrame, ReferenceFrame target, CosmicTime time) =>
            stateInThisFrame + StateRelativeTo(target, time);

        private static StateVector StateRelativeToAncestor(ReferenceFrame frame, ReferenceFrame ancestor, CosmicTime time)
        {
            StateVector accumulated = default;
            var current = frame;
            while (!ReferenceEquals(current, ancestor))
            {
                accumulated += current.StateInParent(time);
                current = current.Parent;
                if (current == null)
                    throw new InvalidOperationException("Ancestor was not on this frame's parent chain.");
            }
            return accumulated;
        }

        /// <summary>Nearest frame that is an ancestor of (or equal to) both inputs, or null if unrelated.</summary>
        public static ReferenceFrame CommonAncestor(ReferenceFrame a, ReferenceFrame b)
        {
            if (a == null || b == null) return null;

            // Level the two chains, then step up together.
            while (a.Depth > b.Depth) a = a.Parent;
            while (b.Depth > a.Depth) b = b.Parent;
            while (a != null && b != null && !ReferenceEquals(a, b))
            {
                a = a.Parent;
                b = b.Parent;
            }
            return ReferenceEquals(a, b) ? a : null;
        }

        /// <summary>Find a descendant frame by id, searching this subtree.</summary>
        public ReferenceFrame Find(string id)
        {
            if (Id == id) return this;
            foreach (var child in _children)
            {
                var found = child.Find(id);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Enumerate this frame and every descendant, depth first.</summary>
        public IEnumerable<ReferenceFrame> Traverse()
        {
            yield return this;
            foreach (var child in _children)
            {
                foreach (var descendant in child.Traverse()) yield return descendant;
            }
        }

        /// <summary>Drop any memoised absolute states — call after retargeting an ephemeris.</summary>
        public void InvalidateCache()
        {
            foreach (var frame in Traverse()) frame._cachedTime = double.NaN;
        }

        public override string ToString() =>
            Parent == null ? $"{Id} (root)" : $"{Id} (child of {Parent.Id}, depth {Depth})";
    }
}
