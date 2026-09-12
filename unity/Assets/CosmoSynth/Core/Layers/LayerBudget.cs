namespace CosmoSynth.Core
{
    /// <summary>
    /// What one layer promises to cost at <c>Weight = 1</c>. CONTRACT §4.2, §7.3.
    ///
    /// Every field is a ceiling, not a measurement. The play-mode <c>budget</c> assertion sums
    /// the rows of every layer with <c>Weight &gt; 0</c>, adds the bodies, and fails the capture
    /// if the totals of §7.3 are exceeded — so a layer that quietly grows its mesh is caught by
    /// arithmetic rather than by a dropped frame in a headset nobody is wearing.
    ///
    /// <see cref="StepCoverage"/> is drawn from the ONE shared raymarch budget (B = 30 of the
    /// global 33). Two enclosing volumes at coverage 1.0 would be 20 ms against an 11 ms frame,
    /// which is why §4.3 keeps exactly one of them alive per frame.
    /// </summary>
    public readonly struct LayerBudget
    {
        /// <summary>Draw calls.</summary>
        public readonly int Draws;

        /// <summary>Vertices processed per eye.</summary>
        public readonly int Vertices;

        /// <summary>Transparent overdraw, in whole eye screens.</summary>
        public readonly float FragmentScreens;

        /// <summary>Raymarch steps over the area of both eyes. Shared budget: see the remarks.</summary>
        public readonly float StepCoverage;

        /// <summary>Resident texture memory, MB.</summary>
        public readonly float TextureMB;

        public LayerBudget(int draws, int vertices, float fragmentScreens, float stepCoverage,
            float textureMB)
        {
            Draws = draws;
            Vertices = vertices;
            FragmentScreens = fragmentScreens;
            StepCoverage = stepCoverage;
            TextureMB = textureMB;
        }

        public override string ToString() =>
            $"{Draws} draws, {Vertices} v, {FragmentScreens:0.##} screens, " +
            $"{StepCoverage:0.#} steps, {TextureMB:0.#} MB";
    }
}
