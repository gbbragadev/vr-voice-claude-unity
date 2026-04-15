using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoiceClaude.Editor
{
    // One-shot project configurator invoked via
    //   Unity.exe -batchmode -quit -executeMethod VoiceClaude.Editor.ProjectBootstrap.Run
    // Sets PlayerSettings for Android IL2CPP ARM64, writes Packages/manifest.json
    // with Meta XR Core SDK 85.0.0 (from Meta's NPM scoped registry) + the full
    // set of built-in Unity modules required by Meta XR Core (animation, xr, vr,
    // particlesystem, terrain, physics2d), and flips graphics API to Vulkan.
    // Phase 3 target: immersive MR app on Quest 3 with native passthrough.
    //
    // Note: com.meta.xr.sdk.all umbrella was discontinued in Meta XR releases
    // after v76. Phase 3 uses com.meta.xr.sdk.core directly (only package needed
    // for OVRCameraRig + OVRPassthroughLayer + OVRManager). Other Meta packages
    // (audio, voice, haptics, interaction) can be added later per-feature.
    public static class ProjectBootstrap
    {
        private const string PackageName = "com.gbbraga.vrvoiceclaude";
        private const string ProductName = "vr-voice-claude";
        private const string CompanyName = "gbbraga";

        public static void Run()
        {
            Debug.Log("[ProjectBootstrap] start");
            WriteManifest();
            ConfigurePlayer();
            ConfigureGraphics();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectBootstrap] done");
            EditorApplication.Exit(0);
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.applicationIdentifier = PackageName;

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            Debug.Log("[ProjectBootstrap] player settings configured");
        }

        private static void ConfigureGraphics()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            Debug.Log("[ProjectBootstrap] graphics API set to Vulkan");
        }

        private static void WriteManifest()
        {
            var manifestPath = Path.Combine(Directory.GetCurrentDirectory(), "Packages", "manifest.json");
            var manifest = @"{
  ""scopedRegistries"": [
    {
      ""name"": ""Meta XR"",
      ""url"": ""https://npm.developer.oculus.com"",
      ""scopes"": [
        ""com.meta.xr""
      ]
    }
  ],
  ""dependencies"": {
    ""com.meta.xr.sdk.core"": ""85.0.0"",
    ""com.unity.ugui"": ""2.0.0"",
    ""com.unity.textmeshpro"": ""3.2.0-pre.10"",
    ""com.unity.modules.androidjni"": ""1.0.0"",
    ""com.unity.modules.animation"": ""1.0.0"",
    ""com.unity.modules.audio"": ""1.0.0"",
    ""com.unity.modules.imageconversion"": ""1.0.0"",
    ""com.unity.modules.imgui"": ""1.0.0"",
    ""com.unity.modules.jsonserialize"": ""1.0.0"",
    ""com.unity.modules.particlesystem"": ""1.0.0"",
    ""com.unity.modules.physics"": ""1.0.0"",
    ""com.unity.modules.physics2d"": ""1.0.0"",
    ""com.unity.modules.screencapture"": ""1.0.0"",
    ""com.unity.modules.terrain"": ""1.0.0"",
    ""com.unity.modules.ui"": ""1.0.0"",
    ""com.unity.modules.uielements"": ""1.0.0"",
    ""com.unity.modules.unitywebrequest"": ""1.0.0"",
    ""com.unity.modules.unitywebrequestaudio"": ""1.0.0"",
    ""com.unity.modules.unitywebrequestwww"": ""1.0.0"",
    ""com.unity.modules.video"": ""1.0.0"",
    ""com.unity.modules.vr"": ""1.0.0"",
    ""com.unity.modules.xr"": ""1.0.0""
  }
}";
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            File.WriteAllText(manifestPath, manifest);
            Debug.Log($"[ProjectBootstrap] wrote manifest to {manifestPath}");
            UnityEditor.PackageManager.Client.Resolve();
        }
    }
}
