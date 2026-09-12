namespace CosmoSynth.Core
{
    /// <summary>
    /// STUB (integrator, CONTRACT §5.2 / P0.3) — which plane the room is levelled to.
    ///
    /// Only the enum. <c>Planes.Up(plane, ctx)</c> and the levelling animation belong to the
    /// integrator's own file; nothing in the <c>deep-sky</c> lane calls them — the deep layers
    /// take their frames from <see cref="StandardFrames"/> directly, because a grid must keep its
    /// native sampling frame rather than be resampled (CONTRACT §3.4).
    /// </summary>
    public enum PresentationPlane
    {
        Horizon, LocalVertical, FocusEquator, Equatorial, Ecliptic, Galactic, Supergalactic
    }
}
