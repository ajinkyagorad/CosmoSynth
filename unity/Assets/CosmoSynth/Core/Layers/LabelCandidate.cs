namespace CosmoSynth.Core
{
    /// <summary>
    /// One thing a layer would like named, offered to the label budget.
    ///
    /// STUB, lane `nav-ui`: integrator-owned framework type (§4.2), written here to the contract's
    /// exact signature because <c>LabelLayer</c> and the panel need it now. Delete at merge.
    ///
    /// Two positions, because sky content has a direction and no distance: a constellation name
    /// belongs at a bearing, not at a point in space.
    /// </summary>
    public readonly struct LabelCandidate
    {
        public readonly string Id;
        public readonly string Text;
        public readonly LabelCategory Category;

        /// <summary>Absolute ICRF km. Used when <see cref="IsDirectionOnly"/> is false.</summary>
        public readonly Double3 PositionKm;

        /// <summary>ICRF unit direction. Used when <see cref="IsDirectionOnly"/> is true.</summary>
        public readonly Double3 DirectionIcrf;

        public readonly bool IsDirectionOnly;

        /// <summary>Higher wins the greedy declutter (§5.6.10).</summary>
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
