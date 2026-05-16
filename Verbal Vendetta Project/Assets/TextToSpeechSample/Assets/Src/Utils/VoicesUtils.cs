using System;
using UnityEngine;

namespace Unity.InferenceEngine.Samples.TTS.Utils
{
    public static class VoicesUtils
    {
        const string k_IndexFilePath = "voicesIndex";

        public static string[]  GetVoicesList()
        {
            var voicesIndex = Resources.Load<TextAsset>(k_IndexFilePath);
            if (voicesIndex == null)
            {
                Debug.LogError($"[VoicesUtils] Failed to load voices index at 'Resources/{k_IndexFilePath}'");
                return Array.Empty<string>();
            }

            var voiceText = voicesIndex.text.Replace(".bin", string.Empty);
            return voiceText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
