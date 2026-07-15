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

        /// <summary>
        /// Beat / pause lines like «...» / "…" — no TTS, no prefetch.
        /// </summary>
        internal static bool IsNonVerbalPause(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            var value = Sanitize(text);
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            value = value.Trim('«', '»', '"', '\u201C', '\u201D', '\'', '\u2018', '\u2019').Trim();
            value = value.Replace("...", string.Empty).Replace("…", string.Empty);
            value = value.Replace(".", string.Empty).Replace("·", string.Empty).Replace("•", string.Empty);
            value = Regex.Replace(value, @"\s+", string.Empty);
            return value.Length == 0;
        }
    }
}
