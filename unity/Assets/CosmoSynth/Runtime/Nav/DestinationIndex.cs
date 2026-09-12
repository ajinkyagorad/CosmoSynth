using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Everywhere the viewer can go, assembled from the things that know: the nineteen rungs, the
    /// catalogue <b>by id</b>, and every registered layer's <c>CollectDestinations</c>.
    ///
    /// Nothing here is hand-listed. The dashboard it replaces kept an array of star names beside
    /// an array of catalogue indices, and when the catalogue was re-sorted the two stopped
    /// agreeing: "GJ 876" flew to GJ 1002 and reported success. An id cannot drift; an index into
    /// somebody else's list always will.
    ///
    /// A plain class. It is rebuilt when the catalogue changes and when a layer registers, and
    /// otherwise read.
    /// </summary>
    public sealed class DestinationIndex
    {
        private readonly UniverseSimulation _simulation;
        private readonly List<Destination> _all = new List<Destination>();
        private readonly List<Destination> _recent = new List<Destination>();
        private readonly Dictionary<string, Destination> _byId =
            new Dictionary<string, Destination>(System.StringComparer.Ordinal);

        private bool _dirty = true;

        /// <summary>How many destinations RECENT keeps (§5.6.5).</summary>
        public const int RecentCapacity = 8;

        public DestinationIndex(UniverseSimulation simulation)
        {
            _simulation = simulation;
        }

        public IReadOnlyList<Destination> All
        {
            get { if (_dirty) Refresh(); return _all; }
        }

        public IReadOnlyList<Destination> Recent => _recent;

        public void MarkDirty() => _dirty = true;

        public Destination Find(string id)
        {
            if (_dirty) Refresh();
            return id != null && _byId.TryGetValue(id, out Destination destination) ? destination : null;
        }

        /// <summary>Everything of one kind, nearest the viewer first.</summary>
        public List<Destination> OfKind(DestinationKind kind, Double3 viewerKm)
        {
            var result = new List<Destination>();
            IReadOnlyList<Destination> all = All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Kind == kind) result.Add(all[i]);

            result.Sort((a, b) =>
                DistanceKm(a, viewerKm).CompareTo(DistanceKm(b, viewerKm)));
            return result;
        }

        /// <summary>Distance from a point, reading the live position of anything that moves.</summary>
        public double DistanceKm(Destination destination, Double3 fromKm)
        {
            if (destination == null) return double.PositiveInfinity;
            return (PositionKm(destination) - fromKm).Magnitude;
        }

        /// <summary>
        /// Where a destination is <b>now</b>. A moving target is re-read from the catalogue every
        /// call, which is the whole reason <see cref="Destination.BodyId"/> exists.
        /// </summary>
        public Double3 PositionKm(Destination destination)
        {
            if (destination == null) return Double3.Zero;
            if (destination.IsMoving && _simulation != null &&
                _simulation.Views.TryGetValue(destination.BodyId, out CelestialBodyView view))
            {
                return view.AbsolutePositionKm;
            }
            return destination.PositionIcrfKm;
        }

        public void NoteVisited(Destination destination)
        {
            if (destination == null) return;
            _recent.RemoveAll(d => d.Id == destination.Id);
            _recent.Insert(0, destination);
            while (_recent.Count > RecentCapacity) _recent.RemoveAt(_recent.Count - 1);
        }

        /// <summary>Rebuild from the rungs, the catalogue and every registered layer.</summary>
        public void Refresh()
        {
            _dirty = false;
            _all.Clear();
            _byId.Clear();

            for (int i = 0; i < LayerTable.Rungs.Length; i++)
            {
                Rung rung = LayerTable.Rungs[i];
                Add(new Destination(
                    "rung:" + rung.Id, rung.Caption, DestinationKind.Rung,
                    null, null, Double3.Zero,
                    0.5 * rung.SpanKm, rung.SpanMetres, rung.Plane,
                    ProvenanceClass.Measured, "CONTRACT 5.1 rung " + rung.Index));
            }

            AddCatalogue();

            IReadOnlyList<CosmoLayer> layers = LayerRegistry.All;
            var fromLayers = new List<Destination>();
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] == null) continue;
                fromLayers.Clear();
                layers[i].CollectDestinations(fromLayers);
                for (int j = 0; j < fromLayers.Count; j++) Add(fromLayers[j]);
            }
        }

        private void Add(Destination destination)
        {
            if (destination == null || string.IsNullOrEmpty(destination.Id)) return;
            if (_byId.ContainsKey(destination.Id)) return;
            _byId.Add(destination.Id, destination);
            _all.Add(destination);
        }

        private void AddCatalogue()
        {
            CelestialCatalog catalog = _simulation != null ? _simulation.Catalog : null;
            if (catalog == null) return;

            foreach (CelestialBody body in catalog.Bodies)
            {
                if (body == null || body.Type == BodyType.Barycenter || body.Type == BodyType.Ring) continue;

                DestinationKind kind = KindOf(body);
                double framingRadiusKm = FramingRadiusKm(catalog, body);
                if (framingRadiusKm <= 0.0) continue;

                Add(new Destination(
                    "body:" + body.Id, body.Name, kind,
                    body.Id, null, Double3.Zero,
                    framingRadiusKm, RoomMetres(kind), null,
                    body.Provenance != null ? body.Provenance.WeakestClass() : ProvenanceClass.Unknown,
                    catalog.Source));
            }
        }

        private static DestinationKind KindOf(CelestialBody body) => body.Type switch
        {
            BodyType.Star => DestinationKind.Star,
            BodyType.Spacecraft => DestinationKind.Spacecraft,
            BodyType.Nebula => DestinationKind.Nebula,
            BodyType.Galaxy => DestinationKind.Galaxy,
            BodyType.StarCluster => DestinationKind.Cluster,
            _ => body.Site.HasValue ? DestinationKind.Site : DestinationKind.Body
        };

        /// <summary>The room size a kind arrives at (§5.7.3).</summary>
        public static double RoomMetres(DestinationKind kind) => kind switch
        {
            DestinationKind.Spacecraft => 0.4,
            DestinationKind.Site => 2.5,
            DestinationKind.Star => 0.7,
            DestinationKind.Cluster => 0.7,
            DestinationKind.Nebula => 0.7,
            DestinationKind.Galaxy => 0.7,
            DestinationKind.Structure => 1.0,
            DestinationKind.Cmb => 1.2,
            _ => 0.55
        };

        /// <summary>
        /// Half the span the arrival frames (§5.7.3).
        ///
        /// A star frames its whole planetary system, or 40 AU where it has none — otherwise
        /// arriving at a star puts the viewer inside a 1.4 million km sphere with nothing else in
        /// view, which is a correct render of a star and a useless destination.
        /// </summary>
        public static double FramingRadiusKm(CelestialCatalog catalog, CelestialBody body)
        {
            if (body.Type == BodyType.Spacecraft)
            {
                double metres = body.Model.HasValue ? body.Model.Value.LengthMetres : 0.0;
                return metres > 0.0 ? 0.5 * metres * 1e-3 : 0.01;
            }

            if (body.Type == BodyType.Star)
            {
                double outermostKm = 0.0;
                if (catalog != null)
                {
                    foreach (string childId in catalog.ChildIdsOf(body.Id))
                    {
                        CelestialBody child = catalog.Get(childId);
                        if (child?.Orbit == null || !child.Orbit.HasValue) continue;
                        double apoapsis = child.Orbit.Value.ApoapsisKm;
                        if (apoapsis > outermostKm) outermostKm = apoapsis;
                    }
                }
                return System.Math.Max(2.2 * outermostKm, 40.0 * Constants.AstronomicalUnit);
            }

            double radiusKm = body.MeanRadiusKm ?? body.EquatorialRadiusKm ?? 0.0;
            if (radiusKm <= 0.0) return 0.0;

            // A surface site is framed by its host, turned so the site faces the user.
            if (body.Site.HasValue && catalog != null)
            {
                CelestialBody host = catalog.Get(body.ParentId);
                double hostRadius = host?.MeanRadiusKm ?? radiusKm;
                return hostRadius;
            }

            return radiusKm;
        }
    }
}
