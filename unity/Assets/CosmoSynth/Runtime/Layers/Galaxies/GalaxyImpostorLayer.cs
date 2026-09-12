using System;
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Galaxies as oriented impostors, with a point below a few pixels and a cross-fade between.
    /// CONTRACT §4.5, §5.4 <c>galaxiesLocal</c> / <c>galaxies2mrs</c>, C.10, C.11; Atlas MWA-17.
    ///
    /// Two representations of the same catalogue, in one mesh each:
    ///
    /// <b>The point</b> — every galaxy, one additive sprite, sized and brightened from its
    /// absolute magnitude. This is what a galaxy is at 20 Mpc per metre, and it is correct: below
    /// about three pixels there is no shape to show.
    ///
    /// <b>The resolved cloud</b> — the nearest few dozen, drawn as a scatter of sprites laid out
    /// in the galaxy's OWN disc plane, from its measured axis ratio and position angle. It fades
    /// in as those galaxies pass a few pixels across, so M31 becomes a tilted ellipse at the angle
    /// it is really tilted at, and the Large Magellanic Cloud stops being a dot.
    ///
    /// <b>Unknown shape is drawn as unknown.</b> A galaxy whose ellipticity or position angle was
    /// never published gets no cloud and a dimmer point, and the layer counts them for the panel.
    /// The Atlas drew 542 of its 911 galaxies with a DEFAULT ellipticity, as ellipses, at angles
    /// nobody measured — and from the picture there is no way to tell those from the 369 real
    /// ones. That is the failure this rule exists to prevent, and it is not an aesthetic one.
    ///
    /// <b>Known gap:</b> the contract's §4.5 row asks for "a galaxy-disc port of Point" — a shader
    /// with an elliptical profile, one quad per galaxy. It does not exist in the shader set, and a
    /// layer that asks for a shader that is not in <c>CosmoShaders.All</c> ships magenta. The
    /// sprite cloud uses only shaders that exist and gets the orientation from the same numbers;
    /// the report asks the shader lane for the real one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GalaxyImpostorLayer : CosmoLayer
    {
        /// <summary>How many galaxies may be resolved into clouds. Each costs its sprite count.</summary>
        public const int MaxResolved = 28;

        /// <summary>Sprites in one resolved galaxy.</summary>
        public const int CloudSprites = 220;

        /// <summary>Below this apparent size a galaxy is a point and nothing else, in pixels.</summary>
        public const float PointOnlyPixels = 3f;

        /// <summary>By this size the cloud is fully in.</summary>
        public const float FullyResolvedPixels = 10f;

        [SerializeField] private string _layerId = "galaxiesLocal";
        [SerializeField] private string _kind = "localvolume";

        private GalaxySet _set;
        private ChunkedPoints _points;
        private ChunkedPoints _resolved;
        private LayerBudget _budget = new LayerBudget(8, 120_000, 0.15f, 0f, 0.1f);
        private int _unknownShape;
        private readonly List<int> _heroes = new List<int>();
        private double _heroRadiusKm;
        private double _heroDistanceKm;

        public override string LayerId => _layerId;

        public override IReadOnlyList<string> DataKinds => new[] { _kind };

        public override LayerBudget Budget => _budget;

        public override LayerBlend Blend => LayerBlend.Additive;

        public void Configure(string layerId, string kind, LayerBudget budget)
        {
            _layerId = layerId;
            _kind = kind;
            _budget = budget;
        }

        public override void OnData(string kind, object parsed)
        {
            if (kind != _kind) return;

            _set = parsed as GalaxySet;
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

        // ---- building ------------------------------------------------------------------------

        private void Build()
        {
            _points?.Clear();
            _resolved?.Clear();
            _unknownShape = 0;
            _heroes.Clear();

            List<Double3> positions = new List<Double3>(_set.Count);
            for (int i = 0; i < _set.Count; i++)
            {
                // The Milky Way's own row is in the catalogue and is flagged IsHome. It is never
                // drawn as a point: its disc comes from GalaxyModel, and drawing both would put a
                // second galaxy inside the one the viewer is standing in.
                positions.Add(_set[i].IsHome ? FarAway : _set[i].PositionUnits);
                if (_set[i].EllUnknown || _set[i].PaUnknown) _unknownShape++;
            }

            _points = new ChunkedPoints(transform, _layerId)
            {
                KmPerUnit = _set.KmPerUnit,
                ParsecsPerUnit = _set.KmPerUnit / GalaxyModel.KmPerParsec,
            };
            _points.Build(positions, DescribePoint, maxPerChunk: 8192, maxChunks: 8);
            _points.CreateRenderers(CosmoShaders.Material(CosmoShaders.Point), 3050);
            _points.ConfigureLook(3.0f, 80.0f, -14.0f, -22.0f, 5.0f, 18.0f, space: 0f);

            ChooseHeroes();
            BuildResolved();
        }

        /// <summary>A sentinel far outside any rung, for rows that must never be drawn as points.</summary>
        private static readonly Double3 FarAway = new Double3(0.0, 0.0, 1e12);

        /// <summary>
        /// The galaxies worth resolving: biggest angular size at the Sun, which is what the viewer
        /// will be looking at when the cross-fade opens. Chosen once, not per frame — a set that
        /// reshuffled as the viewer moved would pop.
        /// </summary>
        private void ChooseHeroes()
        {
            List<KeyValuePair<double, int>> ranked = new List<KeyValuePair<double, int>>();
            for (int i = 0; i < _set.Count; i++)
            {
                GalaxyRecord g = _set[i];
                if (g.IsHome || g.SizeUnknown || g.EllUnknown || g.PaUnknown) continue;
                double distance = g.PositionUnits.Magnitude;
                if (distance <= 0.0) continue;
                ranked.Add(new KeyValuePair<double, int>(g.Size / distance, i));
            }
            ranked.Sort((a, b) => b.Key.CompareTo(a.Key));

            for (int i = 0; i < ranked.Count && _heroes.Count < MaxResolved; i++)
                _heroes.Add(ranked[i].Value);

            if (_heroes.Count > 0)
            {
                GalaxyRecord first = _set[_heroes[0]];
                _heroRadiusKm = first.Size * _set.KmPerUnit;
                _heroDistanceKm = first.PositionUnits.Magnitude * _set.KmPerUnit;
            }
        }

        private readonly List<Double3> _cloudPositions = new List<Double3>();
        private readonly List<Color> _cloudColours = new List<Color>();
        private readonly List<float> _cloudMagnitudes = new List<float>();

        /// <summary>
        /// Lay each hero's sprites out in its own disc plane. The plane comes from the published
        /// position angle and axis ratio through <see cref="GalaxyOrientation"/>; only the
        /// front/back choice is invented, and that is a hash of the name so it never changes.
        /// </summary>
        private void BuildResolved()
        {
            _cloudPositions.Clear();
            _cloudColours.Clear();
            _cloudMagnitudes.Clear();

            uint seed = 0xC0503517u;
            for (int h = 0; h < _heroes.Count; h++)
            {
                int index = _heroes[h];
                GalaxyRecord g = _set[index];
                _set.RaDec(index, out double ra, out double dec);

                GalaxyOrientation.SkyBasis(ra, dec, out _, out _, out Double3 outward);
                Double3 major = GalaxyOrientation.MajorAxis(ra, dec, g.Pa);
                Double3 normal = GalaxyOrientation.DiscNormal(outward, g.InclinationDeg, major,
                    GalaxyOrientation.FaceSign(_set.Name(index)));
                Double3 minor = Double3.Cross(normal, major).Normalized;

                Color colour = CosmoTheme.FromLinear(ColourFor(g)).gamma;
                float magnitude = (float)(g.AbsMag + 5.0);   // spread one galaxy over many sprites

                for (int s = 0; s < CloudSprites; s++)
                {
                    // Exponential disc in the galaxy's own plane, thin perpendicular to it.
                    double r = g.Size * (-Math.Log(1.0 - Next(ref seed) * 0.98) * 0.6);
                    double a = Next(ref seed) * MathD.TwoPi;
                    double thickness = g.Size * 0.08 * (Next(ref seed) - 0.5);
                    Double3 offset = major * (r * Math.Cos(a)) + minor * (r * Math.Sin(a))
                                     + normal * thickness;
                    _cloudPositions.Add(g.PositionUnits + offset);
                    _cloudColours.Add(colour);
                    _cloudMagnitudes.Add(magnitude);
                }
            }

            if (_cloudPositions.Count == 0) return;

            _resolved = new ChunkedPoints(transform, _layerId + " resolved")
            {
                KmPerUnit = _set.KmPerUnit,
                ParsecsPerUnit = _set.KmPerUnit / GalaxyModel.KmPerParsec,
            };
            _resolved.Build(_cloudPositions, DescribeCloud, maxPerChunk: 4096, maxChunks: 4);
            _resolved.CreateRenderers(CosmoShaders.Material(CosmoShaders.Point), 3050);
            _resolved.ConfigureLook(3.0f, 48.0f, -9.0f, -17.0f, 5.0f, 12.0f, space: 0f);
        }

        private readonly List<PointMesh.Sprite> _one = new List<PointMesh.Sprite>(1);

        private IReadOnlyList<PointMesh.Sprite> DescribePoint(int i)
        {
            GalaxyRecord g = _set[i];
            _one.Clear();
            _one.Add(new PointMesh.Sprite
            {
                // Unknown shape is drawn round AND dimmer, so "we do not know" is visible in the
                // picture and not only in a panel nobody opens.
                Colour = CosmoTheme.FromLinear(ColourFor(g)).gamma,
                Magnitude = g.EllUnknown || g.PaUnknown ? g.AbsMag + 1.2f : g.AbsMag,
                Shape = PointMesh.ShapeCore,
                Hidden = g.IsHome ? 1f : 0f,
            });
            return _one;
        }

        private IReadOnlyList<PointMesh.Sprite> DescribeCloud(int i)
        {
            _one.Clear();
            _one.Add(new PointMesh.Sprite
            {
                Colour = _cloudColours[i],
                Magnitude = _cloudMagnitudes[i],
                Shape = PointMesh.ShapeCore,
                Hidden = 0f,
            });
            return _one;
        }

        /// <summary>
        /// Colour from absolute magnitude: bright giants blue-white, dwarfs warm. A rough stand-in
        /// for a morphology colour where no T-type is published (Atlas L403-418, kept because it
        /// is a reading aid rather than a claim).
        /// </summary>
        private static Double3 ColourFor(in GalaxyRecord g)
        {
            double t = MathD.Clamp01((-g.AbsMag - 8.0) / 12.0);
            return new Double3(1.0, 0.93 - 0.05 * (1.0 - t), 0.82 + 0.14 * t);
        }

        private static double Next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFFu) / 16777216.0;
        }

        // ---- per frame ------------------------------------------------------------------------

        protected override void SetRenderersEnabled(bool on)
        {
            _points?.SetEnabled(on);
            _resolved?.SetEnabled(on);
        }

        protected override void ApplyCore(in FrameContext ctx)
        {
            float glow = 1f;
            LayerDef def = LayerTable.Find(_layerId);
            if (def != null)
                for (int p = 0; p < def.Params.Length; p++)
                    if (def.Params[p].Id == "glow") glow = (float)State.Param(_layerId, "glow");

            _points?.Apply(ctx, Camera.main, Weight, glow, 0.2f, new Vector2(0f, 0f));

            if (_resolved != null)
            {
                // One cross-fade for the whole hero set, driven by the largest one's TRUE apparent
                // size. Per-galaxy fades would need per-galaxy draws, and 28 draws is the entire
                // galaxy budget.
                float fade = Weight * ResolvedFade(ctx);
                _resolved.SetEnabled(fade > 0.004f);
                _resolved.Apply(ctx, Camera.main, fade, glow, 0.2f, new Vector2(0f, 0f));
            }
        }

        /// <summary>0 below three pixels, 1 above ten, smooth between. The cross-fade.</summary>
        private float ResolvedFade(in FrameContext ctx)
        {
            if (_heroRadiusKm <= 0.0) return 0f;
            double pixels = ctx.ApparentPixels(_heroRadiusKm, Math.Max(1.0, _heroDistanceKm));
            return Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(PointOnlyPixels, FullyResolvedPixels, (float)pixels));
        }

        // ---- the rest -------------------------------------------------------------------------

        public override void CollectLabels(List<LabelCandidate> into)
        {
            if (_set == null || Weight <= 0f) return;
            for (int i = 0; i < _set.Count; i++)
            {
                GalaxyRecord g = _set[i];
                if (g.IsHome) continue;
                string name = _set.Name(i);
                if (string.IsNullOrEmpty(name)) continue;
                if (!g.IsLocalGroupLandmark && g.AbsMag > -18.0) continue;   // the giants, not 44,000 rows
                into.Add(LabelCandidate.At(_layerId + ":" + i, name,
                    _layerId == "galaxiesLocal" ? LabelCategory.LocalGroup : LabelCategory.Structures,
                    _set.PositionKm(i), (float)(-g.AbsMag),
                    g.EllUnknown ? ProvenanceClass.Inferred : ProvenanceClass.Measured));
            }
        }

        public override void CollectDestinations(List<Destination> into)
        {
            if (_set == null) return;
            for (int h = 0; h < _heroes.Count; h++)
            {
                int i = _heroes[h];
                string name = _set.Name(i);
                if (string.IsNullOrEmpty(name)) continue;
                into.Add(new Destination(
                    _layerId + ":" + i, name, DestinationKind.Galaxy, null, _layerId,
                    _set.PositionKm(i), Math.Max(1.0, _set[i].Size * _set.KmPerUnit) * 3.0, 0.9,
                    PresentationPlane.Supergalactic, ProvenanceClass.Measured,
                    _set.Kind == "localvolume" ? "LVDB / UNGC" : "2MRS"));
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
                if (_set[i].IsHome) continue;
                Double3 rel = _set.PositionKm(i) - ctx.EyeKm;
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
                PositionKm = _set.PositionKm(bestIndex),
                TrueDistanceKm = bestDistance,
                AngleRad = (float)best,
            };
            return true;
        }

        public override double NearestSurfaceKm(Double3 absoluteKm)
        {
            if (_set == null || Weight <= 0f) return double.PositiveInfinity;
            double nearest = double.PositiveInfinity;
            for (int h = 0; h < _heroes.Count; h++)
            {
                int i = _heroes[h];
                double d = Double3.Distance(_set.PositionKm(i), absoluteKm)
                           - _set[i].Size * _set.KmPerUnit;
                if (d < nearest) nearest = d;
            }
            return Math.Max(0.0, nearest);
        }

        public override void SelfCheck(List<string> problems)
        {
            if (!HasData) return;
            int draws = (_points?.Chunks.Count ?? 0) + (_resolved?.Chunks.Count ?? 0);
            if (draws > _budget.Draws)
                problems.Add(_layerId + ": " + draws + " chunks, over its " + _budget.Draws + "-draw budget");
            if (_set != null && _unknownShape == 0 && _set.Count > 100)
                problems.Add(_layerId + ": not one row has an unknown ellipticity, which no real " +
                             "catalogue manages — a default was probably written over the blanks");
        }

        private void OnDestroy()
        {
            _points?.Clear();
            _resolved?.Clear();
        }
    }
}
