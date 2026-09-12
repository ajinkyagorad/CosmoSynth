using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The star field, in three dimensions. CONTRACT §5.4 <c>stars</c>, §7.3; Atlas MWA-11/13/14.
    ///
    /// This replaces a direction-only shell of 9,000 quads pinned at 900 units, whose own comment
    /// read "distance is deliberately discarded here". Discarding it is why the app stopped at
    /// 130 light years: with no distance there is nothing to fly through, the sky is a wallpaper,
    /// and every star is the same distance away however far you travel.
    ///
    /// Now: 120,000 stars at their measured parsec positions, split into chunks so a float offset
    /// is exact to well under a pixel, billboarded in the vertex stage, with the absolute
    /// magnitude turned into an apparent one <b>per star per frame</b> from the eye's actual
    /// distance. Fly to Sirius and the sky rearranges, because it is a real volume.
    ///
    /// <b>The 146 rows with no distance keep the shell</b> — and only the shell. Their direction
    /// is measured and their distance is not, so drawing them in the volume would mean inventing
    /// one. They sit at infinity, per eye, and switch off once the viewer is more than half a
    /// parsec from the Sun, where the parallax they cannot express would start to matter.
    ///
    /// <b>The size floor is a bug fix, not a taste.</b> A one-pixel feature has no shape: given a
    /// Gaussian profile the visible core is about half the quad, so a two-pixel quad renders as a
    /// square patch whatever texture it is cut from. That shipped. The cure is a floor wide enough
    /// for the falloff to resolve in, with faintness carried in brightness instead — a dimmer star
    /// is not a smaller star. And the floor needs a ceiling with it, because a minimum with no
    /// maximum is unbounded in the worst direction; this project has already put the camera inside
    /// an inflated lander and produced a black screen with passthrough working.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StarFieldLayer : CosmoLayer
    {
        /// <summary>Population cap. 120 k x 4 vertices is the §7.3 row; the halo and spike quads fit in the slack.</summary>
        public const int MaxStars = 118000;

        /// <summary>Brighter than this gets a halo quad as well as a core.</summary>
        public const double HaloMagnitude = 3.0;

        /// <summary>Brighter than this gets two diffraction-spike quads on top of the halo.</summary>
        public const double SpikeMagnitude = 1.0;

        /// <summary>
        /// Pixel bounds for the quad. The lower one is the backstop under the measured
        /// <see cref="StarLook.FaintestQuadDmm"/>: at the contract's 1,140 px/rad that dmm figure
        /// is already 4.3 px, so the floor only bites on a low-resolution capture — which is
        /// exactly where the square-patch failure was found.
        /// </summary>
        public const float MinimumPixels = 3.0f;

        /// <summary>And the ceiling that has to come with it.</summary>
        public const float MaximumPixels = 64.0f;

        /// <summary>Beyond half a parsec from the Sun the distance-unknown shell stops being honest.</summary>
        public const double ShellVantageLimitParsecs = 0.5;

        private StarCatalog _catalog;
        private StarNames _names;

        private ChunkedPoints _volume;
        private GameObject _shellObject;
        private MeshRenderer _shellRenderer;
        private Material _shellMaterial;
        private Mesh _shellMesh;

        private readonly List<int> _volumeIndex = new List<int>();   // volume point -> catalogue row
        private readonly List<Double3> _volumePc = new List<Double3>();
        private readonly HashSet<int> _hidden = new HashSet<int>();
        private int _shellStars;

        public override string LayerId => "stars";

        public override IReadOnlyList<string> DataKinds => new[] { "stars", "stars_named" };

        /// <summary>
        /// §7.3: 64 chunk draws plus one shell draw; 480 k vertices; half an eye screen of
        /// additive fragments; no raymarch; no texture — the profile is analytic.
        /// </summary>
        public override LayerBudget Budget => new LayerBudget(65, 480_584, 0.502f, 0f, 0f);

        public override LayerBlend Blend => LayerBlend.Additive;

        public override void OnData(string kind, object parsed)
        {
            if (kind == "stars_named") { _names = parsed as StarNames; return; }
            if (kind != "stars") return;

            _catalog = parsed as StarCatalog;
            if (_catalog == null || _catalog.Count == 0)
            {
                HasData = false;
                AbsenceReason = "sky/stars.csstars did not parse; no stars drawn";
                return;
            }

            BuildVolume();
            BuildShell();
            HasData = true;
            AbsenceReason = null;
        }

        // ---- building -----------------------------------------------------------------------

        private void BuildVolume()
        {
            _volume?.Clear();
            _volumeIndex.Clear();
            _volumePc.Clear();

            int limit = Mathf.Min(_catalog.Count, MaxStars);
            for (int i = 0; i < limit; i++)
            {
                CatalogStar star = _catalog.Stars[i];
                if (!star.DistanceParsecs.HasValue) continue;       // the shell takes these
                _volumeIndex.Add(i);
                _volumePc.Add(star.PositionParsecs);
            }

            _volume = new ChunkedPoints(transform, "Stars")
            {
                KmPerUnit = GalaxyModel.KmPerParsec,
                ParsecsPerUnit = 1.0,
            };
            _volume.Build(_volumePc, DescribeStar);
            _volume.CreateRenderers(CosmoShaders.Material(CosmoShaders.Point), 3050);
            _volume.ConfigureLook(MinimumPixels, MaximumPixels, 7.0f, 1.5f,
                StarLook.FaintestQuadDmm, StarLook.BrightestQuadDmm, space: 0f);
        }

        private readonly List<PointMesh.Sprite> _parts = new List<PointMesh.Sprite>(4);

        /// <summary>
        /// One star's sprites. The magnitude written here is ABSOLUTE: the shader adds
        /// <c>5 log10(d) - 5</c> from the chunk-relative distance, which is what makes the sky
        /// change as the viewer moves rather than merely getting closer.
        /// </summary>
        private IReadOnlyList<PointMesh.Sprite> DescribeStar(int volumePoint)
        {
            int row = _volumeIndex[volumePoint];
            CatalogStar star = _catalog.Stars[row];
            double distance = star.DistanceParsecs ?? 1.0;
            float absolute = (float)StarQuery.AbsoluteMagnitude(star.ApparentMagnitude, distance);

            // Linear from Core, display for the vertex stream: the shader decodes with
            // CS_DisplayToLinear, so handing it linear values would darken every star by the
            // gamma curve. One conversion, here, and never `.linear` as well (CONTRACT 7.4).
            Color colour = CosmoTheme.FromLinear(StarCatalog.ColorFor(star)).gamma;

            _parts.Clear();
            _parts.Add(new PointMesh.Sprite
            {
                Colour = colour,
                Magnitude = absolute,
                Shape = PointMesh.ShapeCore,
                Hidden = 0f,
            });

            // The bright end is told apart by what is AROUND the dot, not by dot size: past
            // magnitude 1.5 the brightness channel is saturated and a bigger disc just reads as
            // a nearer object.
            if (star.ApparentMagnitude <= HaloMagnitude)
            {
                _parts.Add(new PointMesh.Sprite
                {
                    Colour = colour, Magnitude = absolute, Shape = PointMesh.ShapeHalo, Hidden = 0f,
                });
            }
            if (star.ApparentMagnitude <= SpikeMagnitude)
            {
                _parts.Add(new PointMesh.Sprite
                {
                    Colour = colour, Magnitude = absolute, Shape = PointMesh.ShapeSpikeX, Hidden = 0f,
                });
                _parts.Add(new PointMesh.Sprite
                {
                    Colour = colour, Magnitude = absolute, Shape = PointMesh.ShapeSpikeY, Hidden = 0f,
                });
            }
            return _parts;
        }

        /// <summary>
        /// The distance-unknown rows, as directions at infinity. One mesh, one draw, no chunking:
        /// a chunk origin is a position, and these have none.
        /// </summary>
        private void BuildShell()
        {
            DestroyShell();

            List<PointMesh.Sprite> sprites = new List<PointMesh.Sprite>();
            int limit = Mathf.Min(_catalog.Count, MaxStars);
            for (int i = 0; i < limit; i++)
            {
                CatalogStar star = _catalog.Stars[i];
                if (star.DistanceParsecs.HasValue) continue;
                Double3 dir = star.Direction;
                sprites.Add(new PointMesh.Sprite
                {
                    Offset = new Vector3((float)dir.x, (float)dir.y, (float)dir.z),
                    Colour = CosmoTheme.FromLinear(StarCatalog.ColorFor(star)).gamma,
                    // Sky mode takes an APPARENT magnitude: there is no distance to correct with.
                    Magnitude = (float)star.ApparentMagnitude,
                    Shape = PointMesh.ShapeCore,
                    Hidden = 0f,
                });
            }

            _shellStars = sprites.Count;
            if (_shellStars == 0) return;

            _shellMesh = PointMesh.Build(sprites, "Stars (distance unknown)");
            _shellObject = new GameObject("Stars (distance unknown)");
            _shellObject.transform.SetParent(transform, false);

            _shellObject.AddComponent<MeshFilter>().sharedMesh = _shellMesh;
            _shellRenderer = _shellObject.AddComponent<MeshRenderer>();
            _shellMaterial = CosmoShaders.Material(CosmoShaders.Point);
            _shellMaterial.renderQueue = 1010;
            _shellMaterial.SetFloat(Shader.PropertyToID("_Space"), 2f);
            _shellMaterial.SetVector(Shader.PropertyToID("_PxBounds"),
                new Vector4(MinimumPixels, MaximumPixels, 0f, 0f));
            _shellMaterial.SetVector(Shader.PropertyToID("_MagWindow"), new Vector4(7f, 1.5f, 0f, 0f));
            _shellMaterial.SetVector(Shader.PropertyToID("_SizeDmm"),
                new Vector4(StarLook.FaintestQuadDmm, StarLook.BrightestQuadDmm, 0f, 0f));
            _shellRenderer.sharedMaterial = _shellMaterial;
            _shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shellRenderer.receiveShadows = false;
            _shellRenderer.enabled = false;
        }

        // ---- per frame ------------------------------------------------------------------------

        protected override void SetRenderersEnabled(bool on)
        {
            _volume?.SetEnabled(on);
            if (_shellRenderer != null && !on) _shellRenderer.enabled = false;
        }

        protected override void ApplyCore(in FrameContext ctx)
        {
            float exposure = (float)State.Param(LayerId, "exposure");
            bool observed = State.Param(LayerId, "observed") > 0.5;
            float limit = (float)State.Param(LayerId, "limitMag");

            // OBSERVED SKY. The shader already computes each star's apparent magnitude from the
            // eye; a limit magnitude is therefore the top of the brightness window, and the floor
            // brightness goes to zero so anything below the limit is genuinely absent rather than
            // sitting at the faint floor. The eye-response exponent lives in StarQuery and is what
            // the readout quotes; the rendered ramp is StarLook's, which was measured on a frame.
            float faintMag = observed ? limit : 7.0f;
            float floor = observed ? 0f : StarLook.FaintestBrightness;

            // Nearer than this the chunk-offset float error exceeds half a pixel, and the promoted
            // double-precision star system stands in instead (CONTRACT 3.4).
            Vector2 nearFade = new Vector2(0.002f, 0.02f);

            if (_volume != null)
            {
                _volume.ConfigureLook(MinimumPixels, MaximumPixels, faintMag, 1.5f,
                    StarLook.FaintestQuadDmm, StarLook.BrightestQuadDmm, space: 0f);
                _volume.Apply(ctx, Camera.main, Weight, exposure, floor, nearFade);
            }

            if (_shellRenderer != null)
            {
                // Direction is measured, distance is not. Past half a parsec the shell would be
                // asserting that 146 stars are all infinitely far away in the same direction they
                // were from the Sun, which stops being true the moment you leave.
                bool shellHonest = ctx.LayerContext.ViewerFromSunPc < ShellVantageLimitParsecs;
                _shellRenderer.enabled = shellHonest && Weight > 0f;
                if (_shellRenderer.enabled)
                {
                    _shellMaterial.SetMatrix(Shader.PropertyToID("_LayerToWorld"), ctx.IcrfToWorld);
                    _shellMaterial.SetVector(Shader.PropertyToID("_Look"),
                        new Vector4(floor, exposure, 0f, 0f));
                    _shellMaterial.SetVector(Shader.PropertyToID("_MagWindow"),
                        new Vector4(faintMag, 1.5f, 0f, 0f));
                    WriteFade(_shellMaterial, Weight);
                }
            }
        }

        // ---- the rest of the contract -----------------------------------------------------------

        public override void CollectLabels(List<LabelCandidate> into)
        {
            if (_names == null || _catalog == null || Weight <= 0f) return;
            foreach (StarName entry in _names.Names)
            {
                if (entry.Row < 0 || entry.Row >= _catalog.Count) continue;
                CatalogStar star = _catalog.Stars[entry.Row];
                double? d = star.DistanceParsecs;
                if (!d.HasValue) continue;
                into.Add(LabelCandidate.At(
                    "star:" + entry.Row, entry.Name, LabelCategory.StarNames,
                    star.PositionParsecs * GalaxyModel.KmPerParsec,
                    // Brighter stars win the declutter, which is what a viewer expects a sky to do.
                    (float)(10.0 - star.ApparentMagnitude),
                    ProvenanceClass.Measured));
            }
        }

        public override void CollectDestinations(List<Destination> into)
        {
            if (_names == null || _catalog == null) return;
            foreach (StarName entry in _names.Names)
            {
                if (entry.Row < 0 || entry.Row >= _catalog.Count) continue;
                CatalogStar star = _catalog.Stars[entry.Row];
                if (!star.DistanceParsecs.HasValue) continue;
                if (star.ApparentMagnitude > 2.5) continue;   // the named bright sky, not 640 rows
                into.Add(new Destination(
                    "star:" + entry.Row, entry.Name, DestinationKind.Star, null, LayerId,
                    star.PositionParsecs * GalaxyModel.KmPerParsec,
                    // Framing radius: a light year, so arrival puts the star in its own space
                    // rather than filling the room with one dot.
                    DistanceGrids.KmPerLightYear, 0.7, PresentationPlane.Galactic,
                    ProvenanceClass.Measured, "AT-HYG v4.0"));
            }
        }

        public override bool TryPick(Ray worldRay, in FrameContext ctx, out PickHit hit)
        {
            hit = default;
            if (_catalog == null || Weight <= 0f || _volume == null) return false;

            Vector3 originIcrf = ctx.IcrfToWorld.inverse.MultiplyVector(worldRay.direction).normalized;
            Double3 dir = new Double3(originIcrf.x, originIcrf.y, originIcrf.z);

            double best = 1.6 * MathD.Deg2Rad;   // today's forgiveness
            int bestRow = -1;
            double bestDistance = 0.0;

            // O(N) over the population. A click is not a frame, and the brightest-first ordering
            // of the catalogue means the loop is already in priority order for ties.
            for (int p = 0; p < _volumeIndex.Count; p++)
            {
                int row = _volumeIndex[p];
                CatalogStar star = _catalog.Stars[row];
                if (star.ApparentMagnitude > 6.5) continue;   // only what a viewer can see to aim at
                Double3 rel = star.PositionParsecs * GalaxyModel.KmPerParsec - ctx.EyeKm;
                double distance = rel.Magnitude;
                if (distance <= 0.0) continue;
                double angle = Double3.Angle(rel / distance, dir);
                if (angle >= best) continue;
                best = angle;
                bestRow = row;
                bestDistance = distance;
            }

            if (bestRow < 0) return false;
            hit = new PickHit
            {
                LayerId = LayerId,
                Id = "star:" + bestRow,
                Label = _names != null ? _names.NameOfRow(bestRow) : null,
                PositionKm = _catalog.Stars[bestRow].PositionParsecs * GalaxyModel.KmPerParsec,
                TrueDistanceKm = bestDistance,
                AngleRad = (float)best,
            };
            return true;
        }

        public override double NearestSurfaceKm(Double3 absoluteKm)
        {
            if (_catalog == null || Weight <= 0f) return double.PositiveInfinity;
            double nearest = double.PositiveInfinity;
            // The brightest 2,000 rows are enough to set a flight speed; the full scan is O(120k)
            // and this runs every frame.
            int limit = Mathf.Min(_volumeIndex.Count, 2000);
            for (int p = 0; p < limit; p++)
            {
                Double3 pos = _catalog.Stars[_volumeIndex[p]].PositionParsecs * GalaxyModel.KmPerParsec;
                double d = Double3.Distance(pos, absoluteKm);
                if (d < nearest) nearest = d;
            }
            return nearest;
        }

        public override void SelfCheck(List<string> problems)
        {
            if (!HasData) return;
            if (_volume != null && _volume.Chunks.Count > 64)
                problems.Add("stars: " + _volume.Chunks.Count + " chunks, over the 64-draw budget");
            if (_volume != null && _volume.PointCount + _shellStars > MaxStars + 1)
                problems.Add("stars: population exceeds the vertex budget");
            if (_shellStars > 400)
                problems.Add("stars: " + _shellStars + " distance-unknown rows (expected about 146) — " +
                             "a parse that lost the distance column looks exactly like this");
        }

        /// <summary>
        /// Hide the rows a <c>CelestialBodyView</c> is already drawing, so the Sun is not both a
        /// body and a point. Driven by what the views have, never by what this layer remembers
        /// doing — the mirror-image bug is a layer that skips a star forever after one merge.
        /// </summary>
        public void HideRows(IEnumerable<int> rows)
        {
            _hidden.Clear();
            if (rows != null) foreach (int row in rows) _hidden.Add(row);
            _volume?.SetHidden(_hidden);
        }

        /// <summary>What the observed-sky readout quotes: how many stars pass the limit from here.</summary>
        public int VisibleCount(Double3 vantageParsecs, double limitMagnitude) =>
            StarQuery.CountVisibleFrom(_catalog, vantageParsecs, limitMagnitude);

        private void DestroyShell()
        {
            if (_shellObject != null) Destroy(_shellObject);
            if (_shellMaterial != null) Destroy(_shellMaterial);
            if (_shellMesh != null) Destroy(_shellMesh);
            _shellObject = null;
            _shellRenderer = null;
            _shellMaterial = null;
            _shellMesh = null;
            _shellStars = 0;
        }

        private void OnDestroy()
        {
            _volume?.Clear();
            DestroyShell();
        }
    }
}
