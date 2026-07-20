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

        internal static void Run()
        {
            VoicePaths.EnsureLayout();
            SyncScripts();
            MigrateLegacyRefs();
            MigrateModVoiceToCache();
            MigrateTeddyFromSharedOtto();
            LogStatus();
        }

        /// <summary>Move wav cache out of Program Files into C:\Temp\SovereignSyndicateVoice\voice.</summary>
        private static void MigrateModVoiceToCache()
        {
            var modVoice = VoicePaths.ModVoiceRoot;
            var cacheVoice = VoicePaths.VoiceRoot;
            if (!Directory.Exists(modVoice))
            {
                return;
            }

            var moved = 0;
            try
            {
                foreach (var characterDir in Directory.GetDirectories(modVoice))
                {
                    var character = Path.GetFileName(characterDir);
                    var destDir = Path.Combine(cacheVoice, character);
                    Directory.CreateDirectory(destDir);

                    foreach (var src in Directory.GetFiles(characterDir, "*.wav"))
                    {
                        var dest = Path.Combine(destDir, Path.GetFileName(src));
                        if (File.Exists(dest))
                        {
                            continue;
                        }

                        File.Move(src, dest);
                        moved++;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("VO env: mod voice migrate failed — " + ex.Message);
            }

            if (moved > 0)
            {
                MelonLogger.Msg("VO env: moved " + moved + " wav(s) Mods/voice → " + cacheVoice);
            }
        }

        /// <summary>
        /// v0.5.20: Teddy and Otto used to share voice/otto + otto_ref (denis).
        /// Move Teddy cache/ref aside, leave otto for the new ruslan automaton voice.
        /// </summary>
        private static void MigrateTeddyFromSharedOtto()
        {
            try
            {
                var teddyRef = Path.Combine(VoicePaths.RefsRoot, "teddy_ref.wav");
                var ottoRef = Path.Combine(VoicePaths.RefsRoot, "otto_ref.wav");
                if (!File.Exists(teddyRef) && File.Exists(ottoRef))
                {
                    File.Copy(ottoRef, teddyRef, overwrite: false);
                    var ottoTxt = Path.Combine(VoicePaths.RefsRoot, "otto_ref.txt");
                    var teddyTxt = Path.Combine(VoicePaths.RefsRoot, "teddy_ref.txt");
                    if (File.Exists(ottoTxt) && !File.Exists(teddyTxt))
                    {
                        File.Copy(ottoTxt, teddyTxt, overwrite: false);
                    }

                    MelonLogger.Msg("VO env: cloned otto_ref → teddy_ref (Teddy keeps denis)");
                }

                var ottoDir = Path.Combine(VoicePaths.VoiceRoot, "otto");
                var teddyDir = Path.Combine(VoicePaths.VoiceRoot, "teddy");
                Directory.CreateDirectory(teddyDir);
                if (!Directory.Exists(ottoDir))
                {
                    return;
                }

                var ottoWavs = Directory.GetFiles(ottoDir, "*.wav");
                var teddyWavs = Directory.GetFiles(teddyDir, "*.wav");
                if (ottoWavs.Length == 0 || teddyWavs.Length > 0)
                {
                    return;
                }

                var moved = 0;
                foreach (var src in ottoWavs)
                {
                    var dest = Path.Combine(teddyDir, Path.GetFileName(src));
                    if (File.Exists(dest))
                    {
                        continue;
                    }

                    File.Move(src, dest);
                    moved++;
                }

                if (moved > 0)
                {
                    MelonLogger.Msg("VO env: moved " + moved + " wav(s) otto/ → teddy/ (Teddy cache)");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("VO env: teddy/otto migrate failed — " + ex.Message);
            }
        }

        private static void SyncScripts()
        {
            foreach (var file in ScriptFiles)
            {
                var dest = Path.Combine(VoicePaths.ScriptsRoot, file);
                foreach (var sourceDir in ScriptSourceDirs)
                {
                    var src = Path.Combine(sourceDir, file);
                    if (!File.Exists(src))
                    {
                        continue;
                    }

                    try
                    {
                        var missing = !File.Exists(dest);
                        var newer = !missing && File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dest);
                        if (!missing && !newer)
                        {
                            break;
                        }

                        File.Copy(src, dest, overwrite: true);
                        MelonLogger.Msg("VO env: " + (missing ? "installed" : "updated") + " script " + file);
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
            var legacyRefs = Path.Combine(VoicePaths.CacheRoot, "refs");
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
                !File.Exists(Path.Combine(VoicePaths.CacheRoot, "venv", "Scripts", "python.exe")))
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
