using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// A Cartesian position/velocity pair at an instant, relative to some central body and
    /// expressed in some frame. Units are km and km/s (see <see cref="Constants"/>).
    ///
    /// The state vector — not the orbital elements — is the authoritative representation.
    /// Elements are a lossy view of it: they are undefined or ill-conditioned for circular
    /// and equatorial orbits, whereas a state vector is always well defined.
    /// </summary>
    [Serializable]
    public struct StateVector
    {
        /// <summary>Position relative to the central body, km.</summary>
        public Double3 Position;

        /// <summary>Velocity relative to the central body, km/s.</summary>
        public Double3 Velocity;

        public StateVector(Double3 position, Double3 velocity)
        {
            Position = position;
            Velocity = velocity;
        }

        public double Radius => Position.Magnitude;
        public double Speed => Velocity.Magnitude;

        /// <summary>Specific angular momentum vector, km^2/s. Normal to the orbital plane.</summary>
        public Double3 AngularMomentum => Double3.Cross(Position, Velocity);

        /// <summary>Specific orbital energy, km^2/s^2. Negative for bound orbits.</summary>
        public double SpecificEnergy(double gm) => 0.5 * Velocity.SqrMagnitude - gm / Position.Magnitude;

        /// <summary>Eccentricity vector — points at periapsis, magnitude equals eccentricity.</summary>
        public Double3 EccentricityVector(double gm)
        {
            double r = Position.Magnitude;
            double v2 = Velocity.SqrMagnitude;
            double rv = Double3.Dot(Position, Velocity);
            return (Position * (v2 - gm / r) - Velocity * rv) / gm;
        }

        public static StateVector operator +(StateVector a, StateVector b) =>
            new StateVector(a.Position + b.Position, a.Velocity + b.Velocity);

        public static StateVector operator -(StateVector a, StateVector b) =>
            new StateVector(a.Position - b.Position, a.Velocity - b.Velocity);

        public static StateVector operator -(StateVector a) => new StateVector(-a.Position, -a.Velocity);

        /// <summary>Apply an instantaneous velocity change (a burn), leaving position untouched.</summary>
        public StateVector WithDeltaV(Double3 deltaV) => new StateVector(Position, Velocity + deltaV);

        public bool IsFinite => Position.IsFinite && Velocity.IsFinite;

        public override string ToString() => $"r={Position.ToString("F3")} km, v={Velocity.ToString("F6")} km/s";
    }
}
