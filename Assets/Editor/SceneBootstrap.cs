using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoiceClaude.Editor
{
    // Programmatically assembles Assets/Scenes/Main.unity and registers it in
    // EditorBuildSettings. Invoked via
    //   Unity.exe -batchmode -quit -executeMethod VoiceClaude.Editor.SceneBootstrap.Run
    // Must run AFTER ProjectBootstrap (which resolves packages) so TMPro and
    // UGUI types are available.
    public static class SceneBootstrap
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        public static void Run()
        {
            Debug.Log("[SceneBootstrap] start");
            Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildEventSystem();
            // Sprint 1 REVISED: voice pipeline reactivated. Camera frames now come
            // from the MediaProjection Java plugin (Assets/Plugins/Android/), not
            // WebCamTexture. CameraCapture.StartCamera() triggers the OS consent
            // dialog on first run; user accepts once per session.
            var (canvasGO, label, border) = BuildCanvas();
            var voiceManager = BuildVoiceManager();
            WireTranscriptPanel(canvasGO, voiceManager, label, border);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[SceneBootstrap] scene saved to {ScenePath}");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBootstrap] done");
            EditorApplication.Exit(0);
        }

        private static void BuildCamera()
        {
            // OVRCameraRig from Meta XR SDK — replaces simple Camera.main for MR.
            // Use reflection to avoid hard compile dep on Meta SDK in case package
            // hasn't resolved yet (early bootstrap runs may not have the assembly).
            var ovrRigType = System.Type.GetType("OVRCameraRig, Oculus.VR");
            if (ovrRigType == null)
            {
                Debug.LogError("[SceneBootstrap] OVRCameraRig type not found. Run ProjectBootstrap first AND complete Meta XR SDK GUI import (see META_XR_SDK_FIRST_RUN.md).");
                EditorApplication.Exit(1);
                return;
            }

            var rigGO = new GameObject("OVRCameraRig");
            rigGO.AddComponent(ovrRigType);

            // OVRManager configures tracking origin + permission flow
            var ovrManagerType = System.Type.GetType("OVRManager, Oculus.VR");
            if (ovrManagerType != null)
            {
                var manager = rigGO.AddComponent(ovrManagerType);
                // trackingOriginType = TrackingOrigin.FloorLevel via reflection
                var prop = ovrManagerType.GetProperty("trackingOriginType");
                if (prop != null)
                {
                    // FloorLevel = 1 in OVRManager.TrackingOrigin enum
                    prop.SetValue(manager, System.Enum.ToObject(prop.PropertyType, 1));
                }
            }

            // OVRPassthroughLayer in Underlay mode — renders camera feed behind app content
            var passthroughType = System.Type.GetType("OVRPassthroughLayer, Oculus.VR");
            if (passthroughType != null)
            {
                var ptLayer = rigGO.AddComponent(passthroughType);
                // overlayType = Underlay (enum value 1)
                var overlayProp = passthroughType.GetProperty("overlayType");
                if (overlayProp != null)
                {
                    overlayProp.SetValue(ptLayer, System.Enum.ToObject(overlayProp.PropertyType, 1));
                }
            }
            else
            {
                Debug.LogWarning("[SceneBootstrap] OVRPassthroughLayer type not found — passthrough won't render");
            }

            // Find the CenterEye camera created by OVRCameraRig and configure
            // clear flags for transparent MR rendering
            var centerEyeGO = GameObject.Find("CenterEyeAnchor");
            if (centerEyeGO != null)
            {
                var cam = centerEyeGO.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // alpha 0 = passthrough shows through
                    cam.nearClipPlane = 0.1f;
                    cam.farClipPlane = 50f;
                }
            }
            else
            {
                Debug.LogWarning("[SceneBootstrap] CenterEyeAnchor not found — OVRCameraRig may not have spawned children yet");
            }
        }

        private static void BuildEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static (GameObject canvas, Text label, Image border) BuildCanvas()
        {
            var canvasGO = new GameObject("TranscriptCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var crt = canvasGO.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(1600, 900);
            crt.position = new Vector3(0, 1.5f, 2f);
            crt.localScale = Vector3.one * 0.001f;

            // Border (background panel with tinted color used by TranscriptPanel)
            var borderGO = new GameObject("Border");
            borderGO.transform.SetParent(canvasGO.transform, false);
            var borderRect = borderGO.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var borderImage = borderGO.AddComponent<Image>();
            borderImage.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);

            // Legacy uGUI Text child (uses built-in Arial — no TMP setup needed)
            var txtGO = new GameObject("TranscriptText");
            txtGO.transform.SetParent(canvasGO.transform, false);
            var label = txtGO.AddComponent<Text>();
            label.text = "pronto — fale algo";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 48;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var trt = label.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.05f, 0.05f);
            trt.anchorMax = new Vector2(0.95f, 0.95f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return (canvasGO, label, borderImage);
        }

        private static GameObject BuildVoiceManager()
        {
            var go = new GameObject("VoiceManager");
            var mic = go.AddComponent<VoiceClaude.MicCapture>();
            var cam = go.AddComponent<VoiceClaude.CameraCapture>();
            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            var playback = go.AddComponent<VoiceClaude.AudioPlayback>();
            var session = go.AddComponent<VoiceClaude.VoiceSession>();
            session.mic = mic;
            session.cameraCapture = cam;
            session.playback = playback;
            return go;
        }

        private static void WireTranscriptPanel(GameObject canvasGO, GameObject voiceManager,
            Text label, Image border)
        {
            var panel = canvasGO.AddComponent<VoiceClaude.TranscriptPanel>();
            panel.session = voiceManager.GetComponent<VoiceClaude.VoiceSession>();
            panel.text = label;
            panel.border = border;
        }
    }
}
