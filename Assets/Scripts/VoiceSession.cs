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
            mic.OnSpeechStart += HandleSpeechStart;
            mic.OnSpeechEnd += HandleSpeechEnd;
            playback.OnPlaybackEnded += HandlePlaybackEnded;

            // Camera starts at Start() and stays running — avoids the ~500ms
            // warm-up latency every time the user wakes the app.
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
            if (State != VoiceState.Recording) return;
            mic.StopCapture();
            State = VoiceState.Thinking;
            try
            {
                // Grab the frame BEFORE any await — user is still looking
                // at whatever they just asked about.
                byte[] frameJpeg = cameraCapture.GrabJpeg(maxDim: 640, quality: 70);

                byte[] wav = MicCapture.EncodeWav(samples);
                string userText = await ApiClient.SttAsync(wav);
                OnUserSaid?.Invoke(userText);
                _history.Add(new ChatMessage { role = "user", content = userText });

                string reply = await ApiClient.ChatAsync(_history, frameJpeg);
                OnClaudeSaid?.Invoke(reply);
                _history.Add(new ChatMessage { role = "assistant", content = reply });

                byte[] mp3 = await ApiClient.TtsAsync(reply);
                State = VoiceState.Speaking;
                StartCoroutine(playback.PlayMp3(mp3));
            }
            catch (Exception e)
            {
                Debug.LogError($"VoiceSession error: {e.Message}");
                StartListening();
            }
        }

        private void HandlePlaybackEnded()
        {
            StartListening();
        }
    }
}
