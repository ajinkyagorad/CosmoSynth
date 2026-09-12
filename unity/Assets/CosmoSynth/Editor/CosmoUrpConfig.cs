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

            // Lighting is still Unity's: one point light at the host star and a dim directional
            // fill. CONTRACT §8.1 sets both of these to 0 because CosmoSynth/Body is meant to
            // carry _LightDirWS itself — but that shader is the shader lane's, and it has not
            // landed. Turning the lights off before their replacement exists would ship an alpha
            // of black spheres. FLIP BOTH TO 0 in the same commit that lands CosmoSynth/Body.
            ("m_MainLightRenderingMode", 1),          // PerPixel: the ambient directional fill
            ("m_AdditionalLightsRenderingMode", 1),   // PerPixel: the star's point light
            ("m_AdditionalLightsPerObjectLimit", 1),  // exactly the star

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

            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;

                // Everything URP now owns, switched off at the quality level so the two cannot
                // disagree. MSAA in particular: a quality level asking for 2x while the URP asset
                // asks for 4x is a value nobody chose.
                QualitySettings.pixelLightCount = 0;
                QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                QualitySettings.vSyncCount = 0;
                QualitySettings.antiAliasing = 0;
            }
            QualitySettings.SetQualityLevel(current, false);

            GraphicsSettings.defaultRenderPipeline = asset;
            SetAndroidDefaultQuality(current);
            AssetDatabase.SaveAssets();

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

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) != asset)
                    problems.Add($"quality level {i} '{QualitySettings.names[i]}' not on CS_URP");
            }

            int android = ReadAndroidDefaultQuality();
            if (android < 0 || android >= QualitySettings.names.Length) problems.Add("Android default quality unset");
            report.Add($"verify quality levels on CS_URP: {QualitySettings.names.Length}, Android default={android}");
            return problems;
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
