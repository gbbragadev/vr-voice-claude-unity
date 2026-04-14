using System.IO;
using TMPro;
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
            var (canvasGO, tmp, border) = BuildCanvas();
            var voiceManager = BuildVoiceManager();
            WireTranscriptPanel(canvasGO, voiceManager, tmp, border);

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
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camGO.AddComponent<AudioListener>();
            camGO.transform.position = new Vector3(0, 1.6f, 0);
        }

        private static void BuildEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static (GameObject canvas, TextMeshProUGUI text, Image border) BuildCanvas()
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

            // TMP text child
            var txtGO = new GameObject("TranscriptText");
            txtGO.transform.SetParent(canvasGO.transform, false);
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "pronto — fale algo";
            tmp.fontSize = 48;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            var trt = tmp.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.05f, 0.05f);
            trt.anchorMax = new Vector2(0.95f, 0.95f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return (canvasGO, tmp, borderImage);
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
            TextMeshProUGUI tmp, Image border)
        {
            var panel = canvasGO.AddComponent<VoiceClaude.TranscriptPanel>();
            panel.session = voiceManager.GetComponent<VoiceClaude.VoiceSession>();
            panel.text = tmp;
            panel.border = border;
        }
    }
}
