using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Runtime
{
    /// <summary>What a row is, which decides how the surface draws it and whether it is pressable.</summary>
    public enum PanelRowKind { Caption, Button, Toggle, Slider, Text }

    /// <summary>
    /// One cell of the panel. Cells flow left to right and wrap: <see cref="Span"/> is in quarters
    /// of the content width, so four buttons fit a row and a caption takes all four.
    /// </summary>
    public readonly struct PanelRow
    {
        public readonly PanelRowKind Kind;
        public readonly string Id;
        public readonly string Text;

        /// <summary>The state suffix a LAYERS row carries: "on", "missing: sky/stars.csstars".</summary>
        public readonly string Suffix;

        public readonly int Span;
        public readonly bool On;
        public readonly bool Enabled;

        /// <summary>Slider position, 0 to 1.</summary>
        public readonly float Value;

        public readonly ProvenanceClass Provenance;
        public readonly bool ShowGlyph;

        public PanelRow(PanelRowKind kind, string id, string text, int span = 4, bool on = false,
            bool enabled = true, float value = 0f, string suffix = null,
            ProvenanceClass provenance = ProvenanceClass.Unknown, bool showGlyph = false)
        {
            Kind = kind;
            Id = id;
            Text = text;
            Span = span < 1 ? 1 : (span > 4 ? 4 : span);
            On = on;
            Enabled = enabled;
            Value = value;
            Suffix = suffix;
            Provenance = provenance;
            ShowGlyph = showGlyph;
        }

        public bool Pressable => Enabled && Kind != PanelRowKind.Caption && Kind != PanelRowKind.Text;
    }

    /// <summary>
    /// The panel's contents, as rows, generated from the tables and never hand-listed.
    ///
    /// <c>LayerTable.Rungs</c> makes the ladder, <c>LayerTable.Layers</c> makes the LAYERS tab and
    /// the SOLO buttons, <c>WarpLadder</c> makes the rate controls, <c>XrInput.Bindings</c> makes
    /// the HELP legend, and <c>DestinationIndex</c> makes every GO list. The hand-written legend
    /// this replaces had already drifted from the code — it advertised "trigger: boost" for a
    /// trigger that mostly grabbed and never mentioned the wrist dashboard at all.
    ///
    /// A plain class with no engine calls beyond the components it drives, so the row list can be
    /// built and asserted on without a headset.
    /// </summary>
    public sealed class PanelModel
    {
        public enum Tab { Go, Layers, Time, Tools, Info }

        /// <summary>Rows visible at once before the content scrolls (§5.6.3).</summary>
        public const int VisibleRows = 7;

        private readonly List<PanelRow> _rows = new List<PanelRow>(64);

        private UniverseSimulation _simulation;
        private SimulationClock _clock;
        private ViewOptions _options;
        private ScaleControl _scale;
        private TravelController _travel;
        private DestinationIndex _destinations;
        private MeasureTool _measure;
        private BodySelector _selector;
        private XrRig _rig;
        private System.Action _recentre;

        private string _subPage;
        private string _openLayer;
        private bool _allLayers;

        /// <summary>Credits, from the manifest when there is one.</summary>
        private IReadOnlyList<string> _credits;

        public Tab ActiveTab { get; private set; } = Tab.Go;

        /// <summary>Which sub-list is open, or null for the tab's root.</summary>
        public string SubPage => _subPage;

        public int ScrollRow { get; private set; }

        public IReadOnlyList<PanelRow> Rows => _rows;

        /// <summary>Raised whenever the rows change, so the surface lays out once.</summary>
        public event System.Action Changed;

        public void Bind(UniverseSimulation simulation, SimulationClock clock, ViewOptions options,
            ScaleControl scale, TravelController travel, DestinationIndex destinations,
            MeasureTool measure, BodySelector selector, XrRig rig, System.Action recentre)
        {
            _simulation = simulation;
            _clock = clock;
            _options = options;
            _scale = scale;
            _travel = travel;
            _destinations = destinations;
            _measure = measure;
            _selector = selector;
            _rig = rig;
            _recentre = recentre;
        }

        /// <summary>Called by the data loader once the manifest's credits are known.</summary>
        public void SetCredits(IReadOnlyList<string> credits)
        {
            _credits = credits;
            Rebuild();
        }

        public void SetTab(Tab tab)
        {
            ActiveTab = tab;
            _subPage = null;
            _openLayer = null;
            ScrollRow = 0;
            Rebuild();
        }

        public void Scroll(int lines)
        {
            int maximum = System.Math.Max(0, RowCountAsLaidOut() - VisibleRows);
            ScrollRow = System.Math.Max(0, System.Math.Min(maximum, ScrollRow + lines));
            Changed?.Invoke();
        }

        // ---- header and footer ----------------------------------------------

        public string HeaderMode => _options == null ? "REAL" : _options.Stage switch
        {
            Stage.Fictional => "FICTIONAL SECTOR ?",
            Stage.Phenomenon => "PHENOMENON",
            _ => "REAL"
        };

        public string HeaderClock
        {
            get
            {
                if (_clock == null) return string.Empty;
                CalendarDate date = _clock.Now.ToUtc();
                string stamp = date.Year >= CalendarUtilMinimumYear && date.Year <= 9999
                    ? $"{date.Year:0000}-{date.Month:00}-{date.Day:00} {date.Hour:00}:{date.Minute:00}:{(int)date.Second:00} UTC"
                    : Format.Significant(_clock.Now.SecondsPastJ2000 / Constants.SecondsPerJulianYear) + " yr from J2000";
                string warp = _clock.IsPaused ? "paused" : _clock.WarpLabel;
                return stamp + " · " + warp;
            }
        }

        /// <summary>Calendar dates display exactly from year -4700 (§5.8.2).</summary>
        private const int CalendarUtilMinimumYear = -4700;

        public string HeaderScale => _scale != null ? _scale.Readout() : string.Empty;

        public string FooterName
        {
            get
            {
                CelestialBodyView selected = _selector != null ? _selector.Selected : null;
                if (selected != null) return selected.Body.Name;
                return "Nothing selected";
            }
        }

        /// <summary>Every active chip, full set. The header shows four and a <c>+N</c>.</summary>
        public List<HonestyChip> Chips() => HonestyState.All(_options, LevelHint());

        /// <summary>
        /// "Level to Galactic", when zooming has left the viewer nearer a rung whose plane differs
        /// from the active one. It never levels by itself (§5.2 rule 4).
        /// </summary>
        public string LevelHint()
        {
            if (_scale == null || _options == null || _options.PinnedPlane.HasValue) return null;
            Rung nearest = _scale.CurrentRung;
            if (nearest == null || nearest.Plane == _options.ActivePlane) return null;
            return Planes.Name(nearest.Plane);
        }

        // ---- building --------------------------------------------------------

        public void Rebuild()
        {
            _rows.Clear();
            switch (ActiveTab)
            {
                case Tab.Go: BuildGo(); break;
                case Tab.Layers: BuildLayers(); break;
                case Tab.Time: BuildTime(); break;
                case Tab.Tools: BuildTools(); break;
                case Tab.Info: BuildInfo(); break;
            }
            Changed?.Invoke();
        }

        private void Caption(string text) => _rows.Add(new PanelRow(PanelRowKind.Caption, null, text));

        private void Text(string text) => _rows.Add(new PanelRow(PanelRowKind.Text, null, text));

        private void Button(string id, string text, int span = 2, bool on = false, bool enabled = true) =>
            _rows.Add(new PanelRow(PanelRowKind.Button, id, text, span, on, enabled));

        private void Toggle(string id, string text, bool on, string suffix = null,
            ProvenanceClass provenance = ProvenanceClass.Unknown, bool glyph = false, bool enabled = true) =>
            _rows.Add(new PanelRow(PanelRowKind.Toggle, id, text, 4, on, enabled, 0f, suffix, provenance, glyph));

        private void Slider(string id, string text, float value) =>
            _rows.Add(new PanelRow(PanelRowKind.Slider, id, text, 4, false, true, value));

        private void Back() => Button("back", "< Back", 2);

        // ---- GO --------------------------------------------------------------

        private void BuildGo()
        {
            if (_subPage != null) { BuildGoSubList(_subPage); return; }

            Caption("LADDER");
            string currentRungId = _scale != null && _scale.CurrentRung != null ? _scale.CurrentRung.Id : null;
            for (int g = 0; g < LayerTable.RungGroups.Length; g++)
            {
                string group = LayerTable.RungGroups[g];
                Caption(group);
                for (int i = 0; i < LayerTable.Rungs.Length; i++)
                {
                    Rung rung = LayerTable.Rungs[i];
                    if (rung.Group != group) continue;
                    Button("go.rung." + rung.Id, rung.Caption, 2, rung.Id == currentRungId);
                }
            }

            if (_destinations != null && _destinations.Recent.Count > 0)
            {
                Caption("RECENT");
                for (int i = 0; i < _destinations.Recent.Count; i++)
                {
                    Destination destination = _destinations.Recent[i];
                    Button("go.dest." + destination.Id, destination.Label, 2);
                }
            }

            Caption("DESTINATIONS");
            AddKindButton(DestinationKind.Body, "Solar System");
            AddKindButton(DestinationKind.Spacecraft, "Spacecraft");
            AddKindButton(DestinationKind.Site, "Surface sites");
            AddKindButton(DestinationKind.Star, "Stars");
            AddKindButton(DestinationKind.Nebula, "Clouds and nebulae");
            AddKindButton(DestinationKind.Cluster, "Clusters");
            AddKindButton(DestinationKind.Galaxy, "Galaxies");
            AddKindButton(DestinationKind.Structure, "Large-scale structure");
            AddKindButton(DestinationKind.Cmb, "Cosmic microwave background");
        }

        private void AddKindButton(DestinationKind kind, string caption)
        {
            int count = CountOfKind(kind);
            _rows.Add(new PanelRow(PanelRowKind.Button, "go.kind." + kind, caption, 4, false, count > 0,
                0f, count > 0 ? count.ToString() : "none in this build"));
        }

        private int CountOfKind(DestinationKind kind)
        {
            if (_destinations == null) return 0;
            IReadOnlyList<Destination> all = _destinations.All;
            int count = 0;
            for (int i = 0; i < all.Count; i++) if (all[i].Kind == kind) count++;
            return count;
        }

        private void BuildGoSubList(string page)
        {
            Back();
            if (!page.StartsWith("kind.", System.StringComparison.Ordinal) || _destinations == null) return;
            if (!System.Enum.TryParse(page.Substring(5), out DestinationKind kind)) return;

            Double3 viewer = _simulation != null ? _simulation.Origin.OriginKm : Double3.Zero;
            List<Destination> list = _destinations.OfKind(kind, viewer);
            Caption(kind.ToString().ToUpperInvariant() + " · " + list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                Destination destination = list[i];
                _rows.Add(new PanelRow(PanelRowKind.Button, "go.dest." + destination.Id, destination.Label,
                    4, false, true, 0f, Format.Distance(_destinations.DistanceKm(destination, viewer)),
                    destination.Provenance, showGlyph: true));
            }
        }

        // ---- LAYERS ----------------------------------------------------------

        private void BuildLayers()
        {
            if (_options == null) return;

            Caption("SHOW");
            Button("layers.show.rung", "This rung", 2, !_allLayers);
            Button("layers.show.all", "All layers", 2, _allLayers);

            Caption("SOLO");
            Button("solo.None", "ALL", 1, _options.Layers.Solo == SoloGroup.None);
            Button("solo.Solar", "SOLAR", 1, _options.Layers.Solo == SoloGroup.Solar);
            Button("solo.Stars", "STARS", 1, _options.Layers.Solo == SoloGroup.Stars);
            Button("solo.Dust", "DUST", 1, _options.Layers.Solo == SoloGroup.Dust);
            Button("solo.Galaxies", "GALAXIES", 2, _options.Layers.Solo == SoloGroup.Galaxies);
            Button("solo.Beyond", "BEYOND", 2, _options.Layers.Solo == SoloGroup.Beyond);

            double log10U = _scale != null ? _scale.Log10U : 0.0;
            LayerContext context = BuildLayerContext();

            LayerGroup? section = null;
            int elsewhere = 0;

            for (int i = 0; i < LayerTable.Layers.Length; i++)
            {
                LayerDef def = LayerTable.Layers[i];
                bool inWindow = LayerState.Window(def, log10U) > 0.0;
                if (!_allLayers && !inWindow) { elsewhere++; continue; }

                if (section != def.Group)
                {
                    section = def.Group;
                    Caption(SectionName(def.Group));
                }

                LayerStatus status = _options.Layers.Status(def.Id, log10U, context);
                Toggle("layer." + def.Id, def.Caption, _options.Layers.IsOn(def.Id), status.ToString(),
                    def.Weakest, glyph: true, enabled: status.Kind != LayerStatusKind.NotInThisBuild &&
                                                       status.Kind != LayerStatusKind.FeatureMissing);

                if (_openLayer != def.Id || def.Params == null) continue;

                // The accordion carries the off switch, because the row itself now opens the
                // accordion. Without this a layer with sliders can be opened and never turned off.
                Button("layeroff." + def.Id, "Switch " + def.Caption + " off", 4);

                for (int p = 0; p < def.Params.Length; p++)
                {
                    LayerParam param = def.Params[p];
                    double value = _options.Layers.Param(def.Id, param.Id);
                    float t = param.Max > param.Min
                        ? (float)MathD.Clamp01((value - param.Min) / (param.Max - param.Min))
                        : 0f;
                    Slider("param." + def.Id + "." + param.Id,
                        param.Caption + "  " + Format.Significant(value) + param.Unit, t);
                }
            }

            if (!_allLayers && elsewhere > 0)
                Button("layers.show.all", elsewhere + " layers apply at other scales", 4);

            Caption("LABELS");
            Toggle("labels.master", "All labels", _options.LabelsMaster);
            foreach (LabelCategory category in System.Enum.GetValues(typeof(LabelCategory)))
                Toggle("labels." + category, category.ToString(), _options.LabelCategoryOn(category));
        }

        private static string SectionName(LayerGroup group) => group switch
        {
            LayerGroup.SolarSystem => "SOLAR SYSTEM",
            LayerGroup.Stars => "STARS",
            LayerGroup.DustAndNebulae => "DUST AND NEBULAE",
            LayerGroup.Galaxies => "GALAXIES",
            LayerGroup.Beyond => "BEYOND",
            _ => "GUIDES"
        };

        /// <summary>
        /// The context the LAYERS tab asks its questions with.
        ///
        /// Built here rather than read from <c>FrameContext</c> because the panel runs before the
        /// renderer has published one on the first frame, and a tab that cannot draw on frame 1 is
        /// a tab that flickers every time it is opened.
        /// </summary>
        private LayerContext BuildLayerContext()
        {
            LoadedKinds kinds = DataKindsLoaded;
            return new LayerContext(kinds, LoadedKinds.Empty, _options.Stage, _options.RoomVisibility,
                _options.CmbBackdrop, false, 0.0, double.PositiveInfinity, 0f, 0f, 0f, false, 0f,
                double.PositiveInfinity, _options.ObservedSky);
        }

        /// <summary>
        /// Which data kinds are present. Set by the data loader; empty until then, which is what
        /// makes every layer row read "not in this build (&lt;kind&gt;)" rather than silently "off".
        /// </summary>
        public LoadedKinds DataKindsLoaded { get; set; } = LoadedKinds.Empty;

        // ---- TIME ------------------------------------------------------------

        private void BuildTime()
        {
            if (_clock == null) return;

            Caption("TRANSPORT");
            Button("time.slower", "<<", 1);
            Button("time.pause", _clock.IsPaused ? ">" : "||", 1);
            Button("time.faster", ">>", 1);
            Button("time.now", "Now", 1);

            Caption("RATE · " + _clock.WarpLabel);
            Button("time.rate.-10", "-1 yr", 1);
            Button("time.rate.-9", "-30 d", 1);
            Button("time.rate.-7", "-1 d", 1);
            Button("time.rate.1", "Real", 1);
            Button("time.rate.5", "1 h", 1);
            Button("time.rate.7", "1 d", 1);
            Button("time.rate.9", "30 d", 1);
            Button("time.rate.10", "1 yr", 1);

            Caption("DEEP TIME");
            Button("time.jump.-31557600000000", "-1 Myr", 1);
            Button("time.jump.-315576000000", "-10 kyr", 1);
            Button("time.jump.-3155760000", "-100 yr", 1);
            Button("time.jump.3155760000", "+100 yr", 1);
            Button("time.jump.315576000000", "+10 kyr", 1);
            Button("time.jump.31557600000000", "+1 Myr", 1);

            Caption("DATE");
            Button("time.step.year.-1", "- YEAR", 1);
            Button("time.step.year.1", "+ YEAR", 1);
            Button("time.step.day.-1", "- DAY", 1);
            Button("time.step.day.1", "+ DAY", 1);
            Button("time.step.hour.-1", "- HOUR", 1);
            Button("time.step.hour.1", "+ HOUR", 1);

            Caption("KEY DATES");
            Button("time.date.2000-01-01", "J2000.0", 2);
            Button("time.date.1969-07-20", "Apollo 11 landing", 2);
            Button("time.date.1977-09-05", "Voyager 1 launch", 2);
            Button("time.date.2012-08-25", "Voyager 1 heliopause", 2);
            Button("time.date.2015-07-14", "New Horizons at Pluto", 2);
            Button("time.date.2018-11-05", "Voyager 2 heliopause", 2);
        }

        // ---- TOOLS -----------------------------------------------------------

        private void BuildTools()
        {
            if (_options == null) return;

            Caption("MEASURE");
            Button("tool.measure", _measure != null && _measure.Active ? "MEASURE: on" : "MEASURE: off", 2,
                _measure != null && _measure.Active);
            Button("tool.measure.clear", "Clear", 2, false, _measure != null && _measure.Count > 0);
            Button("units.Auto", "Auto", 1, _options.MeasureUnit == DistanceUnit.Auto);
            Button("units.Kilometres", "km", 1, _options.MeasureUnit == DistanceUnit.Kilometres);
            Button("units.AstronomicalUnits", "AU", 1, _options.MeasureUnit == DistanceUnit.AstronomicalUnits);
            Button("units.LightYears", "ly", 1, _options.MeasureUnit == DistanceUnit.LightYears);

            Caption("SIZES");
            Button("sizes.True", "True", 1, _options.Sizes == SizePreset.True);
            Button("sizes.Readable", "Readable", 1, _options.Sizes == SizePreset.Readable);
            Button("sizes.Times10", "x10", 1, _options.Sizes == SizePreset.Times10);
            Button("sizes.Times100", "x100", 1, _options.Sizes == SizePreset.Times100);
            Button("sizes.Times1000", "x1000", 1, _options.Sizes == SizePreset.Times1000);

            Caption("OBSERVED SKY");
            Button("sky.toggle", _options.ObservedSky ? "Observed sky: on" : "Model (flux-true): on", 4,
                _options.ObservedSky);
            Button("sky.vantage.Sun", "From the Sun", 1, _options.Vantage == SkyVantage.Sun);
            Button("sky.vantage.Viewer", "From me", 1, _options.Vantage == SkyVantage.Viewer);
            Button("sky.vantage.Selected", "From selected", 1, _options.Vantage == SkyVantage.Selected);
            Button("sky.vantage.Site", "From a site", 1, _options.Vantage == SkyVantage.Site);
            Slider("sky.limit", "Limit magnitude " + Format.Significant(_options.LimitMagnitude, 3),
                (float)((_options.LimitMagnitude - 3.0) / 11.0));
            Toggle("sky.extinction", "Extinction (needs the dust map)", _options.Extinction);
            Button("sky.site", "SITE: " + _options.Site.Name, 4);

            Caption("ROOM");
            Slider("room.visibility", "Room visible " + Format.Significant(_options.RoomVisibility * 100.0, 3) + "%",
                _options.RoomVisibility);
            Button("room.passthrough", "Passthrough", 2, _options.RoomVisibility > 0.5f);
            Button("room.space", "Space", 2, _options.RoomVisibility <= 0.5f);
            Toggle("room.cmbBackdrop", "CMB backdrop in passthrough", _options.CmbBackdrop);

            Caption("PLANE");
            Button("plane.Auto", "Auto", 1, !_options.PinnedPlane.HasValue);
            Button("plane.Ecliptic", "Ecliptic", 1, _options.PinnedPlane == PresentationPlane.Ecliptic);
            Button("plane.Galactic", "Galactic", 1, _options.PinnedPlane == PresentationPlane.Galactic);
            Button("plane.Supergalactic", "Supergal.", 1, _options.PinnedPlane == PresentationPlane.Supergalactic);

            Caption("VIEW");
            Toggle("view.clean", "Clean view", _options.CleanView);
            Toggle("view.smoothTurn", "Smooth turn", _options.SmoothTurn);
            Toggle("view.freeTilt", "Free tilt", _options.FreeTilt);
            Button("view.handedness", "Handedness: " + _options.Handedness, 2);
            Button("view.recentre", "Recentre", 2);

            Caption("SOUND");
            Text("Sound: not in this build");
        }

        // ---- INFO ------------------------------------------------------------

        private void BuildInfo()
        {
            if (_subPage != null) { BuildInfoPage(_subPage); return; }

            Button("info.inspector", "INSPECTOR", 2);
            Button("info.view", "THIS VIEW", 2);
            Button("info.credits", "CREDITS & LICENCES", 2);
            Button("info.status", "STATUS", 2);
            Button("info.help", "HELP", 2);
        }

        private void BuildInfoPage(string page)
        {
            Back();
            switch (page)
            {
                case "inspector": BuildInspectorPage(); break;
                case "view": BuildThisViewPage(); break;
                case "credits": BuildCreditsPage(); break;
                case "status": BuildStatusPage(); break;
                case "help": BuildHelpPage(); break;
            }
        }

        private void BuildInspectorPage()
        {
            CelestialBodyView selected = _selector != null ? _selector.Selected : null;
            if (selected == null) { Text("Point at something and pull the trigger."); return; }

            Double3 viewer = _simulation != null ? _simulation.Origin.OriginKm : Double3.Zero;
            InspectorModel model = InspectorModel.For(_simulation, selected, viewer, _options);

            Caption(model.Name + (model.Kind != null ? " · " + model.Kind : string.Empty));
            if (model.Notice != null) Text("! " + model.Notice);

            Caption("NOW");
            for (int i = 0; i < model.Now.Count; i++) AddField(model.Now[i]);

            Caption("FACTS");
            for (int i = 0; i < model.Facts.Count; i++) AddField(model.Facts[i]);
        }

        private void AddField(in InspectorField field) =>
            _rows.Add(new PanelRow(PanelRowKind.Text, null, field.Label + ": " + field.Value, 4, false, true,
                0f, field.Source, field.Provenance, showGlyph: true));

        private void BuildThisViewPage()
        {
            Caption("HOW HONEST THIS VIEW IS");
            Text(HonestyState.Statement(_options));

            List<HonestyChip> chips = Chips();
            if (chips.Count > 0)
            {
                Caption("ACTIVE DEPARTURES · " + chips.Count);
                for (int i = 0; i < chips.Count; i++) Text(chips[i].Text);
            }

            Rung rung = _scale != null ? _scale.CurrentRung : null;
            if (rung != null)
            {
                Caption("THIS RUNG");
                Text(rung.Caption + " · " + Format.ScaleReadout(_scale.UnitsPerKm));
                Text("Plane: " + Planes.Name(_options.ActivePlane));
            }

            Caption("WHAT WE DO NOT KNOW HERE");
            Text("Angular sizes and directions are exact, and so is front-to-back order inside a group.");
            Text("What a far group loses is stereo depth beyond the compression knee, which a real observer cannot see past about 76 m either.");
        }

        private void BuildCreditsPage()
        {
            Caption("CREDITS AND LICENCES");
            if (_credits != null && _credits.Count > 0)
            {
                for (int i = 0; i < _credits.Count; i++) Text(_credits[i]);
                return;
            }

            // Honest degradation: the manifest is the source of the credits list, and until the
            // data lanes ship one this page says exactly what it does know rather than nothing.
            Text("The manifest has not been read in this build.");
            if (_simulation?.Catalog != null)
            {
                Text("Catalogue: " + _simulation.Catalog.Name);
                if (!string.IsNullOrEmpty(_simulation.Catalog.Source)) Text("Source: " + _simulation.Catalog.Source);
            }
            Text("Solar System ephemerides: NASA JPL Horizons.");
            Text("Spacecraft models: NASA 3D Resources.");
        }

        private void BuildStatusPage()
        {
            Caption("STATUS");
            if (_scale != null) Text(_scale.Describe());
            if (_travel != null) Text(_travel.Describe());
            if (_measure != null) Text("measure: " + _measure.Describe());
            if (_simulation?.Catalog != null)
            {
                Text("catalogue: " + _simulation.Catalog.Name + " · " + _simulation.Catalog.Count + " bodies");
                Text("views: " + _simulation.Views.Count);
            }
            Text("layers registered: " + LayerRegistry.All.Count);
            Text("data kinds loaded: " + DataKindsLoaded.Count);
        }

        private void BuildHelpPage()
        {
            BindDevice device = _rig != null && _rig.XrActive ? BindDevice.Controller : BindDevice.Keyboard;
            Caption(device == BindDevice.Controller ? "CONTROLLERS" : "KEYBOARD AND MOUSE");
            for (int i = 0; i < XrInput.Bindings.Length; i++)
            {
                Binding binding = XrInput.Bindings[i];
                if (binding.Device != device) continue;
                Text(binding.Control + " · " + binding.Action);
            }

            Caption("HANDS");
            for (int i = 0; i < XrInput.Bindings.Length; i++)
            {
                Binding binding = XrInput.Bindings[i];
                if (binding.Device != BindDevice.Hand) continue;
                Text(binding.Control + " · " + binding.Action);
            }
        }

        // ---- actions ---------------------------------------------------------

        /// <summary>Route a press. Ids are the rows' own, so nothing is matched by position.</summary>
        public void Activate(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (id == "back") { _subPage = null; ScrollRow = 0; Rebuild(); return; }

            if (Starts(id, "go.rung.", out string rungId)) { _travel?.TravelToRung(rungId); Rebuild(); return; }
            if (Starts(id, "go.kind.", out string kind)) { _subPage = "kind." + kind; ScrollRow = 0; Rebuild(); return; }
            if (Starts(id, "go.dest.", out string destinationId))
            {
                Destination destination = _destinations?.Find(destinationId);
                if (destination != null) _travel?.Travel(destination);
                Rebuild();
                return;
            }

            if (id == "layers.show.rung") { _allLayers = false; ScrollRow = 0; Rebuild(); return; }
            if (id == "layers.show.all") { _allLayers = true; ScrollRow = 0; Rebuild(); return; }
            if (Starts(id, "solo.", out string solo) && System.Enum.TryParse(solo, out SoloGroup group))
            { _options.SetSolo(group); Rebuild(); return; }

            if (Starts(id, "layeroff.", out string offId))
            {
                _options.SetLayerOn(offId, false);
                _openLayer = null;
                Rebuild();
                return;
            }

            if (Starts(id, "layer.", out string layerId))
            {
                // A press toggles; a press on an already-on layer that has parameters opens them.
                LayerDef def = LayerTable.Find(layerId);
                if (def != null && def.Params != null && def.Params.Length > 0 && _options.Layers.IsOn(layerId))
                    _openLayer = _openLayer == layerId ? null : layerId;
                else
                    _options.SetLayerOn(layerId, !_options.Layers.IsOn(layerId));
                Rebuild();
                return;
            }

            if (id == "labels.master") { _options.SetLabelsMaster(!_options.LabelsMaster); Rebuild(); return; }
            if (Starts(id, "labels.", out string categoryName) &&
                System.Enum.TryParse(categoryName, out LabelCategory category))
            { _options.SetLabelCategory(category, !_options.LabelCategoryOn(category)); Rebuild(); return; }

            if (ActivateTime(id)) { Rebuild(); return; }
            if (ActivateTools(id)) { Rebuild(); return; }

            if (Starts(id, "info.", out string page)) { _subPage = page; ScrollRow = 0; Rebuild(); return; }
        }

        private bool ActivateTime(string id)
        {
            if (_clock == null) return false;

            switch (id)
            {
                case "time.slower": _clock.WarpIndex = WarpLadder.Step(_clock.WarpIndex, -1); return true;
                case "time.faster": _clock.WarpIndex = WarpLadder.Step(_clock.WarpIndex, 1); return true;
                case "time.pause": _clock.TogglePause(); return true;
                case "time.now": _clock.SetEpochToNow(); _clock.WarpIndex = WarpLadder.RealTimeIndex; return true;
            }

            if (Starts(id, "time.rate.", out string rate) && int.TryParse(rate, out int index))
            { _clock.WarpIndex = WarpLadder.Clamp(index); return true; }

            if (Starts(id, "time.jump.", out string jump) &&
                double.TryParse(jump, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double seconds))
            { _clock.Now = _clock.Now + seconds; return true; }

            if (Starts(id, "time.step.", out string step))
            {
                string[] parts = step.Split('.');
                if (parts.Length != 2 || !int.TryParse(parts[1], out int sign)) return false;
                double amount = parts[0] switch
                {
                    "year" => Constants.SecondsPerJulianYear,
                    "day" => Constants.SecondsPerDay,
                    _ => 3600.0
                };
                _clock.Now = _clock.Now + amount * sign;
                return true;
            }

            if (Starts(id, "time.date.", out string date))
            {
                string[] parts = date.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[0], out int y) &&
                    int.TryParse(parts[1], out int m) && int.TryParse(parts[2], out int d))
                {
                    _clock.SetEpochUtc(y, m, d);
                    return true;
                }
            }

            return false;
        }

        private bool ActivateTools(string id)
        {
            if (_options == null) return false;

            switch (id)
            {
                case "tool.measure": _measure?.Toggle(); return true;
                case "tool.measure.clear": _measure?.Clear(); return true;
                case "sky.toggle": _options.ObservedSky = !_options.ObservedSky; return true;
                case "sky.extinction": _options.Extinction = !_options.Extinction; return true;
                case "sky.site": CycleSite(); return true;
                case "room.passthrough": SetRoom(1f); return true;
                case "room.space": SetRoom(0f); return true;
                case "room.cmbBackdrop": _options.CmbBackdrop = !_options.CmbBackdrop; return true;
                case "view.clean": _options.CleanView = !_options.CleanView; _options.NotifyChanged(); return true;
                case "view.smoothTurn": _options.SmoothTurn = !_options.SmoothTurn; _options.NotifyChanged(); return true;
                case "view.freeTilt": _options.FreeTilt = !_options.FreeTilt; _options.NotifyChanged(); return true;
                case "view.recentre": _recentre?.Invoke(); return true;
                case "view.handedness":
                    _options.Handedness = _options.Handedness == Handedness.Right ? Handedness.Left : Handedness.Right;
                    _options.NotifyChanged();
                    return true;
            }

            if (Starts(id, "units.", out string unit) && System.Enum.TryParse(unit, out DistanceUnit parsedUnit))
            { _options.MeasureUnit = parsedUnit; return true; }

            if (Starts(id, "sizes.", out string sizes) && System.Enum.TryParse(sizes, out SizePreset preset))
            { _options.Sizes = preset; return true; }

            if (Starts(id, "sky.vantage.", out string vantage) && System.Enum.TryParse(vantage, out SkyVantage parsedVantage))
            { _options.Vantage = parsedVantage; return true; }

            if (Starts(id, "plane.", out string plane))
            {
                if (plane == "Auto") _options.SetPinnedPlane(null);
                else if (System.Enum.TryParse(plane, out PresentationPlane parsedPlane))
                    _options.SetPinnedPlane(parsedPlane);
                return true;
            }

            return false;
        }

        private void SetRoom(float visibility)
        {
            _options.RoomVisibility = visibility;
            _rig?.SetPassthrough(visibility > 0.5f);
        }

        private void CycleSite()
        {
            for (int i = 0; i < ObservingSites.All.Length; i++)
            {
                if (ObservingSites.All[i].Id != _options.Site.Id) continue;
                _options.Site = ObservingSites.All[(i + 1) % ObservingSites.All.Length];
                return;
            }
            _options.Site = ObservingSites.Default;
        }

        /// <summary>Drag a slider to a fraction of its range.</summary>
        public void SetSlider(string id, float t)
        {
            if (string.IsNullOrEmpty(id) || _options == null) return;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);

            if (id == "room.visibility") { SetRoom(t); Rebuild(); return; }
            if (id == "sky.limit") { _options.LimitMagnitude = 3.0 + 11.0 * t; Rebuild(); return; }

            if (!Starts(id, "param.", out string rest)) return;
            int split = rest.IndexOf('.');
            if (split <= 0) return;

            string layerId = rest.Substring(0, split);
            string paramId = rest.Substring(split + 1);
            LayerDef def = LayerTable.Find(layerId);
            if (def?.Params == null) return;

            for (int i = 0; i < def.Params.Length; i++)
            {
                if (def.Params[i].Id != paramId) continue;
                _options.SetLayerParam(layerId, paramId,
                    def.Params[i].Min + t * (def.Params[i].Max - def.Params[i].Min));
                Rebuild();
                return;
            }
        }

        private static bool Starts(string id, string prefix, out string rest)
        {
            if (id.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                rest = id.Substring(prefix.Length);
                return true;
            }
            rest = null;
            return false;
        }

        /// <summary>How many laid-out rows there are, counting wrapped cells as one row each.</summary>
        public int RowCountAsLaidOut()
        {
            int rows = 0;
            int span = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Span >= 4 || span + _rows[i].Span > 4) { rows++; span = _rows[i].Span >= 4 ? 0 : _rows[i].Span; }
                else span += _rows[i].Span;
            }
            if (span > 0) rows++;
            return rows;
        }
    }
}
