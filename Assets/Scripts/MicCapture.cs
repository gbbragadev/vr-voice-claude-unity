using System;
using UnityEngine;

namespace VoiceClaude
{
    public class MicCapture : MonoBehaviour
    {
        public const int SampleRate = 16000;
        private const int BufferSeconds = 10;
        private const float SpeechThreshold = 0.02f;
        private const float SilenceHoldSeconds = 0.8f;
        private const int MinSpeechFrames = 4;

        public event Action OnSpeechStart;
        public event Action<float[]> OnSpeechEnd;

        private AudioClip _clip;
        private int _lastPos;
        private bool _isSpeaking;
        private float _silenceSince;
        private int _speechFrames;
        private readonly System.Collections.Generic.List<float> _buffer = new();

        public void StartCapture()
        {
            if (_clip != null) return;
            _clip = Microphone.Start(null, true, BufferSeconds, SampleRate);
            _lastPos = 0;
            _isSpeaking = false;
            _speechFrames = 0;
            _buffer.Clear();
        }

        public void StopCapture()
        {
            if (_clip == null) return;
            Microphone.End(null);
            Destroy(_clip);
            _clip = null;
        }

        private void Update()
        {
            if (_clip == null) return;
            int pos = Microphone.GetPosition(null);
            if (pos == _lastPos) return;

            int len = pos >= _lastPos ? pos - _lastPos : _clip.samples - _lastPos + pos;
            if (len == 0) return;
            var samples = new float[len];
            _clip.GetData(samples, _lastPos);
            _lastPos = pos;

            float rms = ComputeRms(samples);
            if (rms > SpeechThreshold)
            {
                if (!_isSpeaking)
                {
                    _speechFrames++;
                    if (_speechFrames >= MinSpeechFrames)
                    {
                        _isSpeaking = true;
                        _buffer.Clear();
                        OnSpeechStart?.Invoke();
                    }
                }
                _silenceSince = 0f;
                if (_isSpeaking) _buffer.AddRange(samples);
            }
            else if (_isSpeaking)
            {
                _buffer.AddRange(samples);
                _silenceSince += (float)len / SampleRate;
                if (_silenceSince >= SilenceHoldSeconds)
                {
                    _isSpeaking = false;
                    _speechFrames = 0;
                    _silenceSince = 0f;
                    var snapshot = _buffer.ToArray();
                    _buffer.Clear();
                    OnSpeechEnd?.Invoke(snapshot);
                }
            }
            else
            {
                _speechFrames = 0;
            }
        }

        private static float ComputeRms(float[] s)
        {
            double sum = 0;
            for (int i = 0; i < s.Length; i++) sum += s[i] * s[i];
            return (float)Math.Sqrt(sum / s.Length);
        }

        public static byte[] EncodeWav(float[] samples, int sampleRate = SampleRate)
        {
            using var ms = new System.IO.MemoryStream();
            using var w = new System.IO.BinaryWriter(ms);
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + samples.Length * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(sampleRate); w.Write(sampleRate * 2);
            w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(samples.Length * 2);
            for (int i = 0; i < samples.Length; i++)
            {
                short v = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
                w.Write(v);
            }
            return ms.ToArray();
        }
    }
}
