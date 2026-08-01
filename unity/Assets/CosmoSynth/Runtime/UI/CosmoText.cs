using System.Text;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Text creation and — the part that was missing — text <i>measurement</i>.
    ///
    /// Every panel in this app was laid out by choosing a character size and a panel size
    /// independently and hoping. They did not agree. The wrist dashboard set
    /// <c>characterSize = 0.0034</c> at <c>fontSize = 72</c>, which is a line 3.4 cm tall, and
    /// drew twelve of them onto a backing quad 12.4 cm high: the panel showed three and a half
    /// lines and the remaining eight hung off the bottom into empty space. The spatial panel was
    /// out by a factor of four the same way. Neither is visible in a screenshot of the default
    /// view — one needs a tracked hand and the other needs a selection — which is exactly why it
    /// survived.
    ///
    /// The fix is not a better guess. It is to measure: ask the font for its metrics, lay the
    /// text out against them, and size the container to the result. Then a panel cannot overflow,
    /// because its size is derived from its contents rather than asserted alongside them.
    ///
    /// <b>On <c>TextMesh.fontSize</c>.</b> The code this replaces described it as a quality
    /// setting — "high, then scaled down — keeps glyphs crisp up close". It is not. Rendered size
    /// is <c>fontSize × characterSize × 0.1</c> font pixels of world space, so raising fontSize
    /// for crispness silently makes the text bigger, and that belief is how three panels ended up
    /// mis-sized in three different directions. Here fontSize is fixed and
    /// <see cref="CharacterSizeFor"/> solves for the character size that yields a wanted line
    /// height.
    /// </summary>
    public static class CosmoText
    {
        /// <summary>
        /// The atlas resolution glyphs are rasterised at. Fixed, because it is one half of the
        /// size calculation and varying it per panel is what made the layouts incomparable.
        /// High enough that a panel held at arm's length is not visibly soft.
        /// </summary>
        public const int FontSize = 64;

        /// <summary>World units per font pixel, for a given character size. Unity's own factor.</summary>
        private const float WorldPerFontPixel = 0.1f;

        private static Font _font;
        private static Material _textMaterial;

        /// <summary>The built-in dynamic font every surface shares.</summary>
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                // Unity renamed the built-in font; fall back so this works on either.
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        /// <summary>
        /// The material every glyph in the app is drawn with — the font's own, at a render queue
        /// above the panels.
        ///
        /// Both are transparent and transparent geometry is ordered by queue before it is ordered
        /// by distance, so leaving text on the font's default 3000 and panels on 3200 draws the
        /// panel <i>over</i> its own text. A panel whose contents are hidden behind it is not a
        /// subtle bug, but it only appears once both are on screen together.
        ///
        /// Cloning the font material means owning the atlas-rebuild problem: a dynamic font
        /// repacks its texture whenever a new glyph is requested, and a clone left holding the
        /// previous atlas renders the right glyphs from the wrong places — text turns to confetti
        /// the first time a new character appears. Hence the subscription.
        /// </summary>
        public static Material TextMaterial
        {
            get
            {
                if (_textMaterial != null) return _textMaterial;
                if (Font == null || Font.material == null) return null;

                _textMaterial = new Material(Font.material) { name = "CosmoText", renderQueue = 3210 };

                // Static event, named explicitly so it is not mistaken for one on the instance
                // the property above returns.
                UnityEngine.Font.textureRebuilt -= OnFontTextureRebuilt;
                UnityEngine.Font.textureRebuilt += OnFontTextureRebuilt;
                return _textMaterial;
            }
        }

        private static void OnFontTextureRebuilt(Font font)
        {
            if (_textMaterial != null && font == _font) _textMaterial.mainTexture = font.material.mainTexture;
        }

        /// <summary>Height of one line, in world units, at a given character size.</summary>
        public static float LineHeight(float characterSize)
        {
            Font font = Font;
            if (font == null || font.fontSize <= 0) return characterSize * FontSize * 0.138f;
            return font.lineHeight * (FontSize / (float)font.fontSize) * characterSize * WorldPerFontPixel;
        }

        /// <summary>
        /// The character size that produces a wanted line height. The inverse of
        /// <see cref="LineHeight"/>, and the only way a caller should ever set a text size.
        /// </summary>
        public static float CharacterSizeFor(float lineHeightMetres)
        {
            float unit = LineHeight(1f);
            return unit > 1e-9f ? lineHeightMetres / unit : lineHeightMetres * 0.11f;
        }

        /// <summary>
        /// Width of a single line of text, in world units.
        ///
        /// Measured from the font's own advance widths rather than from the renderer's bounds,
        /// because bounds are only valid once the mesh has been rebuilt — which happens during
        /// rendering, a frame after the text was set. Laying out against a stale bound is how a
        /// panel ends up correctly sized for the string it held last frame.
        /// </summary>
        public static float MeasureWidth(string line, float characterSize)
        {
            if (string.IsNullOrEmpty(line)) return 0f;

            Font font = Font;
            if (font == null) return line.Length * characterSize * FontSize * 0.5f * WorldPerFontPixel;

            font.RequestCharactersInTexture(line, FontSize, FontStyle.Normal);

            float pixels = 0f;
            for (int i = 0; i < line.Length; i++)
            {
                pixels += font.GetCharacterInfo(line[i], out CharacterInfo info, FontSize, FontStyle.Normal)
                    ? info.advance
                    // A glyph the atlas does not have still occupies space; half an em is the
                    // usual approximation and beats treating it as zero width.
                    : FontSize * 0.5f;
            }

            return pixels * characterSize * WorldPerFontPixel;
        }

        /// <summary>Width and height of a block of text, in world units.</summary>
        public static Vector2 Measure(string text, float characterSize)
        {
            if (string.IsNullOrEmpty(text)) return Vector2.zero;

            string[] lines = text.Split('\n');
            float widest = 0f;
            for (int i = 0; i < lines.Length; i++) widest = Mathf.Max(widest, MeasureWidth(lines[i], characterSize));

            return new Vector2(widest, lines.Length * LineHeight(characterSize));
        }

        /// <summary>
        /// Break text so no line exceeds a width, in world units.
        ///
        /// Breaks on spaces where it can and mid-word where it must, so a long unbroken token —
        /// an exoplanet designation, say — is truncated rather than allowed to run off the panel.
        /// </summary>
        public static string Wrap(string text, float characterSize, float maximumWidthMetres)
        {
            if (string.IsNullOrEmpty(text) || maximumWidthMetres <= 0f) return text;

            var wrapped = new StringBuilder(text.Length + 16);
            string[] paragraphs = text.Split('\n');

            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) wrapped.Append('\n');

                string paragraph = paragraphs[p];
                if (MeasureWidth(paragraph, characterSize) <= maximumWidthMetres)
                {
                    wrapped.Append(paragraph);
                    continue;
                }

                float lineWidth = 0f;
                int lineStart = 0;
                int lastBreak = -1;

                for (int i = 0; i < paragraph.Length; i++)
                {
                    lineWidth += MeasureWidth(paragraph[i].ToString(), characterSize);
                    if (paragraph[i] == ' ') lastBreak = i;

                    if (lineWidth <= maximumWidthMetres) continue;

                    // Prefer the last space; fall back to breaking here.
                    int breakAt = lastBreak > lineStart ? lastBreak : i;
                    wrapped.Append(paragraph, lineStart, breakAt - lineStart).Append('\n');

                    lineStart = breakAt + (lastBreak > lineStart ? 1 : 0);
                    lastBreak = -1;
                    lineWidth = MeasureWidth(paragraph.Substring(lineStart, i - lineStart + 1), characterSize);
                }

                if (lineStart < paragraph.Length) wrapped.Append(paragraph, lineStart, paragraph.Length - lineStart);
            }

            return wrapped.ToString();
        }

        /// <summary>Shorten to fit a width, ending in an ellipsis rather than a hard cut.</summary>
        public static string Truncate(string text, float characterSize, float maximumWidthMetres)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (MeasureWidth(text, characterSize) <= maximumWidthMetres) return text;

            for (int length = text.Length - 1; length > 0; length--)
            {
                string candidate = text.Substring(0, length) + "…";
                if (MeasureWidth(candidate, characterSize) <= maximumWidthMetres) return candidate;
            }
            return "…";
        }

        /// <summary>
        /// Create a text object with the project's defaults.
        /// </summary>
        /// <param name="lineHeightMetres">Wanted line height in world units. Callers should get
        /// this from <see cref="CosmoTheme.Metres"/> so the size is angular, not linear.</param>
        public static TextMesh Create(Transform parent, string name, float lineHeightMetres,
            TextAnchor anchor, Color colour)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<TextMesh>();
            text.font = Font;
            text.fontSize = FontSize;
            text.characterSize = CharacterSizeFor(lineHeightMetres);
            text.anchor = anchor;
            text.alignment = anchor == TextAnchor.UpperCenter || anchor == TextAnchor.MiddleCenter ||
                             anchor == TextAnchor.LowerCenter
                ? TextAlignment.Center
                : TextAlignment.Left;
            text.color = colour;
            text.richText = false;

            var renderer = textObject.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // TextMesh does not assign its own material; without this the glyphs draw with
            // whatever the primitive came with, which is the pink error material in a player.
            Material material = TextMaterial;
            if (material != null) renderer.sharedMaterial = material;

            return text;
        }
    }
}
