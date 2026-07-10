using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MelonLoader;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace SovereignSyndicateVoice
{
    internal static class VoicePrefetch
    {
        private const string QueuePath = @"C:\Temp\SovereignSyndicateVoice\prefetch_queue.tsv";
        private const string PriorityPath = @"C:\Temp\SovereignSyndicateVoice\prefetch_priority.tsv";
        private const string LockPath = @"C:\Temp\SovereignSyndicateVoice\prefetch.lock";
        private const string LogPath = @"C:\Temp\SovereignSyndicateVoice\prefetch.log";
        private const string PythonScript = @"C:\Users\HYPERPC\IdeaProjects\Sovereign Syndicate\scripts\generate_dialogue_batch.py";
        private const string PythonScriptFallback = @"C:\Temp\SovereignSyndicateVoice\generate_dialogue_batch.py";
        private const int LookaheadLimit = 2;
        private const int BranchPrefetchLimit = 6;
        private const int MenuBranchDepth = 3;
        private const int MaxQueueSize = 20;
        private const int WorkerIdleExitSec = 90;

        internal static void OnConversationStart(Transform actor)
        {
            if (VoiceMod.Player == null)
            {
                return;
            }

            var db = DialogueManager.masterDatabase;
            if (db == null)
            {
                return;
            }

            var conv = db.GetConversation(DialogueManager.lastConversationID);
            if (conv == null)
            {
                return;
            }

            var start = FindStartEntry(conv);
            if (start == null)
            {
                return;
            }

            var lines = CollectBranchMissing(conv, db, start, BranchPrefetchLimit);
            if (lines.Count == 0)
            {
                MelonLogger.Msg("VO prefetch: branch ready for " + conv.Title);
                return;
            }

            WriteQueue(lines);
            MelonLogger.Msg("VO prefetch: " + lines.Count + " on-path for " + conv.Title);
            TryLaunchGenerator(lines.Count);
        }

        internal static void OnLineShown(DialogueEntry entry)
        {
            if (VoiceMod.Player == null || entry == null || entry.id == 0)
            {
                return;
            }

            var title = entry.Title ?? string.Empty;
            if (title.StartsWith("START") || title.StartsWith("Blood"))
            {
                return;
            }

            var db = DialogueManager.masterDatabase;
            if (db == null)
            {
                return;
            }

            var conv = db.GetConversation(DialogueManager.lastConversationID);
            if (conv == null)
            {
                return;
            }

            var upcoming = GetUpcomingMissing(conv, db, entry, LookaheadLimit);
            if (upcoming.Count == 0)
            {
                return;
            }

            for (var i = upcoming.Count - 1; i >= 0; i--)
            {
                WritePriority(upcoming[i]);
            }

            MelonLogger.Msg("VO prefetch ahead: " + string.Join(", ", upcoming.Select(l => l.Key)));
            TryLaunchGenerator(upcoming.Count);
        }

        internal static void OnResponseMenu(Response[] responses)
        {
            if (VoiceMod.Player == null || responses == null || responses.Length == 0)
            {
                return;
            }

            var db = DialogueManager.masterDatabase;
            if (db == null)
            {
                return;
            }

            var conv = db.GetConversation(DialogueManager.lastConversationID);
            if (conv == null)
            {
                return;
            }

            var lines = new List<PrefetchLine>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var response in responses)
            {
                if (response == null || response.destinationEntry == null)
                {
                    continue;
                }

                foreach (var line in CollectBranchMissing(conv, db, response.destinationEntry, MenuBranchDepth))
                {
                    if (seen.Add(line.Key))
                    {
                        lines.Add(line);
                    }
                }
            }

            if (lines.Count == 0)
            {
                return;
            }

            PrependQueue(lines);
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                WritePriority(lines[i]);
            }

            MelonLogger.Msg("VO prefetch menu: " + string.Join(", ", lines.Select(l => l.Key)));
            TryLaunchGenerator(lines.Count);
        }

        internal static void RequestLine(string character, string key, string textRu)
        {
            if (string.IsNullOrEmpty(character) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(textRu))
            {
                return;
            }

            if (VoiceMod.Player.HasModWav(key))
            {
                return;
            }

            var line = new PrefetchLine(character, key, textRu, ParseEntryId(key));
            PrependQueue(new List<PrefetchLine> { line });
            WritePriority(line);
            MelonLogger.Msg("VO prefetch hot: " + key);
            TryLaunchGenerator(1);
        }

        private static List<PrefetchLine> GetUpcomingMissing(
            Conversation conv,
            DialogueDatabase db,
            DialogueEntry afterEntry,
            int maxCount)
        {
            var next = GetSinglePathNext(conv, afterEntry);
            if (next == null)
            {
                return new List<PrefetchLine>();
            }

            return CollectBranchMissing(conv, db, next, maxCount);
        }

        private static List<PrefetchLine> CollectBranchMissing(
            Conversation conv,
            DialogueDatabase db,
            DialogueEntry from,
            int maxCount)
        {
            var actorMap = BuildActorMap(db);
            var lines = new List<PrefetchLine>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entry = from;
            var visited = new HashSet<int>();

            while (entry != null && lines.Count < maxCount)
            {
                if (!visited.Add(entry.id))
                {
                    break;
                }

                var title = entry.Title ?? string.Empty;
                if (!title.StartsWith("START") && !title.StartsWith("Blood"))
                {
                    TryAddVoicedLineIfMissing(actorMap, entry, lines, seenKeys);
                }

                entry = GetSinglePathNext(conv, entry);
            }

            return lines;
        }

        private static DialogueEntry GetSinglePathNext(Conversation conv, DialogueEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            DialogueEntry candidate = null;
            var count = 0;
            foreach (var link in entry.outgoingLinks)
            {
                if (link.destinationConversationID != conv.id)
                {
                    continue;
                }

                var dest = FindEntry(conv, link.destinationDialogueID);
                if (dest == null)
                {
                    continue;
                }

                count++;
                candidate = dest;
                if (count > 1)
                {
                    return null;
                }
            }

            return candidate;
        }

        private static DialogueEntry FindStartEntry(Conversation conv)
        {
            return FindEntry(conv, 0) ??
                   conv.dialogueEntries.FirstOrDefault(e => (e.Title ?? string.Empty).StartsWith("START"));
        }

        private static void TryAddVoicedLineIfMissing(
            Dictionary<int, string> actorMap,
            DialogueEntry entry,
            List<PrefetchLine> lines,
            HashSet<string> seenKeys)
        {
            var key = "e" + entry.id;
            if (seenKeys.Contains(key) || VoiceMod.Player.HasModWav(key))
            {
                return;
            }

            if (!actorMap.TryGetValue(entry.ActorID, out var character))
            {
                return;
            }

            if (!DialogueLineRules.ShouldVoice(entry))
            {
                return;
            }

            var text = entry.currentLocalizedDialogueText;
            if (string.IsNullOrEmpty(text))
            {
                text = entry.DialogueText;
            }

            if (string.IsNullOrEmpty(text) || !ContainsCyrillic(text))
            {
                return;
            }

            if (!seenKeys.Add(key))
            {
                return;
            }

            lines.Add(new PrefetchLine(character, key, text.Replace("\r\n", " ").Replace("\n", " "), entry.id));
        }

        private static DialogueEntry FindEntry(Conversation conv, int entryId)
        {
            foreach (var entry in conv.dialogueEntries)
            {
                if (entry.id == entryId)
                {
                    return entry;
                }
            }

            return null;
        }

        private static int ParseEntryId(string key)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith("e"))
            {
                return -1;
            }

            int id;
            return int.TryParse(key.Substring(1), out id) ? id : -1;
        }

        private static void WriteQueue(List<PrefetchLine> lines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(QueuePath));
            var rows = new List<string> { "character\tkey\ttext_ru" };
            rows.AddRange(TrimLines(lines).Select(l => l.Character + "\t" + l.Key + "\t" + l.Text));
            File.WriteAllLines(QueuePath, rows, new UTF8Encoding(false));
        }

        private static void PrependQueue(List<PrefetchLine> front)
        {
            var merged = new List<PrefetchLine>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in front)
            {
                if (seen.Add(line.Key))
                {
                    merged.Add(line);
                }
            }

            if (File.Exists(QueuePath))
            {
                foreach (var line in ReadQueueLines())
                {
                    if (seen.Add(line.Key))
                    {
                        merged.Add(line);
                    }
                }
            }

            WriteQueue(merged);
        }

        private static List<PrefetchLine> TrimLines(List<PrefetchLine> lines)
        {
            if (lines.Count <= MaxQueueSize)
            {
                return lines;
            }

            return lines.Take(MaxQueueSize).ToList();
        }

        private static List<PrefetchLine> ReadQueueLines()
        {
            var lines = new List<PrefetchLine>();
            if (!File.Exists(QueuePath))
            {
                return lines;
            }

            foreach (var row in File.ReadAllLines(QueuePath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(row) || row.StartsWith("character"))
                {
                    continue;
                }

                var parts = row.Split('\t');
                if (parts.Length < 3)
                {
                    continue;
                }

                lines.Add(new PrefetchLine(parts[0], parts[1], parts[2], ParseEntryId(parts[1])));
            }

            return lines;
        }

        private static void WritePriority(PrefetchLine line)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PriorityPath));
            var rows = new List<string> { "character\tkey\ttext_ru" };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { line.Key };

            if (File.Exists(PriorityPath))
            {
                foreach (var existing in ReadPriorityLines())
                {
                    if (seen.Add(existing.Key))
                    {
                        rows.Add(existing.Character + "\t" + existing.Key + "\t" + existing.Text);
                    }
                }
            }

            rows.Insert(1, line.Character + "\t" + line.Key + "\t" + line.Text);
            File.WriteAllLines(PriorityPath, rows, new UTF8Encoding(false));
        }

        private static List<PrefetchLine> ReadPriorityLines()
        {
            var lines = new List<PrefetchLine>();
            if (!File.Exists(PriorityPath))
            {
                return lines;
            }

            foreach (var row in File.ReadAllLines(PriorityPath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(row) || row.StartsWith("character"))
                {
                    continue;
                }

                var parts = row.Split('\t');
                if (parts.Length < 3)
                {
                    continue;
                }

                lines.Add(new PrefetchLine(parts[0], parts[1], parts[2], ParseEntryId(parts[1])));
            }

            return lines;
        }

        private static Dictionary<int, string> BuildActorMap(DialogueDatabase db)
        {
            var map = new Dictionary<int, string>();
            foreach (var actor in db.actors)
            {
                var character = DialogueLineRules.MapActor(actor.Name);
                if (character != null)
                {
                    map[actor.id] = character;
                }
            }

            return map;
        }

        private static bool ContainsCyrillic(string text)
        {
            foreach (var ch in text)
            {
                if (ch >= '\u0400' && ch <= '\u04FF')
                {
                    return true;
                }
            }

            return false;
        }

        private static void TryLaunchGenerator(int limit)
        {
            if (IsWorkerRunning())
            {
                return;
            }

            if (!HasPendingWork())
            {
                return;
            }

            StartWorker();
        }

        private static bool HasPendingWork()
        {
            foreach (var line in ReadPriorityLines())
            {
                if (!VoiceMod.Player.HasModWav(line.Key))
                {
                    return true;
                }
            }

            foreach (var line in ReadQueueLines())
            {
                if (!VoiceMod.Player.HasModWav(line.Key))
                {
                    return true;
                }
            }

            return false;
        }

        private static void StartWorker()
        {
            var python = @"C:\Temp\SovereignSyndicateVoice\venv\Scripts\python.exe";
            var script = File.Exists(PythonScript) ? PythonScript : PythonScriptFallback;
            if (!File.Exists(python) || !File.Exists(script))
            {
                MelonLogger.Warning("VO prefetch: python/script missing");
                return;
            }

            try
            {
                var outDir = VoiceMod.Player.VoiceRoot;
                var startInfo = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments =
                        "\"" + script + "\" --daemon --queue \"" + QueuePath + "\" --out-dir \"" + outDir +
                        "\" --idle-exit-sec " + WorkerIdleExitSec,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(script),
                };
                Process.Start(startInfo);
                MelonLogger.Msg("VO prefetch: XTTS worker started");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("VO prefetch worker launch failed: " + ex.Message);
            }
        }

        private static bool IsWorkerRunning()
        {
            if (!File.Exists(LockPath))
            {
                return false;
            }

            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(LockPath);
            if (age.TotalMinutes > 45)
            {
                File.Delete(LockPath);
                return false;
            }

            return true;
        }

        private sealed class PrefetchLine
        {
            internal PrefetchLine(string character, string key, string text, int entryId = -1)
            {
                Character = character;
                Key = key;
                Text = text;
                EntryId = entryId;
            }

            internal string Character { get; }
            internal string Key { get; }
            internal string Text { get; }
            internal int EntryId { get; }
        }
    }
}
