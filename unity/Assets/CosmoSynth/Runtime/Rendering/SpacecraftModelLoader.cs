using System;
using System.Collections;
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Streams the shipped spacecraft meshes out of StreamingAssets and hands them to their
    /// views, one at a time.
    ///
    /// It mirrors <see cref="PlanetTextureBaker"/> deliberately: a body appears immediately as
    /// the default sphere and is upgraded when its geometry arrives, rather than the scene
    /// waiting on two megabytes of mesh before anything is visible. On Android the files live
    /// compressed inside the APK and can only be reached through UnityWebRequest, so this has
    /// to be a coroutine whatever the desktop could get away with.
    ///
    /// Loading is ordered by apparent size, so whatever the user is looking at resolves first.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Spacecraft Model Loader")]
    public sealed class SpacecraftModelLoader : MonoBehaviour
    {
        [Tooltip("Meshes uploaded per frame. Each is a GPU upload on the main thread.")]
        [SerializeField] private int _loadsPerFrame = 1;

        private UniverseSimulation _simulation;

        /// <summary>Meshes by StreamingAssets path. Survives a catalogue rebuild deliberately.</summary>
        private readonly Dictionary<string, CsMesh.Model> _loaded =
            new Dictionary<string, CsMesh.Model>(StringComparer.Ordinal);

        /// <summary>Paths that could not be read, so a missing file is not retried every frame.</summary>
        private readonly HashSet<string> _failed = new HashSet<string>(StringComparer.Ordinal);

        private bool _running;

        /// <summary>Distinct meshes held in memory.</summary>
        public int Loaded => _loaded.Count;

        /// <summary>Models that were asked for and could not be read.</summary>
        public int Failed => _failed.Count;

        /// <summary>
        /// Resolved lazily rather than cached in Awake.
        ///
        /// This component is added by the bootstrap alongside the simulation, and the order in
        /// which sibling components receive Awake is not defined. Caching a lookup there is the
        /// exact shape of three bugs this project has already shipped: the reference comes back
        /// null once, is never retried, and the feature silently does nothing forever.
        /// </summary>
        private UniverseSimulation Simulation =>
            _simulation != null ? _simulation : (_simulation = GetComponent<UniverseSimulation>());

        private void Update()
        {
            if (_running || Simulation == null || Simulation.Catalog == null) return;

            CelestialBodyView next = SelectNext(out string path);
            if (next == null) return;

            // Already in memory: serve it straight away, with no read and no coroutine.
            //
            // This branch is not just an optimisation, it is what makes the loader survive a
            // catalogue merge. Merging the exoplanets calls SetCatalog again, which destroys
            // and recreates every view — so a spacecraft that had already been given its mesh
            // comes back without one. The old version tracked which *bodies* it had served and
            // skipped them forever, so any model applied before the merge was silently lost:
            // the loader reported 24 loaded while the ISS was still drawn as a grey sphere.
            // Driving the decision from what each view actually has, rather than from what this
            // component remembers doing, makes that self-healing.
            if (_loaded.TryGetValue(path, out CsMesh.Model cached))
            {
                next.ApplyModel(cached);
                return;
            }

            StartCoroutine(LoadRoutine(next, path));
        }

        /// <summary>
        /// The view still missing geometry that currently appears largest, so the queue serves
        /// what is being looked at rather than whatever the dictionary enumerated first.
        /// </summary>
        private CelestialBodyView SelectNext(out string path)
        {
            CelestialBodyView best = null;
            string bestPath = null;
            double bestPixels = double.NegativeInfinity;

            foreach (CelestialBodyView view in Simulation.Views.Values)
            {
                if (view == null || view.HasModel) continue;

                CelestialBody body = view.Body;
                if (body?.Model == null || !body.Model.Value.IsPresent) continue;

                string candidate = body.Model.Value.Path;
                if (_failed.Contains(candidate)) continue;

                if (view.ApparentDiameterPixels > bestPixels)
                {
                    bestPixels = view.ApparentDiameterPixels;
                    best = view;
                    bestPath = candidate;
                }
            }

            path = bestPath;
            return best;
        }

        private IEnumerator LoadRoutine(CelestialBodyView view, string path)
        {
            _running = true;

            string id = view.Body.Id;

            byte[] data = null;
            yield return Simulation.ReadStreamingAssetBytes(path, result => data = result);

            if (data == null)
            {
                Debug.LogWarning(
                    $"[CosmoSynth] no mesh for '{id}' at StreamingAssets/{path}. It will be drawn " +
                    "as a sphere. Build the models with 'python3 tools/cosmodata/build_spacecraft.py'.");
                _failed.Add(path);
                _running = false;
                yield break;
            }

            CsMesh.Model model = CsMesh.Parse(data, id);
            if (model == null)
            {
                _failed.Add(path);
                _running = false;
                yield break;
            }

            _loaded[path] = model;

            // The view may have been destroyed and rebuilt by a catalogue merge while this read
            // was in flight, in which case this instance is a leftover; the mesh is cached, and
            // the next Update applies it to whatever view exists now.
            if (view != null) view.ApplyModel(model);

            _running = false;
        }

        private void OnDestroy()
        {
            foreach (CsMesh.Model model in _loaded.Values)
            {
                if (model?.Mesh != null) Destroy(model.Mesh);
            }
            _loaded.Clear();
        }

        /// <summary>One line for the diagnostics readout.</summary>
        public string StatusLine()
        {
            int triangles = 0;
            foreach (CsMesh.Model model in _loaded.Values) triangles += model.TriangleCount;

            // Counting the views actually carrying geometry, not just the meshes in memory. The
            // two disagreeing is precisely the bug this component used to have.
            int applied = 0, wanted = 0;
            if (Simulation != null)
            {
                foreach (CelestialBodyView view in Simulation.Views.Values)
                {
                    if (view?.Body?.Model == null || !view.Body.Model.Value.IsPresent) continue;
                    wanted++;
                    if (view.HasModel) applied++;
                }
            }

            return $"models {applied}/{wanted} applied, {_loaded.Count} meshes cached " +
                   $"({triangles} triangles), {Failed} failed";
        }
    }
}
