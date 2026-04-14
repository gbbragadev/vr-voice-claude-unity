using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VoiceClaude.Editor
{
    // Batch build entry point invoked by unity-ssh.sh via
    //   Unity.exe -batchmode -quit -executeMethod VoiceClaude.Editor.Builder.BuildApk
    // Builds an Android APK to Builds/vr-voice-claude.apk and exits with 0 on
    // success, 1 on failure (so the SSH caller can detect the outcome).
    public static class Builder
    {
        private const string OutputDir = "Builds";
        private const string OutputName = "vr-voice-claude.apk";
        private const string PackageName = "com.gbbraga.vrvoiceclaude";

        [MenuItem("VoiceClaude/Build APK")]
        public static void BuildApk()
        {
            try
            {
                if (!Directory.Exists(OutputDir))
                    Directory.CreateDirectory(OutputDir);

                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

                PlayerSettings.applicationIdentifier = PackageName;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.bundleVersionCode++;

                var scenes = new[] { "Assets/Scenes/Main.unity" };
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(OutputDir, OutputName),
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                Debug.Log($"[Builder] Result: {summary.result}, size: {summary.totalSize} bytes, duration: {summary.totalTime}");

                if (summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"[Builder] OK — {Path.Combine(OutputDir, OutputName)}");
                    EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError($"[Builder] FAILED — {summary.totalErrors} errors");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Builder] Exception: {e}");
                EditorApplication.Exit(1);
            }
        }
    }
}
