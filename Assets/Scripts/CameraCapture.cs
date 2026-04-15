using UnityEngine;

namespace VoiceClaude
{
    // Bridge to MediaProjectionCaptureActivity Java plugin. Captures the Quest 3
    // composited framebuffer (real world + virtual app overlays) so Claude can see
    // what the user sees. Replaces Phase 2's WebCamTexture approach which only
    // captured the headset's avatar render device.
    public class CameraCapture : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const string JavaClassName = "com.gbbraga.vrvoiceclaude.MediaProjectionCaptureActivity";
        private AndroidJavaClass _javaClass;

        private AndroidJavaClass GetJavaClass()
        {
            if (_javaClass == null)
            {
                _javaClass = new AndroidJavaClass(JavaClassName);
            }
            return _javaClass;
        }

        private void LogJavaLastError()
        {
            try
            {
                var cls = GetJavaClass();
                string lastError = cls.CallStatic<string>("getLastError");
                if (!string.IsNullOrEmpty(lastError))
                {
                    Debug.LogWarning($"CameraCapture: Java reports {lastError}");
                }
            }
            catch (System.Exception)
            {
                // Swallow — we're already in an error path; don't spam.
            }
        }
#else
        private static bool _editorStubWarned;
        private static void WarnEditorStub(string member)
        {
            if (_editorStubWarned) return;
            _editorStubWarned = true;
            Debug.Log($"CameraCapture: Editor stub active ({member}). MediaProjection runs Android-only — deploy to Quest 3 to capture frames.");
        }
#endif

        public bool IsReady
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    return GetJavaClass().CallStatic<bool>("isReady");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"CameraCapture JNI error: IsReady: {e.Message}");
                    LogJavaLastError();
                    return false;
                }
#else
                WarnEditorStub("IsReady");
                return false;
#endif
            }
        }

        // Triggers the MediaProjection consent dialog on first call.
        // IsReady remains false until the user accepts; poll before GrabJpeg.
        public void StartCamera()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                GetJavaClass().CallStatic("requestProjectionPermission");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"CameraCapture JNI error: StartCamera: {e.Message}");
                LogJavaLastError();
            }
#else
            WarnEditorStub("StartCamera");
#endif
        }

        public void StopCamera()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                GetJavaClass().CallStatic("stopCapture");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"CameraCapture JNI error: StopCamera: {e.Message}");
                LogJavaLastError();
            }
#else
            WarnEditorStub("StopCamera");
#endif
        }

        public byte[] GrabJpeg(int maxDim = 640, int quality = 70)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return GetJavaClass().CallStatic<byte[]>("getLatestFrameJpeg", maxDim, quality);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"CameraCapture JNI error: GrabJpeg: {e.Message}");
                LogJavaLastError();
                return null;
            }
#else
            WarnEditorStub("GrabJpeg");
            return null;
#endif
        }

        private void OnDestroy()
        {
            StopCamera();
        }
    }
}
