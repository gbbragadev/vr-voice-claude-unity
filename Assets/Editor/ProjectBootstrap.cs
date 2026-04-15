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
    // with Meta XR SDK All-in-One umbrella package + stock Unity modules
    // (including animation module required by Meta XR Core), and flips graphics
    // API to Vulkan.
    // Phase 3 target: immersive MR app on Quest 3 with native passthrough.
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
  ""dependencies"": {
    ""com.meta.xr.sdk.all"": ""76.0.0"",
    ""com.unity.ugui"": ""2.0.0"",
    ""com.unity.textmeshpro"": ""3.2.0-pre.10"",
    ""com.unity.modules.androidjni"": ""1.0.0"",
    ""com.unity.modules.animation"": ""1.0.0"",
    ""com.unity.modules.audio"": ""1.0.0"",
    ""com.unity.modules.imageconversion"": ""1.0.0"",
    ""com.unity.modules.imgui"": ""1.0.0"",
    ""com.unity.modules.jsonserialize"": ""1.0.0"",
    ""com.unity.modules.physics"": ""1.0.0"",
    ""com.unity.modules.screencapture"": ""1.0.0"",
    ""com.unity.modules.ui"": ""1.0.0"",
    ""com.unity.modules.uielements"": ""1.0.0"",
    ""com.unity.modules.unitywebrequest"": ""1.0.0"",
    ""com.unity.modules.unitywebrequestaudio"": ""1.0.0"",
    ""com.unity.modules.unitywebrequestwww"": ""1.0.0"",
    ""com.unity.modules.video"": ""1.0.0""
  }
}";
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            File.WriteAllText(manifestPath, manifest);
            Debug.Log($"[ProjectBootstrap] wrote manifest to {manifestPath}");
            UnityEditor.PackageManager.Client.Resolve();
        }
    }
}
