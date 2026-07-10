using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace SovereignSyndicateVoice
{
    [HarmonyPatch(typeof(Localize), "Get", new Type[] { typeof(string) })]
    internal static class LocalizeGetPatch
    {
        internal static void Prefix(string key)
        {
            if (VoiceMod.Player == null || !VoiceKeyFilter.ShouldPlayFromLocalize(key))
            {
                return;
            }

            VoiceMod.Player.SetActiveLoadscreenKey(key);
        }

        internal static void Postfix(string key)
        {
            if (VoiceMod.Player == null || !VoiceKeyFilter.ShouldPlayFromLocalize(key))
            {
                return;
            }

            VoiceMod.Player.TryPlay(key);
        }
    }

    [HarmonyPatch(typeof(AC.RuntimeLanguages), "GetSpeechAudioClip")]
    internal static class AcSpeechAudioPatch
    {
        internal static void Postfix(int lineID, AC.Char _speaker, ref AudioClip __result)
        {
            if (VoiceMod.Player == null)
            {
                return;
            }

            var key = VoiceMod.Player.TakeActiveLoadscreenKey();
            if (string.IsNullOrEmpty(key))
            {
                key = LoadscreenKeyResolver.FromLineId(lineID, _speaker);
            }

            if (string.IsNullOrEmpty(key) || !VoiceMod.Player.HasModWav(key))
            {
                return;
            }

            var path = VoiceMod.Player.ResolvePath(key, null);
            if (path == null || !FmodVoicePlayer.TryPlay(path, out _))
            {
                return;
            }

            VoiceAudioHost.StopEnglishSpeech();
        }
    }

    [HarmonyPatch(typeof(LoadScreen), "StartLoadScene")]
    internal static class LoadScreenExitPatch
    {
        internal static void Prefix()
        {
            VoiceMod.Player?.StopAllVo();
            MelonLogger.Msg("VO stop: loadscreen exit");
        }
    }

    [HarmonyPatch(typeof(LoadScreen), "DisableLoadScreenUI")]
    internal static class LoadScreenDisablePatch
    {
        internal static void Postfix()
        {
            VoiceMod.Player?.StopAllVo();
        }
    }

    [HarmonyPatch(typeof(AbstractUISubtitleControls), "ShowSubtitle")]
    internal static class SubtitleControlsPatch
    {
        internal static void Postfix(Subtitle subtitle)
        {
            DialogueHooks.OnSubtitle(subtitle, "ShowSubtitle");
        }
    }

    internal static class DialogueHooks
    {
        private static bool _subscribed;
        private static string _lastHandledKey = string.Empty;
        private static float _lastHandledTime;

        internal static void ResetForSceneLoad()
        {
            _subscribed = false;
            _lastHandledKey = string.Empty;
            _lastHandledTime = 0f;
        }

        internal static bool IsSubscribed
        {
            get { return _subscribed; }
        }

        internal static void TrySubscribe()
        {
            DialogueDumper.TryDump();

            if (_subscribed || DialogueManager.instance == null)
            {
                return;
            }

            var events = DialogueManager.instance.GetComponent<DialogueSystemEvents>();
            if (events == null)
            {
                events = DialogueManager.instance.gameObject.AddComponent<DialogueSystemEvents>();
            }

            events.conversationEvents.onConversationLine.AddListener(s => OnSubtitle(s, "event"));
            events.barkEvents.onBarkLine.AddListener(s => OnSubtitle(s, "bark"));
            events.conversationEvents.onConversationStart.AddListener(VoicePrefetch.OnConversationStart);
            events.conversationEvents.onConversationResponseMenu.AddListener(OnResponseMenu);
            events.conversationEvents.onConversationEnd.AddListener(OnConversationEnd);
            _subscribed = true;
            MelonLogger.Msg("Dialogue System hooks attached");
            DialogueDumper.TryDump();
        }

        internal static void OnSubtitle(Subtitle subtitle, string source)
        {
            if (VoiceMod.Player == null || subtitle == null)
            {
                return;
            }

            var entry = subtitle.dialogueEntry;
            if (entry == null)
            {
                return;
            }

            var dedupeKey = entry.id + ":" + (subtitle.formattedText != null ? subtitle.formattedText.text : entry.Title);
            var now = Time.unscaledTime;
            if (dedupeKey == _lastHandledKey && now - _lastHandledTime < 0.75f)
            {
                return;
            }

            _lastHandledKey = dedupeKey;
            _lastHandledTime = now;

            VoiceMod.Player.StopLoadscreenVo();

            var character = MapSpeaker(subtitle.speakerInfo) ?? MapSpeakerFromEntry(entry);
            if (character != null)
            {
                if (FmodVoicePlayer.IsPlaying)
                {
                    FmodVoicePlayer.Stop();
                    MelonLogger.Msg("VO stop: switch to id=" + entry.id);
                }
            }

            var lineText = subtitle.formattedText != null ? subtitle.formattedText.text : null;
            MelonLogger.Msg(
                "VO dialogue [" + source + "]: id=" + entry.id + " title=" + entry.Title + " text=" + (lineText ?? "?") +
                " speaker=" + (character ?? "?"));

            VoicePrefetch.OnLineShown(entry);
            VoiceMod.Player.TryPlayDialogueSubtitle(entry, lineText, character, DialogueManager.lastConversationID);
        }

        private static void OnResponseMenu(Response[] responses)
        {
            VoicePrefetch.OnResponseMenu(responses);
            StopCurrentVo("response menu");
        }

        private static void OnConversationEnd(Transform actor)
        {
            StopCurrentVo("conversation end");
        }

        private static void StopCurrentVo(string reason)
        {
            if (!FmodVoicePlayer.IsPlaying)
            {
                return;
            }

            FmodVoicePlayer.Stop();
            VoiceMod.Player?.StopLoadscreenVo();
            MelonLogger.Msg("VO stop: " + reason);
        }

        private static string MapSpeakerFromEntry(DialogueEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            var db = DialogueManager.masterDatabase;
            if (db == null)
            {
                return null;
            }

            foreach (var actor in db.actors)
            {
                if (actor.id != entry.ActorID)
                {
                    continue;
                }

                return DialogueLineRules.MapActor(actor.Name);
            }

            return null;
        }

        private static string MapSpeaker(CharacterInfo speaker)
        {
            if (speaker == null)
            {
                return null;
            }

            var name = (speaker.nameInDatabase ?? speaker.Name ?? string.Empty).ToLowerInvariant();
            if (name.Contains("atticus") || name.Contains("daley") || name.Contains("daily"))
            {
                return "atticus";
            }

            if (name.Contains("clara"))
            {
                return "clara";
            }

            if (name.Contains("otto") || name.Contains("teddy") || name.Contains("ted"))
            {
                return "otto";
            }

            return null;
        }
    }
}
