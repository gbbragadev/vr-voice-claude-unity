using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoiceClaude
{
    // Wake-word-less MVP: app always listens, VAD detects when the user
    // starts speaking, records until 800ms of silence, then processes the
    // turn. Room ambient noise will not trigger recording because the RMS
    // threshold (0.02) is well above idle room noise.
    public enum VoiceState { Listening, Recording, Thinking, Speaking }

    public class VoiceSession : MonoBehaviour
    {
        public MicCapture mic;
        public CameraCapture cameraCapture;
        public AudioPlayback playback;

        public event Action<VoiceState> OnStateChanged;
        public event Action<string> OnUserSaid;
        public event Action<string> OnClaudeSaid;

        private readonly List<ChatMessage> _history = new();
        private VoiceState _state = VoiceState.Listening;

        public VoiceState State
        {
            get => _state;
            private set
            {
                if (_state == value) return;
                _state = value;
                OnStateChanged?.Invoke(value);
            }
        }

        private void Start()
        {
            // Quest panel apps lose window focus whenever the user looks away to
            // another panel, which would pause Unity and kill the AAudio stream
            // (mic stops producing samples). Keep running regardless of focus.
            Application.runInBackground = true;
            Debug.Log("[VoiceSession] Start — runInBackground=true");

            mic.OnSpeechStart += HandleSpeechStart;
            mic.OnSpeechEnd += HandleSpeechEnd;
            playback.OnPlaybackEnded += HandlePlaybackEnded;

            cameraCapture.StartCamera();
            StartListening();
        }

        private void StartListening()
        {
            mic.StartCapture();
            State = VoiceState.Listening;
        }

        private void HandleSpeechStart()
        {
            if (State == VoiceState.Listening)
                State = VoiceState.Recording;
        }

        private async void HandleSpeechEnd(float[] samples)
        {
            Debug.Log($"[VoiceSession] HandleSpeechEnd state={State} samples={samples.Length}");
            if (State != VoiceState.Recording) return;
            mic.StopCapture();
            State = VoiceState.Thinking;
            try
            {
                byte[] frameJpeg = cameraCapture.GrabJpeg(maxDim: 640, quality: 70);
                Debug.Log($"[VoiceSession] frame={(frameJpeg == null ? "null" : frameJpeg.Length.ToString())}");

                byte[] wav = MicCapture.EncodeWav(samples);
                Debug.Log($"[VoiceSession] wav={wav.Length}, calling STT");
                string userText = await ApiClient.SttAsync(wav);
                Debug.Log($"[VoiceSession] STT='{userText}'");
                OnUserSaid?.Invoke(userText);
                _history.Add(new ChatMessage { role = "user", content = userText });

                Debug.Log("[VoiceSession] calling Chat");
                string reply = await ApiClient.ChatAsync(_history, frameJpeg);
                Debug.Log($"[VoiceSession] Chat reply len={reply?.Length ?? 0}");
                OnClaudeSaid?.Invoke(reply);
                _history.Add(new ChatMessage { role = "assistant", content = reply });

                Debug.Log("[VoiceSession] calling TTS");
                byte[] mp3 = await ApiClient.TtsAsync(reply);
                Debug.Log($"[VoiceSession] TTS mp3={mp3?.Length ?? 0}, playing");
                State = VoiceState.Speaking;
                StartCoroutine(playback.PlayMp3(mp3));
            }
            catch (Exception e)
            {
                Debug.LogError($"[VoiceSession] {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                StartListening();
            }
        }

        private void HandlePlaybackEnded()
        {
            StartListening();
        }
    }
}
