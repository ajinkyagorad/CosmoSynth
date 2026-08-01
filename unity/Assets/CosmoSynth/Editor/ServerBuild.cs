using System;
using System.IO;
using System.Linq;
using CosmoSynth.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmoSynth.EditorTools
{
    /// <summary>
    /// Headless Android build, invoked by <c>~/server-setup/build-cosmosynth.sh</c>.
    ///
    /// Unlike the other projects on this machine, whose build scripts deliberately leave
    /// PlayerSettings alone so server output matches local output, this one <b>does</b> set
    /// them — because there is nothing to match yet. The project was authored entirely from a
    /// terminal and has never been opened in the editor, so ProjectSettings holds Unity's
    /// freshly generated defaults: package name <c>com.DefaultCompany.unity</c>, Mono scripting,
    /// no ARM64. Configuring here means the settings live in reviewable C# rather than in
    /// generated YAML, and the first thing a person opening the editor sees is a project
    /// already targeting the right platform.
    ///
    /// Once someone does configure this project by hand, this method should be reduced to
    /// asserting the settings rather than writing them.
    /// </summary>
    public static class ServerBuild
    {
        private const string ScenePath = "Assets/CosmoSynth/Scenes/SolarSystemLab.unity";
        private const string ProductName = "CosmoSynth";
        private const string PackageName = "com.triton.cosmosynth";
        private const string OutputDirectory = "/home/triton/builds/cosmosynth";

        /// <summary>Bumped by hand; also written into the APK filename and the notes sidecar.</summary>
        private const string Version = "1.9.0";
        private const int VersionCode = 16;

        [MenuItem("CosmoSynth/Build Android APK", priority = 10)]
        public static void BuildAndroid()
        {
            try
            {
                Log("starting Android build");

                ConfigurePlayerSettings();
                EnsureScene();

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
                    foreach (var step in report.steps)
                    {
                        foreach (var message in step.messages)
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

        private static void ConfigurePlayerSettings()
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

            // Vulkan first. The graphics API list is also what makes the difference between a
            // working passthrough build and a black one on Quest, so it is set explicitly
            // rather than left to whatever the default happened to be.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });

            // Debug symbols off and stripping on: the APK is downloaded to a headset over
            // wifi, so size is a feature.
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);

            Log($"player settings: {PackageName} v{Version} ({VersionCode}), IL2CPP/ARM64, minSdk 32");
        }

        /// <summary>
        /// Make sure a scene exists to build. The scene is generated rather than committed —
        /// see <see cref="SceneBuilder"/> for why — so a clean checkout has none until this
        /// runs.
        /// </summary>
        private static void EnsureScene()
        {
            if (File.Exists(ScenePath))
            {
                Log($"scene present: {ScenePath}");
                return;
            }

            Log($"scene missing, generating {ScenePath}");

            string directory = Path.GetDirectoryName(ScenePath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                AssetDatabase.CreateFolder("Assets/CosmoSynth", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("CosmoSynth");
            bootstrap.AddComponent<CosmoSynthBootstrap>();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Could not save the generated scene to {ScenePath}.");
            }

            AssetDatabase.Refresh();
            Log("scene generated");
        }

        /// <summary>Prefixed so the shell script can grep the build log for our lines only.</summary>
        private static void Log(string message) => Debug.Log($"[ServerBuild] {message}");
    }
}
