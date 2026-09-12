using System;
using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Runtime
{
    /// <summary>Which hand holds the pointer. Left mirrors every L/R pair and moves the watch.</summary>
    public enum Handedness { Right, Left }

    /// <summary>Where the observed sky is computed from.</summary>
    public enum SkyVantage { Sun, Viewer, Selected, Site }

    /// <summary>
    /// Every switch the viewer can throw that is not a layer's own, in one object.
    ///
    /// It exists because the alternative is what 1.9.0 had: the size preset lived on the
    /// simulation, the passthrough switch on the rig, the unit choice inside a formatter and the
    /// honesty statement in a desktop-only IMGUI window — so no surface could state what the view
    /// was actually showing, and the one badge the product is named for could not be computed at
    /// all. <see cref="HonestyState"/> reads this object and nothing else.
    ///
    /// A plain class, not a MonoBehaviour: it is state, it has no frame, and a component would
    /// invite someone to find it with <c>FindAnyObjectByType</c>.
    /// </summary>
    public sealed class ViewOptions
    {
        /// <summary>Raised whenever anything here changes, so the panel rebuilds once rather than polling.</summary>
        public event Action Changed;

        private void Raise() => Changed?.Invoke();

        // ---- layers ----------------------------------------------------------

        public LayerState Layers { get; } = new LayerState();

        private readonly HashSet<LabelCategory> _labelsOff = new HashSet<LabelCategory>();

        public bool LabelsMaster { get; private set; } = true;

        public void SetLabelsMaster(bool on) { LabelsMaster = on; Raise(); }

        public bool LabelCategoryOn(LabelCategory category) =>
            LabelsMaster && !_labelsOff.Contains(category);

        public void SetLabelCategory(LabelCategory category, bool on)
        {
            if (on) _labelsOff.Remove(category);
            else _labelsOff.Add(category);
            Raise();
        }

        public void SetLayerOn(string id, bool on) { Layers.SetOn(id, on); Raise(); }

        public void SetSolo(SoloGroup group) { Layers.Solo = group; Raise(); }

        public void SetLayerParam(string layerId, string paramId, double value)
        {
            Layers.SetParam(layerId, paramId, value);
            Raise();
        }

        // ---- honesty ---------------------------------------------------------

        private SizePreset _sizes = SizePreset.Readable;

        public SizePreset Sizes
        {
            get => _sizes;
            set { if (_sizes == value) return; _sizes = value; Raise(); }
        }

        /// <summary>
        /// The largest <c>SizeExaggeration</c> over the bodies actually drawn this frame, written
        /// by the renderer. The chip is on whenever this exceeds 1.05 even in True mode, because
        /// the 2.5 px floor inflates distant bodies and the old rule missed exactly that.
        /// </summary>
        public double MaxDrawnExaggeration { get; set; } = 1.0;

        /// <summary>The focus body's own factor, which is what the on-universe badge names.</summary>
        public double FocusExaggeration { get; set; } = 1.0;

        /// <summary>Effective star gain: the slider times the automatic assist times the pixel floor.</summary>
        public double EffectiveStarGain { get; set; } = 1.0;

        /// <summary>Elevation exaggeration, when a DEM is shipped. 1 until then.</summary>
        public double ReliefFactor { get; set; } = 1.0;

        public bool EphemerisExtrapolated { get; set; }
        public bool StarMotionExtrapolated { get; set; }
        public bool QualityReduced { get; set; }
        public string QualityReducedDetail { get; set; }
        public int MoonsHidden { get; set; }

        // ---- observed sky ----------------------------------------------------

        private bool _observedSky;
        private SkyVantage _vantage = SkyVantage.Sun;
        private double _limitMagnitude = 6.5;
        private bool _extinction = true;
        private ObservingSite _site = ObservingSites.Default;

        public bool ObservedSky
        {
            get => _observedSky;
            set { if (_observedSky == value) return; _observedSky = value; Raise(); }
        }

        public SkyVantage Vantage
        {
            get => _vantage;
            set { if (_vantage == value) return; _vantage = value; Raise(); }
        }

        /// <summary>Faintest apparent magnitude drawn. 3 to 14; 6.5 is the naked-eye sky.</summary>
        public double LimitMagnitude
        {
            get => _limitMagnitude;
            set { double v = MathD.Clamp(value, 3.0, 14.0); if (v == _limitMagnitude) return; _limitMagnitude = v; Raise(); }
        }

        public bool Extinction
        {
            get => _extinction;
            set { if (_extinction == value) return; _extinction = value; Raise(); }
        }

        public ObservingSite Site
        {
            get => _site;
            set { _site = value; Raise(); }
        }

        // ---- room ------------------------------------------------------------

        private float _roomVisibility = 1f;
        private bool _cmbBackdrop;

        /// <summary>1 is full passthrough, 0 is Space. Intermediate values dim the room.</summary>
        public float RoomVisibility
        {
            get => _roomVisibility;
            set
            {
                float v = value < 0f ? 0f : (value > 1f ? 1f : value);
                if (v == _roomVisibility) return;
                _roomVisibility = v;
                Raise();
            }
        }

        public bool CmbBackdrop
        {
            get => _cmbBackdrop;
            set { if (_cmbBackdrop == value) return; _cmbBackdrop = value; Raise(); }
        }

        // ---- plane, units, comfort ------------------------------------------

        /// <summary>null is PLANE: Auto, which takes the plane from the rung being arrived at.</summary>
        public PresentationPlane? PinnedPlane { get; private set; }

        public PresentationPlane ActivePlane { get; set; } = PresentationPlane.Ecliptic;

        public void SetPinnedPlane(PresentationPlane? plane) { PinnedPlane = plane; Raise(); }

        private DistanceUnit _measureUnit = DistanceUnit.Auto;

        public DistanceUnit MeasureUnit
        {
            get => _measureUnit;
            set { if (_measureUnit == value) return; _measureUnit = value; Raise(); }
        }

        public Handedness Handedness { get; set; } = Handedness.Right;

        /// <summary>Clean view hides labels, guides, cards and the panel; any input brings it back.</summary>
        public bool CleanView { get; set; }

        public bool FreeTilt { get; set; }

        public bool SmoothTurn { get; set; }

        public Stage Stage { get; set; } = Stage.Real;

        /// <summary>Notify listeners after a batch of changes made through the public fields.</summary>
        public void NotifyChanged() => Raise();
    }
}
