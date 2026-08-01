using CosmoSynth.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmoSynth.EditorTools
{
    /// <summary>
    /// Editor commands that create the playable scene and check the project's data.
    ///
    /// The scene is generated rather than committed. A .unity file is opaque YAML full of
    /// GUIDs: it cannot be reviewed in a pull request, it silently breaks when a component is
    /// renamed, and it is the most merge-hostile file a Unity project can contain. Generating
    /// it from one legible C# method costs a menu click and removes all of that.
    /// </summary>
    public static class SceneBuilder
    {
        private const string SceneDirectory = "Assets/CosmoSynth/Scenes";
        private const string ScenePath = SceneDirectory + "/SolarSystemLab.unity";

        [MenuItem("CosmoSynth/Create Solar System Lab Scene", priority = 0)]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrap = new GameObject("CosmoSynth");
            bootstrap.AddComponent<CosmoSynthBootstrap>();

            if (!AssetDatabase.IsValidFolder(SceneDirectory))
            {
                AssetDatabase.CreateFolder("Assets/CosmoSynth", "Scenes");
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Debug.Log($"[CosmoSynth] Created {ScenePath}. Press Play to run.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        }

        [MenuItem("CosmoSynth/Validate Shipped Catalogue", priority = 20)]
        public static void ValidateCatalog()
        {
            string path = System.IO.Path.Combine(
                Application.streamingAssetsPath, "CosmoSynth/catalog/solar_system.json");

            if (!System.IO.File.Exists(path))
            {
                Debug.LogError(
                    $"[CosmoSynth] No catalogue at {path}.\n" +
                    "Build it with: python3 tools/cosmodata/build_solar_system.py");
                return;
            }

            Core.CelestialCatalog catalog;
            try
            {
                catalog = Core.CelestialCatalog.Parse(System.IO.File.ReadAllText(path));
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[CosmoSynth] Catalogue will not parse: {exception.Message}");
                return;
            }

            var problems = catalog.Validate();
            if (problems.Count == 0)
            {
                Debug.Log($"[CosmoSynth] '{catalog.Name}' is valid: {catalog.Count} bodies, no problems.");
            }
            else
            {
                Debug.LogWarning($"[CosmoSynth] '{catalog.Name}' has {problems.Count} problems:\n  " +
                                 string.Join("\n  ", problems));
            }

            // Summarise how much of the catalogue rests on measurement rather than invention.
            var counts = new System.Collections.Generic.Dictionary<Core.ProvenanceClass, int>();
            foreach (var body in catalog.Bodies)
            {
                foreach (var pair in body.Provenance.Summary())
                {
                    counts.TryGetValue(pair.Key, out int existing);
                    counts[pair.Key] = existing + pair.Value;
                }
            }

            var summary = new System.Text.StringBuilder("[CosmoSynth] Provenance across all bodies:");
            foreach (var pair in counts) summary.Append($"\n  {pair.Key}: {pair.Value} fields");
            Debug.Log(summary.ToString());
        }

        [MenuItem("CosmoSynth/Documentation/Open Accuracy Notes", priority = 40)]
        public static void OpenAccuracyNotes()
        {
            string path = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../../docs/accuracy.md"));

            if (System.IO.File.Exists(path)) EditorUtility.RevealInFinder(path);
            else Debug.LogWarning($"[CosmoSynth] Expected accuracy notes at {path}.");
        }
    }
}
