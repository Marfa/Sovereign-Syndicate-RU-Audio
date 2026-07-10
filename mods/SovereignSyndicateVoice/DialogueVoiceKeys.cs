using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;

namespace SovereignSyndicateVoice
{
    internal static class DialogueVoiceKeys
    {
        internal static bool IsGenericTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return true;
            }

            return title.StartsWith("START") || title.StartsWith("Blood") ||
                   string.Equals(title, "CONTINUE", StringComparison.OrdinalIgnoreCase);
        }

        internal static string Scoped(DialogueEntry entry, int conversationId)
        {
            return "c" + conversationId + "_e" + entry.id;
        }

        internal static string Primary(DialogueEntry entry, int conversationId)
        {
            if (entry == null)
            {
                return null;
            }

            return Scoped(entry, conversationId);
        }

        internal static IEnumerable<string> BuildLookupOrder(DialogueEntry entry, int conversationId, string lineText)
        {
            if (entry == null)
            {
                if (!string.IsNullOrEmpty(lineText))
                {
                    yield return lineText;
                }

                yield break;
            }

            yield return Scoped(entry, conversationId);

            if (!string.IsNullOrEmpty(lineText))
            {
                yield return lineText;
            }

            if (!string.IsNullOrEmpty(entry.DialogueText) &&
                !string.Equals(entry.DialogueText, lineText, StringComparison.Ordinal))
            {
                yield return entry.DialogueText;
            }

            if (IsGenericTitle(entry.Title))
            {
                yield return "e" + entry.id;
                yield return entry.id.ToString();
            }
        }
    }
}
