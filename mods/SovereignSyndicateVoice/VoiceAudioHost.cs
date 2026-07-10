using System.Collections;
using AC;
using MelonLoader;
using UnityEngine;

namespace SovereignSyndicateVoice
{
    public sealed class VoiceAudioHost : MonoBehaviour
    {
        public void PlayWav(string key, string path)
        {
            StartCoroutine(PlayRoutine(path, key));
        }

        private IEnumerator PlayRoutine(string path, string key)
        {
            if (!FmodVoicePlayer.TryPlay(path, out var duration))
            {
                MelonLogger.Warning("VO FMOD play failed: " + key + " path=" + path);
                yield break;
            }

            for (var i = 0; i < 30 && KickStarter.dialog != null; i++)
            {
                yield return null;
            }

            StopEnglishSpeech();
            MelonLogger.Msg("VO FMOD: " + key + " (" + duration.ToString("F1") + "s)");
        }

        internal static void StopEnglishSpeech()
        {
            if (KickStarter.dialog == null)
            {
                return;
            }

            var speech = KickStarter.dialog.GetLatestSpeech();
            if (speech != null)
            {
                speech.EndSpeechAudio();
            }
        }
    }
}
