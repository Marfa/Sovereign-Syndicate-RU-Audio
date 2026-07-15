using System;
using System.IO;
using MelonLoader;

namespace SovereignSyndicateVoice
{
    /// <summary>
    /// First-run layout under Mods/SovereignSyndicateVoice: scripts, refs, voice folders.
    /// Heavy venv install stays in install_voice_env.bat (not run from MelonLoader).
    /// </summary>
    internal static class VoiceEnvBootstrap
    {
        private static readonly string[] ScriptFiles =
        {
            "generate_dialogue_batch.py",
            "xtts_audio.py",
            "prepare_voice_refs_piper.py",
        };

        private static readonly string[] ScriptSourceDirs =
        {
            @"C:\Users\HYPERPC\IdeaProjects\Sovereign-Syndicate-RU-Audio\scripts",
            @"C:\Users\HYPERPC\IdeaProjects\Sovereign Syndicate\scripts",
        };

        private const string LegacyRoot = @"C:\Temp\SovereignSyndicateVoice";

        internal static void Run()
        {
            VoicePaths.EnsureLayout();
            SyncScripts();
            MigrateLegacyRefs();
            LogStatus();
        }

        private static void SyncScripts()
        {
            foreach (var file in ScriptFiles)
            {
                var dest = Path.Combine(VoicePaths.ScriptsRoot, file);
                if (File.Exists(dest))
                {
                    continue;
                }

                foreach (var sourceDir in ScriptSourceDirs)
                {
                    var src = Path.Combine(sourceDir, file);
                    if (!File.Exists(src))
                    {
                        continue;
                    }

                    try
                    {
                        File.Copy(src, dest, overwrite: false);
                        MelonLogger.Msg("VO env: installed script " + file);
                        break;
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning("VO env: copy script failed " + file + " — " + ex.Message);
                    }
                }
            }
        }

        private static void MigrateLegacyRefs()
        {
            var legacyRefs = Path.Combine(LegacyRoot, "refs");
            if (!Directory.Exists(legacyRefs))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(legacyRefs, "*_ref.wav"))
            {
                var dest = Path.Combine(VoicePaths.RefsRoot, Path.GetFileName(file));
                if (File.Exists(dest))
                {
                    continue;
                }

                try
                {
                    File.Copy(file, dest, overwrite: false);
                    MelonLogger.Msg("VO env: migrated ref " + Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("VO env: migrate ref failed — " + ex.Message);
                }
            }

            foreach (var file in Directory.GetFiles(legacyRefs, "*_ref.txt"))
            {
                var dest = Path.Combine(VoicePaths.RefsRoot, Path.GetFileName(file));
                if (File.Exists(dest))
                {
                    continue;
                }

                try
                {
                    File.Copy(file, dest, overwrite: false);
                }
                catch
                {
                    // ponytail: text companions are optional
                }
            }
        }

        private static void LogStatus()
        {
            MelonLogger.Msg("VO env root: " + VoicePaths.ModRoot);

            var script = Path.Combine(VoicePaths.ScriptsRoot, "generate_dialogue_batch.py");
            if (!File.Exists(script))
            {
                MelonLogger.Warning(
                    "VO env: scripts missing — run install_voice_mod.bat or copy scripts/ into " +
                    VoicePaths.ScriptsRoot);
            }

            if (!File.Exists(VoicePaths.VenvPython) &&
                !File.Exists(Path.Combine(LegacyRoot, "venv", "Scripts", "python.exe")))
            {
                MelonLogger.Warning(
                    "VO env: XTTS venv missing — run install_voice_env.bat (installs into Mods\\SovereignSyndicateVoice\\venv)");
            }

            var hasRef = File.Exists(Path.Combine(VoicePaths.RefsRoot, "clara_ref.wav")) ||
                         File.Exists(Path.Combine(VoicePaths.RefsRoot, "atticus_ref.wav"));
            if (!hasRef)
            {
                MelonLogger.Warning(
                    "VO env: voice refs missing — run install_voice_env.bat (generates Mods\\...\\refs)");
            }
        }
    }
}
