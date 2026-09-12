// CosmoUrpConfig — creates the Quest 3 URP asset and renderer from code and assigns them to
// every quality level and to GraphicsSettings.defaultRenderPipeline.
//
// Ported from ~/projects/Worldfall/Assets/Worldfall/Editor/WorldfallUrpConfig.cs, which is
// headset-proven on this box. Every serialized field name below was read off a real URP 17.5
// asset, never from memory: URP renames these between versions and a FindProperty that returns
// null fails silently, leaving the default in place while the log says the value was set.
//
// It verifies after writing, by re-importing and reading back. A configuration step that only
// writes is a configuration step you find out about on the device.
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmoSynth.EditorTools
{
    public static class CosmoUrpConfig
    {
        public const string SettingsDir = "Assets/CosmoSynth/Settings";
        public const string UrpAssetPath = SettingsDir + "/CS_URP.asset";
        public const string RendererPath = SettingsDir + "/CS_Renderer.asset";

        /// <summary>Serialized int/bool fields on the URP asset and their required values.</summary>
        public static readonly (string prop, int value)[] AssetExpect =
        {
            // 4x MSAA, from the asset rather than from the camera. With ONE camera the attachment
            // is memoryless: it is resolved out of tile memory and never loaded back, so it costs
            // bandwidth nothing. Two cameras is what made it cost, and worse.
            ("m_MSAA", 4),
            ("m_SupportsHDR", 0),
            ("m_RequireDepthTexture", 0),
            ("m_RequireOpaqueTexture", 0),
            ("m_SupportsTerrainHoles", 0),

            // No Unity lights at all. CosmoSynth/Body carries _LightDirWS and _LightColor,
            // written per body per frame from its own host star through the parent chain, and
            // SkyAndRings no longer creates a point light or a directional fill. This is the same
            // commit that lands CosmoSynth/Body, which is the condition the previous note set.
            //
            // It is not just waste. The Android quality level allows exactly ONE per-pixel light
            // and Unity ranks a directional highly whatever its intensity, so a dim fill could
            // take the only slot and demote the star lighting the entire system to a vertex
            // light — on the device only, with nothing in the editor to reproduce it against.
            ("m_MainLightRenderingMode", 0),
            ("m_AdditionalLightsRenderingMode", 0),
            ("m_AdditionalLightsPerObjectLimit", 0),

            // No shadows anywhere. A shadow pass is a second render of the scene, and nothing in
            // this app casts a shadow anyone can see: bodies are separated by millions of km.
            ("m_MainLightShadowsSupported", 0),
            ("m_AdditionalLightShadowsSupported", 0),
            ("m_SoftShadowsSupported", 0),

            ("m_UseSRPBatcher", 1),
            ("m_SupportsDynamicBatching", 0),
            ("m_EnableLODCrossFade", 0),
            ("m_StoreActionsOptimization", 0),        // Auto
            ("m_VolumeFrameworkUpdateMode", 1),       // ViaScripting: no volume update per frame
            ("m_UseAdaptivePerformance", 0),
            ("m_ReflectionProbeBlending", 0),
        };

        public static readonly (string prop, float value)[] AssetExpectFloat =
        {
            ("m_RenderScale", 1f),
            ("m_ShadowDistance", 0f),
        };

        /// <summary>Per-level fields URP owns, forced off so the two cannot disagree.</summary>
        public static readonly (string prop, int value)[] QualityLevelExpect =
        {
            ("pixelLightCount", 0),
            ("shadows", 0),            // ShadowQuality.Disable
            ("antiAliasing", 0),       // the URP asset owns MSAA
            ("vSyncCount", 0),
        };

        /// <summary>The quality level Android builds run at, by name. See AndroidQualityLevelIndex.</summary>
        public const string AndroidQualityLevelName = "Medium";

        public static readonly (string prop, int value)[] RendererExpect =
        {
            ("m_RenderingMode", 0),                   // Forward
            ("m_DepthPrimingMode", 0),                // Disabled
            ("m_IntermediateTextureMode", 0),         // Auto: no intermediate texture unless forced
            ("m_ShadowTransparentReceive", 0),
        };

        public static void Create()
        {
            foreach (string line in Describe("before")) Debug.Log("[XrSetup] urp " + line);

            if (!AssetDatabase.IsValidFolder(SettingsDir))
            {
                AssetDatabase.CreateFolder("Assets/CosmoSynth", "Settings");
            }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            renderer.postProcessData = null;                 // no post-processing on this renderer at all
            renderer.renderingMode = RenderingMode.Forward;
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            renderer.intermediateTextureMode = IntermediateTextureMode.Auto;
            renderer.shadowTransparentReceive = false;
            ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
            SetInts(renderer, RendererExpect);
            EditorUtility.SetDirty(renderer);

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, UrpAssetPath);
            }

            var so = new SerializedObject(asset);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.FindProperty("m_DefaultRendererIndex").intValue = 0;
            so.FindProperty("m_RendererType").intValue = 1;   // UniversalRenderer
            so.ApplyModifiedPropertiesWithoutUndo();
            SetInts(asset, AssetExpect);
            SetFloats(asset, AssetExpectFloat);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            // Quality levels, written through the serialized QualitySettings singleton.
            //
            // SetQualityLevel(i) followed by the instance properties is what the Worldfall port
            // did, and in 6000.5.5f1 batchmode it persists NOTHING: after a complete run every
            // level still read pixelLightCount 4 / shadows 2 / customRenderPipeline {fileID: 0},
            // and GetRenderPipelineAssetAt returned null for all six. There is no public
            // SetRenderPipelineAssetAt in this version (checked against the editor assemblies),
            // so the serialized list is the only writer that sticks. Verify() reads it back off
            // the YAML, which is also where settings_gate.py looks.
            SerializedObject qs = QualitySettingsObject();
            SerializedProperty levels = qs.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = asset;

                // Everything URP now owns, switched off at the quality level so the two cannot
                // disagree. MSAA in particular: a quality level asking for 2x while the URP asset
                // asks for 4x is a value nobody chose.
                foreach ((string prop, int value) in QualityLevelExpect)
                {
                    SerializedProperty p = level.FindPropertyRelative(prop);
                    if (p == null) { Debug.LogWarning($"[XrSetup] urp quality[{i}]: no '{prop}'"); continue; }
                    p.intValue = value;
                }
            }
            qs.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline = asset;
            AssetDatabase.SaveAssets();

            // Last, and only now. SetAndroidDefaultQuality rewrites the YAML as text, so any
            // in-memory change still unsaved at this point would overwrite it on the next save.
            SetAndroidDefaultQuality(AndroidQualityLevelIndex());

            foreach (string line in Describe("after")) Debug.Log("[XrSetup] urp " + line);

            List<string> problems = Verify(out List<string> report);
            foreach (string line in report) Debug.Log("[XrSetup] urp " + line);
            if (problems.Count > 0)
            {
                throw new System.InvalidOperationException(
                    "URP settings did not stick: " + string.Join("; ", problems));
            }
        }

        /// <summary>
        /// <c>m_PerPlatformDefaultQuality</c> is a serialized map with no public setter in this
        /// Unity version, so the YAML line is edited directly. Read back by <see cref="Describe"/>.
        /// </summary>
        private static void SetAndroidDefaultQuality(int level)
        {
            const string path = "ProjectSettings/QualitySettings.asset";
            string text = File.ReadAllText(path);
            var re = new Regex(@"(m_PerPlatformDefaultQuality:(?:\r?\n {4}[^\n]+)*?\r?\n {4}Android: )(\d+)");
            text = re.IsMatch(text)
                ? re.Replace(text, "${1}" + level, 1)
                : text.Replace("m_PerPlatformDefaultQuality:", "m_PerPlatformDefaultQuality:\n    Android: " + level);
            File.WriteAllText(path, text);
            AssetDatabase.Refresh();
        }

        public static int ReadAndroidDefaultQuality()
        {
            string text = File.ReadAllText("ProjectSettings/QualitySettings.asset");
            Match m = new Regex(@"m_PerPlatformDefaultQuality:(?:\r?\n {4}[^\n]+)*?\r?\n {4}Android: (\d+)").Match(text);
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }

        public static IEnumerable<string> Describe(string tag)
        {
            RenderPipelineAsset def = GraphicsSettings.defaultRenderPipeline;
            yield return $"{tag}: defaultRenderPipeline={(def != null ? def.name : "none (Built-in)")}";

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                RenderPipelineAsset rp = QualitySettings.GetRenderPipelineAssetAt(i);
                yield return $"{tag}: quality[{i}] '{QualitySettings.names[i]}' " +
                             $"rp={(rp != null ? rp.name : "(graphics default)")}";
            }

            int android = ReadAndroidDefaultQuality();
            yield return $"{tag}: Android default quality index={android}";

            if (def is UniversalRenderPipelineAsset urp)
            {
                yield return $"{tag}: {urp.name} msaa={urp.msaaSampleCount} hdr={urp.supportsHDR} " +
                             $"scale={urp.renderScale} depthTex={urp.supportsCameraDepthTexture} " +
                             $"opaqueTex={urp.supportsCameraOpaqueTexture} mainShadows={urp.supportsMainLightShadows} " +
                             $"addLights={urp.additionalLightsRenderingMode} srpBatcher={urp.useSRPBatcher}";

                if (RendererOf(urp) is UniversalRendererData rd)
                {
                    yield return $"{tag}: renderer {rd.name} mode={rd.renderingMode} " +
                                 $"depthPriming={rd.depthPrimingMode} intermediate={rd.intermediateTextureMode} " +
                                 $"postProcessData={(rd.postProcessData != null ? rd.postProcessData.name : "none")} " +
                                 $"features={rd.rendererFeatures.Count}";
                }
            }

            yield return $"{tag}: colorSpace={PlayerSettings.colorSpace}";
        }

        public static List<string> Verify(out List<string> report)
        {
            report = new List<string>();
            var problems = new List<string>();

            AssetDatabase.ImportAsset(UrpAssetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(RendererPath, ImportAssetOptions.ForceSynchronousImport);

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (asset == null) { problems.Add("CS_URP.asset missing"); return problems; }
            if (renderer == null) { problems.Add("CS_Renderer.asset missing"); return problems; }

            CheckInts(asset, AssetExpect, "CS_URP", problems, report);
            CheckFloats(asset, AssetExpectFloat, "CS_URP", problems, report);
            CheckInts(renderer, RendererExpect, "CS_Renderer", problems, report);

            if (renderer.postProcessData != null) problems.Add("renderer still has PostProcessData");
            if (RendererOf(asset) != renderer) problems.Add("CS_URP does not point at CS_Renderer");
            if (GraphicsSettings.defaultRenderPipeline != asset)
                problems.Add("GraphicsSettings.defaultRenderPipeline is not CS_URP");

            // Two independent readbacks, because the defect this replaced was "wrote it,
            // logged it, believed it": the engine's own view, and the YAML on disk, which is
            // what settings_gate.py and the build actually consume.
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) != asset)
                    problems.Add($"quality level {i} '{QualitySettings.names[i]}' not on CS_URP (engine)");
            }

            string guid = AssetDatabase.AssetPathToGUID(UrpAssetPath);
            SerializedObject qs = QualitySettingsObject();
            SerializedProperty levels = qs.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                string name = level.FindPropertyRelative("name").stringValue;
                if (level.FindPropertyRelative("customRenderPipeline").objectReferenceValue != asset)
                    problems.Add($"quality level {i} '{name}' customRenderPipeline not CS_URP (serialized)");
                foreach ((string prop, int value) in QualityLevelExpect)
                {
                    SerializedProperty p = level.FindPropertyRelative(prop);
                    int got = p != null ? p.intValue : -1;
                    if (got != value) problems.Add($"quality level {i} '{name}' {prop} = {got} (want {value})");
                }
            }
            foreach (string bad in QualityLevelsWithoutRpGuid(guid))
                problems.Add($"quality level {bad} on disk (QualitySettings.asset)");

            int android = ReadAndroidDefaultQuality();
            int wantAndroid = AndroidQualityLevelIndex();
            if (android != wantAndroid)
                problems.Add($"Android default quality is {android}, want {wantAndroid} ('{AndroidQualityLevelName}')");
            report.Add($"verify quality levels on CS_URP: {levels.arraySize} serialized, " +
                       $"guid {guid}, Android default={android} ('{AndroidQualityLevelName}')");
            return problems;
        }

        /// <summary>
        /// The QualitySettings singleton as a SerializedObject. Per-level fields have no public
        /// setter in this Unity version, and the instance properties do not persist in batchmode.
        /// </summary>
        private static SerializedObject QualitySettingsObject()
        {
            Object singleton = Unsupported.GetSerializedAssetInterfaceSingleton("QualitySettings");
            if (singleton == null)
            {
                throw new System.InvalidOperationException(
                    "QualitySettings singleton is not available: the quality levels cannot be written");
            }
            return new SerializedObject(singleton);
        }

        /// <summary>
        /// The index of the Android default quality level, chosen by NAME. It used to be whatever
        /// level the batchmode editor happened to have active, which silently moved this project
        /// from Medium to Ultra on the first run - the drift the MSAA comment above exists to stop.
        /// </summary>
        public static int AndroidQualityLevelIndex()
        {
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == AndroidQualityLevelName) return i;
            }
            return 0;
        }

        /// <summary>
        /// Levels whose serialized customRenderPipeline does not carry this guid, read off the
        /// YAML rather than out of memory. Empty when every level is on the asset.
        /// </summary>
        private static IEnumerable<string> QualityLevelsWithoutRpGuid(string guid)
        {
            string text = File.ReadAllText("ProjectSettings/QualitySettings.asset");
            Match block = new Regex(@"^  m_QualitySettings:\n(.*?)(?=^  [A-Za-z_])",
                                    RegexOptions.Multiline | RegexOptions.Singleline).Match(text);
            if (!block.Success) { yield return "?: no m_QualitySettings block"; yield break; }
            MatchCollection levels = new Regex(
                @"^\s*- serializedVersion: \d+\n(.*?)(?=^\s*- serializedVersion: |\Z)",
                RegexOptions.Multiline | RegexOptions.Singleline).Matches(block.Groups[1].Value);
            for (int i = 0; i < levels.Count; i++)
            {
                Match rp = new Regex(@"customRenderPipeline:\s*\{fileID:\s*(\d+)(?:,\s*guid:\s*([0-9a-f]+))?")
                    .Match(levels[i].Groups[1].Value);
                string got = rp.Success && rp.Groups[1].Value != "0" ? rp.Groups[2].Value : "none";
                if (got != guid) yield return $"{i} customRenderPipeline guid {got}";
            }
        }

        /// <summary>The default renderer data of a URP asset, read through serialization (no public getter in URP 17).</summary>
        public static ScriptableRendererData RendererOf(UniversalRenderPipelineAsset asset)
        {
            if (asset == null) return null;
            var so = new SerializedObject(asset);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            int index = so.FindProperty("m_DefaultRendererIndex").intValue;
            if (list == null || index < 0 || index >= list.arraySize) return null;
            return list.GetArrayElementAtIndex(index).objectReferenceValue as ScriptableRendererData;
        }

        private static void SetInts(Object o, (string prop, int value)[] expect)
        {
            var so = new SerializedObject(o);
            foreach ((string prop, int value) in expect)
            {
                SerializedProperty p = so.FindProperty(prop);
                if (p == null) { Debug.LogWarning($"[XrSetup] urp {o.name}: no serialized '{prop}'"); continue; }
                if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
                else p.intValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloats(Object o, (string prop, float value)[] expect)
        {
            var so = new SerializedObject(o);
            foreach ((string prop, float value) in expect)
            {
                SerializedProperty p = so.FindProperty(prop);
                if (p == null) { Debug.LogWarning($"[XrSetup] urp {o.name}: no serialized '{prop}'"); continue; }
                p.floatValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CheckInts(Object o, (string prop, int value)[] expect, string label,
            List<string> problems, List<string> report)
        {
            var so = new SerializedObject(o);
            var parts = new List<string>();
            foreach ((string prop, int value) in expect)
            {
                SerializedProperty p = so.FindProperty(prop);
                if (p == null) { problems.Add($"{label}.{prop} not serialised"); parts.Add($"{prop}=absent"); continue; }
                int got = p.propertyType == SerializedPropertyType.Boolean ? (p.boolValue ? 1 : 0) : p.intValue;
                parts.Add($"{prop}={got}");
                if (got != value) problems.Add($"{label}.{prop}={got} expected {value}");
            }
            report.Add($"verify {label}: {string.Join(" ", parts)}");
        }

        private static void CheckFloats(Object o, (string prop, float value)[] expect, string label,
            List<string> problems, List<string> report)
        {
            var so = new SerializedObject(o);
            var parts = new List<string>();
            foreach ((string prop, float value) in expect)
            {
                SerializedProperty p = so.FindProperty(prop);
                if (p == null) { problems.Add($"{label}.{prop} not serialised"); parts.Add($"{prop}=absent"); continue; }
                parts.Add($"{prop}={p.floatValue}");
                if (Mathf.Abs(p.floatValue - value) > 1e-4f) problems.Add($"{label}.{prop}={p.floatValue} expected {value}");
            }
            report.Add($"verify {label} floats: {string.Join(" ", parts)}");
        }
    }
}
