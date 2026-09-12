using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>Which of the three kinds of truth is on stage. CONTRACT §5.3.</summary>
    public enum Stage : byte { Real, Fictional, Phenomenon }

    /// <summary>
    /// Which manifest kinds have actually been delivered. A sorted array plus a binary search,
    /// so asking the question costs nothing per frame and allocates nothing.
    /// </summary>
    public readonly struct LoadedKinds
    {
        private readonly string[] _sorted;

        private LoadedKinds(string[] sorted) { _sorted = sorted; }

        public static readonly LoadedKinds Empty = new LoadedKinds(new string[0]);

        public static LoadedKinds From(IReadOnlyList<string> kinds)
        {
            if (kinds == null || kinds.Count == 0) return Empty;
            string[] copy = new string[kinds.Count];
            for (int i = 0; i < kinds.Count; i++) copy[i] = kinds[i];
            Array.Sort(copy, StringComparer.Ordinal);
            return new LoadedKinds(copy);
        }

        public bool Has(string kind) =>
            _sorted != null && kind != null && Array.BinarySearch(_sorted, kind, StringComparer.Ordinal) >= 0;

        public int Count => _sorted == null ? 0 : _sorted.Length;
    }

    /// <summary>
    /// STUB (integrator, CONTRACT §4.2 / P0.2) — everything a layer's visibility can depend on
    /// besides the scale, built once per frame by <c>ScaleRenderer</c>.
    ///
    /// This copy declares the thirteen fields of §4.2 in declaration order, so the real one is a
    /// drop-in. The <c>deep-sky</c> layers read <see cref="Kinds"/>, <see cref="RoomVisibility"/>,
    /// <see cref="CmbBackdrop"/>, <see cref="ViewerFromSunPc"/> and <see cref="ObservedSkyMode"/>;
    /// the rest are carried so nothing else has to change when the real context lands.
    /// </summary>
    public readonly struct LayerContext
    {
        public readonly LoadedKinds Kinds;
        public readonly Stage Stage;
        public readonly float RoomVisibility;
        public readonly bool CmbBackdrop;
        public readonly bool ViewerOnSite;
        public readonly double ViewerFromSunPc;
        public readonly double NearestStarPc;
        public readonly float FocusPx, SunPx, EarthPx;
        public readonly bool ViewerInsideHeliosphere;
        public readonly float HeliosphereDeg;
        public readonly double NearestNebulaBoxRadii;
        public readonly bool ObservedSkyMode;

        public LayerContext(LoadedKinds kinds, Stage stage, float roomVisibility, bool cmbBackdrop,
            bool viewerOnSite, double viewerFromSunPc, double nearestStarPc,
            float focusPx, float sunPx, float earthPx, bool viewerInsideHeliosphere,
            float heliosphereDeg, double nearestNebulaBoxRadii, bool observedSkyMode)
        {
            Kinds = kinds;
            Stage = stage;
            RoomVisibility = roomVisibility;
            CmbBackdrop = cmbBackdrop;
            ViewerOnSite = viewerOnSite;
            ViewerFromSunPc = viewerFromSunPc;
            NearestStarPc = nearestStarPc;
            FocusPx = focusPx;
            SunPx = sunPx;
            EarthPx = earthPx;
            ViewerInsideHeliosphere = viewerInsideHeliosphere;
            HeliosphereDeg = heliosphereDeg;
            NearestNebulaBoxRadii = nearestNebulaBoxRadii;
            ObservedSkyMode = observedSkyMode;
        }

        public bool Has(string kind) => Kinds.Has(kind);

        /// <summary>
        /// Manifest <c>features[]</c>. Always false here: the stub has no manifest, and a feature
        /// key that cannot be proved present must read as absent (R47) — the panel then says
        /// "not in this build" instead of showing a row that draws nothing.
        /// </summary>
        public bool HasFeature(string feature) => false;
    }
}
