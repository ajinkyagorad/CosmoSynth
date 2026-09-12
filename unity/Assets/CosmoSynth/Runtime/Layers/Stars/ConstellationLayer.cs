using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The 88 modern figures, drawn between the real stars in three dimensions.
    /// CONTRACT §5.4 <c>constellations</c>; Atlas MWA-19.
    ///
    /// The segments are index pairs into the star catalogue, so each line runs from one star's
    /// measured position to another's. That is the whole design and the whole point: a
    /// constellation is a line-of-sight coincidence, and the figures come apart as the viewer
    /// leaves the Solar System. Drawing them as fixed directions on a sky sphere would hide
    /// exactly the thing worth showing.
    ///
    /// They fade out with scale as well, because at 30 kpc per metre a figure spanning 200 pc is
    /// a speck and the lines are noise over the disc.
    ///
    /// <b>Provenance is Fictional, and that is not a slur.</b> The endpoints are measured; the
    /// decision that those seven stars are a plough is a cultural convention with an author. The
    /// panel says so.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConstellationLayer : CosmoLayer
    {
        /// <summary>Line width in dmm — milliradians. 1.4 dmm is about 1.6 px at 1,140 px/rad.</summary>
        public const float WidthDmm = 1.4f;

        private StarCatalog _catalog;
        private Constellations _figures;

        private GameObject _figuresObject;
        private MeshRenderer _figuresRenderer;
        private Material _figuresMaterial;
        private Mesh _figuresMesh;

        private GameObject _boundaryObject;
        private MeshRenderer _boundaryRenderer;
        private Material _boundaryMaterial;
        private Mesh _boundaryMesh;

        private int _segments;

        public override string LayerId => "constellations";

        public override IReadOnlyList<string> DataKinds => new[] { "constellations", "stars" };

        /// <summary>
        /// 695 segments x 4 vertices = 2,780, plus the boundary option. Two draws, inside the
        /// 16-draw / 300 k-vertex ribbon row of §7.3 with room for the orbits to share it.
        /// </summary>
        public override LayerBudget Budget => new LayerBudget(2, 12_000, 0.02f, 0f, 0f);

        public override LayerBlend Blend => LayerBlend.Additive;

        public override void OnData(string kind, object parsed)
        {
            if (kind == "stars") { _catalog = parsed as StarCatalog; }
            else if (kind == "constellations") { _figures = parsed as Constellations; }
            else return;

            if (_catalog == null || _figures == null) return;

            IEnumerator<int> bad = _figures.OutOfRange(_catalog.Count).GetEnumerator();
            if (bad.MoveNext())
            {
                // A shifted index does not throw: it joins two random stars and still looks like
                // a constellation to anyone who does not know the sky. Refuse the whole file.
                HasData = false;
                AbsenceReason = "constellations.json indexes row " + bad.Current +
                                " outside a catalogue of " + _catalog.Count + "; figures not drawn";
                return;
            }

            Build();
            HasData = _segments > 0;
            AbsenceReason = HasData ? null : "constellations.json contained no segments";
        }

        private void Build()
        {
            Destroy0();

            List<RibbonMesh.Polyline> lines = new List<RibbonMesh.Polyline>();
            Color colour = CosmoTheme.Scaled(CosmoTheme.Accent, 0.45f);
            _segments = 0;

            for (int f = 0; f < _figures.Figures.Count; f++)
            {
                ConstellationFigure figure = _figures.Figures[f];
                for (int s = 0; s < figure.SegmentCount; s++)
                {
                    int a = figure.Pairs[s * 2];
                    int b = figure.Pairs[s * 2 + 1];
                    RibbonMesh.Polyline line = new RibbonMesh.Polyline { Colour = colour };
                    line.Points.Add(Parsecs(a));
                    line.Points.Add(Parsecs(b));
                    lines.Add(line);
                    _segments++;
                }
            }

            _figuresMesh = RibbonMesh.Build(lines, "Constellation figures");
            _figuresObject = new GameObject("Constellation figures");
            _figuresObject.transform.SetParent(transform, false);
            _figuresObject.AddComponent<MeshFilter>().sharedMesh = _figuresMesh;
            _figuresRenderer = _figuresObject.AddComponent<MeshRenderer>();
            _figuresMaterial = CosmoShaders.Material(CosmoShaders.Ribbon);
            _figuresMaterial.renderQueue = 3060;
            _figuresMaterial.SetFloat(Shader.PropertyToID("_WidthDmm"), WidthDmm);
            // Placement 1: the vertices are true positions in universe space and the shader
            // compresses each one. Constellations span the whole neighbourhood, so no single
            // rigid similarity describes them.
            _figuresMaterial.SetFloat(Shader.PropertyToID("_Placement"), 1f);
            _figuresRenderer.sharedMaterial = _figuresMaterial;
            _figuresRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _figuresRenderer.receiveShadows = false;
            _figuresRenderer.enabled = false;

            BuildBoundaries();
        }

        /// <summary>
        /// The boundary option: the IAU rectangles, on the sky rather than in the volume.
        ///
        /// Boundaries genuinely ARE directions — they are defined on the B1875 celestial sphere
        /// and have no distance at all — so they are drawn with <c>_Placement = 2</c>, at infinity
        /// per eye. Until <c>constellations.json</c> carries them this draws nothing and says so;
        /// inventing rectangles from the figures would be a fabricated boundary, which is exactly
        /// the kind of plausible-looking invention the provenance rules exist to stop.
        /// </summary>
        private void BuildBoundaries()
        {
            // No boundary polygons in the schema yet. Nothing is drawn and nothing is faked.
        }

        private Vector3 Parsecs(int row)
        {
            Double3 p = _catalog.Stars[row].PositionParsecs;
            // Universe-local metres are what _Placement = 1 expects, and the simulation's origin
            // already carries the scale, so this is done per frame in ApplyCore instead. Here the
            // mesh is built in PARSECS and the object transform carries the scale.
            return new Vector3((float)p.x, (float)p.y, (float)p.z);
        }

        protected override void SetRenderersEnabled(bool on)
        {
            if (_figuresRenderer != null) _figuresRenderer.enabled = on;
            if (_boundaryRenderer != null)
                _boundaryRenderer.enabled = on && State != null && State.Param(LayerId, "boundaries") > 0.5;
        }

        protected override void ApplyCore(in FrameContext ctx)
        {
            if (_figuresObject == null) return;

            // The mesh is in ICRF parsecs; the transform maps parsecs to universe-local metres and
            // orients ICRF to the universe axes. One transform for the whole figure set, so the
            // shader's per-vertex compression is the only thing that touches individual stars.
            //
            // The ICRF-to-universe map has determinant -1 — it swaps handedness — so it is NOT a
            // rotation and `Matrix4x4.rotation` would silently return nonsense. FloatingOrigin
            // publishes the correct decomposition, a quarter turn about x with a negated z scale,
            // and it is the only place that arithmetic is allowed to live.
            float metresPerParsec = (float)(GalaxyModel.KmPerParsec * ctx.UnitsPerKm);
            _figuresObject.transform.localRotation = FloatingOrigin.IcrfMeshRotation;
            _figuresObject.transform.localScale = FloatingOrigin.IcrfMeshScale(metresPerParsec);
            // The mesh's own origin is the ICRF origin, so the object sits wherever that maps to.
            _figuresObject.transform.localPosition = Simulation.Origin.ToRender(Double3.Zero);

            WriteFade(_figuresMaterial, Weight);
            if (_boundaryMaterial != null) WriteFade(_boundaryMaterial, Weight);
        }

        public override void CollectLabels(List<LabelCandidate> into)
        {
            if (_figures == null || _catalog == null || Weight <= 0f) return;
            for (int f = 0; f < _figures.Figures.Count; f++)
            {
                ConstellationFigure figure = _figures.Figures[f];
                if (figure.LabelStar < 0 || figure.LabelStar >= _catalog.Count) continue;
                // The name goes at the brightest star's DIRECTION, with no dot: a figure has no
                // position, and a centroid in three dimensions is meaningless once the viewer has
                // moved off the line of sight the figure was drawn for.
                into.Add(LabelCandidate.Toward(
                    "con:" + figure.Abbr, figure.Iau, LabelCategory.Constellations,
                    _catalog.Stars[figure.LabelStar].Direction, 0.5f, ProvenanceClass.Fictional));
            }
        }

        public override void SelfCheck(List<string> problems)
        {
            if (!HasData) return;
            if (_figures != null && _figures.Figures.Count != 88)
                problems.Add("constellations: " + _figures.Figures.Count + " figures, expected 88");
            if (_segments > 0 && _segments != 695)
                problems.Add("constellations: " + _segments + " segments, expected 695 at the pinned commit");
        }

        private void Destroy0()
        {
            if (_figuresObject != null) Destroy(_figuresObject);
            if (_figuresMaterial != null) Destroy(_figuresMaterial);
            if (_figuresMesh != null) Destroy(_figuresMesh);
            if (_boundaryObject != null) Destroy(_boundaryObject);
            if (_boundaryMaterial != null) Destroy(_boundaryMaterial);
            if (_boundaryMesh != null) Destroy(_boundaryMesh);
            _figuresObject = null; _figuresRenderer = null; _figuresMaterial = null; _figuresMesh = null;
            _boundaryObject = null; _boundaryRenderer = null; _boundaryMaterial = null; _boundaryMesh = null;
            _segments = 0;
        }

        private void OnDestroy() => Destroy0();
    }
}
