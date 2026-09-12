namespace CosmoSynth.Core
{
    /// <summary>What kind of thing a travel target is. CONTRACT §4.2.</summary>
    public enum DestinationKind
    {
        Rung, Body, Spacecraft, Site, Star, Cluster, Nebula, Galaxy,
        Structure, Cmb, Phenomenon, FictionalSector
    }

    /// <summary>
    /// STUB (nav lane, CONTRACT §4.2) — somewhere the user can travel to.
    ///
    /// <see cref="FramingRadiusKm"/> is what arrival frames on: the radius that must fit the
    /// room, in true kilometres. <see cref="RoomMetres"/> is how big that should end up being.
    /// Between them they fix the scale on arrival without anyone naming a units-per-km.
    /// </summary>
    public sealed record Destination(
        string Id, string Label, DestinationKind Kind,
        string BodyId,
        string LayerId,
        Double3 PositionIcrfKm,
        double FramingRadiusKm, double RoomMetres, PresentationPlane? Plane,
        ProvenanceClass Provenance, string Source);
}
