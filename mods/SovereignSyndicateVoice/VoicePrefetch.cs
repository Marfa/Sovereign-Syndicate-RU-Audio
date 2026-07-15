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
        private static string QueuePath { get { return VoicePaths.QueuePath; } }
        private static string PriorityPath { get { return VoicePaths.PriorityPath; } }
        private static string LockPath { get { return VoicePaths.LockPath; } }
        private static string ShutdownPath { get { return VoicePaths.ShutdownPath; } }
        private const int LookaheadLimit = 1;
        private const int BranchPrefetchLimit = 3;
        private const int MenuBranchDepth = 2;
        private const int MaxQueueSize = 12;
        private const int WorkerIdleExitSec = 90;
        private const float WorkerShutdownDelaySec = 60f;
        private const float ConversationDebounceSec = 0.5f;

        private static int _lastStartConvId = -1;
        private static float _lastStartTime;
        private static float _lastEndTime;
        private static float _shutdownAt = -1f;
        private static Process _workerProcess;
        private static bool _launchInProgress;
        private static float _launchStartedAt;
        private static string _lastMenuSignature = string.Empty;
        private static float _lastMenuTime;

        internal static void OnConversationStart(Transform actor)
        {
            if (VoiceMod.Player == null)
            {
                return;
            }

            var convId = DialogueManager.lastConversationID;
            if (convId == _lastStartConvId && Time.time - _lastStartTime < ConversationDebounceSec)
            {
                return;
            }

            _lastStartConvId = convId;
            _lastStartTime = Time.time;

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

            var menuSignature = conv.id + ":" + string.Join(",", lines.Select(l => l.Key).OrderBy(k => k));
            if (menuSignature == _lastMenuSignature && Time.time - _lastMenuTime < ConversationDebounceSec)
            {
                return;
            }

            _lastMenuSignature = menuSignature;
            _lastMenuTime = Time.time;

            PrependQueue(lines);
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                WritePriority(lines[i]);
            }

            MelonLogger.Msg("VO prefetch menu: " + string.Join(", ", lines.Select(l => l.Key)));
        }

        internal static void RequestLine(string character, string key, string textRu)
        {
            if (string.IsNullOrEmpty(character) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(textRu))
            {
                return;
            }

            if (VoiceText.IsNonVerbalPause(textRu))
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
            EnsureWarmWorker(force: true);
        }

        internal static void OnSceneLoaded()
        {
            ForceShutdown("scene load");
        }

        internal static void OnConversationEnd()
        {
            if (Time.time - _lastEndTime < ConversationDebounceSec)
            {
                return;
            }

            _lastEndTime = Time.time;
            ClearPrefetchFiles();
            ScheduleWorkerShutdown();
            MelonLogger.Msg("VO prefetch: worker shutdown scheduled (" + WorkerShutdownDelaySec + "s)");
        }

        internal static void ForceShutdown(string reason)
        {
            _shutdownAt = -1f;
            _launchInProgress = false;
            if (!IsWorkerRunning())
            {
                ClearWorkerLock();
                return;
            }

            ClearPrefetchFiles();
            RequestWorkerShutdown();
            StopWorkerProcess();
            MelonLogger.Msg("VO prefetch: worker stopped (" + reason + ")");
        }

        internal static void Tick()
        {
            if (_shutdownAt < 0f || Time.time < _shutdownAt)
            {
                return;
            }

            _shutdownAt = -1f;
            if (!IsWorkerRunning() && (_workerProcess == null || _workerProcess.HasExited))
            {
                return;
            }

            ForceShutdown("idle timeout");
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
                    TryAddVoicedLineIfMissing(conv.id, actorMap, entry, lines, seenKeys);
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
            int conversationId,
            Dictionary<int, string> actorMap,
            DialogueEntry entry,
            List<PrefetchLine> lines,
            HashSet<string> seenKeys)
        {
            var key = DialogueVoiceKeys.Primary(entry, conversationId);
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

            if (string.IsNullOrEmpty(text) || !ContainsCyrillic(text) || VoiceText.IsNonVerbalPause(text))
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

        private static void EnsureWarmWorker(bool force = false)
        {
            CancelScheduledShutdown();
            if (IsWorkerRunning())
            {
                if (_workerProcess != null && _workerProcess.HasExited)
                {
                    ClearWorkerLock();
                    _workerProcess = null;
                }
                else
                {
                    return;
                }
            }

            if (!force && !HasPendingWork())
            {
                return;
            }

            StartDaemonWorker();
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

        private static void CancelScheduledShutdown()
        {
            _shutdownAt = -1f;
            ClearShutdownFlag();
        }

        private static void ScheduleWorkerShutdown()
        {
            _shutdownAt = Time.time + WorkerShutdownDelaySec;
        }

        private static void ClearPrefetchFiles()
        {
            try
            {
                if (File.Exists(QueuePath))
                {
                    File.Delete(QueuePath);
                }

                if (File.Exists(PriorityPath))
                {
                    File.Delete(PriorityPath);
                }
            }
            catch
            {
                // ponytail: worker may still drain in-memory pending once
            }
        }

        private static void ClearWorkerLock()
        {
            try
            {
                if (File.Exists(LockPath))
                {
                    File.Delete(LockPath);
                }
            }
            catch
            {
                // ponytail: stale lock cleared on next launch
            }
        }

        private static void StopWorkerProcess()
        {
            try
            {
                if (_workerProcess != null && !_workerProcess.HasExited)
                {
                    KillProcess(_workerProcess);
                }
                else if (TryReadLockPid(out var pid) && IsProcessAlive(pid))
                {
                    KillProcess(Process.GetProcessById(pid));
                }
            }
            catch
            {
                // ponytail: lock cleanup below still unblocks relaunch
            }
            finally
            {
                _workerProcess = null;
                _launchInProgress = false;
                ClearWorkerLock();
                ClearShutdownFlag();
            }
        }

        private static void KillProcess(Process process)
        {
            if (process == null || process.HasExited)
            {
                return;
            }

            if (!process.WaitForExit(3000))
            {
                process.Kill();
                process.WaitForExit(2000);
            }
        }

        private static void ClearShutdownFlag()
        {
            try
            {
                if (File.Exists(ShutdownPath))
                {
                    File.Delete(ShutdownPath);
                }
            }
            catch
            {
                // ponytail: best-effort; daemon also clears on start
            }
        }

        private static void RequestWorkerShutdown()
        {
            try
            {
                File.WriteAllText(ShutdownPath, "1", Encoding.UTF8);
            }
            catch
            {
                // ponytail: idle exit still releases memory if write fails
            }
        }

        private static void StartDaemonWorker()
        {
            if (_launchInProgress && Time.time - _launchStartedAt < 30f)
            {
                return;
            }

            if (IsWorkerRunning())
            {
                return;
            }

            var python = ResolveExistingPath(VoicePaths.PythonCandidates());
            var script = ResolveExistingPath(VoicePaths.ScriptCandidates());
            if (python == null || script == null)
            {
                MelonLogger.Warning(
                    "VO prefetch: missing " +
                    (python == null ? "python (" + VoicePaths.VenvPython + ")" : "") +
                    (python == null && script == null ? "; " : "") +
                    (script == null ? "script (" + Path.Combine(VoicePaths.ScriptsRoot, "generate_dialogue_batch.py") + ")" : ""));
                return;
            }

            _launchInProgress = true;
            _launchStartedAt = Time.time;

            try
            {
                Directory.CreateDirectory(VoicePaths.ModRoot);
                File.WriteAllText(LockPath, "starting", Encoding.UTF8);
                var outDir = VoicePaths.VoiceRoot;
                var startInfo = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments =
                        "\"" + script + "\" --daemon --queue \"" + QueuePath +
                        "\" --priority \"" + PriorityPath +
                        "\" --out-dir \"" + outDir +
                        "\" --refs-dir \"" + VoicePaths.RefsRoot +
                        "\" --mod-root \"" + VoicePaths.ModRoot +
                        "\" --idle-exit-sec " + WorkerIdleExitSec,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(script),
                };
                _workerProcess = Process.Start(startInfo);
                MelonLogger.Msg("VO prefetch: XTTS warm worker started");
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(LockPath))
                    {
                        File.Delete(LockPath);
                    }
                }
                catch
                {
                    // ponytail: stale starting lock cleared on next launch
                }

                MelonLogger.Warning("VO prefetch worker launch failed: " + ex.Message);
            }
            finally
            {
                _launchInProgress = false;
            }
        }

        private static bool TryReadLockPid(out int pid)
        {
            pid = 0;
            if (!File.Exists(LockPath))
            {
                return false;
            }

            var text = File.ReadAllText(LockPath, Encoding.UTF8).Trim();
            if (!text.StartsWith("pid:", StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(text.Substring(4), out pid) && pid > 0;
        }

        private static bool IsProcessAlive(int pid)
        {
            if (pid <= 0)
            {
                return false;
            }

            try
            {
                var process = Process.GetProcessById(pid);
                return process != null && !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWorkerRunning()
        {
            if (_workerProcess != null && !_workerProcess.HasExited)
            {
                return true;
            }

            if (TryReadLockPid(out var pid) && IsProcessAlive(pid))
            {
                return true;
            }

            if (File.Exists(LockPath))
            {
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(LockPath);
                if (age.TotalSeconds > 45f && string.Equals(
                        File.ReadAllText(LockPath, Encoding.UTF8).Trim(),
                        "starting",
                        StringComparison.Ordinal))
                {
                    ClearWorkerLock();
                    MelonLogger.Msg("VO prefetch: cleared abandoned worker lock");
                }
            }

            return false;
        }

        private static string ResolveExistingPath(string[] candidates)
        {
            foreach (var path in candidates)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
            }

            return null;
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
