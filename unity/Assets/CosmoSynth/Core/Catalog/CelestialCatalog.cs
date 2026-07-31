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
        }

        public bool Remove(string id) => _byId.Remove(id);

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

            foreach (var body in _byId.Values)
            {
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

            _rootIds.Sort(StringComparer.Ordinal);
            foreach (var children in _childIds.Values) children.Sort(StringComparer.Ordinal);
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

            // A body with an orbit follows it; a root with a measured position sits there; a
            // root without one is the frame's origin.
            IEphemerisSource ephemeris = body.Orbit.HasValue
                ? (IEphemerisSource)new KeplerEphemeris(body.Orbit.Value)
                : new FixedEphemeris(body.PositionKm ?? Double3.Zero);

            var frame = parentFrame.CreateChild(body.Id, ephemeris);
            frames[body.Id] = frame;

            foreach (string childId in ChildIdsOf(id)) AttachFrame(childId, frame, frames);
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
                    primary.EquatorialRadiusKm.HasValue &&
                    body.Orbit.Value.PeriapsisKm < primary.EquatorialRadiusKm.Value)
                {
                    problems.Add(
                        $"{body.Id}: periapsis {body.Orbit.Value.PeriapsisKm:G6} km is inside {primary.Id}'s " +
                        $"radius of {primary.EquatorialRadiusKm.Value:G6} km.");
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
