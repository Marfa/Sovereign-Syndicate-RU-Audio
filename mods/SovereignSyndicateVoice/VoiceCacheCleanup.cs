using System;
using System.IO;
using MelonLoader;

namespace SovereignSyndicateVoice
{
    internal static class VoiceCacheCleanup
    {
        private static readonly string[] Characters = { "atticus", "clara", "otto" };

        internal static void PurgeSessionWavs(string voiceRoot)
        {
            if (string.IsNullOrEmpty(voiceRoot) || !Directory.Exists(voiceRoot))
            {
                return;
            }

            var removed = 0;
            foreach (var character in Characters)
            {
                var dir = Path.Combine(voiceRoot, character);
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(dir, "*.wav"))
                {
                    try
                    {
                        File.Delete(file);
                        removed++;
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning("VO cleanup failed: " + file + " — " + ex.Message);
                    }
                }
            }

            if (removed > 0)
            {
                MelonLogger.Msg("VO cleanup: removed " + removed + " wav on exit");
            }
        }
    }
}
