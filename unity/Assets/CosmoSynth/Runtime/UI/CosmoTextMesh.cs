using System.Collections.Generic;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Many strings, one mesh, one draw call.
    ///
    /// Legacy <c>TextMesh</c> is one <c>MeshRenderer</c> each, and dynamic batching is off (§8.1),
    /// so a multi-row panel plus two dozen world labels would be fifty to eighty draw calls — a
    /// third of the whole 150-draw frame budget on text. Sharing a material does not merge draws;
    /// the SRP Batcher removes state-change cost, not draws.
    ///
    /// So the glyphs are laid out here, against the same dynamic-font atlas
    /// <see cref="CosmoText"/> already measures with, into a single <c>Mesh</c> with per-vertex
    /// colour. §7.3 allows the whole interface six draws; this is one of them, whatever it holds.
    ///
    /// The dynamic font repacks its atlas whenever an unseen glyph is requested, and a mesh built
    /// against the previous packing renders the right glyphs from the wrong places — text turns to
    /// confetti. Hence <see cref="NeedsRebuild"/> and the subscription.
    /// </summary>
    public sealed class CosmoTextMesh
    {
        private readonly Transform _root;
        private readonly MeshFilter _filter;
        private readonly MeshRenderer _renderer;
        private readonly Mesh _mesh;
        private readonly Material _material;

        private readonly List<Vector3> _vertices = new List<Vector3>(1024);
        private readonly List<Vector2> _uvs = new List<Vector2>(1024);
        private readonly List<Color> _colours = new List<Color>(1024);
        private readonly List<int> _indices = new List<int>(1536);

        private bool _atlasChanged;

        /// <summary>True when the font atlas repacked since the last build, so this must be rebuilt.</summary>
        public bool NeedsRebuild => _atlasChanged;

        /// <summary>Glyph quads in the current mesh. The play-mode report prints it.</summary>
        public int Glyphs { get; private set; }

        public Transform Root => _root;

        public CosmoTextMesh(Transform parent, string name)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            _root = host.transform;

            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();

            _filter = host.AddComponent<MeshFilter>();
            _filter.sharedMesh = _mesh;

            _renderer = host.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            _material = CosmoShaders.Material(CosmoShaders.Text);
            RepointAtlas();
            _renderer.sharedMaterial = _material;

            Font.textureRebuilt += OnFontTextureRebuilt;
        }

        private void OnFontTextureRebuilt(Font font)
        {
            if (font != CosmoText.Font) return;
            _atlasChanged = true;
            RepointAtlas();
        }

        private void RepointAtlas()
        {
            Font font = CosmoText.Font;
            if (font != null && font.material != null)
                _material.SetTexture("_MainTex", font.material.mainTexture);
        }

        public bool Visible
        {
            get => _renderer != null && _renderer.enabled;
            set { if (_renderer != null) _renderer.enabled = value; }
        }

        /// <summary>Start a new frame's worth of text.</summary>
        public void Begin()
        {
            _vertices.Clear();
            _uvs.Clear();
            _colours.Clear();
            _indices.Clear();
            Glyphs = 0;
        }

        /// <summary>
        /// Lay one line of text out with its left edge at <paramref name="x"/> and the <b>top</b>
        /// of its line box at <paramref name="y"/>, in the mesh's local metres.
        /// </summary>
        public void AddLine(string text, float x, float y, float characterSize, Color colour) =>
            AddLine(text, x, y, 0f, characterSize, colour);

        /// <summary>
        /// The same, at a depth.
        ///
        /// World labels need it: they share one mesh parented to the camera, so each label's
        /// forward distance is its own local z and the glyphs stay depth-sorted against the bodies
        /// they name.
        /// </summary>
        public void AddLine(string text, float x, float y, float z, float characterSize, Color colour)
        {
            if (string.IsNullOrEmpty(text)) return;

            Font font = CosmoText.Font;
            if (font == null) return;

            font.RequestCharactersInTexture(text, CosmoText.FontSize, FontStyle.Normal);

            // Unity's own factor from font pixels to world units, and the scale from the atlas
            // size the metrics are quoted at to the size we asked for.
            float scale = characterSize * 0.1f * (CosmoText.FontSize / (float)Mathf.Max(1, font.fontSize));
            float baseline = y - font.ascent * scale;
            float pen = x;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n') continue;

                if (!font.GetCharacterInfo(c, out CharacterInfo info, CosmoText.FontSize, FontStyle.Normal))
                {
                    // A glyph the font does not have still occupies space. Half an em, as
                    // CosmoText.MeasureWidth does, so measurement and drawing agree.
                    pen += CosmoText.FontSize * 0.5f * characterSize * 0.1f;
                    continue;
                }

                if (c != ' ')
                {
                    float x0 = pen + info.minX * characterSize * 0.1f;
                    float x1 = pen + info.maxX * characterSize * 0.1f;
                    float y0 = baseline + info.minY * characterSize * 0.1f;
                    float y1 = baseline + info.maxY * characterSize * 0.1f;

                    int v = _vertices.Count;
                    _vertices.Add(new Vector3(x0, y0, z));
                    _vertices.Add(new Vector3(x0, y1, z));
                    _vertices.Add(new Vector3(x1, y1, z));
                    _vertices.Add(new Vector3(x1, y0, z));

                    _uvs.Add(info.uvBottomLeft);
                    _uvs.Add(info.uvTopLeft);
                    _uvs.Add(info.uvTopRight);
                    _uvs.Add(info.uvBottomRight);

                    _colours.Add(colour);
                    _colours.Add(colour);
                    _colours.Add(colour);
                    _colours.Add(colour);

                    _indices.Add(v); _indices.Add(v + 1); _indices.Add(v + 2);
                    _indices.Add(v); _indices.Add(v + 2); _indices.Add(v + 3);
                    Glyphs++;
                }

                pen += info.advance * characterSize * 0.1f;
            }
        }

        /// <summary>Lay text out centred horizontally on <paramref name="cx"/>.</summary>
        public void AddCentred(string text, float cx, float y, float characterSize, Color colour) =>
            AddLine(text, cx - 0.5f * CosmoText.MeasureWidth(text, characterSize), y, characterSize, colour);

        /// <summary>Upload what was added. Cheap when nothing changed is not free: call once a frame.</summary>
        public void End()
        {
            _mesh.Clear();
            if (_vertices.Count == 0)
            {
                _atlasChanged = false;
                return;
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colours);
            _mesh.SetTriangles(_indices, 0, calculateBounds: true);
            _atlasChanged = false;
        }

        public void Destroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            if (!Application.isPlaying) return;
            if (_material != null) Object.Destroy(_material);
            if (_mesh != null) Object.Destroy(_mesh);
            if (_root != null) Object.Destroy(_root.gameObject);
        }
    }
}
