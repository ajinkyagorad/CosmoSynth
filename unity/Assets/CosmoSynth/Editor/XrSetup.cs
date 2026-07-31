using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmoSynth.Runtime;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
// FeatureHelpers lives in the editor namespace, not the runtime one that OpenXRFeature is in.
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace CosmoSynth.EditorTools
{
    /// <summary>
    /// Configures OpenXR for immersive Meta Quest 3 mixed reality.
    ///
    /// Done in code rather than by hand-editing the XR settings assets, because those are YAML
    /// full of package GUIDs and script references — unreviewable in a diff and silently broken
    /// by a package version bump. The official APIs (<see cref="XRPackageMetadataStore"/>,
    /// <see cref="OpenXRSettings"/>) resolve the same state and survive upgrades.
    ///
    /// The feature that actually decides whether this launches immersive or in a flat 2D panel
    /// is <c>com.unity.openxr.feature.metaquest</c> — Meta Quest Support. It is what injects
    /// <c>com.oculus.intent.category.VR</c> into the merged AndroidManifest. Without it you get
    /// an APK that installs, launches, and shows a flat window inside the Quest shell while
    /// reporting a completely successful build.
    /// </summary>
    public static class XrSetup
    {
        /// <summary>
        /// Features to enable for Android, by OpenXR feature id.
        ///
        /// Ids rather than types: the concrete classes live in different packages and some are
        /// internal, so a typed reference would not compile without a hard dependency on each.
        /// Anything not found is reported rather than skipped silently — a missing passthrough
        /// feature is the difference between mixed reality and a black void, and that must not
        /// be something you discover on the headset.
        /// </summary>
        private static readonly string[] RequiredAndroidFeatures =
        {
            // Immersive launch + Quest-specific manifest entries. Without this it is a flat app.
            "com.unity.openxr.feature.metaquest",

            // Passthrough. AR Foundation's camera subsystem on Meta's runtime is what composites
            // the real world underneath our framebuffer.
            "com.unity.openxr.feature.arfoundation-meta-camera",
            "com.unity.openxr.feature.arfoundation-meta-session",

            // Hand tracking, as a data source for gestures.
            //
            // Note the id. XR Hands' HandTracking feature originally used
            // "...input.handtracking", which COLLIDES with the Microsoft Hand Interaction
            // Profile in the OpenXR package; the package deprecated it and moved to
            // "...handtrackingsubsystem". Enabling the old id silently enables Microsoft's
            // interaction profile instead, so XRHandSubsystem never starts and — because
            // com.unity.xr.hands gates its AndroidManifest injection on the real feature being
            // enabled — the APK ships without com.oculus.permission.HAND_TRACKING. The result
            // is a build that succeeds, launches immersive, and simply never sees a hand.
            "com.unity.openxr.feature.input.handtrackingsubsystem",

            // Meta's aim/pinch pose extension, which is what makes pinch detection track the
            // runtime's own gesture recognition rather than only raw joint positions.
            "com.unity.openxr.feature.input.metahandtrackingaim",

            // Interaction profiles: Touch controllers, plus the generic hand-interaction profile
            // so pinch/poke poses are bound when the controllers are put down.
            "com.unity.openxr.feature.input.oculustouch",
            "com.unity.openxr.feature.input.metaquestplus",
            "com.unity.openxr.feature.input.handinteraction",
            "com.unity.openxr.feature.input.handinteractionposes",
        };

        /// <summary>Optional extras — nice to have, never fatal if the package is absent.</summary>
        private static readonly string[] OptionalAndroidFeatures =
        {
            "com.unity.openxr.feature.meta-boundary-visibility",
            "com.unity.openxr.feature.compositionlayers",
        };

        /// <summary>
        /// Features that must be OFF. Foveated rendering only supports the legacy API on the
        /// Built-in Render Pipeline, and OpenXR's project validation treats that as a build
        /// error rather than a warning — so leaving it enabled fails the build outright with a
        /// message that does not name the feature responsible.
        /// </summary>
        private static readonly string[] DisabledAndroidFeatures =
        {
            "com.unity.openxr.feature.foveatedrendering",
        };

        [MenuItem("CosmoSynth/Configure XR for Quest", priority = 5)]
        public static void Configure()
        {
            Log("configuring OpenXR for Android / Meta Quest");

            EnsureLoader();
            EnsureFeatures();
            EnsureRenderSettings();
            EnsureBothInputHandlers();
            EnsureShadersIncluded();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("done");
        }

        /// <summary>Batchmode entry point; exits non-zero if anything required is missing.</summary>
        public static void Run()
        {
            try
            {
                Configure();
                EditorApplication.Exit(_failures == 0 ? 0 : 1);
            }
            catch (Exception exception)
            {
                Log($"FAILED: {exception}");
                EditorApplication.Exit(2);
            }
        }

        private static int _failures;

        // ---- Loader ---------------------------------------------------------

        private static void EnsureLoader()
        {
            // The settings object is found through EditorBuildSettings' config-object table, not
            // by asset path, which is why simply having the asset in Assets/XR is not enough.
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                    out XRGeneralSettingsPerBuildTarget perBuildTarget) || perBuildTarget == null)
            {
                Log("no XRGeneralSettingsPerBuildTarget registered; creating one");
                perBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perBuildTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBuildTarget, true);
            }

            perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            XRGeneralSettings android = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Android);

            if (android == null || android.Manager == null)
            {
                Fail("could not obtain XRGeneralSettings for Android");
                return;
            }

            // Load XR on startup — otherwise the app boots into a flat window and waits for
            // someone to call the initialisation manually.
            android.InitManagerOnStart = true;

            const string loader = "UnityEngine.XR.OpenXR.OpenXRLoader";
            if (!android.Manager.activeLoaders.Any(l => l != null && l.GetType().FullName == loader))
            {
                if (XRPackageMetadataStore.AssignLoader(android.Manager, loader, BuildTargetGroup.Android))
                    Log($"assigned {loader} for Android");
                else
                    Fail($"could not assign {loader} for Android");
            }
            else
            {
                Log($"{loader} already assigned for Android");
            }

            EditorUtility.SetDirty(perBuildTarget);
            EditorUtility.SetDirty(android);
        }

        // ---- Features -------------------------------------------------------

        private static void EnsureFeatures()
        {
            // Rebuilds the feature list from what the installed packages actually provide. Without
            // it, features added by a newly installed package are simply not present in settings.
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);

            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Fail("no OpenXRSettings for Android");
                return;
            }

            OpenXRFeature[] features = settings.GetFeatures();
            Log($"{features.Length} OpenXR features available for Android");

            var byId = new Dictionary<string, OpenXRFeature>(StringComparer.OrdinalIgnoreCase);
            foreach (OpenXRFeature feature in features)
            {
                string id = FeatureId(feature);
                if (!string.IsNullOrEmpty(id)) byId[id] = feature;
            }

            foreach (string id in RequiredAndroidFeatures) Enable(byId, id, required: true);
            foreach (string id in OptionalAndroidFeatures) Enable(byId, id, required: false);

            foreach (string id in DisabledAndroidFeatures)
            {
                if (!byId.TryGetValue(id, out OpenXRFeature feature)) continue;
                feature.enabled = false;
                EditorUtility.SetDirty(feature);
                Log($"  disabled {id}");
            }

            EditorUtility.SetDirty(settings);

            // Meta's build hook looks its own feature up with AssetDatabase.FindAssets and bails
            // to null unless it finds exactly one, which then surfaces as a bare
            // NullReferenceException from deep inside the package with nothing naming the cause.
            // Counting it here turns that into a diagnosis instead of an afternoon.
            string[] metaQuestAssets = AssetDatabase.FindAssets("t:MetaQuestFeature");
            Log($"AssetDatabase sees {metaQuestAssets.Length} MetaQuestFeature asset(s) " +
                "(Meta's build hook requires exactly 1)");
            foreach (string guid in metaQuestAssets) Log($"    {AssetDatabase.GUIDToAssetPath(guid)}");

            if (metaQuestAssets.Length != 1)
            {
                Fail($"Meta Quest build hook will throw: it needs exactly one MetaQuestFeature " +
                     $"asset but the database has {metaQuestAssets.Length}.");
            }
        }

        private static void Enable(Dictionary<string, OpenXRFeature> byId, string id, bool required)
        {
            if (byId.TryGetValue(id, out OpenXRFeature feature))
            {
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
                Log($"  enabled {id}");
                return;
            }

            if (required) Fail($"  REQUIRED feature not found: {id}");
            else Log($"  optional feature not present, skipping: {id}");
        }

        /// <summary>
        /// Read a feature's id from its <c>OpenXRFeatureAttribute</c>. The attribute type is
        /// resolved by name because it lives in an editor assembly this one does not reference.
        /// </summary>
        private static string FeatureId(OpenXRFeature feature)
        {
            if (feature == null) return null;

            foreach (Attribute attribute in feature.GetType().GetCustomAttributes(false).OfType<Attribute>())
            {
                if (attribute.GetType().Name != "OpenXRFeatureAttribute") continue;

                PropertyInfo property = attribute.GetType().GetProperty("FeatureId");
                if (property != null) return property.GetValue(attribute) as string;

                FieldInfo field = attribute.GetType().GetField("FeatureId");
                if (field != null) return field.GetValue(attribute) as string;
            }

            // Fall back to the serialised field the settings asset itself uses.
            FieldInfo internalId = typeof(OpenXRFeature).GetField("featureIdInternal",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return internalId?.GetValue(feature) as string;
        }

        // ---- Render settings -------------------------------------------------

        private static void EnsureRenderSettings()
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null) return;

            // Single-pass instanced: one draw call renders both eyes. On a mobile GPU the
            // multi-pass alternative costs close to double, and this scene is draw-call bound
            // rather than fill bound.
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;

            // Submitting depth lets the Quest compositor reproject correctly, which matters for
            // passthrough: without it, virtual geometry swims against the real room when the
            // head moves.
            settings.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth16Bit;

            Log($"render mode {settings.renderMode}, depth submission {settings.depthSubmissionMode}");
            EditorUtility.SetDirty(settings);
        }

        /// <summary>
        /// Set Active Input Handling to "Both".
        ///
        /// Installing com.unity.inputsystem silently flips this to New-only, at which point
        /// every legacy <c>Input.GetKeyDown</c> throws InvalidOperationException at runtime —
        /// so the desktop controls and the HUD toggle would die on first keypress while the
        /// build reported success. XR itself uses the new system; "Both" keeps the flat
        /// editor-testing path alive alongside it.
        ///
        /// There is no PlayerSettings API for this field, so it is written through
        /// SerializedObject. The change needs a domain reload to take effect, which is free
        /// here because each batchmode invocation is a fresh editor.
        /// </summary>
        private static void EnsureBothInputHandlers()
        {
            const int both = 2;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Fail("could not open ProjectSettings.asset to set Active Input Handling");
                return;
            }

            var serialized = new SerializedObject(assets[0]);
            SerializedProperty handler = serialized.FindProperty("activeInputHandler");
            if (handler == null)
            {
                Fail("activeInputHandler property not found");
                return;
            }

            if (handler.intValue == both)
            {
                Log("active input handling already 'Both'");
                return;
            }

            handler.intValue = both;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Log("active input handling set to 'Both' (was " + handler.intValue + ")");
        }

        /// <summary>
        /// Write every shader the runtime looks up by name into Always Included Shaders.
        ///
        /// This is not optional for this project, it is load-bearing. CosmoSynth builds its
        /// whole scene at runtime, so no asset references any shader — and Unity strips shaders
        /// nothing references. In the editor every shader is always loaded, so it looks correct;
        /// in the player <c>Shader.Find</c> returns null and every object renders magenta. The
        /// failure appears only on the device and only after a full IL2CPP build.
        ///
        /// The list is read from <see cref="CosmoShaders.All"/>, which is the same array the
        /// runtime uses, so the two cannot drift apart.
        /// </summary>
        private static void EnsureShadersIncluded()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Fail("could not open GraphicsSettings.asset to register shaders");
                return;
            }

            var serialized = new SerializedObject(assets[0]);
            SerializedProperty included = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (included == null)
            {
                Fail("m_AlwaysIncludedShaders property not found");
                return;
            }

            // Collect what is already there so the list does not grow duplicates on every run.
            var present = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < included.arraySize; i++)
            {
                var existing = included.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (existing != null) present.Add(existing.name);
            }

            int added = 0;
            foreach (string name in CosmoShaders.All)
            {
                if (present.Contains(name)) continue;

                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    Fail($"shader '{name}' does not exist in this project at all");
                    continue;
                }

                included.InsertArrayElementAtIndex(included.arraySize);
                included.GetArrayElementAtIndex(included.arraySize - 1).objectReferenceValue = shader;
                present.Add(name);
                added++;
                Log($"  always-include shader: {name}");
            }

            if (added > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }

            Log($"always-included shaders: {included.arraySize} total, {added} added this run");
        }

        private static void Fail(string message)
        {
            _failures++;
            Debug.LogError($"[XrSetup] {message}");
        }

        private static void Log(string message) => Debug.Log($"[XrSetup] {message}");
    }
}
