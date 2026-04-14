using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace VoiceClaude
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioPlayback : MonoBehaviour
    {
        private AudioSource _src;

        public event Action OnPlaybackEnded;

        private void Awake()
        {
            _src = GetComponent<AudioSource>();
        }

        public IEnumerator PlayMp3(byte[] mp3)
        {
            string tmp = Path.Combine(Application.persistentDataPath, "tts_out.mp3");
            File.WriteAllBytes(tmp, mp3);
            using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + tmp, AudioType.MPEG);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Audio load failed: {req.error}");
                OnPlaybackEnded?.Invoke();
                yield break;
            }
            _src.clip = DownloadHandlerAudioClip.GetContent(req);
            _src.Play();
            yield return new WaitWhile(() => _src.isPlaying);
            OnPlaybackEnded?.Invoke();
        }
    }
}
