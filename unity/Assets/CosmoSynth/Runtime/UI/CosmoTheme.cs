using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The one place colour, type size and panel construction are decided.
    ///
    /// Before this existed each panel invented its own. <c>ProvenanceHud</c> and
    /// <c>SpatialPanel</c> both defined the provenance colours and they had already drifted —
    /// "measured" was (0.45, 0.85, 0.55) in one file and (0.45, 0.90, 0.55) in the other — so the
    /// same fact was a different green depending on which surface you read it on. Two panels
    /// generated near-identical backing textures with different alphas and different edge
    /// falloffs. Nothing was shared, so nothing was consistent, and there was no way to change
    /// the look of the app except by editing every file that draws.
    ///
    /// <b>Sizes are angular, not linear.</b> This is the part a flat-screen habit gets wrong. A
    /// panel worn on the wrist is read at 35 cm and one beside a planet at 3 m; a type size in
    /// metres that suits one is illegible or absurd on the other. Everything here is expressed in
    /// <b>dmm</b> — millimetres subtended at one metre, which is a milliradian, which is the
    /// angle the eye actually resolves. Multiply by the viewing distance to get metres. That
    /// makes "is this readable?" a question with one answer instead of one per surface.
    /// </summary>
    public static class CosmoTheme
    {
        // ---- Palette ---------------------------------------------------------
        //
        // Tuned against two backdrops the app genuinely has to work on: black space, where an
        // unbordered dark panel simply disappears, and passthrough, where a bright room shows
        // through anything translucent. Hence a defined stroke on every panel and a surface
        // alpha high enough to hold contrast over a lit wall.

        /// <summary>Panel fill. Deliberately near-opaque — see the class remarks.</summary>
        public static readonly Color Surface = new Color(0.043f, 0.063f, 0.090f, 0.93f);

        /// <summary>Fill for a raised row: the selected item in a list.</summary>
        public static readonly Color SurfaceRaised = new Color(0.106f, 0.169f, 0.235f, 0.96f);

        /// <summary>Panel edge. Without this a dark panel has no silhouette against space.</summary>
        public static readonly Color Stroke = new Color(0.180f, 0.278f, 0.376f, 0.95f);

        /// <summary>Edge of the panel that currently has input.</summary>
        public static readonly Color StrokeActive = new Color(0.310f, 0.640f, 0.820f, 1f);

        /// <summary>Headings and values. ~15:1 against <see cref="Surface"/>.</summary>
        public static readonly Color TextPrimary = new Color(0.910f, 0.941f, 0.969f);

        /// <summary>Labels and supporting text. ~7:1 — still comfortably readable.</summary>
        public static readonly Color TextSecondary = new Color(0.604f, 0.682f, 0.761f);

        /// <summary>Hints and units. ~3.5:1: legible, never used for anything load-bearing.</summary>
        public static readonly Color TextMuted = new Color(0.392f, 0.471f, 0.549f);

        /// <summary>Interactive accent. Cyan, chosen because no body in the catalogue is cyan —
        /// so "this is a control" is never confused with "this is a planet".</summary>
        public static readonly Color Accent = new Color(0.310f, 0.765f, 0.969f);

        /// <summary>Warning / not-to-scale notices.</summary>
        public static readonly Color Warning = new Color(0.949f, 0.694f, 0.361f);

        /// <summary>
        /// Provenance colours — the app's most meaning-bearing palette, defined once.
        ///
        /// Balanced for roughly equal perceived lightness, so the five read as a set of
        /// categories rather than as a scale from good to bad. They are never the only channel:
        /// every surface that uses them also carries a glyph, because roughly one man in twelve
        /// cannot separate the green from the amber.
        /// </summary>
        public static Color Provenance(ProvenanceClass provenanceClass) => provenanceClass switch
        {
            ProvenanceClass.Measured => new Color(0.435f, 0.851f, 0.541f),
            ProvenanceClass.Inferred => new Color(0.498f, 0.706f, 0.961f),
            ProvenanceClass.Procedural => new Color(0.949f, 0.694f, 0.361f),
            ProvenanceClass.Fictional => new Color(0.894f, 0.541f, 0.847f),
            _ => new Color(0.541f, 0.576f, 0.627f)
        };

        /// <summary>
        /// The glyph that carries the same meaning as the colour, for the colour-blind and for
        /// any surface that cannot tint part of a string.
        /// </summary>
        public static char ProvenanceGlyph(ProvenanceClass provenanceClass) => provenanceClass switch
        {
            ProvenanceClass.Measured => '=',
            ProvenanceClass.Inferred => '~',
            ProvenanceClass.Procedural => '*',
            ProvenanceClass.Fictional => '?',
            _ => '·'
        };

        // ---- Type scale, in dmm (millimetres at one metre) --------------------
        //
        // Calibrated against a captured frame rather than guessed: the anchor beacon's readout
        // measured 26 dmm on screen and reads as comfortably large, so body text sits just under
        // it. Meta's own guidance is a ~1 degree minimum for sustained reading; 24 dmm of line
        // height is about 1.4 degrees per line and 0.9 of a degree of cap height, which clears it.

        public const float TypeDisplay = 44f;
        public const float TypeTitle = 32f;
        public const float TypeBody = 24f;
        public const float TypeCaption = 19f;

        /// <summary>Diagnostics only. Below the comfortable-reading floor on purpose: this is
        /// text you consult, not text you read.</summary>
        public const float TypeMicro = 16f;

        // ---- Spacing, also in dmm --------------------------------------------

        public const float SpaceTight = 8f;
        public const float SpaceRow = 14f;
        public const float SpacePanelPadding = 22f;
        public const float CornerRadius = 26f;
        public const float StrokeWidth = 3f;

        /// <summary>Convert an angular size to metres at a given viewing distance.</summary>
        public static float Metres(float dmm, float viewingDistanceMetres) =>
            dmm * 0.001f * Mathf.Max(0.05f, viewingDistanceMetres);

        // ---- Panel art -------------------------------------------------------

        /// <summary>
        /// The rounded-rectangle panel sprite: dark fill, a defined stroke, and antialiased
        /// corners.
        ///
        /// Generated rather than shipped, so there is no asset to import and no import setting to
        /// get wrong. It is drawn from a signed distance to the rounded rectangle, which is what
        /// makes the corners and the stroke crisp at any size — the previous panels used a
        /// smoothstep on the distance to the nearest <i>edge</i>, which is not a corner radius at
        /// all: it produces a rectangle with a soft haze all the way round, and stretching a
        /// 64-pixel texture across 13 cm turned that haze into a centimetre of mush with no
        /// discernible edge.
        ///
        /// The texture is generated once and shared. Nine-slicing would be better still, but a
        /// single sprite at this resolution holds up to the sizes these panels are actually drawn
        /// at and costs one texture for the whole application.
        /// </summary>
        public static Texture2D PanelTexture
        {
            get
            {
                if (_panelTexture != null) return _panelTexture;
                _panelTexture = BuildPanelTexture(256, 0.16f, 0.018f);
                return _panelTexture;
            }
        }

        private static Texture2D _panelTexture;
        private static Texture2D _rowTexture;

        /// <summary>A softer, borderless fill for the selected row inside a list.</summary>
        public static Texture2D RowTexture
        {
            get
            {
                if (_rowTexture != null) return _rowTexture;
                _rowTexture = BuildPanelTexture(128, 0.34f, 0f);
                return _rowTexture;
            }
        }

        /// <summary>
        /// Draw a rounded rectangle into a square texture.
        /// </summary>
        /// <param name="resolution">Texture edge, pixels.</param>
        /// <param name="cornerFraction">Corner radius as a fraction of half the edge.</param>
        /// <param name="strokeFraction">Stroke width as a fraction of the edge. 0 for no stroke.</param>
        private static Texture2D BuildPanelTexture(int resolution, float cornerFraction, float strokeFraction)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, mipChain: true, linear: false)
            {
                name = "CosmoPanel",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[resolution * resolution];
            float radius = cornerFraction;
            // One pixel, in the normalised space the distance field is measured in. The edge is
            // faded across roughly this much, which is what antialiases it.
            float feather = 1.5f / resolution;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // Position in [-0.5, 0.5], so the rectangle is the unit square centred on 0.
                    float px = (x + 0.5f) / resolution - 0.5f;
                    float py = (y + 0.5f) / resolution - 0.5f;

                    // Signed distance to a rounded rectangle: negative inside, positive outside.
                    float qx = Mathf.Abs(px) - (0.5f - radius);
                    float qy = Mathf.Abs(py) - (0.5f - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                               Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                    // Coverage of the shape, and coverage of the stroke band just inside its edge.
                    float fill = 1f - Mathf.SmoothStep(-feather, feather, distance);
                    float stroke = strokeFraction <= 0f
                        ? 0f
                        : (1f - Mathf.SmoothStep(-feather, feather, distance)) *
                          Mathf.SmoothStep(-strokeFraction - feather, -strokeFraction + feather, distance);

                    // Fill and stroke are composited into the texture rather than left to two
                    // quads. One draw call, and no chance of the two drifting apart in size.
                    Color colour = Color.Lerp(Surface, Stroke, Mathf.Clamp01(stroke));
                    float alpha = Mathf.Clamp01(fill) * Mathf.Lerp(Surface.a, Stroke.a, Mathf.Clamp01(stroke));

                    pixels[y * resolution + x] = new Color(colour.r, colour.g, colour.b, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return texture;
        }

        /// <summary>
        /// A material for panel art that fades as a whole.
        ///
        /// <c>Unlit/Transparent</c> ignores vertex and material colour entirely, which is why the
        /// old panels could not fade: the wrist dashboard faded its text and left the backing at
        /// full opacity, so the panel snapped into existence and then its contents drifted in
        /// after it. <c>Sprites/Default</c> multiplies by the material tint, so one alpha drives
        /// the whole surface.
        /// </summary>
        public static Material PanelMaterial()
        {
            Material material = CosmoShaders.Material(CosmoShaders.SpritesDefault);
            material.mainTexture = PanelTexture;
            // Above transparent geometry — the atmosphere shells — so a panel in front of a
            // planet is not sorted behind its own limb.
            material.renderQueue = 3200;
            return material;
        }

        /// <summary>As <see cref="PanelMaterial"/>, for the borderless row highlight.</summary>
        public static Material RowMaterial()
        {
            Material material = CosmoShaders.Material(CosmoShaders.SpritesDefault);
            material.mainTexture = RowTexture;
            material.renderQueue = 3201;
            return material;
        }

        // ---- Orbit colour ----------------------------------------------------

        /// <summary>
        /// The colour one body's orbit is drawn in.
        ///
        /// Taking the planet's own colour and using it directly does not work: Mercury and the
        /// Moon are near-black, Pluto is brown, and their paths vanish. Taking a single blue for
        /// everything — which is what the app used to do — gives the wireframe-diagram look, a
        /// dozen identical hairlines with nothing to say which belongs to which world.
        ///
        /// So the hue comes from the body and the lightness is normalised: enough saturation to
        /// associate the path with its planet at a glance, and a floor on luminance so no orbit
        /// is ever drawn in a colour the display cannot separate from black.
        /// </summary>
        public static Color OrbitColour(Double3 displayColor)
        {
            var body = new Color((float)displayColor.x, (float)displayColor.y, (float)displayColor.z);

            Color.RGBToHSV(body, out float hue, out float saturation, out float value);

            // A body with no useful hue (grey rock, ice) borrows the interface accent rather than
            // being drawn in grey, which at one pixel wide is indistinguishable from an artefact.
            if (saturation < 0.12f)
            {
                Color.RGBToHSV(Accent, out hue, out _, out _);
                saturation = 0.45f;
            }

            return Color.HSVToRGB(hue, Mathf.Clamp(saturation * 1.25f, 0.35f, 0.80f), 0.95f);
        }
    }
}
