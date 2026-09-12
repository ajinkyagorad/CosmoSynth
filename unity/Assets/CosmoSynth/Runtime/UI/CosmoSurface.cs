using System.Collections.Generic;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>Which atlas tile a rectangle is cut from.</summary>
    public enum SurfaceStyle { Panel, Fill }

    /// <summary>
    /// A world-space surface: one mesh of rectangles, one mesh of glyphs, and a hit test.
    ///
    /// <b>Two draw calls, whatever it holds.</b> That is the whole design. The three panels this
    /// replaces used one quad and one <c>TextMesh</c> per element, so a five-row list was a dozen
    /// draws and the twelve-row wrist dashboard was twenty-six — against a §7.3 allowance of six
    /// for the entire interface.
    ///
    /// <b>Nothing here takes a size in metres from its caller.</b> Sizes arrive in dmm and are
    /// converted through the viewing distance handed to <see cref="Begin"/>, so the same layout
    /// code produces a panel that reads identically on the wrist at 42 cm and pinned at 3 m. The
    /// panels this replaces each declared a width and a character size independently, and all
    /// three had them disagree — the wrist dashboard drew twelve 3.4 cm lines onto a 12.4 cm
    /// backing and showed three and a half of them.
    /// </summary>
    public sealed class CosmoSurface
    {
        private readonly Transform _root;
        private readonly MeshRenderer _chromeRenderer;
        private readonly Mesh _chrome;
        private readonly Material _material;
        private readonly CosmoTextMesh _text;

        private readonly List<Vector3> _vertices = new List<Vector3>(256);
        private readonly List<Vector2> _uvs = new List<Vector2>(256);
        private readonly List<Color> _colours = new List<Color>(256);
        private readonly List<int> _indices = new List<int>(384);

        private readonly List<Rect> _hitRects = new List<Rect>(64);
        private readonly List<int> _hitIds = new List<int>(64);

        private float _distance = CosmoTheme.WristDistanceMetres;
        private float _opacity = 1f;

        /// <summary>Panel extent in world metres, valid after <see cref="End"/>.</summary>
        public Vector2 Size { get; private set; }

        /// <summary>Position and orient this. Leave the children alone.</summary>
        public Transform Root => _root;

        /// <summary>The viewing distance the current layout was sized for.</summary>
        public float ViewingDistance => _distance;

        public CosmoSurface(Transform parent, string name)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            _root = host.transform;

            _chrome = new Mesh { name = name + " chrome" };
            _chrome.MarkDynamic();

            var chromeObject = new GameObject("Chrome");
            chromeObject.transform.SetParent(_root, false);
            chromeObject.AddComponent<MeshFilter>().sharedMesh = _chrome;
            _chromeRenderer = chromeObject.AddComponent<MeshRenderer>();
            _chromeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _chromeRenderer.receiveShadows = false;
            _chromeRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _chromeRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            _material = CosmoTheme.SurfaceMaterial();
            _chromeRenderer.sharedMaterial = _material;

            _text = new CosmoTextMesh(_root, "Text");
            _text.Root.localPosition = new Vector3(0f, 0f, -0.0015f);
        }

        /// <summary>Angular size to metres at this surface's viewing distance.</summary>
        public float Dmm(float dmm) => CosmoTheme.Metres(dmm, _distance);

        /// <summary>Width of a string at an angular type size, in metres.</summary>
        public float MeasureWidth(string text, float dmm) =>
            CosmoText.MeasureWidth(text, CosmoText.CharacterSizeFor(Dmm(dmm)));

        /// <summary>
        /// Start a layout. The first rectangle is reserved for the backing, which
        /// <see cref="End"/> fills in once the contents have decided how big it is.
        /// </summary>
        public void Begin(float viewingDistanceMetres)
        {
            _distance = Mathf.Clamp(viewingDistanceMetres, 0.15f, 40f);

            _vertices.Clear();
            _uvs.Clear();
            _colours.Clear();
            _indices.Clear();
            _hitRects.Clear();
            _hitIds.Clear();
            _text.Begin();

            // Reserved backing quads, patched in End. Reserved rather than appended because a mesh
            // draws in index order and the backing has to be behind everything on it. Two, so the
            // panel has a stroke: the second is inset by End.
            AddQuad(0f, 0f, 0f, 0f, CosmoTheme.Stroke, SurfaceStyle.Panel);
            AddQuad(0f, 0f, 0f, 0f, CosmoTheme.Surface, SurfaceStyle.Panel);
        }

        /// <summary>A rectangle, in metres, centred on (cx, cy) in the surface's local plane.</summary>
        public void Rect(float cx, float cy, float width, float height, Color colour,
            SurfaceStyle style = SurfaceStyle.Fill, int hitId = -1)
        {
            AddQuad(cx, cy, width, height, colour, style);
            if (hitId < 0) return;
            _hitRects.Add(new Rect(cx - 0.5f * width, cy - 0.5f * height, width, height));
            _hitIds.Add(hitId);
        }

        /// <summary>
        /// A rectangle with a defined edge, drawn as the stroke behind and the fill inset in front.
        ///
        /// Two quads rather than a bright ring in the mask, because <c>CosmoSynth/Panel</c> takes
        /// colour from the vertex and reads only the mask's alpha. Without a stroke a dark panel
        /// has no silhouette at all against space.
        /// </summary>
        public void StrokedRect(float cx, float cy, float width, float height, Color fill, Color stroke,
            SurfaceStyle style = SurfaceStyle.Panel, int hitId = -1)
        {
            float inset = 2f * Dmm(CosmoTheme.StrokeWidth);
            AddQuad(cx, cy, width, height, stroke, style);
            AddQuad(cx, cy, Mathf.Max(0f, width - inset), Mathf.Max(0f, height - inset), fill, style);
            if (hitId < 0) return;
            _hitRects.Add(new Rect(cx - 0.5f * width, cy - 0.5f * height, width, height));
            _hitIds.Add(hitId);
        }

        /// <summary>
        /// A hit box with no art, for a row that is only text. Padded by 3 mm as §5.6.8 asks,
        /// because a fingertip is not a mouse pointer.
        /// </summary>
        public void HitBox(float cx, float cy, float width, float height, int hitId)
        {
            _hitRects.Add(new Rect(cx - 0.5f * width - 0.003f, cy - 0.5f * height - 0.003f,
                width + 0.006f, height + 0.006f));
            _hitIds.Add(hitId);
        }

        /// <summary>Text with its left edge at x and the top of its line box at y, both metres.</summary>
        public void Text(string text, float x, float y, float dmm, Color colour) =>
            _text.AddLine(text, x, y, CosmoText.CharacterSizeFor(Dmm(dmm)), Fade(colour));

        /// <summary>Text centred horizontally on cx.</summary>
        public void TextCentred(string text, float cx, float y, float dmm, Color colour) =>
            _text.AddCentred(text, cx, y, CosmoText.CharacterSizeFor(Dmm(dmm)), Fade(colour));

        private Color Fade(Color colour) => CosmoTheme.WithAlpha(colour, colour.a * _opacity);

        /// <summary>Finish the layout at a size the caller derived from its own contents.</summary>
        public void End(float widthMetres, float heightMetres)
        {
            Size = new Vector2(widthMetres, heightMetres);
            PatchBacking(widthMetres, heightMetres);

            _chrome.Clear();
            _chrome.SetVertices(_vertices);
            _chrome.SetUVs(0, _uvs);
            _chrome.SetColors(_colours);
            _chrome.SetTriangles(_indices, 0, calculateBounds: true);

            _text.End();
            ApplyOpacity();
        }

        /// <summary>The id of the topmost hit box under a point in the surface's local plane.</summary>
        public int HitId(Vector2 local)
        {
            for (int i = _hitRects.Count - 1; i >= 0; i--)
                if (_hitRects[i].Contains(local)) return _hitIds[i];
            return -1;
        }

        /// <summary>
        /// Fade the whole surface together.
        ///
        /// The wrist dashboard faded only its text: the backing appeared instantly at full opacity
        /// and the contents drifted in afterwards, which reads as the panel being broken rather
        /// than as it arriving.
        /// </summary>
        public float Opacity
        {
            get => _opacity;
            set
            {
                _opacity = Mathf.Clamp01(value);
                ApplyOpacity();
                bool visible = _opacity > 0.01f;
                if (_root != null && _root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
            }
        }

        private void ApplyOpacity()
        {
            // Vertex colour carries the opacity — every colour that goes into the mesh is
            // multiplied by it in AddQuad and Text — so chrome and glyphs fade together with one
            // material and one pass. Set Opacity before Begin; a surface lays out every frame.
            if (_chromeRenderer != null) _chromeRenderer.enabled = _opacity > 0.01f;
            _text.Visible = _opacity > 0.01f;
        }

        /// <summary>True when the font atlas repacked and the layout must be rebuilt this frame.</summary>
        public bool NeedsRebuild => _text.NeedsRebuild;

        /// <summary>
        /// Align with the viewer's screen and report the distance for the next layout.
        ///
        /// Aligned to the camera's plane rather than aimed at its position: aiming keystones badly
        /// away from the centre of the view — the status card sits about thirty degrees off-axis
        /// and, pointed at the eye, projected to a visible trapezium.
        /// </summary>
        public float FaceViewer(Transform camera)
        {
            if (camera == null || _root == null) return _distance;
            _root.rotation = camera.rotation;
            return Vector3.Distance(_root.position, camera.position);
        }

        public string Describe() =>
            $"{Size.x:F3}x{Size.y:F3} m at {(_root != null ? _root.position.ToString() : "?")} " +
            $"opacity={_opacity:F2} rects={_hitRects.Count} glyphs={_text.Glyphs} draws=2";

        public void Destroy()
        {
            _text?.Destroy();
            if (!Application.isPlaying) return;
            if (_material != null) Object.Destroy(_material);
            if (_chrome != null) Object.Destroy(_chrome);
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        // ---- mesh building ---------------------------------------------------

        private void AddQuad(float cx, float cy, float width, float height, Color rawColour, SurfaceStyle style)
        {
            Color colour = Fade(rawColour);
            Rect uv = style == SurfaceStyle.Panel ? CosmoTheme.AtlasPanel : CosmoTheme.AtlasFill;

            float x0 = cx - 0.5f * width, x1 = cx + 0.5f * width;
            float y0 = cy - 0.5f * height, y1 = cy + 0.5f * height;

            int v = _vertices.Count;
            _vertices.Add(new Vector3(x0, y0, 0f));
            _vertices.Add(new Vector3(x0, y1, 0f));
            _vertices.Add(new Vector3(x1, y1, 0f));
            _vertices.Add(new Vector3(x1, y0, 0f));

            _uvs.Add(new Vector2(uv.xMin, uv.yMin));
            _uvs.Add(new Vector2(uv.xMin, uv.yMax));
            _uvs.Add(new Vector2(uv.xMax, uv.yMax));
            _uvs.Add(new Vector2(uv.xMax, uv.yMin));

            _colours.Add(colour);
            _colours.Add(colour);
            _colours.Add(colour);
            _colours.Add(colour);

            _indices.Add(v); _indices.Add(v + 1); _indices.Add(v + 2);
            _indices.Add(v); _indices.Add(v + 2); _indices.Add(v + 3);
        }

        private void PatchBacking(float width, float height)
        {
            if (_vertices.Count < 8) return;
            SetQuad(0, width, height);
            float inset = 2f * Dmm(CosmoTheme.StrokeWidth);
            SetQuad(4, Mathf.Max(0f, width - inset), Mathf.Max(0f, height - inset));
        }

        private void SetQuad(int first, float width, float height)
        {
            _vertices[first] = new Vector3(-0.5f * width, -0.5f * height, 0f);
            _vertices[first + 1] = new Vector3(-0.5f * width, 0.5f * height, 0f);
            _vertices[first + 2] = new Vector3(0.5f * width, 0.5f * height, 0f);
            _vertices[first + 3] = new Vector3(0.5f * width, -0.5f * height, 0f);
        }
    }
}
