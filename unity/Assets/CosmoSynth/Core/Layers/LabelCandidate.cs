namespace CosmoSynth.Core
{
    /// <summary>
    /// STUB (integrator, CONTRACT §4.2 / P0.2) — one thing a layer would like labelled.
    ///
    /// Two flavours, because sky content has no position worth naming: a molecular cloud at a
    /// measured distance is labelled <see cref="At"/> its position, while a constellation figure
    /// is labelled <see cref="Toward"/> a direction and has no distance at all. The declutter
    /// pass in the UI lane keeps the highest <see cref="Priority"/> and drops the rest.
    /// </summary>
    public readonly struct LabelCandidate
    {
        public readonly string Id, Text;
        public readonly LabelCategory Category;
        public readonly Double3 PositionKm;
        public readonly Double3 DirectionIcrf;
        public readonly bool IsDirectionOnly;
        public readonly float Priority;
        public readonly ProvenanceClass Provenance;

        public LabelCandidate(string id, string text, LabelCategory category, Double3 positionKm,
            Double3 directionIcrf, bool isDirectionOnly, float priority, ProvenanceClass provenance)
        {
            Id = id;
            Text = text;
            Category = category;
            PositionKm = positionKm;
            DirectionIcrf = directionIcrf;
            IsDirectionOnly = isDirectionOnly;
            Priority = priority;
            Provenance = provenance;
        }

        public static LabelCandidate At(string id, string text, LabelCategory c, Double3 positionKm,
            float priority, ProvenanceClass p) =>
            new LabelCandidate(id, text, c, positionKm, Double3.Zero, false, priority, p);

        public static LabelCandidate Toward(string id, string text, LabelCategory c, Double3 directionIcrf,
            float priority, ProvenanceClass p) =>
            new LabelCandidate(id, text, c, Double3.Zero, directionIcrf, true, priority, p);
    }
}
