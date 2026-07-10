namespace SovereignSyndicateVoice
{
    internal static class VoiceKeyFilter
    {
        internal static bool ShouldPlayFromLocalize(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (key.StartsWith("LOADSCREEN_"))
            {
                return false;
            }

            // CSV keys for in-scene Adventure Creator lines, e.g. S021_PALLADIUM_AC_ATT_ALT_1
            if (key.Length > 3 && key[0] == 'S' && key.Contains("_AC_"))
            {
                return true;
            }

            return false;
        }
    }
}
