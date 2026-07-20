using System;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace SovereignSyndicateVoice
{
    /// <summary>
    /// Mod runtime (DLL, scripts, refs, prefetch control files) under Mods/SovereignSyndicateVoice.
    /// Generated wav cache lives in C:\Temp\SovereignSyndicateVoice\voice (writable, survives sessions).
    /// </summary>
    internal static class VoicePaths
    {
        internal const string CacheRoot = @"C:\Temp\SovereignSyndicateVoice";

        private static string _modRoot;
        private static bool _resolved;

        internal static string ModRoot
        {
            get
            {
                EnsureResolved();
                return _modRoot;
            }
        }

        internal static string VoiceRoot
        {
            get { return Path.Combine(CacheRoot, "voice"); }
        }

        /// <summary>Optional shipped/session wav under Mods (fallback lookup only).</summary>
        internal static string ModVoiceRoot
        {
            get { return Path.Combine(ModRoot, "voice"); }
        }

        internal static string RefsRoot
        {
            get { return Path.Combine(ModRoot, "refs"); }
        }

        internal static string ScriptsRoot
        {
            get { return Path.Combine(ModRoot, "scripts"); }
        }

        internal static string VenvPython
        {
            get { return Path.Combine(ModRoot, "venv", "Scripts", "python.exe"); }
        }

        internal static string QueuePath
        {
            get { return Path.Combine(ModRoot, "prefetch_queue.tsv"); }
        }

        internal static string PriorityPath
        {
            get { return Path.Combine(ModRoot, "prefetch_priority.tsv"); }
        }

        internal static string LockPath
        {
            get { return Path.Combine(ModRoot, "prefetch.lock"); }
        }

        internal static string LogPath
        {
            get { return Path.Combine(ModRoot, "prefetch.log"); }
        }

        internal static string ShutdownPath
        {
            get { return Path.Combine(ModRoot, "prefetch_shutdown"); }
        }

        internal static string DumpDir
        {
            get { return Path.Combine(ModRoot, "lines_ru"); }
        }

        internal static void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }

            var gameDir = Path.GetDirectoryName(Application.dataPath);
            _modRoot = Path.Combine(gameDir ?? string.Empty, "Mods", "SovereignSyndicateVoice");
            _resolved = true;
        }

        internal static string[] PythonCandidates()
        {
            return new[]
            {
                VenvPython,
                Path.Combine(CacheRoot, "venv", "Scripts", "python.exe"),
                @"C:\Users\HYPERPC\AppData\Local\Programs\Python\Python311\python.exe",
            };
        }

        internal static string[] ScriptCandidates()
        {
            return new[]
            {
                Path.Combine(ScriptsRoot, "generate_dialogue_batch.py"),
                @"C:\Users\HYPERPC\IdeaProjects\Sovereign-Syndicate-RU-Audio\scripts\generate_dialogue_batch.py",
                @"C:\Users\HYPERPC\IdeaProjects\Sovereign Syndicate\scripts\generate_dialogue_batch.py",
                Path.Combine(CacheRoot, "generate_dialogue_batch.py"),
            };
        }

        internal static void EnsureLayout()
        {
            EnsureResolved();
            Directory.CreateDirectory(VoiceRoot);
            Directory.CreateDirectory(Path.Combine(VoiceRoot, "atticus"));
            Directory.CreateDirectory(Path.Combine(VoiceRoot, "clara"));
            Directory.CreateDirectory(Path.Combine(VoiceRoot, "teddy"));
            Directory.CreateDirectory(Path.Combine(VoiceRoot, "otto"));
            Directory.CreateDirectory(RefsRoot);
            Directory.CreateDirectory(ScriptsRoot);
            Directory.CreateDirectory(DumpDir);
        }
    }
}
