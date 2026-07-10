using System.Collections;
using MelonLoader;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace SovereignSyndicateVoice
{
    internal static class VoicePendingReplay
    {
        private static int _entryId = -1;
        private static string _character;
        private static string _lineText;
        private static DialogueEntry _entry;
        private static float _deadline;
        private static int _conversationId = -1;
        private static bool _running;

        internal static void Register(DialogueEntry entry, string character, string lineText)
        {
            if (entry == null || string.IsNullOrEmpty(character))
            {
                return;
            }

            _entry = entry;
            _entryId = entry.id;
            _conversationId = DialogueManager.lastConversationID;
            _character = character;
            _lineText = lineText;
            _deadline = Time.unscaledTime + 90f;

            if (!_running)
            {
                MelonCoroutines.Start(WaitAndReplay());
            }
        }

        private static IEnumerator WaitAndReplay()
        {
            _running = true;

            while (Time.unscaledTime < _deadline)
            {
                if (_entry == null)
                {
                    break;
                }

                if (VoiceMod.Player != null)
                {
                    var ready = false;
                    foreach (var key in DialogueVoiceKeys.BuildLookupOrder(_entry, _conversationId, _lineText))
                    {
                        if (!VoiceMod.Player.HasModWav(key))
                        {
                            continue;
                        }

                        MelonLogger.Msg("VO replay ready: id=" + _entryId + " key=" + key);
                        VoiceMod.Player.TryPlayDialogueSubtitle(_entry, _lineText, _character, _conversationId);
                        ready = true;
                        break;
                    }

                    if (ready)
                    {
                        break;
                    }
                }

                yield return new WaitForSeconds(0.25f);
            }

            _entry = null;
            _entryId = -1;
            _conversationId = -1;
            _running = false;
        }
    }
}
