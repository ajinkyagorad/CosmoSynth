using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// A world-space panel that sizes itself to what it contains.
    ///
    /// The three panels this replaces each declared a width in metres and a character size
    /// independently, and in all three cases the text did not fit — by 3.5x on the wrist, by 4x
    /// beside a selected body. A container whose size is asserted rather than derived will always
    /// drift from its contents the first time the contents change.
    ///
    /// So: nothing here takes a size. A caller supplies text and a viewing distance; the panel
    /// picks type sizes from the angular scale in <see cref="CosmoTheme"/>, wraps the body to a
    /// sensible measure, asks <see cref="CosmoText"/> how big the result is, and lays the backing
    /// out around it. It cannot overflow, because the backing is computed from the text.
    ///
    /// Structure is a title, a rule, and a body. The rule is not decoration: with a single flat
    /// block of text — which is what all three panels were — there is no hierarchy at all, and a
    /// reader has to parse the whole panel to find the one line they wanted.
    /// </summary>
    public sealed class CosmoPanel
    {
        private readonly Transform _root;
        private readonly Transform _backing;
        private readonly Transform _rule;
        private readonly Transform _highlight;
        private readonly TextMesh _title;
        private readonly TextMesh _body;

        private readonly Material _backingMaterial;
        private readonly Material _ruleMaterial;
        private readonly Material _highlightMaterial;

        private float _distance = 1f;
        private float _opacity = 1f;
        private Vector2 _size;

        /// <summary>Maximum line length, in dmm. Roughly 40 characters — the measure typography
        /// has settled on for readability, and about as much as fits on a wrist.</summary>
        private const float ContentWidthDmm = 520f;

        /// <summary>The panel's transform. Position and orient this; leave the children alone.</summary>
        public Transform Root => _root;

        /// <summary>Panel extent in world units. Valid after <see cref="SetContent"/>.</summary>
        public Vector2 Size => _size;

        public CosmoPanel(Transform parent, string name)
        {
            var rootObject = new GameObject(name);
            rootObject.transform.SetParent(parent, false);
            _root = rootObject.transform;

            _backingMaterial = CosmoTheme.PanelMaterial();
            _backing = CreateQuad(_root, "Backing", _backingMaterial);
            _backing.localPosition = new Vector3(0f, 0f, 0.002f);

            _highlightMaterial = CosmoTheme.RowMaterial();
            _highlight = CreateQuad(_root, "Row Highlight", _highlightMaterial);
            _highlight.gameObject.SetActive(false);

            _ruleMaterial = CosmoTheme.RowMaterial();
            _ruleMaterial.color = CosmoTheme.Accent;
            _rule = CreateQuad(_root, "Rule", _ruleMaterial);

            _title = CosmoText.Create(_root, "Title", 0.01f, TextAnchor.UpperLeft, CosmoTheme.TextPrimary);
            _body = CosmoText.Create(_root, "Body", 0.01f, TextAnchor.UpperLeft, CosmoTheme.TextSecondary);
        }

        private static Transform CreateQuad(Transform parent, string name, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return quad.transform;
        }

        /// <summary>
        /// Fill the panel and lay it out for a viewing distance.
        ///
        /// The distance is what makes the type angular. The same call at 0.35 m and at 3 m
        /// produces a panel that subtends the same angle and reads identically, which is the only
        /// way one component can serve both a wrist and a planet.
        /// </summary>
        /// <param name="bodyTypeDmm">Angular size of the body text. A reference card that is read
        /// once can use a smaller size than a panel of values that is read repeatedly.</param>
        /// <param name="contentWidthDmm">Widest line, angularly. The default is a comfortable
        /// measure; surfaces with somewhere specific to fit — beside a globe that already fills
        /// most of the view — need to say so, or they are laid out correctly and then placed
        /// off-screen.</param>
        public void SetContent(string title, string body, float viewingDistanceMetres,
            float bodyTypeDmm = CosmoTheme.TypeBody, float contentWidthDmm = ContentWidthDmm)
        {
            _distance = Mathf.Clamp(viewingDistanceMetres, 0.15f, 40f);

            float padding = CosmoTheme.Metres(CosmoTheme.SpacePanelPadding, _distance);
            float contentWidth = CosmoTheme.Metres(contentWidthDmm, _distance);
            float gap = CosmoTheme.Metres(CosmoTheme.SpaceRow, _distance);

            float titleSize = CosmoText.CharacterSizeFor(CosmoTheme.Metres(CosmoTheme.TypeTitle, _distance));
            float bodySize = CosmoText.CharacterSizeFor(CosmoTheme.Metres(bodyTypeDmm, _distance));

            _title.characterSize = titleSize;
            _body.characterSize = bodySize;

            bool hasTitle = !string.IsNullOrEmpty(title);
            string titleText = hasTitle ? CosmoText.Truncate(title, titleSize, contentWidth) : string.Empty;
            string bodyText = CosmoText.Wrap(body ?? string.Empty, bodySize, contentWidth);

            _title.text = titleText;
            _body.text = bodyText;

            Vector2 titleExtent = hasTitle ? CosmoText.Measure(titleText, titleSize) : Vector2.zero;
            Vector2 bodyExtent = CosmoText.Measure(bodyText, bodySize);

            // The panel is as wide as its widest line and as tall as its stack — never the
            // other way round.
            float innerWidth = Mathf.Max(titleExtent.x, bodyExtent.x);
            float ruleHeight = hasTitle ? CosmoTheme.Metres(CosmoTheme.StrokeWidth, _distance) : 0f;
            float innerHeight = titleExtent.y + (hasTitle ? gap + ruleHeight + gap : 0f) + bodyExtent.y;

            _size = new Vector2(innerWidth + padding * 2f, innerHeight + padding * 2f);
            _backing.localScale = new Vector3(_size.x, _size.y, 1f);

            // Content is laid out downward from the top-left of the inner box.
            float left = -_size.x * 0.5f + padding;
            float top = _size.y * 0.5f - padding;

            _title.gameObject.SetActive(hasTitle);
            _rule.gameObject.SetActive(hasTitle);

            if (hasTitle)
            {
                _title.transform.localPosition = new Vector3(left, top, 0f);

                float ruleY = top - titleExtent.y - gap - ruleHeight * 0.5f;
                _rule.localPosition = new Vector3(0f, ruleY, 0.001f);
                _rule.localScale = new Vector3(_size.x - padding * 2f, ruleHeight, 1f);

                top = ruleY - ruleHeight * 0.5f - gap;
            }

            _body.transform.localPosition = new Vector3(left, top, 0f);
            _bodyTop = top;
            _bodyLeft = left;

            ApplyOpacity();
        }

        private float _bodyTop;
        private float _bodyLeft;

        /// <summary>
        /// Put the selection bar behind one line of the body, or hide it with a negative index.
        ///
        /// A filled bar rather than the "&gt;" character the lists used to mark selection with.
        /// A leading glyph is invisible at a glance — the eye has to read the column to find it —
        /// where a bar is the thing you see first, which is what a selection indicator is for.
        /// </summary>
        public void SetSelectedLine(int lineIndex)
        {
            if (lineIndex < 0)
            {
                _highlight.gameObject.SetActive(false);
                return;
            }

            float lineHeight = CosmoText.LineHeight(_body.characterSize);
            float padding = CosmoTheme.Metres(CosmoTheme.SpacePanelPadding, _distance);

            _highlight.gameObject.SetActive(true);
            _highlight.localScale = new Vector3(_size.x - padding, lineHeight, 1f);
            _highlight.localPosition = new Vector3(
                0f, _bodyTop - (lineIndex + 0.5f) * lineHeight, 0.0015f);
        }

        /// <summary>Colour of the title, used to carry a body's weakest provenance claim.</summary>
        public void SetTitleColour(Color colour)
        {
            _titleColour = colour;
            ApplyOpacity();
        }

        private Color _titleColour = CosmoTheme.TextPrimary;

        /// <summary>Mark the panel as holding input, by lighting its edge.</summary>
        public void SetActive(bool active)
        {
            _accent = active ? CosmoTheme.StrokeActive : CosmoTheme.Neutral;
            ApplyOpacity();
        }

        private Color _accent = CosmoTheme.Neutral;

        /// <summary>
        /// Fade the whole panel together.
        ///
        /// The wrist dashboard used to fade only its text: the dark backing appeared instantly at
        /// full opacity and the contents drifted in afterwards, which reads as the panel being
        /// broken rather than as it arriving. One opacity, applied to everything.
        /// </summary>
        public float Opacity
        {
            get => _opacity;
            set
            {
                _opacity = Mathf.Clamp01(value);
                ApplyOpacity();
                bool visible = _opacity > 0.01f;
                if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
            }
        }

        private void ApplyOpacity()
        {
            _backingMaterial.color = CosmoTheme.WithAlpha(_accent, _opacity);
            _ruleMaterial.color = CosmoTheme.WithAlpha(CosmoTheme.Accent, _opacity * 0.9f);
            _highlightMaterial.color = CosmoTheme.WithAlpha(CosmoTheme.SurfaceRaised, _opacity * 0.95f);

            _title.color = CosmoTheme.WithAlpha(_titleColour, _opacity);
            _body.color = CosmoTheme.WithAlpha(CosmoTheme.TextSecondary, _opacity);
        }

        /// <summary>
        /// Align the panel with the viewer's screen, and report the distance for layout.
        ///
        /// Aligned to the camera's plane rather than aimed at its position. Aiming works for a
        /// panel near the middle of the view and keystones badly away from it: the status card
        /// sits about thirty degrees off-axis and, pointed at the eye, projected to a visible
        /// trapezium with one edge a third longer than the other. Matching the camera's rotation
        /// keeps every panel a rectangle wherever it is placed, which is why every flat interface
        /// in the world is drawn that way.
        /// </summary>
        public float FaceViewer(Transform camera)
        {
            _root.rotation = camera.rotation;
            return Vector3.Distance(_root.position, camera.position);
        }

        /// <summary>Diagnostics: where this panel is, how big, and whether it is showing.</summary>
        public string Describe() =>
            $"{_size.x:F3}x{_size.y:F3} m at {_root.position} opacity={_opacity:F2} " +
            $"lineHeight={CosmoText.LineHeight(_body.characterSize) * 1000f:F1} mm";

        public void Destroy()
        {
            if (!Application.isPlaying) return;
            if (_backingMaterial != null) Object.Destroy(_backingMaterial);
            if (_ruleMaterial != null) Object.Destroy(_ruleMaterial);
            if (_highlightMaterial != null) Object.Destroy(_highlightMaterial);
        }
    }
}
