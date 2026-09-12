using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>
    /// A collection of celestial bodies with their parent/child structure, plus the machinery
    /// to turn that structure into a live <see cref="ReferenceFrame"/> tree.
    ///
    /// The catalogue is the boundary between imported data and the running simulation. Both
    /// real archives and the procedural and AI generators produce catalogues; nothing
    /// downstream needs to know which, except by reading the provenance.
    /// </summary>
    public sealed class CelestialCatalog
    {
        private readonly Dictionary<string, CelestialBody> _byId = new Dictionary<string, CelestialBody>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _childIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _rootIds = new List<string>();

        /// <summary>
        /// Ids in the order they were added, which is the order the source documents list them.
        ///
        /// <see cref="RootIds"/> is built from this rather than from the dictionary, so that
        /// "RootIds is in insertion order" — which three comments in this codebase, CONTRACT C10
        /// and <c>CatalogAssembler</c>'s whole reason for existing all assert — is actually true.
        /// It was not: <see cref="Rebuild"/> sorted the roots ordinally, so RootIds[0] was
        /// whichever root sorted first alphabetically. With the exoplanets merged that is an
        /// `exo-*` id ahead of `sun`, which is exactly the failure the assembler was written to
        /// prevent, surviving inside the type it was supposed to be prevented in.
        ///
        /// A Dictionary enumerates in insertion order only while nothing is removed, and
        /// <see cref="Remove"/> exists — a removal frees a slot that the next insert reuses. So
        /// the order is tracked rather than inferred.
        /// </summary>
        private readonly List<string> _order = new List<string>();

        /// <summary>Free-text name of this catalogue, e.g. "Solar System (JPL DE440)".</summary>
        public string Name { get; set; } = "Untitled catalogue";

        /// <summary>Where the data came from, for the provenance panel.</summary>
        public string Source { get; set; }

        /// <summary>ISO-8601 date this catalogue was generated.</summary>
        public string GeneratedUtc { get; set; }

        public int Count => _byId.Count;
        public IEnumerable<CelestialBody> Bodies => _byId.Values;
        public IReadOnlyList<string> RootIds => _rootIds;

        // ---- Population ------------------------------------------------------

        /// <summary>
        /// Add a body. Parent links are resolved lazily by <see cref="Rebuild"/>, so bodies may
        /// be added in any order — which matters because importers naturally emit moons before
        /// or after their planets depending on the source's own ordering.
        /// </summary>
        public void Add(CelestialBody body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (string.IsNullOrWhiteSpace(body.Id)) throw new ArgumentException("Body needs an Id.", nameof(body));
            if (_byId.ContainsKey(body.Id))
                throw new ArgumentException($"Catalogue already contains a body with id '{body.Id}'.", nameof(body));

            _byId[body.Id] = body;
            _order.Add(body.Id);
        }

        public bool Remove(string id)
        {
            if (id == null || !_byId.Remove(id)) return false;
            for (int i = 0; i < _order.Count; i++)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(_order[i], id)) continue;
                _order.RemoveAt(i);
                break;
            }
            return true;
        }

        /// <summary>
        /// Absorb another catalogue's bodies into this one.
        ///
        /// Used to put the exoplanets and the Solar System in one frame tree, which they must
        /// share: both are positioned in ICRF about the solar system barycentre, so a distance
        /// from Earth to Proxima Centauri b only means anything if they are in the same tree.
        ///
        /// Id collisions are skipped rather than overwritten, and counted — the Solar System is
        /// loaded first and is the authority for anything that appears in both.
        /// </summary>
        public int MergeFrom(CelestialCatalog other)
        {
            if (other == null) return 0;

            int added = 0, collisions = 0;
            foreach (CelestialBody body in other.Bodies)
            {
                if (_byId.ContainsKey(body.Id)) { collisions++; continue; }
                _byId[body.Id] = body;
                // Not through Add(): this path deliberately skips the duplicate-id throw. It must
                // still keep _order, or a merged body is in the catalogue but in no tree at all —
                // Rebuild walks _order, so the spacecraft ended up with no reference frame.
                _order.Add(body.Id);
                added++;
            }

            if (collisions > 0)
            {
                Name = $"{Name} + {other.Name} ({collisions} duplicate ids skipped)";
            }
            else if (added > 0)
            {
                Name = $"{Name} + {other.Name}";
            }

            Rebuild();
            return added;
        }

        public bool Contains(string id) => id != null && _byId.ContainsKey(id);

        public CelestialBody Get(string id)
        {
            if (id != null && _byId.TryGetValue(id, out var body)) return body;
            return null;
        }

        public bool TryGet(string id, out CelestialBody body)
        {
            body = null;
            return id != null && _byId.TryGetValue(id, out body);
        }

        /// <summary>Look a body up by display name or by any of its cross-identifications.</summary>
        public CelestialBody FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            foreach (var body in _byId.Values)
            {
                if (string.Equals(body.Name, name, StringComparison.OrdinalIgnoreCase)) return body;
            }
            foreach (var body in _byId.Values)
            {
                foreach (string designation in body.Designations)
                {
                    if (string.Equals(designation, name, StringComparison.OrdinalIgnoreCase)) return body;
                }
            }
            return null;
        }

        /// <summary>Rebuild the parent/child index. Call after adding or removing bodies.</summary>
        public void Rebuild()
        {
            _childIds.Clear();
            _rootIds.Clear();

            foreach (string id in _order)
            {
                if (!_byId.TryGetValue(id, out var body)) continue;
                if (string.IsNullOrEmpty(body.ParentId))
                {
                    _rootIds.Add(body.Id);
                    continue;
                }

                if (!_byId.ContainsKey(body.ParentId))
                {
                    // A dangling parent reference makes the body a root rather than dropping
                    // it: losing an object silently is worse than showing it in the wrong place,
                    // and Validate() reports the inconsistency either way.
                    _rootIds.Add(body.Id);
                    continue;
                }

                if (!_childIds.TryGetValue(body.ParentId, out var children))
                {
                    children = new List<string>();
                    _childIds[body.ParentId] = children;
                }
                children.Add(body.Id);
            }

            // Roots are NOT sorted: RootIds[0] is the first root of the first document, which
            // is what UniverseSimulation.SetFocus, CosmoSynthBootstrap and UniverseNavigator all
            // read it as. Children still are, because nothing claims an order for them and a
            // stable one is worth having.
            foreach (var children in _childIds.Values) children.Sort(StringComparer.Ordinal);
        }

        /// <summary>
        /// The star that lights a body: the first star on its chain of parents.
        ///
        /// Not <c>RootIds[0]</c>, which is what the renderer used to ask. RootIds names the roots
        /// of the merged tree, so the moment a second star system merged in, the light that
        /// models the entire Solar System was attached to whichever root came first —
        /// 61 Vir, 11 parsecs away — and every planet went dark while the build reported success.
        ///
        /// A star returns itself. A body with no star above it (a rogue planet, an unparented
        /// spacecraft) returns null, and the caller draws it unlit rather than guessing. The hop
        /// limit is a cycle guard: a hand-edited catalogue can name its own ancestor, and an
        /// unbounded walk would hang the frame rather than reporting a bad file.
        /// </summary>
        public CelestialBody HostStarOf(string id)
        {
            const int maximumHops = 32;

            CelestialBody body = Get(id);
            for (int hop = 0; body != null && hop < maximumHops; hop++)
            {
                if (body.IsStar) return body;
                if (string.IsNullOrEmpty(body.ParentId)) return null;
                body = Get(body.ParentId);
            }
            return null;
        }

        public IReadOnlyList<string> ChildIdsOf(string id) =>
            _childIds.TryGetValue(id ?? string.Empty, out var children) ? children : (IReadOnlyList<string>)Array.Empty<string>();

        public IEnumerable<CelestialBody> ChildrenOf(string id)
        {
            foreach (string childId in ChildIdsOf(id))
            {
                if (_byId.TryGetValue(childId, out var child)) yield return child;
            }
        }

        /// <summary>Walk the hierarchy depth-first from the roots.</summary>
        public IEnumerable<CelestialBody> Traverse()
        {
            foreach (string rootId in _rootIds)
            {
                foreach (var body in TraverseFrom(rootId)) yield return body;
            }
        }

        private IEnumerable<CelestialBody> TraverseFrom(string id)
        {
            if (!_byId.TryGetValue(id, out var body)) yield break;
            yield return body;
            foreach (string childId in ChildIdsOf(id))
            {
                foreach (var descendant in TraverseFrom(childId)) yield return descendant;
            }
        }

        // ---- Frame construction ----------------------------------------------

        /// <summary>
        /// Build a live reference-frame tree mirroring the catalogue's hierarchy.
        ///
        /// Each body becomes a frame whose origin follows its orbit, so a query for "where is
        /// the lander relative to Titan" resolves through Titan's frame and never forms a
        /// heliocentric coordinate at all.
        /// </summary>
        public ReferenceFrame BuildFrameTree(string rootFrameId = "ssb")
        {
            var root = ReferenceFrame.CreateRoot(rootFrameId);
            var frames = new Dictionary<string, ReferenceFrame>(StringComparer.OrdinalIgnoreCase);

            foreach (string id in _rootIds) AttachFrame(id, root, frames);
            return root;
        }

        private void AttachFrame(string id, ReferenceFrame parentFrame, Dictionary<string, ReferenceFrame> frames)
        {
            if (!_byId.TryGetValue(id, out var body)) return;

            var frame = parentFrame.CreateChild(body.Id, EphemerisFor(body));
            frames[body.Id] = frame;

            foreach (string childId in ChildIdsOf(id)) AttachFrame(childId, frame, frames);
        }

        /// <summary>
        /// Choose how a body's origin moves relative to its parent.
        ///
        /// The order is by strength of evidence, not convenience. A sampled trajectory is
        /// measured state vectors and beats elements fitted to them; a surface site is exact;
        /// elements are a model; a fixed position is a body that does not move in this
        /// catalogue at all.
        /// </summary>
        private IEphemerisSource EphemerisFor(CelestialBody body)
        {
            if (body.Trajectory != null) return body.Trajectory.CreateEphemeris();

            if (body.Site.HasValue)
            {
                // The site is expressed on the *parent's* ellipsoid and carried by the
                // parent's rotation, so both come from the parent, not from the lander.
                CelestialBody host = Get(body.ParentId);
                if (host != null && host.Orientation.HasValue)
                {
                    double equatorial = host.EquatorialRadiusKm ?? 0.0;
                    double polar = host.PolarRadiusKm ?? equatorial;
                    return new SurfaceFixedEphemeris(
                        host.Orientation.Value,
                        body.Site.Value.BodyFixedPositionKm(equatorial, polar));
                }

                // Without the host's rotation model there is no way to turn a latitude into a
                // position, and every remaining option puts the lander at the parent's centre —
                // buried at the core, drawn inside the planet, and indistinguishable from a
                // model that failed to load. Nothing here can fix that, so the only useful
                // thing to do is make sure it is reported: Validate() checks for exactly this
                // case. The earlier comment claimed it already did, and it did not — it
                // reported a missing *parent*, which is a different fault.
            }

            if (body.Orbit.HasValue) return new KeplerEphemeris(body.Orbit.Value);

            return new FixedEphemeris(body.PositionKm ?? Double3.Zero);
        }

        // ---- Validation ------------------------------------------------------

        /// <summary>
        /// Validate every body plus the relationships between them. Returns one message per
        /// problem, prefixed with the offending body's id.
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            foreach (var body in _byId.Values)
            {
                foreach (string problem in body.Validate()) problems.Add($"{body.Id}: {problem}");

                if (!string.IsNullOrEmpty(body.ParentId) && !_byId.ContainsKey(body.ParentId))
                    problems.Add($"{body.Id}: parent '{body.ParentId}' is not in the catalogue.");

                // A landing site is a latitude and a longitude, which are meaningless without
                // the rotation model and the ellipsoid they are quoted against — and both
                // belong to the host, not to the lander. Missing either, EphemerisFor has no
                // choice but to leave the body at its parent's centre, buried inside it. That
                // is not a difference anyone can see, so it has to be said here.
                if (body.Site.HasValue && _byId.TryGetValue(body.ParentId ?? string.Empty, out var ground))
                {
                    if (!ground.Orientation.HasValue)
                    {
                        problems.Add(
                            $"{body.Id}: sits on '{ground.Id}', which has no rotation model, so its " +
                            "latitude and longitude cannot be turned into a position and it would be " +
                            "placed at the centre of the body it is standing on.");
                    }
                    if (!ground.EquatorialRadiusKm.HasValue)
                    {
                        problems.Add(
                            $"{body.Id}: sits on '{ground.Id}', whose radius is unknown, so there is no " +
                            "surface to put it on.");
                    }
                }

                // A body's orbit must be governed by its parent's mass. Getting this wrong is
                // easy when merging sources and produces an orbit that looks right but runs at
                // the wrong rate, which is far harder to notice than an obviously wrong shape.
                if (body.Orbit.HasValue && _byId.TryGetValue(body.ParentId ?? string.Empty, out var parent))
                {
                    double expected = parent.GravitationalParameter + body.GravitationalParameter;
                    double actual = body.Orbit.Value.GravitationalParameter;
                    if (expected > 0.0 && System.Math.Abs(actual - expected) / expected > 1e-3)
                    {
                        problems.Add(
                            $"{body.Id}: orbit uses GM {actual:G8} but parent '{parent.Id}' plus this body gives " +
                            $"{expected:G8} (a {System.Math.Abs(actual - expected) / expected:P2} discrepancy).");
                    }
                }

                // A moon outside its planet's Hill sphere is not bound to it.
                if (body.Type == BodyType.Moon && body.Orbit.HasValue &&
                    _byId.TryGetValue(body.ParentId ?? string.Empty, out var host) && host.Orbit.HasValue)
                {
                    double hill = Constants.HillRadius(
                        host.GravitationalParameter,
                        host.Orbit.Value.GravitationalParameter - host.GravitationalParameter,
                        host.Orbit.Value.SemiMajorAxisKm,
                        host.Orbit.Value.Eccentricity);

                    if (hill > 0.0 && body.Orbit.Value.ApoapsisKm > hill)
                    {
                        problems.Add(
                            $"{body.Id}: apoapsis {body.Orbit.Value.ApoapsisKm:G6} km lies outside " +
                            $"{host.Id}'s Hill radius of {hill:G6} km, so the orbit is not stable.");
                    }
                }

                // A body orbiting inside its parent's surface is a data error, not a scenario.
                if (body.Orbit.HasValue && _byId.TryGetValue(body.ParentId ?? string.Empty, out var primary) &&
                    primary.EquatorialRadiusKm.HasValue)
                {
                    double surface = SurfaceRadiusUnderPeriapsis(body.Orbit.Value, primary);
                    if (body.Orbit.Value.PeriapsisKm < surface)
                    {
                        problems.Add(
                            $"{body.Id}: periapsis {body.Orbit.Value.PeriapsisKm:G6} km is inside {primary.Id}'s " +
                            $"surface, which is {surface:G6} km under the point periapsis occurs over.");
                    }
                }
            }

            // Cycles would make the frame tree infinite.
            foreach (var body in _byId.Values)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var current = body;
                while (current != null && !string.IsNullOrEmpty(current.ParentId))
                {
                    if (!seen.Add(current.Id))
                    {
                        problems.Add($"{body.Id}: parent chain contains a cycle.");
                        break;
                    }
                    _byId.TryGetValue(current.ParentId, out current);
                }
            }

            return problems;
        }

        /// <summary>
        /// Radius of <paramref name="primary"/>'s surface directly under an orbit's periapsis.
        ///
        /// The comparison this feeds used to be against the equatorial radius, which is only the
        /// right question for an equatorial orbit. A flattened planet's surface is thousands of
        /// kilometres closer to its centre at the poles — Jupiter is 71 492 km across the
        /// equator and 66 854 km through the poles — so a polar orbiter can pass well clear of
        /// the cloud tops at a radius that is "inside" the equatorial figure.
        ///
        /// Juno is the case that found it. Its perijove radius is 71 491 km, one kilometre under
        /// Jupiter's equatorial radius and comfortably outside the planet, because perijove
        /// occurs at high northern latitude where the surface is far lower. The old test failed
        /// it the moment Jupiter stopped being modelled as a sphere — the check was wrong, not
        /// the data, and the fix is to ask where periapsis actually is rather than to widen a
        /// bound until the complaint stops.
        ///
        /// Needs the primary's pole to know what latitude means. Without one, the polar radius
        /// is the conservative answer: it is the smallest the body ever is, so nothing correct
        /// is ever reported, at the cost of missing an equatorial orbit that grazes.
        /// </summary>
        private static double SurfaceRadiusUnderPeriapsis(OrbitalElements orbit, CelestialBody primary)
        {
            double equatorial = primary.EquatorialRadiusKm ?? 0.0;
            double polar = primary.PolarRadiusKm ?? equatorial;
            if (polar >= equatorial || !primary.Orientation.HasValue) return polar;

            // Latitude of the periapsis direction, measured from the primary's own equator.
            // The pole drifts by a tenth of a degree per century, so any epoch will do.
            Double3 pole = primary.Orientation.Value.PoleDirectionAt(new CosmicTime(0.0));
            Double3 direction = orbit.PeriapsisDirection;
            if (pole.SqrMagnitude <= 0.0 || direction.SqrMagnitude <= 0.0) return polar;

            double sinLatitude = MathD.Clamp(
                Double3.Dot(pole.Normalized, direction.Normalized), -1.0, 1.0);
            double cosLatitude = System.Math.Sqrt(System.Math.Max(0.0, 1.0 - sinLatitude * sinLatitude));

            // Radius of the reference ellipsoid along that direction.
            double horizontal = cosLatitude / equatorial, vertical = sinLatitude / polar;
            return 1.0 / System.Math.Sqrt(horizontal * horizontal + vertical * vertical);
        }

        // ---- Serialisation ---------------------------------------------------

        public JsonValue ToJson()
        {
            var json = JsonValue.Object();
            json["name"] = JsonValue.String(Name);
            if (!string.IsNullOrEmpty(Source)) json["source"] = JsonValue.String(Source);
            if (!string.IsNullOrEmpty(GeneratedUtc)) json["generated"] = JsonValue.String(GeneratedUtc);

            var bodies = JsonValue.Array();
            foreach (var body in _byId.Values) bodies.Add(body.ToJson());
            json["bodies"] = bodies;

            return json;
        }

        public string ToJsonString(bool indented = true) => ToJson().ToString(indented);

        public static CelestialCatalog FromJson(JsonValue json)
        {
            if (json == null || !json.IsObject)
                throw new FormatException("A catalogue must be a JSON object.");

            var catalog = new CelestialCatalog
            {
                Name = json["name"].AsString("Untitled catalogue"),
                Source = json["source"].AsString(),
                GeneratedUtc = json["generated"].AsString()
            };

            var bodies = json["bodies"];
            if (!bodies.IsArray)
                throw new FormatException("A catalogue must contain a 'bodies' array.");

            foreach (var entry in bodies) catalog.Add(CelestialBody.FromJson(entry));

            catalog.Rebuild();
            return catalog;
        }

        public static CelestialCatalog Parse(string jsonText) => FromJson(JsonValue.Parse(jsonText));

        public override string ToString() => $"{Name} ({Count} bodies, {_rootIds.Count} roots)";
    }
}
