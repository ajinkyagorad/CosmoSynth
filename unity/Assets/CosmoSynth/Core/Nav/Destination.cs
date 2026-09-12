namespace CosmoSynth.Core
{
    /// <summary>What kind of thing a travel target is, which decides how arrival is framed.</summary>
    public enum DestinationKind
    {
        Rung,
        Body,
        Spacecraft,
        Site,
        Star,
        Cluster,
        Nebula,
        Galaxy,
        Structure,
        Cmb,
        Phenomenon,
        FictionalSector
    }

    /// <summary>
    /// One place the viewer can be taken, wherever it came from.
    ///
    /// The list is never hand-kept. <c>UniverseSimulation</c> contributes every catalogue body
    /// <b>by id</b>, and each layer contributes its own through <c>CollectDestinations</c>. The
    /// hand-kept list is what sent "GJ 876" to GJ 1002: the dashboard held an array of star names
    /// and a parallel array of indices into a catalogue that had since been re-sorted, and nothing
    /// in either array knew the other existed.
    ///
    /// <see cref="BodyId"/> is the difference between a moving target and a fixed one. A body is
    /// re-read from the catalogue every frame of the flight, so a nine-second trip to Jupiter
    /// arrives at Jupiter rather than at where Jupiter was when the button was pressed.
    /// </summary>
    public sealed record Destination(
        string Id,
        string Label,
        DestinationKind Kind,
        string BodyId,
        string LayerId,
        Double3 PositionIcrfKm,
        double FramingRadiusKm,
        double RoomMetres,
        PresentationPlane? Plane,
        ProvenanceClass Provenance,
        string Source)
    {
        /// <summary>
        /// The scale that frames this destination: its span filling the room size it asked for.
        /// Clamped, because a 3 m lander framed at 0.4 m asks for a scale past true size.
        /// </summary>
        public double FramingUnitsPerKm =>
            ScaleLimits.Clamp(RoomMetres / System.Math.Max(2.0 * FramingRadiusKm, 1e-30));

        /// <summary>True when the target moves and the plan must re-read it every frame.</summary>
        public bool IsMoving => !string.IsNullOrEmpty(BodyId);
    }
}
