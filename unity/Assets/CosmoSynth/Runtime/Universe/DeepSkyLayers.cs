using System.Collections;
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// STAND-IN (integrator, CONTRACT §4.3 <c>BuildLayers()</c> and §6.5 <c>DataLoader</c>) —
    /// creates the <c>deep-sky</c> layers, binds them to the simulation and the layer state, and
    /// delivers whatever data files are actually present.
    ///
    /// It exists because the bootstrap slot and the manifest-driven loader belong to the
    /// integrator and are not written yet, and a layer nobody creates is a layer nobody can test.
    /// At merge this whole file is replaced by one <c>AddLayer&lt;T&gt;()</c> line per layer in
    /// the bootstrap and by the real loader; the layers themselves need no change, because they
    /// only ever see <c>Bind</c> and <c>OnData</c>.
    ///
    /// <b>The absent case is the normal case.</b> Four data lanes are still running, so on this
    /// branch every file is missing. Every layer therefore reports "not in this build", draws
    /// nothing, and logs once — never per frame. Nothing here fabricates a placeholder catalogue
    /// to make the scene look populated; a fake star field would be indistinguishable from a
    /// working one in a screenshot, which is exactly how this project has been fooled before.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class DeepSkyLayers : MonoBehaviour
    {
        /// <summary>Where the shipped binaries live under StreamingAssets.</summary>
        public const string Root = "SA/";

        private readonly List<CosmoLayer> _layers = new List<CosmoLayer>();
        private readonly List<string> _loaded = new List<string>();
        private UniverseSimulation _simulation;

        /// <summary>The one layer state, until the panel owns it.</summary>
        public LayerState State { get; } = new LayerState();

        /// <summary>Kinds that were actually delivered, for <see cref="LayerContext"/>.</summary>
        public LoadedKinds Kinds { get; private set; } = LoadedKinds.Empty;

        public IReadOnlyList<CosmoLayer> Layers => _layers;

        private void Start()
        {
            _simulation = GetComponentInParent<UniverseSimulation>();
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();
            if (_simulation == null)
            {
                Debug.LogWarning("[CosmoSynth] DeepSkyLayers found no UniverseSimulation; " +
                                 "no deep-sky layers created.");
                return;
            }

            BuildLayers();
            StartCoroutine(LoadAll());
        }

        /// <summary>One line per layer, which is what the bootstrap slot will be.</summary>
        private void BuildLayers()
        {
            Add<StarFieldLayer>("Stars");
            Add<ConstellationLayer>("Constellations");
            Add<DustVolumeLayer>("Dust");
            Add<SpiralArmsLayer>("Spiral arms");
            Add<SunOrbitLayer>("Sun orbit");
            Add<MilkyWayDiscLayer>("Milky Way disc");
            Add<StructureLayer>("Structures");
            Add<CmbLayer>("CMB");
            Add<DistanceGridLayer>("Distance grids");

            // The five point sets differ by a colour rule and a unit, so they are five instances
            // of one class rather than five near-identical files.
            AddPointSet("cepheids", "cepheids", GalaxyModel.KmPerParsec, 1.0,
                new LayerBudget(2, 20_000, 0.04f, 0f, 0f), null, ProvenanceClass.Inferred, -4.2f);
            AddPointSet("globulars", "globulars", GalaxyModel.KmPerParsec, 1.0,
                new LayerBudget(2, 20_000, 0.04f, 0f, 0f), LabelCategory.MilkyWay,
                ProvenanceClass.Inferred, -7.0f);
            AddPointSet("masers", "masers", GalaxyModel.KmPerParsec, 1.0,
                new LayerBudget(2, 20_000, 0.02f, 0f, 0f), LabelCategory.MilkyWay,
                ProvenanceClass.Measured, -5.0f);
            AddPointSet("groupsCf4", "cf4_groups", GalaxyModel.KmPerParsec * 1e6, 1e6,
                new LayerBudget(2, 40_000, 0.05f, 0f, 0f), LabelCategory.Structures,
                ProvenanceClass.Measured, -22.0f);
            AddPointSet("sdss", "sdss", GalaxyModel.KmPerParsec * 1e6, 1e6,
                new LayerBudget(4, 120_000, 0.10f, 0f, 0f), LabelCategory.Structures,
                ProvenanceClass.Inferred, -21.0f);

            AddGalaxies("galaxiesLocal", "localvolume", new LayerBudget(8, 120_000, 0.15f, 0f, 0.1f));
            AddGalaxies("galaxies2mrs", "2mrs", new LayerBudget(6, 200_000, 0.15f, 0f, 0.1f));
        }

        private T Add<T>(string name) where T : CosmoLayer
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            T layer = go.AddComponent<T>();
            layer.Bind(_simulation, State);
            _layers.Add(layer);
            return layer;
        }

        private void AddPointSet(string layerId, string kind, double kmPerUnit, double parsecsPerUnit,
            LayerBudget budget, LabelCategory? labels, ProvenanceClass provenance, float nominalAbsMag)
        {
            PointSetLayer layer = Add<PointSetLayer>(layerId);
            layer.Configure(layerId, kind, kmPerUnit, parsecsPerUnit, budget, labels, provenance,
                nominalAbsMag);
        }

        private void AddGalaxies(string layerId, string kind, LayerBudget budget)
        {
            GalaxyImpostorLayer layer = Add<GalaxyImpostorLayer>(layerId);
            layer.Configure(layerId, kind, budget);
        }

        // ---- data ---------------------------------------------------------------------------

        /// <summary>
        /// Deliver every kind whose file is present. StreamingAssets is not a directory on
        /// Android — it lives compressed inside the APK, where <c>System.IO</c> reports "not
        /// found" — so this goes through the simulation's own coroutine reader, which the desktop
        /// path uses too so the headset never takes a branch the editor did not exercise.
        /// </summary>
        private IEnumerator LoadAll()
        {
            yield return Deliver("stars", Root + "sky/stars.csstars", StarCatalogFrom);
            yield return Deliver("cmb", Root + "cmb/cmb_sky.cscb", null);
            yield return Deliver("dust", Root + "galaxy/dust.csdu", null);
            yield return Deliver("localvolume", Root + "lv/localvolume.cslv", null);
            yield return Deliver("2mrs", Root + "lss/2mrs.csrs", null);
            yield return Deliver("cepheids", Root + "galaxy/cepheids.csgp", null);
            yield return Deliver("globulars", Root + "galaxy/globulars.csgp", null);
            yield return Deliver("masers", Root + "galaxy/masers.csgp", null);
            yield return Deliver("cf4_groups", Root + "lss/cf4_groups.cspt", null);
            yield return Deliver("sdss", Root + "lss/sdss.cspt", null);
            yield return Deliver("structures", Root + "lss/structures.json", null);
            yield return Deliver("constellations", Root + "sky/constellations.json", null);
            yield return Deliver("stars_named", Root + "sky/stars_named.json", null);

            Kinds = LoadedKinds.From(_loaded);
            Debug.Log("[CosmoSynth] deep-sky: " + _loaded.Count + " of 13 kinds present; " +
                      AbsenceSummary());
        }

        /// <summary>
        /// Read a file and hand it to every layer that asked for its kind. A missing file is not
        /// an error: it logs once, the kind is never delivered, and every consuming layer's weight
        /// stays zero for the rest of the session.
        /// </summary>
        private IEnumerator Deliver(string kind, string path, System.Func<byte[], object> parse)
        {
            byte[] bytes = null;
            yield return _simulation.ReadStreamingAssetBytes(path, result => bytes = result);

            if (bytes == null || bytes.Length == 0)
            {
                for (int i = 0; i < _layers.Count; i++)
                {
                    if (!Consumes(_layers[i], kind)) continue;
                    _layers[i].OnData(kind, null);
                }
                yield break;
            }

            object parsed = parse != null ? parse(bytes) : null;
            if (parsed == null)
            {
                // The file is there and no Core parser is: the data lanes own those readers, and
                // writing a second one here would guarantee two formats that disagree.
                Debug.Log("[CosmoSynth] deep-sky: " + kind + " is present (" + bytes.Length +
                          " B) but its Core parser has not landed yet; layer left absent.");
                for (int i = 0; i < _layers.Count; i++)
                    if (Consumes(_layers[i], kind)) _layers[i].OnData(kind, null);
                yield break;
            }

            _loaded.Add(kind);
            Kinds = LoadedKinds.From(_loaded);
            for (int i = 0; i < _layers.Count; i++)
                if (Consumes(_layers[i], kind)) _layers[i].OnData(kind, parsed);
        }

        private static object StarCatalogFrom(byte[] bytes)
        {
            try { return StarCatalog.Parse(bytes, StarFieldLayer.MaxStars); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CosmoSynth] deep-sky: stars.csstars did not parse: " + e.Message);
                return null;
            }
        }

        private static bool Consumes(CosmoLayer layer, string kind)
        {
            IReadOnlyList<string> kinds = layer.DataKinds;
            for (int i = 0; i < kinds.Count; i++) if (kinds[i] == kind) return true;
            return false;
        }

        /// <summary>One line naming what is absent, for the play-mode report.</summary>
        public string AbsenceSummary()
        {
            List<string> absent = new List<string>();
            for (int i = 0; i < _layers.Count; i++)
                if (!_layers[i].HasData) absent.Add(_layers[i].LayerId);
            return absent.Count == 0 ? "every layer has its data" : "absent: " + string.Join(", ", absent);
        }

        /// <summary>
        /// The frame's layer context. One constructor call, in one place; the fields this lane
        /// cannot measure yet are handed their honest defaults rather than plausible ones.
        /// </summary>
        public LayerContext BuildContext(Double3 eyeKm, float roomVisibility, bool cmbBackdrop,
            bool observedSky)
        {
            double fromSunPc = eyeKm.Magnitude / GalaxyModel.KmPerParsec;
            return new LayerContext(Kinds, Stage.Real, roomVisibility, cmbBackdrop,
                viewerOnSite: false, viewerFromSunPc: fromSunPc, nearestStarPc: double.PositiveInfinity,
                focusPx: 0f, sunPx: 0f, earthPx: 0f, viewerInsideHeliosphere: false,
                heliosphereDeg: 0f, nearestNebulaBoxRadii: double.PositiveInfinity,
                observedSkyMode: observedSky);
        }
    }
}
