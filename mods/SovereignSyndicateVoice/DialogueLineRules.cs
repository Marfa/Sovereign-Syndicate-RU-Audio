using System;
using MelonLoader;
using PixelCrushers.DialogueSystem;

namespace SovereignSyndicateVoice
{
    internal static class DialogueLineRules
    {
        internal static bool ShouldVoice(DialogueEntry entry)
        {
            var title = entry.Title ?? string.Empty;
            if (title.StartsWith("START") || title.StartsWith("Blood") || title.StartsWith("A {"))
            {
                return false;
            }

            if (title.EndsWith("-3") || title.EndsWith("-4"))
            {
                return false;
            }

            return title.EndsWith("-2") || (title.StartsWith("\"") && title.Contains("?"));
        }

        internal static string MapActor(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            var low = name.Trim().ToLowerInvariant();

            // Route short codes used in conversation titles / actor names
            if (low == "att" || low == "atticus" || low.Contains("atticus") ||
                low.Contains("daley") || low.Contains("daily"))
            {
                return "atticus";
            }

            if (low == "cla" || low == "clara" || low.Contains("clara"))
            {
                return "clara";
            }

            // Teddy Redgrave (TED route) — voice/teddy/ + teddy_ref
            if (low == "ted" || low == "teddy" || low.Contains("teddy") || low.Contains("redgrave") ||
                low.Contains("ted"))
            {
                return "teddy";
            }

            // Automaton companion Otto — separate male voice (ruslan ref)
            if (low.Contains("otto"))
            {
                return "otto";
            }

            return null;
        }

        internal static bool IsGenericSpeaker(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return true;
            }

            var low = name.Trim().ToLowerInvariant();
            return low == "player" ||
                   low == "narrator" ||
                   low == "description" ||
                   low == "pc" ||
                   low.StartsWith("player ", StringComparison.Ordinal);
        }

        /// <summary>
        /// PC examine / monologue lines often use Actor=Player/Narrator.
        /// Resolve Atticus/Clara/Teddy from conversation title ("… / CLA") or AC player.
        /// </summary>
        internal static string ResolveCurrentPcCharacter()
        {
            try
            {
                var db = DialogueManager.masterDatabase;
                if (db != null)
                {
                    var conv = db.GetConversation(DialogueManager.lastConversationID);
                    var fromTitle = MapRouteFromConversationTitle(conv != null ? conv.Title : null);
                    if (fromTitle != null)
                    {
                        return fromTitle;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("VO PC resolve (conversation): " + ex.Message);
            }

            try
            {
                if (AC.KickStarter.player != null)
                {
                    var mapped = MapActor(AC.KickStarter.player.name);
                    if (mapped != null)
                    {
                        return mapped;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("VO PC resolve (AC player): " + ex.Message);
            }

            return null;
        }

        internal static string MapRouteFromConversationTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return null;
            }

            var parts = title.Split('/');
            var last = parts[parts.Length - 1].Trim();
            var mapped = MapActor(last);
            if (mapped != null)
            {
                return mapped;
            }

            var upper = title.ToUpperInvariant();
            if (upper.Contains("/ CLA") || upper.EndsWith(" CLA"))
            {
                return "clara";
            }

            if (upper.Contains("/ ATT") || upper.EndsWith(" ATT"))
            {
                return "atticus";
            }

            if (upper.Contains("/ TED") || upper.EndsWith(" TED"))
            {
                return "teddy";
            }

            return null;
        }
    }
}
