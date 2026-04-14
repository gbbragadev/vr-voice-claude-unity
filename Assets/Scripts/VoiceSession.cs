using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoiceClaude
{
    public enum VoiceState { Idle, Listening, Recording, Thinking, Speaking }

    public class VoiceSession : MonoBehaviour
    {
        public WakeWordDetector wakeDetector;
        public MicCapture mic;
        public CameraCapture cameraCapture;
        public AudioPlayback playback;

        public event Action<VoiceState> OnStateChanged;
        public event Action<string> OnUserSaid;
        public event Action<string> OnClaudeSaid;

        private readonly List<ChatMessage> _history = new();
        private VoiceState _state = VoiceState.Idle;

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
            wakeDetector.Init();
            wakeDetector.OnWakeDetected += HandleWake;
            mic.OnSpeechStart += HandleSpeechStart;
            mic.OnSpeechEnd += HandleSpeechEnd;
            playback.OnPlaybackEnded += HandlePlaybackEnded;

            // Camera starts at Start() and stays running — avoids the ~500ms
            // warm-up latency every time the user wakes the app.
            cameraCapture.StartCamera();

            wakeDetector.Start_();
            State = VoiceState.Idle;
        }

        private void HandleWake()
        {
            if (State != VoiceState.Idle) return;
            wakeDetector.Stop_();
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
                ReturnToIdle();
            }
        }

        private void HandlePlaybackEnded()
        {
            ReturnToIdle();
        }

        private void ReturnToIdle()
        {
            wakeDetector.Start_();
            State = VoiceState.Idle;
        }
    }
}
