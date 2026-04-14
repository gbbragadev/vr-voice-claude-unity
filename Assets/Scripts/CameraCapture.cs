using UnityEngine;

namespace VoiceClaude
{
    // Grabs a JPEG frame from the Quest 3 passthrough camera via WebCamTexture.
    // Requires horizonos.permission.HEADSET_CAMERA in AndroidManifest.xml.
    // Meta enabled WebCamTexture passthrough access in Quest OS v71+ for sideloaded apps.
    public class CameraCapture : MonoBehaviour
    {
        private WebCamTexture _tex;
        private Texture2D _readback;

        public bool IsReady => _tex != null && _tex.isPlaying && _tex.width > 16;

        public void StartCamera()
        {
            if (_tex != null) return;
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("CameraCapture: nenhum device disponível");
                return;
            }
            _tex = new WebCamTexture(devices[0].name, 1280, 960, 30);
            _tex.Play();
        }

        public void StopCamera()
        {
            if (_tex == null) return;
            _tex.Stop();
            _tex = null;
            if (_readback != null) { Destroy(_readback); _readback = null; }
        }

        public byte[] GrabJpeg(int maxDim = 640, int quality = 70)
        {
            if (!IsReady)
            {
                Debug.LogWarning("CameraCapture: não pronto, retornando null");
                return null;
            }

            int srcW = _tex.width;
            int srcH = _tex.height;
            float scale = Mathf.Min(1f, (float)maxDim / Mathf.Max(srcW, srcH));
            int dstW = Mathf.RoundToInt(srcW * scale);
            int dstH = Mathf.RoundToInt(srcH * scale);

            if (_readback == null || _readback.width != dstW || _readback.height != dstH)
            {
                if (_readback != null) Destroy(_readback);
                _readback = new Texture2D(dstW, dstH, TextureFormat.RGB24, false);
            }

            var rt = RenderTexture.GetTemporary(dstW, dstH, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(_tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            _readback.ReadPixels(new Rect(0, 0, dstW, dstH), 0, 0);
            _readback.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            return _readback.EncodeToJPG(quality);
        }

        private void OnDestroy() => StopCamera();
    }
}
