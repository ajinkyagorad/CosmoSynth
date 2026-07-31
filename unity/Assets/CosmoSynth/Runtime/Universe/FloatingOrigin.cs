using System;
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Implemented by anything that needs to know when the world has been shifted underneath it.
    /// Trails, line renderers and particle systems hold positions in world space that the
    /// origin shift does not move for them.
    /// </summary>
    public interface IFloatingOriginListener
    {
        void OnOriginShifted(Vector3 shift);
    }

    /// <summary>
    /// Keeps the camera near Unity's world origin by moving the universe instead of the viewer.
    ///
    /// A 32-bit float carries about 7 significant decimal digits. At 10 km from the origin the
    /// gap between representable positions is already around a millimetre, which is enough to
    /// make a held hologram shimmer; at 1000 km it is 6 cm and geometry visibly swims. Since
    /// the simulation spans billions of kilometres, the only workable answer is to never let
    /// the rendered scene get far from zero: absolute positions live in
    /// <see cref="Double3"/> kilometres, and this component periodically rebases the mapping
    /// between those and Unity's float transforms.
    ///
    /// The shift is applied to root-level transforms only — children move with their parents —
    /// and the rebase threshold is chosen so it happens well before precision becomes visible.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("CosmoSynth/Floating Origin")]
    public sealed class FloatingOrigin : MonoBehaviour
    {
        [Header("Rebasing")]
        [Tooltip("Distance from the Unity origin, in render units, that triggers a rebase.")]
        [SerializeField] private float _rebaseThreshold = 4000f;

        [Tooltip("The transform kept near the origin. Usually the camera rig.")]
        [SerializeField] private Transform _focus;

        [Header("Diagnostics")]
        [SerializeField] private int _rebaseCount;

        private readonly List<IFloatingOriginListener> _listeners = new List<IFloatingOriginListener>();

        /// <summary>The absolute position, in km, that currently maps to Unity's origin.</summary>
        public Double3 OriginKm { get; private set; } = Double3.Zero;

        /// <summary>Render units emitted per kilometre. Set by the active scale band.</summary>
        public double UnitsPerKm { get; private set; } = 1.0;

        /// <summary>Number of rebases so far this session. Useful for spotting thrashing.</summary>
        public int RebaseCount => _rebaseCount;

        /// <summary>Raised after the world has been shifted, with the applied offset.</summary>
        public event Action<Vector3> OriginShifted;

        public Transform Focus
        {
            get => _focus;
            set => _focus = value;
        }

        /// <summary>The current absolute-to-render mapping.</summary>
        public ScaleSpace.RenderTransform Transform => new ScaleSpace.RenderTransform(OriginKm, UnitsPerKm);

        public void Register(IFloatingOriginListener listener)
        {
            if (listener != null && !_listeners.Contains(listener)) _listeners.Add(listener);
        }

        public void Unregister(IFloatingOriginListener listener) => _listeners.Remove(listener);

        /// <summary>Convert an absolute position to a Unity world position.</summary>
        public Vector3 ToRender(Double3 absoluteKm)
        {
            Double3 local = (absoluteKm - OriginKm) * UnitsPerKm;
            return new Vector3((float)local.x, (float)local.y, (float)local.z);
        }

        /// <summary>Convert a Unity world position back to an absolute position.</summary>
        public Double3 ToAbsolute(Vector3 renderPosition) =>
            new Double3(renderPosition.x, renderPosition.y, renderPosition.z) / UnitsPerKm + OriginKm;

        /// <summary>Scale a length from km into render units, with no translation.</summary>
        public float ScaleLength(double km) => (float)(km * UnitsPerKm);

        /// <summary>
        /// Change the render scale. Everything is re-derived from absolute coordinates on the
        /// next frame, so this is safe to call at any time — it is how the scale bands and the
        /// tabletop/true-scale toggle work.
        /// </summary>
        public void SetUnitsPerKm(double unitsPerKm)
        {
            if (unitsPerKm <= 0.0 || double.IsNaN(unitsPerKm))
            {
                Debug.LogError($"[CosmoSynth] Ignoring invalid render scale {unitsPerKm}.");
                return;
            }
            UnitsPerKm = unitsPerKm;
        }

        /// <summary>Move the absolute origin, without touching any transforms.</summary>
        public void SetOrigin(Double3 absoluteKm) => OriginKm = absoluteKm;

        private void LateUpdate()
        {
            if (_focus == null) return;

            Vector3 focusPosition = _focus.position;
            if (focusPosition.sqrMagnitude < _rebaseThreshold * _rebaseThreshold) return;

            Rebase(focusPosition);
        }

        /// <summary>
        /// Shift the whole scene so the focus sits back at the origin.
        ///
        /// Only root transforms are moved: a child's world position follows its parent, so
        /// shifting both would move it twice.
        /// </summary>
        public void Rebase(Vector3 focusPosition)
        {
            Vector3 shift = -focusPosition;

            // Fold the shift into the absolute origin so the mapping stays consistent.
            OriginKm += new Double3(focusPosition.x, focusPosition.y, focusPosition.z) / UnitsPerKm;

            var scene = gameObject.scene;
            var roots = new List<GameObject>(scene.rootCount);
            scene.GetRootGameObjects(roots);

            foreach (GameObject root in roots)
            {
                // Skip this object itself if it has no spatial meaning, but do not special-case
                // anything else: an object that should not move belongs on a different scene.
                root.transform.position += shift;
            }

            _rebaseCount++;

            // Anything holding baked world-space positions has to be told: line renderers in
            // world space, trail renderers and particle systems in world simulation space all
            // keep their own vertex data that the transform shift does not reach.
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                var listener = _listeners[i];
                if (listener == null)
                {
                    _listeners.RemoveAt(i);
                    continue;
                }
                listener.OnOriginShifted(shift);
            }

            OriginShifted?.Invoke(shift);
        }

        private void OnValidate()
        {
            if (_rebaseThreshold < 100f) _rebaseThreshold = 100f;
        }
    }
}
