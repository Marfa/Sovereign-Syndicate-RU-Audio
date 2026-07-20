using System;
using System.IO;
using System.Text;
using MelonLoader;

namespace SovereignSyndicateVoice
{
    /// <summary>
    /// Mod settings under Mods/SovereignSyndicateVoice/settings.ini (created on first launch).
    /// </summary>
    internal static class VoiceSettings
    {
        private const string FileName = "settings.ini";

        /// <summary>When true, purge voice/{character}/*.wav on application quit. Default: true.</summary>
        internal static bool DeleteWavOnExit { get; private set; } = true;

        internal static string SettingsPath
        {
            get { return Path.Combine(VoicePaths.ModRoot, FileName); }
        }

        internal static void LoadOrCreate()
        {
            VoicePaths.EnsureLayout();
            var path = SettingsPath;
            if (!File.Exists(path))
            {
                WriteDefaults(path);
                DeleteWavOnExit = true;
                MelonLogger.Msg("VO settings: first launch — created " + path);
                MelonLogger.Msg("VO settings: delete_wav_on_exit=true (default). Set false to keep wav cache.");
                return;
            }

            try
            {
                DeleteWavOnExit = true;
                foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                    {
                        continue;
                    }

                    var eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }

                    var key = line.Substring(0, eq).Trim();
                    var value = line.Substring(eq + 1).Trim();
                    if (key.Equals("delete_wav_on_exit", StringComparison.OrdinalIgnoreCase))
                    {
                        DeleteWavOnExit = ParseBool(value, defaultValue: true);
                    }
                }

                MelonLogger.Msg(
                    "VO settings: delete_wav_on_exit=" + (DeleteWavOnExit ? "true" : "false") +
                    " (" + path + ")");
            }
            catch (Exception ex)
            {
                DeleteWavOnExit = true;
                MelonLogger.Warning("VO settings: read failed — " + ex.Message + "; using delete_wav_on_exit=true");
            }
        }

        private static void WriteDefaults(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Sovereign Syndicate Voice — settings (created on first launch)");
            sb.AppendLine("# Edit this file, then restart the game.");
            sb.AppendLine("#");
            sb.AppendLine("# delete_wav_on_exit=true  — remove C:\\Temp\\SovereignSyndicateVoice\\voice on exit");
            sb.AppendLine("# delete_wav_on_exit=false — keep generated wav cache between sessions (default cache path)");
            sb.AppendLine("# Mods/.../voice is always cleared on exit (legacy/session folder only)");
            sb.AppendLine("delete_wav_on_exit=true");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static bool ParseBool(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            value = value.Trim().Trim('"').ToLowerInvariant();
            if (value == "1" || value == "true" || value == "yes" || value == "on")
            {
                return true;
            }

            if (value == "0" || value == "false" || value == "no" || value == "off")
            {
                return false;
            }

            return defaultValue;
        }
    }
}
