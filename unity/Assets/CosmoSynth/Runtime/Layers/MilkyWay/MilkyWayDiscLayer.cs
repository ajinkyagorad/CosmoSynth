using System;
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The Milky Way seen from outside: a disc, a bulge, and the arms picked out in it.
    /// CONTRACT §5.4 <c>mwDisc</c>.
    ///
    /// This is the layer that answers "why is it empty out here". Past a few kiloparsecs per metre
    /// the catalogue stars are all within 130 parsecs of the Sun and vanish into one speck, the
    /// dust cube is a grain, and there is genuinely nothing else — so the viewer flies out of a
    /// galaxy they cannot see and into a black room. The galaxy has to be drawn.
    ///
    /// <b>It is Procedural and says so.</b> Nobody has photographed the Milky Way from outside;
    /// every picture of it ever published is an artist's reconstruction. What constrains this one
    /// is real: the scale length and scale height of the disc, the bulge radius, R0, and the seven
    /// fitted arms — so the shape is an inference from measurements even though no individual
    /// point is a star anyone has seen. The provenance glyph is Procedural, the note says
    /// "constrained by the maser arm fit", and the layer fades out as the real stars and dust take
    /// over from inside, so the two are never both claiming to be the same thing.
    ///
    /// Deterministic: the same seed gives the same galaxy on every run and every device. A cloud
    /// that reshuffled between frames would be obvious; one that reshuffled between sessions would
    /// just quietly make screenshots incomparable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MilkyWayDiscLayer : CosmoLayer
    {
        public const int DiscPoints = 22000;
        public const int BulgePoints = 6000;
        public const int ArmPoints = 12000;

        /// <summary>Exponential scale length of the disc, kpc (INFERRED; the commonly adopted value).</summary>
        public const double ScaleLengthKpc = 2.6;

        private ChunkedPoints _points;
        private readonly List<Double3> _positionsKpc = new List<Double3>();
        private readonly List<Color> _colours = new List<Color>();
        private readonly List<float> _magnitudes = new List<float>();

        public override string LayerId => "mwDisc";

        public override IReadOnlyList<string> DataKinds => new string[0];

        /// <summary>
        /// 40,000 points, 160 k vertices, at most 8 chunk draws. §7.3 has no row of its own for
        /// this layer: it is taken from the slack in the galaxies row (16 draws / 340 k vertices,
        /// of which the galaxy layers here use about half) and needs its own row at integration.
        /// </summary>
        public override LayerBudget Budget => new LayerBudget(8, 160_000, 0.15f, 0f, 0f);

        public override LayerBlend Blend => LayerBlend.Additive;

        public override void OnData(string kind, object parsed) { }

        private void Awake()
        {
            Build();
            HasData = true;
        }

        private void Build()
        {
            _points?.Clear();
            _positionsKpc.Clear();
            _colours.Clear();
            _magnitudes.Clear();

            uint seed = 0x5EED4A11u;

            // The disc: exponential in radius, exponential in |z|, with a hole where the bulge is.
            for (int i = 0; i < DiscPoints; i++)
            {
                double r = -ScaleLengthKpc * Math.Log(1.0 - Next(ref seed) * 0.985);
                if (r > GalaxyModel.DiscRadiusKpc) r = GalaxyModel.DiscRadiusKpc * Next(ref seed);
                double a = Next(ref seed) * MathD.TwoPi;
                double z = ExponentialSigned(ref seed, GalaxyModel.DiscScaleHeightKpc);
                Add(new Double3(r * Math.Cos(a), r * Math.Sin(a), z),
                    GalaxyModel.DiscColourLinear, -1.5f);
            }

            // The bulge: a flattened spheroid, warmer and older.
            for (int i = 0; i < BulgePoints; i++)
            {
                double r = GalaxyModel.BulgeRadiusKpc * Math.Pow(Next(ref seed), 0.6);
                double a = Next(ref seed) * MathD.TwoPi;
                double cosT = Next(ref seed) * 2.0 - 1.0;
                double sinT = Math.Sqrt(Math.Max(0.0, 1.0 - cosT * cosT));
                Add(new Double3(r * sinT * Math.Cos(a), r * sinT * Math.Sin(a), r * cosT * 0.6),
                    GalaxyModel.BulgeColourLinear, -2.2f);
            }

            // The arms: scattered about the fitted curves, and ONLY over their fitted ranges, so
            // this cloud makes no claim the arm layer does not also make.
            for (int i = 0; i < ArmPoints; i++)
            {
                SpiralArm arm = GalaxyModel.Arms[(int)(Next(ref seed) * GalaxyModel.Arms.Length) %
                                                GalaxyModel.Arms.Length];
                double beta = MathD.Lerp(arm.BetaMinDeg, arm.BetaMaxDeg, Next(ref seed));
                Double3 p = GalaxyModel.ArmPointKpc(arm, beta);
                double spread = arm.WidthKpc * 2.0;
                Add(new Double3(
                        p.x + (Next(ref seed) - 0.5) * spread,
                        p.y + (Next(ref seed) - 0.5) * spread,
                        ExponentialSigned(ref seed, GalaxyModel.DiscScaleHeightKpc * 0.5)),
                    GalaxyModel.ArmColourLinear, -2.6f);
            }

            _points = new ChunkedPoints(transform, "Milky Way disc")
            {
                KmPerUnit = GalaxyModel.KmPerKiloparsec,
                ParsecsPerUnit = 1000.0,
            };
            _points.Build(_positionsKpc, Describe, maxPerChunk: 6000, maxChunks: 8);
            _points.CreateRenderers(CosmoShaders.Material(CosmoShaders.Point), 3050);
            // Soft and wide: this is a diffuse impression, not a resolved population, and pinpricks
            // would read as stars the viewer could fly to.
            _points.ConfigureLook(3.0f, 40.0f, 4.0f, -3.0f, 6.0f, 14.0f, space: 0f);
        }

        /// <summary>
        /// Points are authored in galactocentric kpc and stored in <b>ICRF</b> kpc, converted once
        /// here. The chunk machinery puts the eye into the layer's units from an ICRF position and
        /// hands the shader the one global ICRF rotation, so a population stored in any other
        /// frame would be rotated by a matrix that does not describe it — and it would still look
        /// like a galaxy, just not like ours, pointing the wrong way.
        /// </summary>
        private void Add(Double3 galactocentricKpc, Double3 colourLinear, float absoluteMagnitude)
        {
            _positionsKpc.Add(GalaxyModel.IcrfPcFrom(galactocentricKpc) / 1000.0);
            _colours.Add(CosmoTheme.FromLinear(colourLinear).gamma);
            _magnitudes.Add(absoluteMagnitude);
        }

        private readonly List<PointMesh.Sprite> _one = new List<PointMesh.Sprite>(1);

        private IReadOnlyList<PointMesh.Sprite> Describe(int i)
        {
            _one.Clear();
            _one.Add(new PointMesh.Sprite
            {
                Colour = _colours[i],
                Magnitude = _magnitudes[i],
                Shape = PointMesh.ShapeCore,
                Hidden = 0f,
            });
            return _one;
        }

        /// <summary>xorshift32 in [0, 1). Deterministic, seeded, and the same on every device.</summary>
        private static double Next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFFu) / 16777216.0;
        }

        private static double ExponentialSigned(ref uint state, double scale)
        {
            double u = Next(ref state);
            double magnitude = -scale * Math.Log(1.0 - u * 0.999);
            return Next(ref state) < 0.5 ? -magnitude : magnitude;
        }

        protected override void SetRenderersEnabled(bool on) => _points?.SetEnabled(on);

        protected override void ApplyCore(in FrameContext ctx) =>
            _points?.Apply(ctx, Camera.main, Weight, 1f, 0.3f, new Vector2(0f, 0f));

        public override void CollectDestinations(List<Destination> into)
        {
            into.Add(new Destination(
                "mw:centre", "Galactic Centre (Sgr A*)", DestinationKind.Structure, null, LayerId,
                GalaxyModel.GalacticCentreIcrfKm,
                GalaxyModel.DiscRadiusKpc * GalaxyModel.KmPerKiloparsec, 0.7,
                PresentationPlane.Galactic, ProvenanceClass.Measured,
                "Reid & Brunthaler 2004 (position); R0 Reid+2019"));

            into.Add(new Destination(
                "mw:disc", "Milky Way", DestinationKind.Galaxy, null, LayerId,
                GalaxyModel.GalacticCentreIcrfKm,
                GalaxyModel.DiscRadiusKpc * GalaxyModel.KmPerKiloparsec, 0.9,
                PresentationPlane.Galactic, ProvenanceClass.Procedural,
                "impression constrained by the Reid+2019 arm fit"));
        }

        public override void CollectLabels(List<LabelCandidate> into)
        {
            if (Weight <= 0f) return;
            into.Add(LabelCandidate.At("mw:centre", "Sgr A*", LabelCategory.MilkyWay,
                GalaxyModel.GalacticCentreIcrfKm, 1.0f, ProvenanceClass.Measured));
            into.Add(LabelCandidate.At("mw:sun", "The Sun", LabelCategory.MilkyWay,
                Double3.Zero, 0.9f, ProvenanceClass.Measured));
        }

        public override double NearestSurfaceKm(Double3 absoluteKm)
        {
            if (Weight <= 0f) return double.PositiveInfinity;
            // The galaxy as a body: distance to the disc's bounding sphere about the centre, so
            // flight speed at extragalactic scales is set by the Galaxy rather than by the Sun.
            double centre = Double3.Distance(absoluteKm, GalaxyModel.GalacticCentreIcrfKm);
            return Math.Max(0.0, centre - GalaxyModel.DiscRadiusKpc * GalaxyModel.KmPerKiloparsec);
        }

        public override void SelfCheck(List<string> problems)
        {
            if (_points == null) return;
            if (_points.PointCount != DiscPoints + BulgePoints + ArmPoints)
                problems.Add("mwDisc: " + _points.PointCount + " points, expected " +
                             (DiscPoints + BulgePoints + ArmPoints));
            if (_points.Chunks.Count > Budget.Draws)
                problems.Add("mwDisc: " + _points.Chunks.Count + " chunks, over its draw budget");
        }

        private void OnDestroy() => _points?.Clear();
    }
}
