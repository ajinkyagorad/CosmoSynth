using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The one place colour, type size and surface art are decided.
    ///
    /// Before this existed each panel invented its own, and the provenance colours had already
    /// drifted between two files, so the same fact was a different green depending which surface
    /// you read it on.
    ///
    /// <b>Sizes are angular, not linear.</b> Everything here is in <b>dmm</b> — millimetres
    /// subtended at one metre, which is a milliradian, which is the angle the eye resolves.
    /// Multiply by the viewing distance for metres. A panel worn on the wrist is read at 42 cm and
    /// one beside a planet at 3 m; a size in metres that suits one is illegible or absurd on the
    /// other.
    /// </summary>
    public static class CosmoTheme
    {
        // ---- Palette ---------------------------------------------------------

        /// <summary>Panel fill. Deliberately near-opaque: it has to hold contrast over a lit wall.</summary>
        public static readonly Color Surface = new Color(0.043f, 0.063f, 0.090f, 0.93f);

        /// <summary>Fill for a raised row: hover, and the selected item in a list.</summary>
        public static readonly Color SurfaceRaised = new Color(0.106f, 0.169f, 0.235f, 0.96f);

        /// <summary>Panel edge. Without this a dark panel has no silhouette against space.</summary>
        public static readonly Color Stroke = new Color(0.180f, 0.278f, 0.376f, 0.95f);

        /// <summary>Edge of the surface that currently has input.</summary>
        public static readonly Color StrokeActive = new Color(0.310f, 0.640f, 0.820f, 1f);

        /// <summary>Headings and values. ~15:1 against <see cref="Surface"/>.</summary>
        public static readonly Color TextPrimary = new Color(0.910f, 0.941f, 0.969f);

        /// <summary>Labels and supporting text. ~7:1.</summary>
        public static readonly Color TextSecondary = new Color(0.604f, 0.682f, 0.761f);

        /// <summary>Hints, units and disabled controls. ~3.5:1: legible, never load-bearing.</summary>
        public static readonly Color TextMuted = new Color(0.392f, 0.471f, 0.549f);

        /// <summary>
        /// Interactive accent (§5.6.9).
        ///
        /// Moved from (0.310, 0.765, 0.969), which sat 14 degrees of hue from the Inferred blue —
        /// close enough that an Accent control and an Inferred fact read as the same category.
        /// This one is 28 degrees from Inferred and 50 from Measured, and still cyan, which is a
        /// colour no body in the catalogue has.
        /// </summary>
        public static readonly Color Accent = new Color(0.200f, 0.867f, 0.929f);

        /// <summary>
        /// Warnings and every honesty chip (§5.6.9).
        ///
        /// Moved from (0.949, 0.694, 0.361), which was <b>byte-identical</b> to the Procedural
        /// provenance colour — so "SIZES NOT TO SCALE" was drawn in the colour that means
        /// "procedurally generated" and read as a category rather than as a warning. Coral, and
        /// always with a <c>!</c> glyph, because colour is never the only channel.
        /// </summary>
        public static readonly Color Warning = new Color(0.973f, 0.451f, 0.388f);

        /// <summary>Plain white, as a token so no other file has to name a colour to get one.</summary>
        public static readonly Color Neutral = new Color(1f, 1f, 1f, 1f);

        /// <summary>Fully transparent, for a rect that is laid out but not drawn.</summary>
        public static readonly Color Invisible = new Color(0f, 0f, 0f, 0f);

        // ---- Render clears ---------------------------------------------------

        /// <summary>
        /// The passthrough clear. <b>Alpha zero is the whole point.</b> Quest composites
        /// passthrough underneath the app's framebuffer, so alpha is coverage: (0,0,0,1) is an
        /// opaque black wall that looks correct everywhere except on the headset.
        /// </summary>
        public static readonly Color PassthroughClear = new Color(0f, 0f, 0f, 0f);

        /// <summary>The full-space clear: very dark blue, opaque, for when there is no room to show.</summary>
        public static readonly Color SpaceClear = new Color(0.004f, 0.005f, 0.011f, 1f);

        /// <summary>Ambient fill, so a night side reads as unlit rather than as missing geometry.</summary>
        public static readonly Color AmbientFill = new Color(0.05f, 0.055f, 0.07f, 1f);

        /// <summary>The tint of ring particles: dirty ice, warmer than the panel palette.</summary>
        public static readonly Color RingDust = new Color(0.88f, 0.84f, 0.76f, 1f);

        // ---- Combinators -----------------------------------------------------

        /// <summary>The same colour at a different opacity.</summary>
        public static Color WithAlpha(Color colour, float alpha) =>
            new Color(colour.r, colour.g, colour.b, alpha);

        /// <summary>The same colour, brighter or dimmer. Alpha is untouched.</summary>
        public static Color Scaled(Color colour, float gain) =>
            new Color(colour.r * gain, colour.g * gain, colour.b * gain, colour.a);

        /// <summary>Mix a colour toward white. 1 keeps it, 0 makes it white.</summary>
        public static Color Desaturate(Color colour, float saturation) =>
            Color.Lerp(Neutral, colour, saturation);

        /// <summary>A Core colour triple as a Unity colour.</summary>
        public static Color FromLinear(Double3 rgb) =>
            new Color((float)rgb.x, (float)rgb.y, (float)rgb.z, 1f);

        /// <summary>
        /// Provenance colours — the app's most meaning-bearing palette, defined once.
        ///
        /// They appear <b>only</b> on provenance glyphs, badges and data text. Controls use
        /// neutrals and Accent and are told apart by caption and position, never by hue, so a
        /// category colour can never be mistaken for a claim about where a number came from.
        /// </summary>
        public static Color Provenance(ProvenanceClass provenanceClass) => provenanceClass switch
        {
            ProvenanceClass.Measured => new Color(0.435f, 0.851f, 0.541f),
            ProvenanceClass.Inferred => new Color(0.498f, 0.706f, 0.961f),
            ProvenanceClass.Procedural => new Color(0.949f, 0.694f, 0.361f),
            ProvenanceClass.Fictional => new Color(0.894f, 0.541f, 0.847f),
            _ => new Color(0.541f, 0.576f, 0.627f)
        };

        /// <summary>The glyph that carries the same meaning as the colour, for the colour-blind.</summary>
        public static char ProvenanceGlyph(ProvenanceClass provenanceClass) => provenanceClass switch
        {
            ProvenanceClass.Measured => '=',
            ProvenanceClass.Inferred => '~',
            ProvenanceClass.Procedural => '*',
            ProvenanceClass.Fictional => '?',
            _ => '·'
        };

        // ---- Type scale, in dmm (millimetres at one metre) --------------------

        public const float TypeDisplay = 44f;
        public const float TypeTitle = 32f;
        public const float TypeBody = 24f;
        public const float TypeCaption = 19f;

        /// <summary>Diagnostics only: text you consult, not text you read.</summary>
        public const float TypeMicro = 16f;

        // ---- Spacing, also in dmm --------------------------------------------

        public const float SpaceTight = 8f;
        public const float SpaceRow = 14f;
        public const float SpacePanelPadding = 22f;
        public const float CornerRadius = 26f;
        public const float StrokeWidth = 3f;

        /// <summary>Button row height (§5.6.3): 60 dmm, 25 mm at the 0.42 m wrist distance.</summary>
        public const float ButtonHeight = 60f;

        /// <summary>Gap between buttons, 16 dmm.</summary>
        public const float ButtonGap = 16f;

        /// <summary>Minimum button width, 150 dmm.</summary>
        public const float ButtonMinWidth = 150f;

        /// <summary>Distance a worn panel is read at, and the one its dmm are converted through.</summary>
        public const float WristDistanceMetres = 0.42f;

        /// <summary>Convert an angular size to metres at a given viewing distance.</summary>
        public static float Metres(float dmm, float viewingDistanceMetres) =>
            dmm * 0.001f * Mathf.Max(0.05f, viewingDistanceMetres);

        // ---- Surface art -----------------------------------------------------
        //
        // One atlas, two tiles, so every rectangle a surface draws — the backing, every button,
        // every chip, the selection bar — comes from one texture and therefore one material and
        // one mesh. §7.3 allows the whole interface SIX draw calls; one TextMesh and one quad per
        // row would be fifty.

        /// <summary>Left half of the atlas: a rounded rectangle with a stroke.</summary>
        public static readonly Rect AtlasPanel = new Rect(0f, 0f, 0.5f, 1f);

        /// <summary>Right half: a softer borderless fill, for rows and chips.</summary>
        public static readonly Rect AtlasFill = new Rect(0.5f, 0f, 0.5f, 1f);

        private static Texture2D _surfaceAtlas;

        public static Texture2D SurfaceAtlas
        {
            get
            {
                if (_surfaceAtlas != null) return _surfaceAtlas;
                _surfaceAtlas = BuildAtlas(128);
                return _surfaceAtlas;
            }
        }

        /// <summary>
        /// The material every surface's chrome is drawn with.
        ///
        /// <c>CosmoSynth/Panel</c> is premultiplied-over and <b>writes coverage</b>: a panel is
        /// meant to be readable against a bright room, so it occludes passthrough in proportion to
        /// its own opacity. Colour arrives per vertex, which is what lets one mesh hold a dark
        /// backing, a hovered row and an Accent chip at once.
        /// </summary>
        public static Material SurfaceMaterial()
        {
            Material material = CosmoShaders.Material(CosmoShaders.Panel);
            material.SetTexture("_BaseMap", SurfaceAtlas);
            material.renderQueue = 3200;
            return material;
        }

        /// <summary>
        /// Draw the two tiles into one texture: a rounded rectangle each, differing only in corner
        /// radius, with the shape carried entirely in <b>alpha</b>.
        ///
        /// Alpha only, because <c>CosmoSynth/Panel</c> takes its colour from the vertex and samples
        /// only the mask's alpha — an RGB detail baked into the texture would never be drawn. A
        /// border is therefore two rectangles (<c>CosmoSurface.Rect</c> draws them), not a bright
        /// ring in the texture.
        ///
        /// Both tiles are signed-distance rounded rectangles. The previous panels smoothstepped the
        /// distance to the nearest <i>edge</i>, which is not a corner radius: it makes a rectangle
        /// with a soft haze all round, and stretched across 13 cm the haze became a centimetre of
        /// mush with no discernible edge.
        /// </summary>
        private static Texture2D BuildAtlas(int tile)
        {
            var texture = new Texture2D(tile * 2, tile, TextureFormat.RGBA32, mipChain: true, linear: false)
            {
                name = "CosmoSurfaceAtlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[tile * 2 * tile];
            for (int t = 0; t < 2; t++)
            {
                float corner = t == 0 ? 0.16f : 0.34f;
                float feather = 1.5f / tile;

                for (int y = 0; y < tile; y++)
                {
                    for (int x = 0; x < tile; x++)
                    {
                        float px = (x + 0.5f) / tile - 0.5f;
                        float py = (y + 0.5f) / tile - 0.5f;

                        float qx = Mathf.Abs(px) - (0.5f - corner);
                        float qy = Mathf.Abs(py) - (0.5f - corner);
                        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                                   Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                        float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner;

                        float fill = Mathf.Clamp01(1f - Mathf.SmoothStep(-feather, feather, distance));
                        pixels[y * tile * 2 + t * tile + x] = new Color(1f, 1f, 1f, fill);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return texture;
        }

        // ---- Orbit colour ----------------------------------------------------

        /// <summary>
        /// The colour one body's orbit is drawn in.
        ///
        /// The body's own colour used directly does not work — Mercury and the Moon are
        /// near-black, Pluto is brown, and their paths vanish. A single blue for everything gives
        /// the wireframe-diagram look the user rejected in 1.4.0. So the hue comes from the body
        /// and the lightness is normalised.
        /// </summary>
        public static Color OrbitColour(Double3 displayColor)
        {
            var body = new Color((float)displayColor.x, (float)displayColor.y, (float)displayColor.z);

            Color.RGBToHSV(body, out float hue, out float saturation, out float value);

            if (saturation < 0.12f)
            {
                Color.RGBToHSV(Accent, out hue, out _, out _);
                saturation = 0.45f;
            }

            return Color.HSVToRGB(hue, Mathf.Clamp(saturation * 1.25f, 0.35f, 0.80f), 0.95f);
        }
    }
}
