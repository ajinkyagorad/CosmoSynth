using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmoSynth.Core;
using CosmoSynth.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.OpenXR;

namespace CosmoSynth.EditorTools
{
    /// <summary>
    /// Headless Android build, and the API-side check that the build is configured the way this
    /// project decided.
    ///
    /// Unlike the other projects on this machine, whose build scripts deliberately leave
    /// PlayerSettings alone so server output matches local output, this one <b>does</b> set them —
    /// because there is nothing to match. The project was authored entirely from a terminal and
    /// has never been opened in the editor, so ProjectSettings holds Unity's freshly generated
    /// defaults. Configuring here means the settings live in reviewable C# rather than in
    /// generated YAML.
    ///
    /// <see cref="Diagnostics"/> is the half that matters most. G0c reads the serialised YAML and
    /// Diagnostics reads the live editor API: two independent sources for the same facts, which
    /// agree only when both are right. A setting written by a method that then reports success
    /// without reading it back is exactly how 1.7.0 shipped.
    /// </summary>
    public static class ServerBuild
    {
        private const string ScenePath = "Assets/CosmoSynth/Scenes/SolarSystemLab.unity";
        private const string ProductName = "CosmoSynth";
        private const string PackageName = "com.triton.cosmosynth";

        /// <summary>Bumped by hand; build.sh reads these two lines out of this file and gates on them.</summary>
        private const string Version = "2.0.0-alpha.1";
        private const int VersionCode = 17;

        /// <summary>
        /// Where the APK is written. Never <c>~/builds</c>: only build.sh's publish step may put
        /// anything on the build page, because the page's timeline is one row per APK and an
        /// unreviewed overwrite destroys the previous build and its notes together.
        /// </summary>
        private static string OutputDirectory =>
            Environment.GetEnvironmentVariable("COSMOSYNTH_OUTDIR") ?? "/home/triton/.cache/cosmosynth/staging";

        [MenuItem("CosmoSynth/Build Android APK", priority = 10)]
        public static void BuildAndroid()
        {
            try
            {
                Log("starting Android build");

                PinAndroidTarget();
                ConfigurePlayerSettings();
                ApplySigning();
                RequireScene();

                string apkPath = Path.Combine(OutputDirectory, $"CosmoSynth-{Version}.apk");
                Directory.CreateDirectory(OutputDirectory);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = apkPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                Log($"result={summary.result} size={summary.totalSize / (1024 * 1024)} MB " +
                    $"time={summary.totalTime.TotalSeconds:F0}s errors={summary.totalErrors}");

                if (summary.result != BuildResult.Succeeded)
                {
                    // Surface the actual messages: a build report whose steps are not printed
                    // leaves nothing in the log but "Build failed", which is useless from a
                    // shell script.
                    foreach (BuildStep step in report.steps)
                    {
                        foreach (BuildStepMessage message in step.messages)
                        {
                            if (message.type == LogType.Error || message.type == LogType.Exception)
                                Log($"  {step.name}: {message.content}");
                        }
                    }
                    Log("BUILD FAILED");
                    EditorApplication.Exit(1);
                    return;
                }

                Log($"BUILD_OK {apkPath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Log($"BUILD FAILED with an exception: {exception}");
                EditorApplication.Exit(2);
            }
        }

        /// <summary>
        /// Pin the editor to Android before anything reads a per-target setting.
        ///
        /// Almost every API below — OpenXRSettings, PlayerSettings.Android, the graphics API list
        /// — is per-build-target, and a batchmode editor that has not switched target answers for
        /// whatever platform was last active. That produces a diagnostics dump describing a build
        /// nobody is making, which is worse than no dump at all.
        /// </summary>
        private static void PinAndroidTarget()
        {
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Log($"active build target is {EditorUserBuildSettings.activeBuildTarget}; switching to Android");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }
        }

        /// <summary>
        /// The player identity and backend. Public because <c>XrSetup.Configure</c> calls it:
        /// it is a settings write, it belongs to the settings step (G1a), and G1b asserts it
        /// afterwards from a separate editor launch. The values stay here, next to
        /// <see cref="Version"/>, which is also where tools/build.sh greps for them.
        /// </summary>
        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Triton";
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;

            var android = NamedBuildTarget.Android;

            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            PlayerSettings.Android.bundleVersionCode = VersionCode;

            // Quest 3 runs Android 12L; API 32 is the floor Meta requires and a safe minimum
            // for a plain Android device too.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // ARM64 only. Quest rejects a 32-bit-only APK outright, and shipping both doubles
            // the build time and the download for an ABI nothing here targets.
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // In Unity 6 the enum member is plain NET_Standard; NET_Standard_2_1 was removed.
            PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);

            // Landscape: Quest's 2D fallback panel and any flat build both want it.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // Immersive XR wants the app to own the whole display with no system chrome.
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;

            // Vulkan only. The graphics API list is also what makes the difference between a
            // working passthrough build and a black one on Quest.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

            // Debug symbols off and stripping on: the APK is downloaded to a headset over
            // wifi, so size is a feature.
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);

            Log($"player settings: {PackageName} v{Version} ({VersionCode}), IL2CPP/ARM64, minSdk 32");
        }

        /// <summary>
        /// Sign from the environment, falling back to the debug keystore.
        ///
        /// The debug key is what 1.9.0 was signed with, so 2.0 installs over it without an
        /// uninstall — which is what makes an A/B against the previous build possible at all.
        /// A release keystore must exist <b>before the first Meta upload on any channel</b>,
        /// because the first upload locks the app to that certificate forever; there is no
        /// recovery and no support path (memory: meta-cert-locked-by-first-upload).
        /// </summary>
        private static void ApplySigning()
        {
            string keystore = Environment.GetEnvironmentVariable("COSMOSYNTH_KEYSTORE");
            string storePass = Environment.GetEnvironmentVariable("COSMOSYNTH_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("COSMOSYNTH_KEY_ALIAS");
            string aliasPass = Environment.GetEnvironmentVariable("COSMOSYNTH_KEY_PASS");

            if (string.IsNullOrEmpty(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Log("signing: debug keystore (~/.android/debug.keystore) — set COSMOSYNTH_KEYSTORE to sign for release");
                return;
            }

            if (!File.Exists(keystore))
            {
                throw new FileNotFoundException($"COSMOSYNTH_KEYSTORE points at {keystore}, which does not exist");
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;

            // Never the password. The build log is published beside the APK.
            Log($"signing: custom keystore {Path.GetFileName(keystore)}, alias '{alias}'");
        }

        /// <summary>
        /// The scene is committed. If it is missing, that is a checkout problem and the build must
        /// say so rather than generating a different scene from the one that was reviewed — a
        /// build of a scene nobody wrote is the definition of an unverifiable artefact.
        /// </summary>
        private static void RequireScene()
        {
            if (File.Exists(ScenePath))
            {
                Log($"scene present: {ScenePath}");
                return;
            }
            throw new FileNotFoundException(
                $"no scene at {ScenePath}. Generate it once with CosmoSynth.EditorTools.SceneBuilder, " +
                "commit it, and build from the committed copy.");
        }

        // ---- Diagnostics -----------------------------------------------------

        private static readonly List<string> Failures = new List<string>();

        private static void Require(bool condition, string what)
        {
            if (!condition) Failures.Add(what);
        }

        /// <summary>
        /// Print the whole render configuration, from the live API, and <b>fail</b> on any
        /// deviation from binding decisions 1 and 2.
        ///
        /// This asserts; it does not merely dump. A dump that nobody diffs is a dump that agrees
        /// with whatever is there.
        /// </summary>
        public static void Diagnostics()
        {
            try
            {
                Failures.Clear();
                PinAndroidTarget();

                var android = NamedBuildTarget.Android;
                Log($"unity {Application.unityVersion}");
                Log($"product {PlayerSettings.productName} / {PlayerSettings.GetApplicationIdentifier(android)} " +
                    $"v{PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode})");
                Log($"sdk min={PlayerSettings.Android.minSdkVersion} target={PlayerSettings.Android.targetSdkVersion} " +
                    $"arch={PlayerSettings.Android.targetArchitectures} " +
                    $"backend={PlayerSettings.GetScriptingBackend(android)} " +
                    $"apis={string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))} " +
                    $"colourSpace={PlayerSettings.colorSpace}");
                Log($"swappy(optimizedFramePacing)={PlayerSettings.Android.optimizedFramePacing} " +
                    $"customKeystore={PlayerSettings.Android.useCustomKeystore}");

                Require(PlayerSettings.bundleVersion == Version, $"bundleVersion {PlayerSettings.bundleVersion} != {Version}");
                Require(PlayerSettings.Android.bundleVersionCode == VersionCode,
                    $"versionCode {PlayerSettings.Android.bundleVersionCode} != {VersionCode}");
                Require(PlayerSettings.colorSpace == ColorSpace.Linear,
                    $"colour space is {PlayerSettings.colorSpace}, not Linear");
                Require(PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64,
                    $"architectures {PlayerSettings.Android.targetArchitectures} != ARM64");
                Require(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).SequenceEqual(
                        new[] { GraphicsDeviceType.Vulkan }),
                    "graphics APIs are not exactly [Vulkan]");

                // ---- the render pipeline
                RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;
                Log($"quality level {QualitySettings.GetQualityLevel()} '{QualitySettings.names[QualitySettings.GetQualityLevel()]}', " +
                    $"pipeline {(pipeline != null ? pipeline.name : "Built-in")}");
                Require(pipeline != null, "GraphicsSettings.defaultRenderPipeline is unset (Built-in)");

                if (pipeline is UniversalRenderPipelineAsset urp)
                {
                    Log($"urp msaa={urp.msaaSampleCount} hdr={urp.supportsHDR} depthTex={urp.supportsCameraDepthTexture} " +
                        $"opaqueTex={urp.supportsCameraOpaqueTexture} srpBatcher={urp.useSRPBatcher} " +
                        $"renderScale={urp.renderScale} addLights={urp.additionalLightsRenderingMode}");
                    Require(urp.msaaSampleCount == 4, $"URP MSAA is {urp.msaaSampleCount}, not 4");
                    Require(!urp.supportsHDR, "URP HDR is on");
                    Require(!urp.supportsCameraDepthTexture, "URP depth texture is on");
                    Require(!urp.supportsCameraOpaqueTexture, "URP opaque texture is on");
                    Require(urp.useSRPBatcher, "URP SRP batcher is off");
                    Require(Mathf.Approximately(urp.renderScale, 1f), $"URP render scale is {urp.renderScale}");
                }
                else
                {
                    Failures.Add("the render pipeline asset is not a UniversalRenderPipelineAsset");
                }

                // ---- OpenXR, read through the API rather than from the YAML
                OpenXRSettings xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                if (xr == null)
                {
                    Failures.Add("no OpenXRSettings for Android");
                }
                else
                {
                    Log($"openxr renderMode={xr.renderMode} depthSubmission={xr.depthSubmissionMode} " +
                        $"latency={xr.latencyOptimization} symmetricProjection={xr.symmetricProjection} " +
                        $"optimizeBufferDiscards={xr.optimizeBufferDiscards} foveation={xr.foveatedRenderingApi}");

                    Require(xr.renderMode == OpenXRSettings.RenderMode.SinglePassInstanced,
                        $"render mode is {xr.renderMode}, not SinglePassInstanced");
                    Require(xr.depthSubmissionMode == OpenXRSettings.DepthSubmissionMode.None,
                        $"depth submission is {xr.depthSubmissionMode}, not None");
                    Require(xr.latencyOptimization == OpenXRSettings.LatencyOptimization.PrioritizeInputPolling,
                        $"latency optimization is {xr.latencyOptimization}");
                    Require(xr.symmetricProjection, "symmetric projection is off");
                    Require(xr.optimizeBufferDiscards, "optimize buffer discards is off");
                    Require(xr.foveatedRenderingApi == OpenXRSettings.BackendFovationApi.SRPFoveation,
                        $"foveation API is {xr.foveatedRenderingApi}, not SRPFoveation");

                    var enabled = new List<string>();
                    foreach (var feature in xr.GetFeatures())
                    {
                        if (feature != null && feature.enabled) enabled.Add(feature.GetType().Name);
                    }
                    enabled.Sort();
                    Log($"openxr enabled features ({enabled.Count}): {string.Join(", ", enabled)}");
                }

                int metaQuest = AssetDatabase.FindAssets("t:MetaQuestFeature").Length;
                Log($"MetaQuestFeature assets: {metaQuest}");
                Require(metaQuest == 1, $"{metaQuest} MetaQuestFeature assets; Meta's build hook needs exactly 1");

                // ---- what the player will actually contain
                string[] shaders = CosmoShaders.All;
                Log($"CosmoShaders.All ({shaders.Length}): {string.Join(", ", shaders)}");
                Log($"always-included shaders: {AlwaysIncludedShaderNames()}");
                Log($"scene: {ScenePath} present={File.Exists(ScenePath)}");
                Require(File.Exists(ScenePath), $"the committed scene {ScenePath} is missing");

                Log($"scaled space: d0={ScaledSpace.IdentityRadiusMetres} near={ScaledSpace.NearPlaneMetres} " +
                    $"far={ScaledSpace.FarPlaneMetres} sky={ScaledSpace.SkyRadiusMetres} " +
                    $"scale limits [{ScaleLimits.MinUnitsPerKm:G3}, {ScaleLimits.MaxUnitsPerKm:G3}]");

                foreach (string failure in Failures) Debug.LogError($"[ServerBuild] DIAGNOSTICS_FAIL {failure}");

                if (Failures.Count > 0)
                {
                    Log($"DIAGNOSTICS_FAILED with {Failures.Count} deviation(s)");
                    EditorApplication.Exit(1);
                    return;
                }

                Log("DIAGNOSTICS_OK");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ServerBuild] DIAGNOSTICS_FAIL threw: {exception}");
                EditorApplication.Exit(2);
            }
        }

        private static string AlwaysIncludedShaderNames()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return "(unreadable)";

            var so = new SerializedObject(assets[0]);
            SerializedProperty included = so.FindProperty("m_AlwaysIncludedShaders");
            if (included == null) return "(no property)";

            var names = new List<string>();
            for (int i = 0; i < included.arraySize; i++)
            {
                var shader = included.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                names.Add(shader != null ? shader.name : "(missing)");
            }
            return names.Count == 0 ? "(none)" : string.Join(", ", names);
        }

        /// <summary>Prefixed so the shell script can grep the build log for our lines only.</summary>
        private static void Log(string message) => Debug.Log($"[ServerBuild] {message}");
    }
}
