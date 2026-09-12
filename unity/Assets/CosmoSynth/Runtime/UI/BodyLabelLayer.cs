using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Names the bodies on screen.
    ///
    /// The app had no labels at all. A captured frame of the whole Solar System shows nine
    /// featureless discs and a dozen ellipses, and there is no way to tell which is Mars — the
    /// data is all there, in a catalogue of 211 bodies with measured radii and provenance for
    /// every field, and none of it is legible in the one view that shows the most of it. A
    /// planetarium without labels is a screensaver.
    ///
    /// Two things make labels work rather than make the view worse:
    ///
    /// <b>They are ranked, not exhaustive.</b> 211 names would cover the sky. Bodies compete on
    /// apparent size and the largest handful win, so what gets named is what the viewer can
    /// actually see, and the set changes as they move rather than being a fixed list.
    ///
    /// <b>They are decluttered in screen space.</b> Two labels overlapping is worse than one label
    /// missing — overlapping text is unreadable and, worse, ambiguous about which body either
    /// belongs to. A greedy pass drops any label whose box collides with one already placed. This
    /// is the same rule cartographers use, for the same reason.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("CosmoSynth/Body Labels")]
    public sealed class BodyLabelLayer : MonoBehaviour
    {
        [Tooltip("Most labels drawn at once. Beyond this the view is a word cloud.")]
        [SerializeField] private int _maximumLabels = 14;

        [Tooltip("Apparent diameter, in pixels, at which an unremarkable body earns a name.")]
        [SerializeField] private float _minimumPixels = 3f;

        private UniverseSimulation _simulation;
        private Camera _camera;

        private readonly List<TextMesh> _pool = new List<TextMesh>();
        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly List<Rect> _placed = new List<Rect>();

        private struct Candidate
        {
            public CelestialBodyView View;
            public double Pixels;
            public Vector3 Viewport;
        }

        private void LateUpdate()
        {
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();
            if (_camera == null) _camera = Camera.main;
            if (_simulation?.Catalog == null || _camera == null)
            {
                HideFrom(0);
                return;
            }

            Gather();
            Place();
        }

        /// <summary>
        /// Collect what is on screen and worth naming, most significant first.
        ///
        /// Significance is not apparent size alone, which was the first thing tried and which
        /// labels nothing at all in the view that needs labels most. With the whole Solar System
        /// on a table every planet is under a tenth of a pixel across — the report says "bodies at
        /// 1 px or larger: 0 of 211" — and each is drawn at the minimum-visible floor instead. A
        /// size threshold therefore rejects the entire Solar System and names only whatever the
        /// user last selected.
        ///
        /// So a planet, dwarf planet or nearby star is named because of what it is, and anything
        /// else has to earn it by being big on screen. That is what a star chart does: it labels
        /// the objects that have names, not the objects that happen to be bright.
        /// </summary>
        private void Gather()
        {
            _candidates.Clear();

            foreach (CelestialBodyView view in _simulation.Views.Values)
            {
                if (view == null) continue;

                // Never name something that is not there.
                //
                // A label is the strongest claim this app makes about a position — it is a name
                // written in space with a name's worth of authority — and it was being written
                // over spacecraft the catalogue explicitly does not know the position of. Cassini
                // is labelled above Saturn today because nothing here consulted the window that
                // CelestialBodyView already refuses to draw it outside of.
                if (!view.IsKnown) continue;

                // The focused body is always a candidate however small it is drawn: it is the one
                // the user asked about, and losing its name at the moment it becomes distant is
                // exactly when the name is most wanted.
                bool focused = _simulation.FocusBody == view;
                double score = Significance(view, focused);
                if (score <= 0.0) continue;

                Vector3 viewport = _camera.WorldToViewportPoint(view.transform.position);
                if (viewport.z <= 0f) continue;                      // behind the camera
                if (viewport.x < -0.05f || viewport.x > 1.05f) continue;
                if (viewport.y < -0.05f || viewport.y > 1.05f) continue;

                _candidates.Add(new Candidate { View = view, Pixels = score, Viewport = viewport });
            }

            _candidates.Sort((a, b) => b.Pixels.CompareTo(a.Pixels));
        }

        /// <summary>
        /// How much this body deserves a name, or zero for "not at all".
        ///
        /// The floor per body type is what keeps the ranking meaningful when everything on screen
        /// is drawn at the same minimum size: without it a table-top Solar System is a dozen
        /// bodies all scoring zero, and which of them gets named is down to dictionary order.
        /// Apparent size still wins wherever it is a real number, so a planet filling the view
        /// outranks the star it orbits.
        /// </summary>
        private double Significance(CelestialBodyView view, bool focused)
        {
            if (focused) return double.MaxValue;

            double pixels = view.ApparentDiameterPixels;

            if (NamedByType(view))
            {
                return view.Body.Type switch
                {
                    BodyType.Star => System.Math.Max(pixels, 3.0),
                    BodyType.Planet => System.Math.Max(pixels, 2.0),

                    // A spacecraft ranks just under a planet and above the rest. It is drawn
                    // thousands of times life size and is still a few pixels, so apparent size
                    // says nothing useful about whether it is worth naming — and once it drops
                    // below the size at which its mesh is legible, CelestialBodyView replaces
                    // that mesh with a marker dot. A dot with no name is an unexplained speck.
                    // The name is what makes the degradation a representation instead of a
                    // glitch, so it has to survive exactly the threshold the mesh does not.
                    BodyType.Spacecraft => System.Math.Max(pixels, 1.5),

                    _ => System.Math.Max(pixels, 1.0)
                };
            }

            return pixels >= _minimumPixels ? pixels : 0.0;
        }

        /// <summary>
        /// Whether this body is named for what it is rather than for how large it looks.
        ///
        /// A body pulled onto the sky sphere is one of several hundred catalogue stars light years
        /// away, whose distance is not even being drawn truthfully. Naming those by type would
        /// bury the system the user is standing in under the rest of the galaxy.
        /// </summary>
        private static bool NamedByType(CelestialBodyView view) =>
            !view.DrawnAsPoint &&
            (view.Body.Type == BodyType.Star ||
             view.Body.Type == BodyType.Planet ||
             view.Body.Type == BodyType.DwarfPlanet ||
             view.Body.Type == BodyType.Spacecraft);

        private void Place()
        {
            _placed.Clear();
            int used = 0;

            for (int i = 0; i < _candidates.Count && used < _maximumLabels; i++)
            {
                Candidate candidate = _candidates[i];
                TextMesh label = Label(used);

                string name = candidate.View.Body.Name ?? candidate.View.Body.Id;

                // Type size is angular, so a label beside a planet across the room and one beside
                // a hologram on the table subtend the same angle and read the same.
                float distance = Vector3.Distance(_camera.transform.position,
                    candidate.View.transform.position);
                label.characterSize = CosmoText.CharacterSizeFor(
                    CosmoTheme.Metres(CosmoTheme.TypeCaption, distance));

                // Sit the label clear of the body's drawn disc, up and to the right — one
                // consistent direction, so which name belongs to which dot is never in question.
                //
                // Only for something that is actually on screen: IsGeometryDrawn (via
                // IsPointable) is re-read here rather than trusted from when the candidate was
                // chosen, because the two are different frames and a body can stop being drawn
                // between them.
                if (!candidate.View.IsPointable) continue;
                float clearance = candidate.View.RoomRadius +
                                  CosmoTheme.Metres(CosmoTheme.SpaceRow, distance);
                Vector3 worldOffset = (_camera.transform.up + _camera.transform.right).normalized * clearance;
                Vector3 worldPosition = candidate.View.transform.position + worldOffset;

                if (!TryReserve(name, label.characterSize, worldPosition, distance)) continue;

                // Parented to this layer, which sits under the simulation transform — so a label
                // moves with the universe when the anchor is placed in the room, exactly as the
                // body it names does. Writing world position here would leave every label behind
                // the moment the user grabbed the scene.
                label.transform.position = worldPosition;
                label.transform.rotation = Quaternion.LookRotation(
                    worldPosition - _camera.transform.position, _camera.transform.up);

                bool focused = _simulation.FocusBody == candidate.View;
                Color colour = focused ? CosmoTheme.Accent : CosmoTheme.TextSecondary;

                // Fade only the labels that earned their place by being large, and only near the
                // threshold — otherwise a body drifting across the cut-off flickers its name on
                // and off. A body named for what it is does not fade, because it does not pop.
                float alpha = 1f;
                if (!focused && !NamedByType(candidate.View))
                {
                    alpha = Mathf.Lerp(0.35f, 1f,
                        Mathf.InverseLerp(_minimumPixels, _minimumPixels * 3f, (float)candidate.Pixels));
                }
                label.color = CosmoTheme.WithAlpha(colour, alpha);

                label.text = name;
                label.gameObject.SetActive(true);
                used++;
            }

            HideFrom(used);
        }

        /// <summary>
        /// Claim screen space for a label, or refuse if something already has it.
        /// </summary>
        private bool TryReserve(string name, float characterSize, Vector3 worldPosition, float distance)
        {
            // Convert the label's world extent to a viewport-space box. Measuring the text and
            // projecting it is what makes the test size-aware: a long name needs more room than a
            // short one, and a nearby label needs far more than a distant one.
            Vector2 extent = CosmoText.Measure(name, characterSize);
            float worldPerPixel = ViewportInfo.WorldPerPixel(_camera, distance);
            if (worldPerPixel <= 0f) return false;

            int verticalPixels = ViewportInfo.VerticalPixels(_camera);
            Vector3 viewport = _camera.WorldToViewportPoint(worldPosition);

            float halfWidth = extent.x / worldPerPixel / verticalPixels * 0.5f;
            float halfHeight = extent.y / worldPerPixel / verticalPixels * 0.5f;

            // Anchored upper-left, so the box extends right and down from the anchor point.
            var box = new Rect(viewport.x, viewport.y - halfHeight * 2f, halfWidth * 2f, halfHeight * 2f);

            for (int i = 0; i < _placed.Count; i++)
            {
                if (_placed[i].Overlaps(box)) return false;
            }

            _placed.Add(box);
            return true;
        }

        private TextMesh Label(int index)
        {
            while (_pool.Count <= index)
            {
                TextMesh created = CosmoText.Create(transform, $"Label {_pool.Count}", 0.01f,
                    TextAnchor.UpperLeft, CosmoTheme.TextSecondary);
                created.gameObject.SetActive(false);
                _pool.Add(created);
            }
            return _pool[index];
        }

        private void HideFrom(int index)
        {
            for (int i = index; i < _pool.Count; i++)
            {
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
            }
        }
    }
}
