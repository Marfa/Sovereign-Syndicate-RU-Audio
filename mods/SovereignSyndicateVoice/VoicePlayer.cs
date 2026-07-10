using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MelonLoader;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace SovereignSyndicateVoice
{
    public sealed class VoicePlayer
    {
        private const string DevVoiceRoot = VoicePaths.DevVoiceRoot;

        private static readonly Dictionary<string, string> CharacterTags = new Dictionary<string, string>
        {
            { "atticus", "_ATT_" },
            { "clara", "_CLA_" },
            { "otto", "_TED_" },
        };

        private readonly string _voiceRoot;
        private string _lastKey = string.Empty;
        private float _lastPlayTime;
        private float _lastLoadscreenTime;

        internal string ActiveLoadscreenKey { get; private set; }

        public VoicePlayer(string voiceRoot)
        {
            _voiceRoot = voiceRoot;
        }

        public string VoiceRoot
        {
            get { return _voiceRoot; }
        }

        internal void SetActiveLoadscreenKey(string key)
        {
            ActiveLoadscreenKey = key;
        }

        internal string TakeActiveLoadscreenKey()
        {
            var key = ActiveLoadscreenKey;
            ActiveLoadscreenKey = null;
            return key;
        }

        internal bool HasModWav(string key)
        {
            return !string.IsNullOrEmpty(key) && ResolveWavPath(key, null) != null;
        }

        internal string ResolvePath(string key, string character)
        {
            return ResolveWavPath(key, character);
        }

        public void TryPlay(string key, string character = null)
        {
            if (string.IsNullOrEmpty(key) || !VoiceKeyFilter.ShouldPlayFromLocalize(key))
            {
                return;
            }

            PlayInternal(key, character, logMiss: true);
        }

        public void TryPlayDialogue(string key, string character = null)
        {
            TryPlayDialogueSubtitle(null, key, character);
        }

        public void TryPlayDialogueSubtitle(DialogueEntry entry, string lineText, string character, int conversationId = -1)
        {
            if (entry == null || string.IsNullOrEmpty(character))
            {
                MelonLogger.Msg("VO skip (no speaker): id=" + (entry != null ? entry.id.ToString() : "?"));
                return;
            }

            if (conversationId < 0)
            {
                conversationId = DialogueManager.lastConversationID;
            }

            StopLoadscreenVo();

            foreach (var key in DialogueVoiceKeys.BuildLookupOrder(entry, conversationId, lineText))
            {
                var path = ResolveWavPath(key, character);
                if (path == null)
                {
                    continue;
                }

                if (FmodVoicePlayer.TryPlay(path, out var duration))
                {
                    MelonLogger.Msg("VO play dialogue: id=" + entry.id + " key=" + key + " (" + duration.ToString("F1") + "s)");
                    return;
                }

                MelonLogger.Warning("VO FMOD failed dialogue id=" + entry.id + " key=" + key);
                return;
            }

            MelonLogger.Msg("VO miss dialogue: " + entry.id + " (" + character + ")");
            var missText = lineText ?? entry.DialogueText;
            VoicePrefetch.RequestLine(character, DialogueVoiceKeys.Primary(entry, conversationId), missText);
            VoicePendingReplay.Register(entry, character, missText);
        }

        internal void StopLoadscreenVo()
        {
            if (string.IsNullOrEmpty(ActiveLoadscreenKey))
            {
                return;
            }

            ActiveLoadscreenKey = null;
            FmodVoicePlayer.Stop();
        }

        public void StopAllVo()
        {
            ActiveLoadscreenKey = null;
            FmodVoicePlayer.Stop();
        }

        private void PlayInternal(string key, string character, bool logMiss)
        {
            var now = Time.unscaledTime;
            if (key == _lastKey && now - _lastPlayTime < 0.35f)
            {
                return;
            }

            if (key.StartsWith("LOADSCREEN_") && now - _lastLoadscreenTime < 2f)
            {
                return;
            }

            var path = ResolveWavPath(key, character);
            if (path == null)
            {
                if (logMiss)
                {
                    MelonLogger.Msg("VO miss: " + key + (character != null ? " (" + character + ")" : string.Empty));
                }
                return;
            }

            _lastKey = key;
            _lastPlayTime = now;
            if (key.StartsWith("LOADSCREEN_"))
            {
                _lastLoadscreenTime = now;
            }

            MelonLogger.Msg("VO play: " + key + " -> " + path);
            if (key.StartsWith("LOADSCREEN_"))
            {
                ActiveLoadscreenKey = key;
            }

            VoiceMod.EnsureAudioHost().PlayWav(key, path);
        }

        public void Stop()
        {
            StopAllVo();
            try
            {
                if (AC.KickStarter.dialog != null)
                {
                    var source = AC.KickStarter.dialog.GetNarratorAudioSource();
                    if (source != null)
                    {
                        source.Stop();
                    }
                }
            }
            catch
            {
                // ponytail: AC may be torn down on quit
            }
        }

        private string ResolveWavPath(string key, string character)
        {
            var path = ResolveWavPathInRoot(_voiceRoot, key, character);
            if (path != null)
            {
                return path;
            }

            if (string.Equals(_voiceRoot, DevVoiceRoot, StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(DevVoiceRoot))
            {
                return null;
            }

            return ResolveWavPathInRoot(DevVoiceRoot, key, character);
        }

        private static string ResolveWavPathInRoot(string root, string key, string character)
        {
            var candidates = BuildKeyCandidates(key).ToList();
            var characters = BuildCharacterSearchOrder(key, character);

            foreach (var ch in characters)
            {
                var dir = Path.Combine(root, ch);
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var candidate in candidates)
                {
                    string path = null;
                    try
                    {
                        path = FindWavInDir(dir, candidate);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning("VO path error: " + candidate + " — " + ex.Message);
                    }

                    if (path != null)
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        private static string FindWavInDir(string dir, string candidate)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return null;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var variant in ExpandKeyVariants(candidate))
            {
                var safe = SanitizeFileName(variant);
                if (!string.IsNullOrEmpty(safe))
                {
                    names.Add(safe);
                }
            }

            foreach (var name in names)
            {
                var direct = Path.Combine(dir, name + ".wav");
                if (File.Exists(direct))
                {
                    return direct;
                }
            }

            foreach (var file in Directory.GetFiles(dir, "*.wav"))
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                foreach (var name in names)
                {
                    if (string.Equals(stem, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<string> BuildKeyCandidates(string key)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in ExpandKeyVariants(key))
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }

                var sanitized = SanitizeFileName(candidate);
                if (seen.Add(sanitized))
                {
                    yield return sanitized;
                }
            }
        }

        private static IEnumerable<string> ExpandKeyVariants(string key)
        {
            yield return key;

            var normalized = NormalizeDialogueKey(key);
            if (!string.Equals(normalized, key, StringComparison.Ordinal))
            {
                yield return normalized;
            }
        }

        private static string NormalizeDialogueKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return key;
            }

            var value = key.Trim();
            value = Regex.Replace(value, @"[""«»''\u201C\u201D\u2018\u2019]", string.Empty);
            if (Regex.IsMatch(value, @"^e\d+$"))
            {
                value = Regex.Replace(value, @"-\d+$", string.Empty);
            }
            value = value.Replace('\u2014', ' ').Replace('\u2013', ' ').Replace('—', ' ');
            value = Regex.Replace(value, @"\.{2,}$", string.Empty);
            value = Regex.Replace(value, @"\s+", " ").Trim();
            return value.TrimEnd('-', '.', ' ', '"', '…');
        }

        private static IEnumerable<string> BuildCharacterSearchOrder(string key, string character)
        {
            if (!string.IsNullOrEmpty(character))
            {
                yield return character;
            }

            var fromKey = MatchCharacterFromKey(key);
            if (!string.IsNullOrEmpty(fromKey))
            {
                yield return fromKey;
            }

            if (string.IsNullOrEmpty(character))
            {
                foreach (var name in CharacterTags.Keys)
                {
                    yield return name;
                }
            }
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
            return new string(chars).Trim();
        }

        private static string MatchCharacterFromKey(string key)
        {
            var upper = key.ToUpperInvariant();
            foreach (var pair in CharacterTags)
            {
                if (upper.Contains(pair.Value))
                {
                    return pair.Key;
                }
            }

            if (upper.Contains("_OTT_") || upper.Contains("_OTTO_"))
            {
                return "otto";
            }

            return null;
        }
    }
}
