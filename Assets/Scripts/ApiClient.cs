using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace VoiceClaude
{
    [Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class ChatRequest
    {
        public List<ChatMessage> messages;
        public string image_b64;
    }

    [Serializable]
    public class ChatResponse
    {
        public string reply;
    }

    [Serializable]
    public class SttResponse
    {
        public string text;
        public string language;
    }

    public static class ApiClient
    {
        public static string BaseUrl = "https://voice-api.gbbragadev.com";

        public static async Task<string> SttAsync(byte[] wav)
        {
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("audio", wav, "speech.wav", "audio/wav"),
            };
            using var req = UnityWebRequest.Post($"{BaseUrl}/stt", form);
            await SendAsync(req);
            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"STT {req.responseCode}: {req.error}");
            var parsed = JsonUtility.FromJson<SttResponse>(req.downloadHandler.text);
            return parsed.text;
        }

        public static async Task<string> ChatAsync(List<ChatMessage> history, byte[] imageJpeg = null)
        {
            var payload = new ChatRequest
            {
                messages = history,
                image_b64 = imageJpeg != null ? Convert.ToBase64String(imageJpeg) : null,
            };
            var json = JsonUtility.ToJson(payload);
            using var req = new UnityWebRequest($"{BaseUrl}/chat", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            await SendAsync(req);
            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"Chat {req.responseCode}: {req.error}");
            var parsed = JsonUtility.FromJson<ChatResponse>(req.downloadHandler.text);
            return parsed.reply;
        }

        public static async Task<byte[]> TtsAsync(string text)
        {
            var payload = "{\"text\":" + JsonUtility.ToJson(new StringWrap { value = text }).Replace("{\"value\":", "").TrimEnd('}') + "}";
            using var req = new UnityWebRequest($"{BaseUrl}/tts", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            await SendAsync(req);
            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"TTS {req.responseCode}: {req.error}");
            return req.downloadHandler.data;
        }

        private static Task SendAsync(UnityWebRequest req)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            op.completed += _ => tcs.SetResult(true);
            return tcs.Task;
        }

        [Serializable]
        private class StringWrap { public string value; }
    }
}
