using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The one thing that moves the view: Travel, Follow and Free.
    ///
    /// There were three unrelated travel mechanisms — the dashboard's teleport plus
    /// <c>FrameBodyAtSize</c>, the desktop navigator's <c>TravelTo</c>, and the harness's
    /// <c>FlyToStar</c> — so "go there" meant three different motions and only one of them changed
    /// scale. The panel, the footer, the A button, a double click, a rung tap and the harness all
    /// call <see cref="Travel"/> now.
    ///
    /// The three modes are exclusive states of one writer, which is why they are one component:
    /// every one of them ends in a write to <c>FloatingOrigin.OriginKm</c>, and two writers of one
    /// value is how the 1.9.0 harness ended up reporting "no star within range" from a viewpoint
    /// that had never moved.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    [AddComponentMenu("CosmoSynth/Travel Controller")]
    public sealed class TravelController : MonoBehaviour
    {
        /// <summary>Free: fixed in ICRF. Follow: co-moving with a body. Travelling: playing a plan.</summary>
        public enum ViewMode { Free, Follow, Travelling }

        private UniverseSimulation _simulation;
        private ViewOptions _options;
        private DestinationIndex _destinations;
        private Transform _anchor;
        private Transform _host;
        private Camera _camera;

        private TravelPlan _plan;
        private Destination _target;
        private double _elapsed;

        private string _followingId;
        private Double3 _followOffsetKm;
        private Double3 _lastWrittenOriginKm;
        private bool _haveWritten;

        private ArrivalMarker _marker;

        public ViewMode Mode { get; private set; } = ViewMode.Free;

        /// <summary>The body Follow is tracking, or null.</summary>
        public string FollowingId => _followingId;

        public Destination Target => _target;

        /// <summary>The rung the flight is passing through, for the caption under the header.</summary>
        public string PassingRungCaption { get; private set; }

        /// <summary>One line the panel shows and clears: "Voyager 1 is outside its known window".</summary>
        public string Notice { get; private set; }

        public void Bind(UniverseSimulation simulation, ViewOptions options, DestinationIndex destinations,
            Transform anchor, Transform host, Camera camera)
        {
            _simulation = simulation;
            _options = options;
            _destinations = destinations;
            _anchor = anchor;
            _host = host;
            _camera = camera;
        }

        // ---- travel ----------------------------------------------------------

        /// <summary>Fly to a destination. Returns false only when it cannot be located at all.</summary>
        public bool Travel(Destination destination)
        {
            if (destination == null || _simulation == null) return false;

            Notice = null;
            Double3 targetKm = ResolveCentreKm(destination, out double unitsPerKm, out PresentationPlane plane);
            if (!targetKm.IsFinite) return false;

            var from = new ViewState(_simulation.Origin.OriginKm,
                MathD.SafeLog10(_simulation.Origin.UnitsPerKm), CurrentYawRad(), PlaneUp(_options.ActivePlane));

            PresentationPlane wanted = _options.PinnedPlane ?? plane;
            Double3 targetUp = PlaneUp(wanted);

            var to = new ViewState(targetKm, MathD.SafeLog10(ScaleLimits.Clamp(unitsPerKm)),
                YawFacing(targetKm, targetUp), targetUp);

            _plan = TravelPlan.Create(from, to);
            _target = destination;
            _elapsed = 0.0;
            Mode = ViewMode.Travelling;
            _destinations?.NoteVisited(destination);
            _options.ActivePlane = wanted;
            return true;
        }

        /// <summary>Fly to a rung by id, which is what the GO ladder and <c>rung:&lt;id&gt;</c> do.</summary>
        public bool TravelToRung(string rungId)
        {
            Destination destination = _destinations?.Find("rung:" + rungId);
            return destination != null && Travel(destination);
        }

        /// <summary>
        /// Stop a flight where it is.
        ///
        /// Any grab, stick zoom, stick flight, rung tap or GO cancels (§5.7.1 step 5). The view
        /// stays put rather than snapping: a travel the user interrupted has already shown them
        /// something they wanted to look at.
        /// </summary>
        public void Cancel(string why)
        {
            if (Mode != ViewMode.Travelling) return;
            _plan = null;
            Mode = _followingId != null ? ViewMode.Follow : ViewMode.Free;
            PassingRungCaption = null;
            Notice = why;
        }

        // ---- follow ----------------------------------------------------------

        public void SetFollow(string bodyId)
        {
            if (string.IsNullOrEmpty(bodyId) || _simulation == null) { SetFree(); return; }
            if (!_simulation.Views.TryGetValue(bodyId, out CelestialBodyView view)) { SetFree(); return; }

            _followingId = bodyId;
            _followOffsetKm = _simulation.Origin.OriginKm - view.AbsolutePositionKm;
            if (Mode != ViewMode.Travelling) Mode = ViewMode.Follow;
        }

        public void SetFree()
        {
            _followingId = null;
            if (Mode == ViewMode.Follow) Mode = ViewMode.Free;
        }

        public void ToggleFollow(string bodyId)
        {
            if (_followingId == bodyId) SetFree();
            else SetFollow(bodyId);
        }

        // ---- per frame -------------------------------------------------------

        private void Update()
        {
            if (_simulation == null || _simulation.Origin == null) return;

            // Somebody else moved the view: a grab, a stick, a rung tap. Re-capture the Follow
            // offset from where they left it, so the user can walk round a followed planet and
            // stay there. Detecting it here rather than being told means no other component has
            // to know Follow exists.
            if (_haveWritten && _simulation.Origin.OriginKm != _lastWrittenOriginKm && Mode == ViewMode.Follow)
            {
                RecaptureFollowOffset();
            }

            if (Mode == ViewMode.Travelling) StepTravel();
            else if (Mode == ViewMode.Follow) StepFollow();

            _lastWrittenOriginKm = _simulation.Origin.OriginKm;
            _haveWritten = true;

            _marker?.Tick(Time.unscaledDeltaTime);
        }

        private void StepTravel()
        {
            if (_plan == null) { Mode = ViewMode.Free; return; }

            _elapsed += Time.unscaledDeltaTime;
            Double3 targetKm = _destinations != null ? _destinations.PositionKm(_target) : _plan.To.FocusKm;
            if (_target != null && _target.IsMoving && !targetKm.IsFinite) targetKm = _plan.To.FocusKm;

            ViewState state = _plan.At(_elapsed, targetKm);
            Apply(state);

            Rung passing = LayerTable.NearestRung(state.Log10U);
            PassingRungCaption = passing != null ? passing.Caption : null;

            if (_elapsed < _plan.DurationSeconds) return;

            // Arrival. The marker appears HERE, not on departure: a crosshair drawn at the start
            // of a nine-second flight has faded by the time the viewer gets there, which is
            // exactly backwards.
            Arrive(state, targetKm);
        }

        private void Arrive(ViewState state, Double3 targetKm)
        {
            _plan = null;
            PassingRungCaption = null;

            bool follow = FollowOnArrival(_target);
            if (follow && _target != null && _target.IsMoving) SetFollow(_target.BodyId);
            else { _followingId = null; Mode = ViewMode.Free; }

            if (Mode != ViewMode.Follow) Mode = ViewMode.Free;

            EnsureMarker();
            _marker?.ShowAt(_host, _simulation.Origin, targetKm);
        }

        private bool FollowOnArrival(Destination destination)
        {
            if (destination == null) return false;
            if (destination.Kind == DestinationKind.Rung)
            {
                Rung rung = LayerTable.FindRung(destination.Id.Substring("rung:".Length));
                return rung != null && rung.FollowOnArrival;
            }
            return destination.IsMoving;
        }

        private void StepFollow()
        {
            if (_followingId == null || !_simulation.Views.TryGetValue(_followingId, out CelestialBodyView view))
            {
                SetFree();
                return;
            }

            // A spacecraft outside its window has no position to follow. Say so once and release,
            // rather than following the interpolator's clamped last sample for ever.
            if (!view.IsKnown)
            {
                Notice = view.Body.Name + " is outside its known window: now free";
                SetFree();
                return;
            }

            _simulation.Origin.SetOrigin(view.AbsolutePositionKm + _followOffsetKm);
        }

        private void RecaptureFollowOffset()
        {
            if (_followingId == null) return;
            if (!_simulation.Views.TryGetValue(_followingId, out CelestialBodyView view)) return;
            _followOffsetKm = _simulation.Origin.OriginKm - view.AbsolutePositionKm;
        }

        private void Apply(in ViewState state)
        {
            _simulation.Origin.SetOrigin(state.FocusKm);
            _simulation.Origin.SetUnitsPerKm(System.Math.Pow(10.0, state.Log10U));

            if (_anchor != null)
                _anchor.localRotation = Quaternion.AngleAxis((float)(state.YawRad * MathD.Rad2Deg), Vector3.up);

            ApplyPlaneUp(state.PlaneUpIcrf);
        }

        /// <summary>
        /// Lay an ICRF direction flat on room up, by the minimal rotation that does it (§5.2).
        ///
        /// Written on the Universe host, which is the one transform between the room anchor and
        /// the bodies, so nothing else has to know the plane exists.
        /// </summary>
        private void ApplyPlaneUp(Double3 upIcrf)
        {
            if (_host == null || _simulation?.Origin == null) return;
            Vector3 mapped = _simulation.Origin.MapDirection(upIcrf);
            if (mapped.sqrMagnitude < 1e-8f) return;
            _host.localRotation = Quaternion.FromToRotation(mapped.normalized, Vector3.up);
        }

        private Double3 PlaneUp(PresentationPlane plane)
        {
            var context = new PlaneContext(
                _options != null ? _options.Site.LatitudeDeg : 0.0,
                _options != null ? _options.Site.LongitudeDeg : 0.0,
                _simulation != null ? _simulation.Origin.OriginKm : Double3.Zero,
                _simulation != null && _simulation.FocusBody != null ? _simulation.FocusBody.Body.Id : null,
                _simulation != null && _simulation.Clock != null ? _simulation.Clock.Now : CosmicTime.J2000);
            return Planes.Up(plane, context);
        }

        private double CurrentYawRad() =>
            _anchor != null ? _anchor.localRotation.eulerAngles.y * MathD.Deg2Rad : 0.0;

        /// <summary>
        /// The anchor yaw that puts the direction of travel along the viewer's own forward, so the
        /// dip shows them where they are going rather than an arbitrary side of the scene.
        /// </summary>
        private double YawFacing(Double3 targetKm, Double3 targetUp)
        {
            if (_camera == null || _simulation?.Origin == null || _anchor == null) return CurrentYawRad();

            Double3 travel = targetKm - _simulation.Origin.OriginKm;
            if (travel.SqrMagnitude <= 0.0) return CurrentYawRad();

            Vector3 mappedUp = _simulation.Origin.MapDirection(targetUp);
            Quaternion levelling = mappedUp.sqrMagnitude > 1e-8f
                ? Quaternion.FromToRotation(mappedUp.normalized, Vector3.up)
                : Quaternion.identity;

            // The travel direction as it will be drawn with zero anchor yaw.
            Vector3 sceneDirection = levelling * _simulation.Origin.MapDirection(travel.Normalized);
            sceneDirection.y = 0f;
            if (sceneDirection.sqrMagnitude < 1e-8f) return CurrentYawRad();

            Vector3 forward = _anchor.parent != null
                ? _anchor.parent.InverseTransformDirection(_camera.transform.forward)
                : _camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-8f) return CurrentYawRad();

            return Vector3.SignedAngle(sceneDirection.normalized, forward.normalized, Vector3.up) * MathD.Deg2Rad;
        }

        // ---- where a destination actually is ---------------------------------

        /// <summary>
        /// The absolute point a destination centres on, the scale that frames it and its plane.
        ///
        /// Rungs 2-4 are relative: their tabled U is Earth's, and any other focus recomputes
        /// <c>U = room / span(focus)</c>, so "Planet in hand" around Jupiter is Jupiter in hand.
        /// </summary>
        private Double3 ResolveCentreKm(Destination destination, out double unitsPerKm, out PresentationPlane plane)
        {
            unitsPerKm = destination.FramingUnitsPerKm;
            plane = destination.Plane ?? _options.ActivePlane;

            if (destination.Kind != DestinationKind.Rung)
            {
                plane = destination.Plane ?? PlaneForScale(MathD.SafeLog10(unitsPerKm));
                return _destinations.PositionKm(destination);
            }

            Rung rung = LayerTable.FindRung(destination.Id.Substring("rung:".Length));
            if (rung == null) return _destinations.PositionKm(destination);

            plane = rung.Plane;
            unitsPerKm = ScaleLimits.Clamp(rung.UnitsPerKm);

            CosmicTime now = _simulation.Clock != null ? _simulation.Clock.Now : CosmicTime.J2000;

            switch (rung.CentreKind)
            {
                case "site":
                {
                    if (!TryBody("earth", out CelestialBodyView earth)) break;
                    double radius = earth.Body.EquatorialRadiusKm ?? Constants.EarthEquatorialRadius;
                    Double3 zenith = Planes.Zenith(_options.Site.LatitudeDeg, _options.Site.LongitudeDeg, now);
                    return earth.AbsolutePositionKm + zenith * radius;
                }
                case "orbit":
                {
                    if (!TryBody(rung.CentreId, out CelestialBodyView host)) break;
                    double radius = host.Body.EquatorialRadiusKm ?? Constants.EarthEquatorialRadius;
                    Double3 toSun = TryBody("sun", out CelestialBodyView sun)
                        ? (sun.AbsolutePositionKm - host.AbsolutePositionKm).Normalized
                        : Double3.UnitX;
                    return host.AbsolutePositionKm + toSun * (radius + 420.0);
                }
                case "focusPlanet":
                {
                    CelestialBodyView focus = PlanetFocus();
                    if (focus == null) break;
                    double radius = _simulation.DisplayRadiusKm(focus.Body);
                    if (radius > 0.0) unitsPerKm = ScaleLimits.Clamp(rung.SpanMetres / (2.0 * radius));
                    return focus.AbsolutePositionKm;
                }
                case "focusMoons":
                {
                    CelestialBodyView focus = PlanetFocus();
                    if (focus == null) break;
                    double outermost = OutermostMoonKm(focus.Body.Id);
                    if (outermost > 0.0) unitsPerKm = ScaleLimits.Clamp(rung.SpanMetres / (2.0 * outermost));
                    return focus.AbsolutePositionKm;
                }
                case "hostStar":
                {
                    CelestialBody star = _simulation.FocusBody != null
                        ? _simulation.HostStarOf(_simulation.FocusBody.Body.Id)
                        : null;
                    string id = star != null ? star.Id : "sun";
                    if (TryBody(id, out CelestialBodyView view)) return view.AbsolutePositionKm;
                    break;
                }
                case "body":
                {
                    if (TryBody(rung.CentreId, out CelestialBodyView view)) return view.AbsolutePositionKm;
                    break;
                }
                case "galacticCentre":
                case "milkyWay":
                    return GalacticCentreKm();
                case "milkyWayM31Midpoint":
                {
                    Destination m31 = FindGalaxy("m31");
                    if (m31 == null)
                    {
                        // Honest degradation: M31's position is the `galaxies` lane's data. Until
                        // it ships, Local Group frames the Galactic Centre and says so.
                        Notice = "Local Group is framed on the Galactic Centre: no galaxy catalogue in this build";
                        return GalacticCentreKm();
                    }
                    return (GalacticCentreKm() + _destinations.PositionKm(m31)) * 0.5;
                }
            }

            // No centre available: stay where we are and change scale only, which is still the
            // honest answer to "show me this rung".
            return _simulation.Origin.OriginKm;
        }

        /// <summary>PLANE: Auto takes the plane of the rung nearest the arrival scale (§5.2 rule 3).</summary>
        private PresentationPlane PlaneForScale(double log10U)
        {
            Rung nearest = LayerTable.NearestRung(log10U);
            return nearest != null ? nearest.Plane : _options.ActivePlane;
        }

        private bool TryBody(string id, out CelestialBodyView view)
        {
            view = null;
            return !string.IsNullOrEmpty(id) && _simulation.Views.TryGetValue(id, out view);
        }

        private CelestialBodyView PlanetFocus()
        {
            CelestialBodyView focus = _simulation.FocusBody;
            if (focus != null && (focus.Body.Type == BodyType.Planet || focus.Body.Type == BodyType.Moon ||
                                  focus.Body.Type == BodyType.DwarfPlanet)) return focus;
            return TryBody("earth", out CelestialBodyView earth) ? earth : focus;
        }

        private double OutermostMoonKm(string bodyId)
        {
            if (_simulation.Catalog == null) return 0.0;
            double outermost = 0.0;
            foreach (string childId in _simulation.Catalog.ChildIdsOf(bodyId))
            {
                CelestialBody child = _simulation.Catalog.Get(childId);
                if (child == null || child.Type != BodyType.Moon) continue;
                if (child.Orbit == null || !child.Orbit.HasValue) continue;
                double a = child.Orbit.Value.SemiMajorAxisKm;
                if (a > outermost) outermost = a;
            }
            return outermost;
        }

        /// <summary>
        /// Sgr A*: 8.15 kpc from the Sun toward galactic (l, b) = (0, 0). R0 is Reid 2019, which is
        /// the same constant the <c>sunOrbit</c> guide ring uses.
        /// </summary>
        private Double3 GalacticCentreKm()
        {
            Double3 toCentre = StandardFrames.GalacticToEquatorial * Double3.UnitX;
            Double3 sunKm = TryBody("sun", out CelestialBodyView sun) ? sun.AbsolutePositionKm : Double3.Zero;
            return sunKm + toCentre * (8.15 * Constants.KiloParsec);
        }

        private Destination FindGalaxy(string idFragment)
        {
            if (_destinations == null) return null;
            var all = _destinations.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Kind != DestinationKind.Galaxy) continue;
                if (all[i].Id.IndexOf(idFragment, System.StringComparison.OrdinalIgnoreCase) >= 0) return all[i];
            }
            return null;
        }

        private void EnsureMarker()
        {
            if (_marker != null) return;
            _marker = new ArrivalMarker(_host != null ? _host : transform);
        }

        /// <summary>Diagnostics for the play-mode report.</summary>
        public string Describe()
        {
            string where = _target != null ? _target.Label : "(none)";
            string progress = Mode == ViewMode.Travelling && _plan != null
                ? $" {_elapsed:F1}/{_plan.DurationSeconds:F1} s {(_plan.UsesDip ? "dip" : "direct")}"
                : string.Empty;
            return $"mode={Mode} target={where}{progress} follow={_followingId ?? "-"} " +
                   $"marker={(_marker != null ? _marker.Describe() : "-")}";
        }

        private void OnDestroy() => _marker?.Destroy();
    }
}
