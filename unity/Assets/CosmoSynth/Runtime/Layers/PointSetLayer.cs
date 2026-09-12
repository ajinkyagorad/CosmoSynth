using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Every population that is just points with a distance: Cepheids, globular clusters, maser
    /// parallaxes, the Cosmicflows-4 groups and the SDSS subsample.
    /// CONTRACT §4.5 (the per-point chunk row), §5.4, C.8, C.11.
    ///
    /// One class, five instances, because the difference between them is a colour rule and a
    /// unit — not a rendering strategy. Five near-identical files is how five copies of the same
    /// chunk-culling bug get fixed four times.
    ///
    /// Each instance is configured by <see cref="Configure"/> at creation; the id is what makes it
    /// a distinct layer to the framework, the registry and the panel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PointSetLayer : CosmoLayer
    {
        [SerializeField] private string _layerId = "globulars";
        [SerializeField] private string _kind = "globulars";

        private PointSet _set;
        private ChunkedPoints _points;
        private double _kmPerUnit = GalaxyModel.KmPerParsec;
        private double _parsecsPerUnit = 1.0;
        private LayerBudget _budget = new LayerBudget(2, 20_000, 0.04f, 0f, 0f);
        private LabelCategory? _labels;
        private ProvenanceClass _provenance = ProvenanceClass.Inferred;
        private float _nominalAbsoluteMagnitude = -4.2f;

        public override string LayerId => _layerId;

        public override IReadOnlyList<string> DataKinds => new[] { _kind };

        public override LayerBudget Budget => _budget;

        public override LayerBlend Blend => LayerBlend.Additive;

        /// <summary>
        /// What makes this instance one of the five. Called once, by the host that creates it.
        ///
        /// <paramref name="nominalAbsoluteMagnitude"/> is used only where the file carries no
        /// magnitude of its own — the masers and the CF4 groups. It is a presentation choice, not
        /// a measurement, and the layer reports Procedural for those rows because of it.
        /// </summary>
        public void Configure(string layerId, string kind, double kmPerUnit, double parsecsPerUnit,
            LayerBudget budget, LabelCategory? labels, ProvenanceClass provenance,
            float nominalAbsoluteMagnitude)
        {
            _layerId = layerId;
            _kind = kind;
            _kmPerUnit = kmPerUnit;
            _parsecsPerUnit = parsecsPerUnit;
            _budget = budget;
            _labels = labels;
            _provenance = provenance;
            _nominalAbsoluteMagnitude = nominalAbsoluteMagnitude;
        }

        public override void OnData(string kind, object parsed)
        {
            if (kind != _kind) return;

            _set = parsed as PointSet;
            if (_set == null || _set.Count == 0)
            {
                HasData = false;
                AbsenceReason = _kind + " is not in this build; " + _layerId + " draws nothing";
                return;
            }

            Build();
            HasData = true;
            AbsenceReason = null;
        }

        private void Build()
        {
            _points?.Clear();

            List<Double3> positions = new List<Double3>(_set.Count);
            for (int i = 0; i < _set.Count; i++) positions.Add(_set.PositionUnits(i));

            _points = new ChunkedPoints(transform, _layerId)
            {
                KmPerUnit = _kmPerUnit,
                ParsecsPerUnit = _parsecsPerUnit,
            };
            _points.Build(positions, Describe, maxPerChunk: 4096, maxChunks: 8);
            _points.CreateRenderers(CosmoShaders.Material(CosmoShaders.Point), 3050);

            // Wider than a star, because these are not stars: a globular is a resolved object and
            // a 4 px dot claims it is a point source. The window is narrow because the population
            // spans only a few magnitudes, so it uses the whole brightness range.
            _points.ConfigureLook(4.0f, 96.0f, 2.0f, -10.0f, 5.0f, 16.0f, space: 0f);
        }

        private readonly List<PointMesh.Sprite> _one = new List<PointMesh.Sprite>(1);

        private IReadOnlyList<PointMesh.Sprite> Describe(int i)
        {
            _one.Clear();
            _one.Add(new PointMesh.Sprite
            {
                Colour = CosmoTheme.FromLinear(ColourOf(i)).gamma,
                Magnitude = AbsoluteMagnitudeOf(i),
                Shape = PointMesh.ShapeCore,
                Hidden = 0f,
            });
            return _one;
        }

        /// <summary>
        /// The magnitude the shader turns into a size and a brightness. Absolute, because the
        /// shader adds the distance modulus from the chunk-relative distance.
        /// </summary>
        private float AbsoluteMagnitudeOf(int i)
        {
            double absV = _set.HasColumn("absV") ? _set.Column("absV", i) : double.NaN;
            return double.IsNaN(absV) ? _nominalAbsoluteMagnitude : (float)absV;
        }

        /// <summary>
        /// Colour, linear. Content colours live as numbers here rather than in <c>CosmoTheme</c>,
        /// which owns the interface palette: a Cepheid's blue is a fact about young stars, not a
        /// UI decision, and mixing the two is how a provenance green ends up meaning "globular".
        /// </summary>
        private Double3 ColourOf(int i)
        {
            switch (_layerId)
            {
                case "cepheids":
                {
                    // Age, blue to orange. The Atlas gave every Cepheid the same M = -4.2 while
                    // holding the period that determines it; here the period drives the magnitude
                    // and the age drives only the colour, which is what each actually measures.
                    double age = _set.HasColumn("ageMyr") ? _set.Column("ageMyr", i) : double.NaN;
                    double t = double.IsNaN(age)
                        ? 0.5
                        : MathD.Clamp01(System.Math.Log10(System.Math.Max(1.0, age) / 20.0) / System.Math.Log10(20.0));
                    return Double3.Lerp(new Double3(0.38, 0.62, 1.00), new Double3(1.00, 0.62, 0.32), t);
                }
                case "globulars":
                    return new Double3(1.00, 0.86, 0.62);     // old, metal-poor, warm
                case "masers":
                    return new Double3(0.45, 1.00, 0.72);     // the measured anchors under the arms
                case "groupsCf4":
                    return new Double3(0.80, 0.72, 1.00);
                default:
                    return new Double3(0.86, 0.88, 0.95);     // SDSS and anything else
            }
        }

        protected override void SetRenderersEnabled(bool on) => _points?.SetEnabled(on);

        protected override void ApplyCore(in FrameContext ctx)
        {
            float glow = 1f;
            LayerDef def = LayerTable.Find(_layerId);
            if (def != null)
                for (int p = 0; p < def.Params.Length; p++)
                    if (def.Params[p].Id == "glow") glow = (float)State.Param(_layerId, "glow");

            _points?.Apply(ctx, Camera.main, Weight, glow, 0.25f, new Vector2(0f, 0f));
        }

        public override void CollectLabels(List<LabelCandidate> into)
        {
            if (_set == null || Weight <= 0f || !_labels.HasValue) return;
            for (int i = 0; i < _set.Count; i++)
            {
                string name = _set.Name(i);
                if (string.IsNullOrEmpty(name)) continue;
                into.Add(LabelCandidate.At(
                    _layerId + ":" + i, name, _labels.Value,
                    _set.PositionUnits(i) * _kmPerUnit,
                    (float)(-AbsoluteMagnitudeOf(i)), _provenance));
            }
        }

        public override bool TryPick(Ray worldRay, in FrameContext ctx, out PickHit hit)
        {
            hit = default;
            if (_set == null || Weight <= 0f) return false;

            Vector3 icrf = ctx.IcrfToWorld.inverse.MultiplyVector(worldRay.direction).normalized;
            Double3 dir = new Double3(icrf.x, icrf.y, icrf.z);

            double best = 1.6 * MathD.Deg2Rad;
            int bestIndex = -1;
            double bestDistance = 0.0;

            for (int i = 0; i < _set.Count; i++)
            {
                Double3 rel = _set.PositionUnits(i) * _kmPerUnit - ctx.EyeKm;
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
                LayerId = _layerId,
                Id = _layerId + ":" + bestIndex,
                Label = _set.Name(bestIndex),
                PositionKm = _set.PositionUnits(bestIndex) * _kmPerUnit,
                TrueDistanceKm = bestDistance,
                AngleRad = (float)best,
            };
            return true;
        }

        public override void SelfCheck(List<string> problems)
        {
            if (!HasData || _points == null) return;
            if (_points.Chunks.Count > _budget.Draws)
                problems.Add(_layerId + ": " + _points.Chunks.Count + " chunks, over its " +
                             _budget.Draws + "-draw budget");
        }

        private void OnDestroy() => _points?.Clear();
    }
}
