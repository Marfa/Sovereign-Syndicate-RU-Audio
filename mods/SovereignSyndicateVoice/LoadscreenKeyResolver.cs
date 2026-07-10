using System.IO;
using AC;

namespace SovereignSyndicateVoice
{
    internal static class LoadscreenKeyResolver
    {
        internal static string FromLineId(int lineId, AC.Char character)
        {
            if (KickStarter.speechManager == null)
            {
                return null;
            }

            var line = KickStarter.speechManager.GetLine(lineId);
            if (line != null && IsLoadscreenKey(line.customFilename))
            {
                return NormalizeKey(line.customFilename);
            }

            foreach (var lang in new[] { "English", "Original", "Default" })
            {
                var filename = KickStarter.speechManager.GetLineFilename(lineId, lang);
                var key = NormalizeKey(filename);
                if (IsLoadscreenKey(key))
                {
                    return key;
                }
            }

            if (line != null)
            {
                var assetPath = KickStarter.speechManager.GetAutoAssetPathAndName(
                    lineId,
                    character,
                    "English",
                    false);
                var fromAsset = NormalizeKey(assetPath);
                if (IsLoadscreenKey(fromAsset))
                {
                    return fromAsset;
                }
            }

            return null;
        }

        private static bool IsLoadscreenKey(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("LOADSCREEN_");
        }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            var key = Path.GetFileNameWithoutExtension(value.Trim());
            return key.Replace('\\', '/').Split('/')[0];
        }
    }
}
