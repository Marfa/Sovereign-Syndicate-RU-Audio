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

            var low = name.ToLowerInvariant();
            if (low.Contains("atticus") || low.Contains("daley") || low.Contains("daily"))
            {
                return "atticus";
            }

            if (low.Contains("clara"))
            {
                return "clara";
            }

            if (low.Contains("otto") || low.Contains("teddy") || low.Contains("ted"))
            {
                return "otto";
            }

            return null;
        }
    }
}
