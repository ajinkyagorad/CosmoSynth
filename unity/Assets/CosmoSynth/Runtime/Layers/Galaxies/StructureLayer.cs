using System;
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The named large-scale structures — superclusters, walls, voids, basins — at their published
    /// positions. CONTRACT §5.4 <c>basins</c>, C.11; Atlas MWA-18 at extragalactic scale.
    ///
    /// Without this the cosmic web is 44,000 anonymous dots. Laniakea, the Great Attractor, the
    /// Coma cluster, the Boötes void and the Sloan Great Wall are the handful of names that turn
    /// a cloud into a map, and each one is a marker ring at a real position with a citation
    /// behind it.
    ///
    /// <b>What it deliberately does NOT draw:</b> the watershed outlines. A basin boundary is a
    /// surface computed from the Cosmicflows-4 velocity field, and without <c>cf4_basins</c> the
    /// only way to produce one is to invent it. A ring at the structure's published centre and
    /// radius is a claim the citation supports; a boundary would not be. When the grid ships, the
    /// outline replaces the ring and the layer says which it is drawing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StructureLayer : CosmoLayer
    {
        public const int RingSegments = 64;

        private PlaceCatalog _places;
        private GameObject _object;
        private MeshRenderer _renderer;
        private Material _material;
        private Mesh _mesh;
        private readonly List<Place> _drawn = new List<Place>();

        public override string LayerId => "basins";

        public override IReadOnlyList<string> DataKinds => new[] { "structures" };

        /// <summary>One ribbon draw. A few dozen rings at 64 segments is under 10 k vertices.</summary>
        public override LayerBudget Budget => new LayerBudget(1, 10_000, 0.03f, 0f, 0f);

        public override LayerBlend Blend => LayerBlend.Additive;

        public override void OnData(string kind, object parsed)
        {
            if (kind != "structures") return;

            _places = parsed as PlaceCatalog;
            if (_places == null || _places.Places.Count == 0)
            {
                HasData = false;
                AbsenceReason = "lss/structures.json is not in this build; no structures named";
                return;
            }

            foreach (Place unreferenced in _places.Unreferenced())
            {
                HasData = false;
                AbsenceReason = "lss/structures.json entry '" + unreferenced.Id +
                                "' has no reference; the whole file is refused";
                return;
            }

            Build();
            HasData = _drawn.Count > 0;
            AbsenceReason = HasData ? null : "lss/structures.json contained no usable entries";
        }

        private void Build()
        {
            DestroyAll();
            _drawn.Clear();

            List<RibbonMesh.Polyline> lines = new List<RibbonMesh.Polyline>();
            Color colour = CosmoTheme.Scaled(CosmoTheme.Accent, 0.4f);

            foreach (Place place in _places.Places)
            {
                if (place.RadiusPc <= 0.0 || place.DistancePc <= 0.0) continue;
                _drawn.Add(place);

                Double3 centre = place.IcrfPc;
                Vector3 c = new Vector3((float)centre.x, (float)centre.y, (float)centre.z);

                // Three orthogonal rings: one circle read edge-on is a line, and at these scales
                // the viewer has no way to tell which. Three is the cheapest shape that reads as
                // a sphere of influence without pretending to be a measured surface.
                Double3 normal = centre.Normalized;
                Double3 a = Double3.Cross(normal, Math.Abs(normal.z) < 0.9 ? Double3.UnitZ : Double3.UnitX)
                    .Normalized;
                Double3 b = Double3.Cross(normal, a).Normalized;

                lines.Add(RibbonMesh.Circle(c, ToVector(normal), (float)place.RadiusPc, RingSegments, colour));
                lines.Add(RibbonMesh.Circle(c, ToVector(a), (float)place.RadiusPc, RingSegments, colour));
                lines.Add(RibbonMesh.Circle(c, ToVector(b), (float)place.RadiusPc, RingSegments, colour));
            }

            if (_drawn.Count == 0) return;

            _mesh = RibbonMesh.Build(lines, "Structures");
            _object = new GameObject("Structures");
            _object.transform.SetParent(transform, false);
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _object.AddComponent<MeshRenderer>();
            _material = CosmoShaders.Material(CosmoShaders.Ribbon);
            _material.renderQueue = 3060;
            _material.SetFloat(Shader.PropertyToID("_WidthDmm"), 1.0f);
            _material.SetFloat(Shader.PropertyToID("_Placement"), 1f);
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
        }

        private static Vector3 ToVector(Double3 v) => new Vector3((float)v.x, (float)v.y, (float)v.z);

        protected override void SetRenderersEnabled(bool on)
        {
            if (_renderer != null) _renderer.enabled = on;
        }

        protected override void ApplyCore(in FrameContext ctx)
        {
            if (_object == null || Simulation == null || Simulation.Origin == null) return;
            _object.transform.localPosition = Simulation.Origin.ToRender(Double3.Zero);
            _object.transform.localRotation = FloatingOrigin.IcrfMeshRotation;
            _object.transform.localScale =
                FloatingOrigin.IcrfMeshScale((float)(GalaxyModel.KmPerParsec * ctx.UnitsPerKm));
            WriteFade(_material, Weight);
        }

        public override void CollectLabels(List<LabelCandidate> into)
        {
            if (Weight <= 0f) return;
            for (int i = 0; i < _drawn.Count; i++)
            {
                Place place = _drawn[i];
                into.Add(LabelCandidate.At("structure:" + place.Id, place.Name,
                    LabelCategory.Structures, place.IcrfPc * GalaxyModel.KmPerParsec,
                    (float)Math.Log10(Math.Max(1.0, place.RadiusPc)), place.Provenance));
            }
        }

        public override void CollectDestinations(List<Destination> into)
        {
            if (_places == null) return;
            for (int i = 0; i < _drawn.Count; i++)
            {
                Place place = _drawn[i];
                into.Add(new Destination(
                    "structure:" + place.Id, place.Name, DestinationKind.Structure, null, LayerId,
                    place.IcrfPc * GalaxyModel.KmPerParsec,
                    place.RadiusPc * GalaxyModel.KmPerParsec * 2.0, 1.0,
                    PresentationPlane.Supergalactic, place.Provenance, place.Reference));
            }
        }

        public override bool TryPick(Ray worldRay, in FrameContext ctx, out PickHit hit)
        {
            hit = default;
            if (Weight <= 0f || _drawn.Count == 0) return false;

            Vector3 icrf = ctx.IcrfToWorld.inverse.MultiplyVector(worldRay.direction).normalized;
            Double3 dir = new Double3(icrf.x, icrf.y, icrf.z);

            double best = 1.6 * MathD.Deg2Rad;
            int bestIndex = -1;
            double bestDistance = 0.0;
            for (int i = 0; i < _drawn.Count; i++)
            {
                Double3 rel = _drawn[i].IcrfPc * GalaxyModel.KmPerParsec - ctx.EyeKm;
                double distance = rel.Magnitude;
                if (distance <= 0.0) continue;
                double angle = Double3.Angle(rel / distance, dir);
                if (angle >= best) continue;
                best = angle;
                bestIndex = i;
                bestDistance = distance;
            }

            if (bestIndex < 0) return false;
            hit = new PickHit
            {
                LayerId = LayerId,
                Id = "structure:" + _drawn[bestIndex].Id,
                Label = _drawn[bestIndex].Name,
                PositionKm = _drawn[bestIndex].IcrfPc * GalaxyModel.KmPerParsec,
                TrueDistanceKm = bestDistance,
                AngleRad = (float)best,
            };
            return true;
        }

        public override void SelfCheck(List<string> problems)
        {
            if (!HasData) return;
            if (_mesh != null && _mesh.vertexCount > Budget.Vertices)
                problems.Add("basins: " + _mesh.vertexCount + " vertices, over budget — " +
                             _drawn.Count + " structures at 3 rings each");
        }

        private void DestroyAll()
        {
            if (_object != null) Destroy(_object);
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
            _object = null; _renderer = null; _material = null; _mesh = null;
        }

        private void OnDestroy() => DestroyAll();
    }
}
