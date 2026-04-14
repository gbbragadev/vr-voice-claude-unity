using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VoiceClaude
{
    public class TranscriptPanel : MonoBehaviour
    {
        public VoiceSession session;
        public TMP_Text text;
        public Image border;

        private string _user = "";
        private string _claude = "";

        private static readonly Color ColorIdle = new(0.5f, 0.5f, 0.5f);
        private static readonly Color ColorListening = new(0.2f, 0.5f, 1f);
        private static readonly Color ColorRecording = new(0.2f, 0.9f, 0.3f);
        private static readonly Color ColorThinking = new(1f, 0.6f, 0.1f);
        private static readonly Color ColorSpeaking = new(0.95f, 0.95f, 0.95f);

        private void Start()
        {
            session.OnStateChanged += HandleState;
            session.OnUserSaid += s => { _user = s; Render(); };
            session.OnClaudeSaid += s => { _claude = s; Render(); };
            HandleState(session.State);
            Render();
        }

        private void HandleState(VoiceState s)
        {
            if (border == null) return;
            border.color = s switch
            {
                VoiceState.Idle => ColorIdle,
                VoiceState.Listening => ColorListening,
                VoiceState.Recording => ColorRecording,
                VoiceState.Thinking => ColorThinking,
                VoiceState.Speaking => ColorSpeaking,
                _ => ColorIdle,
            };
        }

        private void Render()
        {
            text.text = $"<b>você:</b> {_user}\n\n<b>claude:</b> {_claude}";
        }
    }
}
