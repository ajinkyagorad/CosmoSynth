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
using UnityEngine.Rendering;
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

            // Foveated rendering. It used to be in the DISABLED list, because OpenXR project
            // validation treats "Only Legacy Foveated Rendering API usage is possible" as a build
            // ERROR on the Built-in pipeline and the build aborts without naming the feature. On
            // URP the SRPFoveation API is available, so the error goes away and the feature is
            // simply free resolution in the periphery.
            "com.unity.openxr.feature.foveatedrendering",
        };

        /// <summary>Optional extras — nice to have, never fatal if the package is absent.</summary>
        private static readonly string[] OptionalAndroidFeatures =
        {
            "com.unity.openxr.feature.meta-boundary-visibility",
            "com.unity.openxr.feature.compositionlayers",
        };

        /// <summary>
        /// Features that must be OFF.
        ///
        /// The deprecated hand-tracking id, which is Microsoft's Hand Interaction Profile wearing
        /// XR Hands' old name. Enabling it instead of "...handtrackingsubsystem" starts the wrong
        /// interaction profile, XRHandSubsystem never starts, and because com.unity.xr.hands gates
        /// its manifest injection on the real feature the APK ships without
        /// com.oculus.permission.HAND_TRACKING — a build that succeeds, launches immersive, and
        /// simply never sees a hand. Two of the four documents in this project's settings had it
        /// on.
        /// </summary>
        private static readonly string[] DisabledAndroidFeatures =
        {
            "com.unity.openxr.feature.input.handtracking",
        };

        [MenuItem("CosmoSynth/Configure XR for Quest", priority = 5)]
        public static void Configure()
        {
            Log("configuring OpenXR for Android / Meta Quest");

            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;

            DeleteOrphanXrAssets();
            EnsureLoader();
            EnsureFeatures();
            EnsureRenderSettings();
            EnsureColorSpace();
            EnsureStaticBatchingOff();
            EnsureUrp();
            EnsureFramePacing();
            EnsureBothInputHandlers();
            EnsureShadersIncluded();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Summarise();
            Log("done");
        }

        /// <summary>
        /// One line carrying every value this method decided, which G0c greps and a human can
        /// read in the build log. A configuration step whose result is not printed is a
        /// configuration step nobody can check.
        /// </summary>
        private static void Summarise()
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;
            int metaQuest = AssetDatabase.FindAssets("t:MetaQuestFeature").Length;

            Debug.Log($"[XrSetup] SUMMARY pipeline={(pipeline != null ? pipeline.name : "Built-in")} " +
                      $"colourSpace={PlayerSettings.colorSpace} " +
                      $"renderMode={(settings != null ? settings.renderMode.ToString() : "?")} " +
                      $"depthSubmission={(settings != null ? settings.depthSubmissionMode.ToString() : "?")} " +
                      $"latency={(settings != null ? settings.latencyOptimization.ToString() : "?")} " +
                      $"symmetricProjection={(settings != null ? settings.symmetricProjection.ToString() : "?")} " +
                      $"bufferDiscards={(settings != null ? settings.optimizeBufferDiscards.ToString() : "?")} " +
                      $"foveation={(settings != null ? settings.foveatedRenderingApi.ToString() : "?")} " +
                      $"metaQuestFeatures={metaQuest} " +
                      $"swappy={PlayerSettings.Android.optimizedFramePacing} " +
                      $"failures={_failures}");
        }

        /// <summary>Batchmode entry point; exits non-zero if anything required is missing.</summary>
        public static void Run()
        {
            try
            {
                Configure();
                if (_failures == 0) Debug.Log("[XrSetup] XRSETUP_OK");
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

            // Only create a manager when there is not one already.
            //
            // Calling this unconditionally adds a fresh XRManagerSettings sub-asset on every run
            // and leaves the old one behind, unreferenced. Five runs, five "Android Providers"
            // managers inside one asset, four of them orphans — and every tool that counts them
            // (including Meta's own build hook, which needs exactly one MetaQuestFeature) then has
            // to guess which is live.
            XRGeneralSettings android = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (android == null || android.Manager == null)
            {
                perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
                android = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            }
            RemoveOrphanManagers(perBuildTarget);

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

        /// <summary>
        /// Delete every XRManagerSettings sub-asset no XRGeneralSettings references.
        ///
        /// They are invisible in the inspector and they are what makes "how many providers are
        /// configured" an unanswerable question. Removed rather than left, because a stale one is
        /// indistinguishable from the live one to anything reading the YAML.
        /// </summary>
        private static void RemoveOrphanManagers(XRGeneralSettingsPerBuildTarget perBuildTarget)
        {
            string path = AssetDatabase.GetAssetPath(perBuildTarget);
            if (string.IsNullOrEmpty(path)) return;

            var live = new HashSet<XRManagerSettings>();
            foreach (BuildTargetGroup group in (BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup)))
            {
                XRGeneralSettings settings = perBuildTarget.SettingsForBuildTarget(group);
                if (settings != null && settings.Manager != null) live.Add(settings.Manager);
            }

            int removed = 0;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(asset is XRManagerSettings manager) || live.Contains(manager)) continue;
                AssetDatabase.RemoveObjectFromAsset(manager);
                UnityEngine.Object.DestroyImmediate(manager, true);
                removed++;
            }

            if (removed > 0)
            {
                EditorUtility.SetDirty(perBuildTarget);
                AssetDatabase.SaveAssets();
            }
            Log($"orphan XRManagerSettings removed: {removed} ({live.Count} live)");
        }

        /// <summary>
        /// Delete the duplicated XR settings directories Unity leaves behind when a package
        /// re-imports: "Assets/XR 1", "Assets/XR 2" and so on. They are empty of anything the
        /// build reads, and they are what the settings gate counts when it reports five "Android
        /// Providers" managers.
        /// </summary>
        private static void DeleteOrphanXrAssets()
        {
            var candidates = new List<string>();
            for (int i = 1; i <= 9; i++)
            {
                candidates.Add($"Assets/XR {i}");
                candidates.Add($"Assets/XR/Settings {i}");
                candidates.Add($"Assets/XR/Loaders {i}");
            }

            int deleted = 0;
            foreach (string path in candidates)
            {
                if (!AssetDatabase.IsValidFolder(path)) continue;
                if (AssetDatabase.DeleteAsset(path)) { deleted++; Log($"  deleted duplicate {path}"); }
                else Fail($"  could not delete duplicate {path}");
            }

            if (deleted > 0) AssetDatabase.Refresh();
            Log($"duplicate XR directories deleted: {deleted}");
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
            if (settings == null) { Fail("no OpenXRSettings for Android to configure"); return; }

            // Single-pass instanced: one draw call renders both eyes. On a mobile GPU the
            // multi-pass alternative costs close to double, and this scene is draw-call bound
            // rather than fill bound.
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;

            // NO depth submission.
            //
            // It was Depth16Bit, which submits a SIXTEEN-BIT depth buffer to the compositor for
            // reprojection — and sixteen bits is 256 times coarser than the twenty-four the whole
            // placement design's separability arithmetic assumes. Submitting depth also forces the
            // depth attachment to be resolved and stored rather than staying memoryless, which is
            // the bandwidth this app has least of. The compositor reprojects on pose alone
            // without it, and every surface here is far enough away that the difference is not
            // perceptible.
            settings.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.None;

            // Poll input as late as possible: this app is a direct-manipulation interface, and
            // hand latency is what it is judged on, not a millisecond of extra render latency.
            settings.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;

            // One symmetric frustum covering both eyes instead of two asymmetric ones. It is what
            // lets a single-pass-instanced draw cull once for both eyes.
            settings.symmetricProjection = true;

            // Safe ONLY with one camera. With two, the second pass loads the attachment the first
            // discarded, and what it loads is undefined tile memory — which is the strongest
            // candidate for the "100 plus Earths on a grid" a headset reported on 1.7.0. Gate G0d
            // counts the cameras and G0c cross-checks this flag against that count.
            settings.optimizeBufferDiscards = true;

            // SRPFoveation, not Legacy: the URP-side API drives foveation on more of the
            // render targets than the legacy one reaches.
            settings.foveatedRenderingApi = OpenXRSettings.BackendFovationApi.SRPFoveation;

            EditorUtility.SetDirty(settings);
            MirrorMetaQuestFeatureFlags();

            Log($"render mode {settings.renderMode}, depth submission {settings.depthSubmissionMode}, " +
                $"latency {settings.latencyOptimization}, symmetric {settings.symmetricProjection}, " +
                $"buffer discards {settings.optimizeBufferDiscards}, foveation {settings.foveatedRenderingApi}");
        }

        /// <summary>
        /// MetaQuestFeature keeps its own copies of two of the flags above, and they are
        /// <c>internal</c> — so they are written through SerializedObject, the same way Active
        /// Input Handling is. If they disagree with the OpenXR settings, Meta's build hook wins
        /// and the settings the log just printed are not the ones that ship.
        /// </summary>
        private static void MirrorMetaQuestFeatureFlags()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:MetaQuestFeature"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset == null || asset.GetType().Name != "MetaQuestFeature") continue;

                    var so = new SerializedObject(asset);
                    int written = 0;
                    foreach (string field in new[] { "m_symmetricProjection", "m_optimizeBufferDiscards" })
                    {
                        SerializedProperty p = so.FindProperty(field);
                        if (p == null) { Log($"  MetaQuestFeature has no '{field}'"); continue; }
                        if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = true;
                        else p.intValue = 1;
                        written++;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Log($"  MetaQuestFeature at {path}: {written} flag(s) set to 1");
                }
            }
        }

        /// <summary>
        /// Linear colour space.
        ///
        /// Gamma means every shader's arithmetic happens on values that are not proportional to
        /// light, so an additive star at half brightness is not half as bright and a terminator
        /// is the wrong shape. It is a project-wide decision and changing it re-tunes every
        /// colour in the app, which is why it belongs in Phase 0 and not later.
        /// </summary>
        /// <summary>
        /// Turn static and dynamic batching off for Android (R12).
        ///
        /// Static batching is ON by default for Android and it bakes vertices into world space,
        /// which silently destroys three object-space assumptions: CosmoSynth/Volume's raymarch
        /// entry point, CosmoSynth/Sky's vertices-are-ICRF-directions, and CosmoSynth/Ribbon's
        /// otherOS endpoint. The "DisableBatching"="True" tags on those three shaders are the
        /// belt; this is the braces, and G0c has a row for it. Practical exposure today is low —
        /// nothing is marked BatchingStatic — which is exactly why it would be reintroduced by a
        /// later layer with nobody noticing.
        /// </summary>
        private static void EnsureStaticBatchingOff()
        {
            const string androidTarget = "Android";

            // Through SerializedObject, like EnsureBothInputHandlers, and NOT through
            // PlayerSettings.SetBatchingForPlatform: that overload is internal in Unity 6, so it
            // does not compile at all here. The serialised field is m_BuildTargetBatching, a list
            // that is EMPTY by default -- and empty means the defaults, which for Android is
            // static batching ON. G0c reads m_StaticBatching: 0 out of the YAML this writes.
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Fail("could not open ProjectSettings.asset to turn batching off");
                return;
            }

            var serialized = new SerializedObject(assets[0]);
            SerializedProperty list = serialized.FindProperty("m_BuildTargetBatching");
            if (list == null || !list.isArray)
            {
                Fail("m_BuildTargetBatching property not found");
                return;
            }

            SerializedProperty entry = null;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty candidate = list.GetArrayElementAtIndex(i);
                SerializedProperty target = candidate.FindPropertyRelative("m_BuildTarget");
                if (target != null && target.stringValue == androidTarget) { entry = candidate; break; }
            }

            if (entry == null)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                SerializedProperty target = entry.FindPropertyRelative("m_BuildTarget");
                if (target == null)
                {
                    Fail("m_BuildTargetBatching entries have no m_BuildTarget field");
                    return;
                }
                target.stringValue = androidTarget;
            }

            SerializedProperty staticBatching = entry.FindPropertyRelative("m_StaticBatching");
            SerializedProperty dynamicBatching = entry.FindPropertyRelative("m_DynamicBatching");
            if (staticBatching == null || dynamicBatching == null)
            {
                Fail("m_StaticBatching / m_DynamicBatching not found on the Android batching entry");
                return;
            }

            staticBatching.intValue = 0;
            dynamicBatching.intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Log("android batching static=0 dynamic=0");
        }

        private static void EnsureColorSpace()
        {
            if (PlayerSettings.colorSpace == ColorSpace.Linear)
            {
                Log("colour space already Linear");
                return;
            }
            PlayerSettings.colorSpace = ColorSpace.Linear;
            Log("colour space set to Linear (was Gamma)");
        }

        /// <summary>Create and assign the URP asset and renderer. See <see cref="CosmoUrpConfig"/>.</summary>
        private static void EnsureUrp()
        {
            try
            {
                CosmoUrpConfig.Create();
            }
            catch (Exception exception)
            {
                Fail($"URP configuration failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Turn off optimized frame pacing, which removes Swappy and libswappywrapper.so.
        ///
        /// The OpenXR runtime owns frame timing on a headset. A second pacer underneath it does
        /// not help and ships a library the artefact gate would otherwise have to warn about.
        /// </summary>
        private static void EnsureFramePacing()
        {
            PlayerSettings.Android.optimizedFramePacing = false;
            Log($"optimized frame pacing: {PlayerSettings.Android.optimizedFramePacing}");
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
