using System.Text.RegularExpressions;

namespace SovereignSyndicateVoice
{
    internal static class VoiceText
    {
        private static readonly Regex HtmlTag = new Regex("<[^>]+>", RegexOptions.Compiled);

        /// <summary>
        /// Prefer DB dialogue text; strip UI rich-text / speaker labels for TTS + prefetch.
        /// </summary>
        internal static string ForVoice(PixelCrushers.DialogueSystem.DialogueEntry entry, string formattedUiText)
        {
            string raw = null;
            if (entry != null)
            {
                raw = entry.currentLocalizedDialogueText;
                if (string.IsNullOrEmpty(raw))
                {
                    raw = entry.DialogueText;
                }
            }

            if (string.IsNullOrEmpty(raw))
            {
                raw = formattedUiText;
            }

            return Sanitize(raw);
        }

        internal static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var value = HtmlTag.Replace(text, string.Empty);
            value = value.Replace("&nbsp;", " ").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
            value = value.Trim();

            // "КЛАРА РИД: «…»" → «…»
            var colon = value.IndexOf(':');
            if (colon > 0 && colon < 40 && value.Length > colon + 1)
            {
                var prefix = value.Substring(0, colon);
                if (prefix.IndexOf('«') < 0 && prefix.IndexOf('"') < 0)
                {
                    value = value.Substring(colon + 1).Trim();
                }
            }

            return value.Trim();
        }
    }
}
