using System;
using UnityEngine;
using Pv.Unity;

namespace VoiceClaude
{
    // Uses Porcupine's built-in "jarvis" keyword. No custom .ppn training needed —
    // only a free Picovoice AccessKey stored at Assets/Resources/secrets.txt.
    public class WakeWordDetector : MonoBehaviour
    {
        public event Action OnWakeDetected;

        private PorcupineManager _porcupine;

        public void Init()
        {
            var keyAsset = Resources.Load<TextAsset>("secrets");
            if (keyAsset == null)
            {
                Debug.LogError("WakeWordDetector: Resources/secrets.txt not found. Paste the Picovoice AccessKey there.");
                return;
            }
            string accessKey = keyAsset.text.Trim();

            _porcupine = PorcupineManager.FromBuiltInKeywords(
                accessKey,
                new System.Collections.Generic.List<Porcupine.BuiltInKeyword> { Porcupine.BuiltInKeyword.JARVIS },
                OnKeyword,
                sensitivities: new float[] { 0.7f }
            );
        }

        public void Start_()
        {
            _porcupine?.Start();
        }

        public void Stop_()
        {
            _porcupine?.Stop();
        }

        private void OnKeyword(int _)
        {
            OnWakeDetected?.Invoke();
        }

        private void OnDestroy()
        {
            _porcupine?.Delete();
        }
    }
}
