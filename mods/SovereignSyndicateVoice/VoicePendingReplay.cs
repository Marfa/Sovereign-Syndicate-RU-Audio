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
            MelonLogger.Msg("VO replay wait: id=" + entry.id + " key=" + DialogueVoiceKeys.Primary(entry, _conversationId));

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
            _character = null;
            _lineText = null;
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
            try
            {
                while (true)
                {
                    // Idle until Register sets pending work (or exit after short idle).
                    if (_pendingEntryId < 0 || _entry == null)
                    {
                        var idleUntil = Time.unscaledTime + 1.0f;
                        while ((_pendingEntryId < 0 || _entry == null) && Time.unscaledTime < idleUntil)
                        {
                            yield return new WaitForSeconds(0.25f);
                        }

                        if (_pendingEntryId < 0 || _entry == null)
                        {
                            yield break;
                        }
                    }

                    var waitEntryId = _pendingEntryId;
                    var waitConvId = _conversationId;
                    var entry = _entry;
                    var character = _character;
                    var lineText = _lineText;
                    var deadline = _deadline;

                    var played = false;
                    while (Time.unscaledTime < deadline)
                    {
                        // Newer Register or Cancel superseded this wait target.
                        if (_pendingEntryId != waitEntryId || _entry == null)
                        {
                            break;
                        }

                        if (!IsStillInConversation(waitConvId))
                        {
                            MelonLogger.Msg("VO replay skip: conversation ended id=" + waitEntryId);
                            if (_pendingEntryId == waitEntryId)
                            {
                                Cancel();
                            }

                            break;
                        }

                        if (VoiceMod.Player != null)
                        {
                            var key = DialogueVoiceKeys.Primary(entry, waitConvId);
                            if (!VoiceMod.Player.HasModWav(key))
                            {
                                yield return new WaitForSeconds(0.25f);
                                continue;
                            }

                            if (FmodVoicePlayer.IsPlaying)
                            {
                                yield return new WaitForSeconds(0.25f);
                                continue;
                            }

                            MelonLogger.Msg("VO replay ready: id=" + waitEntryId + " key=" + key);
                            if (VoiceMod.Player.TryPlayDialogueSubtitle(entry, lineText, character, waitConvId))
                            {
                                if (_pendingEntryId == waitEntryId)
                                {
                                    Cancel();
                                }

                                played = true;
                                break;
                            }

                            MelonLogger.Msg("VO replay retry: play failed id=" + waitEntryId);
                            yield return new WaitForSeconds(0.25f);
                            continue;
                        }

                        yield return new WaitForSeconds(0.25f);
                    }

                    if (!played && _pendingEntryId == waitEntryId)
                    {
                        MelonLogger.Msg("VO replay timeout: id=" + waitEntryId);
                        Cancel();
                    }
                }
            }
            finally
            {
                _running = false;
            }
        }
    }
}
