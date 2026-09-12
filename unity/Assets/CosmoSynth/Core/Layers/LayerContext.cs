using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>Which universe is in the room.</summary>
    public enum Stage : byte { Real, Fictional, Phenomenon }

    /// <summary>Which data files actually loaded, as a sorted array plus a binary search.</summary>
    public readonly struct LoadedKinds
    {
        private readonly string[] _sorted;

        private LoadedKinds(string[] sorted) { _sorted = sorted; }

        public static readonly LoadedKinds Empty = new LoadedKinds(new string[0]);

        public static LoadedKinds From(IReadOnlyList<string> kinds)
        {
            if (kinds == null || kinds.Count == 0) return Empty;
            var copy = new string[kinds.Count];
            for (int i = 0; i < kinds.Count; i++) copy[i] = kinds[i];
            System.Array.Sort(copy, System.StringComparer.Ordinal);
            return new LoadedKinds(copy);
        }

        public bool Has(string kind) =>
            _sorted != null && kind != null &&
            System.Array.BinarySearch(_sorted, kind, System.StringComparer.Ordinal) >= 0;

        public int Count => _sorted?.Length ?? 0;
    }

    /// <summary>
    /// The per-frame answers every layer's visibility condition is a function of (§4.2).
    ///
    /// STUB, lane `nav-ui`: integrator-owned framework type, written to the contract's exact
    /// signature. Built in exactly one place, so it takes one constructor of all fourteen fields
    /// in declaration order and has no setters.
    /// </summary>
    public readonly struct LayerContext
    {
        public readonly LoadedKinds Kinds;
        public readonly Stage Stage;

        /// <summary>1 is full passthrough, 0 is Space.</summary>
        public readonly float RoomVisibility;

        public readonly bool CmbBackdrop;

        /// <summary>Rung <c>sky</c>: the viewer stands on a surface site.</summary>
        public readonly bool ViewerOnSite;

        public readonly double ViewerFromSunPc;
        public readonly double NearestStarPc;

        /// <summary>Apparent diameter in pixels of the focus, the Sun and Earth; 0 if not drawn.</summary>
        public readonly float FocusPx, SunPx, EarthPx;

        public readonly bool ViewerInsideHeliosphere;
        public readonly float HeliosphereDeg;
        public readonly double NearestNebulaBoxRadii;
        public readonly bool ObservedSkyMode;

        /// <summary>Manifest feature keys the importers actually produced (R47).</summary>
        private readonly LoadedKinds _features;

        public LayerContext(LoadedKinds kinds, Stage stage, float roomVisibility, bool cmbBackdrop,
            bool viewerOnSite, double viewerFromSunPc, double nearestStarPc,
            float focusPx, float sunPx, float earthPx, bool viewerInsideHeliosphere,
            float heliosphereDeg, double nearestNebulaBoxRadii, bool observedSkyMode)
            : this(kinds, LoadedKinds.Empty, stage, roomVisibility, cmbBackdrop, viewerOnSite,
                   viewerFromSunPc, nearestStarPc, focusPx, sunPx, earthPx, viewerInsideHeliosphere,
                   heliosphereDeg, nearestNebulaBoxRadii, observedSkyMode)
        {
        }

        /// <summary>
        /// The same, with the manifest's feature list. Separate because <c>HasFeature</c> is in the
        /// contract's type but its source is not in the fourteen-field constructor; a feature key
        /// that is simply absent must read false, never "unknown" (R47).
        /// </summary>
        public LayerContext(LoadedKinds kinds, LoadedKinds features, Stage stage, float roomVisibility,
            bool cmbBackdrop, bool viewerOnSite, double viewerFromSunPc, double nearestStarPc,
            float focusPx, float sunPx, float earthPx, bool viewerInsideHeliosphere,
            float heliosphereDeg, double nearestNebulaBoxRadii, bool observedSkyMode)
        {
            Kinds = kinds;
            _features = features;
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
        public bool HasFeature(string feature) => _features.Has(feature);
    }
}
