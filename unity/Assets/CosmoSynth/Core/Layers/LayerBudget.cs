namespace CosmoSynth.Core
{
    /// <summary>
    /// What one layer promises never to exceed at <c>Weight = 1</c> (§7.3).
    ///
    /// STUB, lane `nav-ui`: the layer framework is integrator-owned (§4.2) and was not in the
    /// foundation branch. Declared here to the contract's exact signature so the panel, the
    /// destinations and the picks can be built against it; delete at merge.
    /// </summary>
    public readonly struct LayerBudget
    {
        public readonly int Draws;
        public readonly int Vertices;

        /// <summary>Transparent fragments, in whole eye-screens.</summary>
        public readonly float FragmentScreens;

        /// <summary>Raymarch steps, in steps over the area of both eyes.</summary>
        public readonly float StepCoverage;

        public readonly float TextureMB;

        public LayerBudget(int draws, int vertices, float fragmentScreens, float stepCoverage, float textureMB)
        {
            Draws = draws;
            Vertices = vertices;
            FragmentScreens = fragmentScreens;
            StepCoverage = stepCoverage;
            TextureMB = textureMB;
        }

        public override string ToString() =>
            $"{Draws} draws, {Vertices} verts, {FragmentScreens:F2} screens, {StepCoverage:F1} steps, {TextureMB:F1} MB";
    }
}
