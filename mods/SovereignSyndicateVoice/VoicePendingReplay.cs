using System.Collections;
using MelonLoader;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace SovereignSyndicateVoice
{
    internal static class VoicePendingReplay
    {
        private static int _pendingEntryId = -1;
        private static string _character;
        private static string _lineText;
        private static DialogueEntry _entry;
        private static float _deadline;
        private static int _conversationId = -1;
        private static bool _running;

        internal static void OnLineShown(int entryId, int conversationId)
        {
            if (_pendingEntryId < 0)
            {
                return;
            }

            if (entryId != _pendingEntryId || conversationId != _conversationId)
            {
                MelonLogger.Msg("VO replay cancel: advanced to id=" + entryId + " (was id=" + _pendingEntryId + ")");
                Cancel();
            }
        }

        internal static void Register(DialogueEntry entry, string character, string lineText)
        {
            if (entry == null || string.IsNullOrEmpty(character))
            {
                return;
            }

            _entry = entry;
            _pendingEntryId = entry.id;
            _conversationId = DialogueManager.lastConversationID;
            _character = character;
            _lineText = lineText;
            _deadline = Time.unscaledTime + 90f;

            if (!_running)
            {
                MelonCoroutines.Start(WaitAndReplay());
            }
        }

        internal static void Cancel()
        {
            _pendingEntryId = -1;
            _entry = null;
            _conversationId = -1;
        }

        private static bool IsStillInConversation(int convId)
        {
            if (DialogueManager.instance == null)
            {
                return false;
            }

            return DialogueManager.isConversationActive && DialogueManager.lastConversationID == convId;
        }

        private static IEnumerator WaitAndReplay()
        {
            _running = true;
            var waitEntryId = _pendingEntryId;
            var waitConvId = _conversationId;

            while (Time.unscaledTime < _deadline)
            {
                if (_entry == null || _pendingEntryId != waitEntryId)
                {
                    break;
                }

                if (!IsStillInConversation(waitConvId))
                {
                    MelonLogger.Msg("VO replay skip: conversation ended id=" + waitEntryId);
                    break;
                }

                if (VoiceMod.Player != null)
                {
                    var key = DialogueVoiceKeys.Primary(_entry, waitConvId);
                    if (!VoiceMod.Player.HasModWav(key))
                    {
                        yield return new WaitForSeconds(0.25f);
                        continue;
                    }

                    if (_pendingEntryId != waitEntryId || FmodVoicePlayer.IsPlaying)
                    {
                        MelonLogger.Msg("VO replay skip: stale id=" + waitEntryId);
                        break;
                    }

                    MelonLogger.Msg("VO replay ready: id=" + waitEntryId + " key=" + key);
                    VoiceMod.Player.TryPlayDialogueSubtitle(_entry, _lineText, _character, waitConvId);
                    _pendingEntryId = -1;
                    _entry = null;
                    break;
                }

                yield return new WaitForSeconds(0.25f);
            }

            _entry = null;
            _conversationId = -1;
            _pendingEntryId = -1;
            _running = false;
        }
    }
}
