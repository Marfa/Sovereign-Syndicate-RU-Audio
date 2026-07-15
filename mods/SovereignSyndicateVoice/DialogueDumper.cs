using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using PixelCrushers.DialogueSystem;

namespace SovereignSyndicateVoice
{
    internal static class DialogueDumper
    {
        private static bool _done;

        private static string DumpDir
        {
            get { return VoicePaths.DumpDir; }
        }

        internal static void TryDump()
        {
            if (_done)
            {
                return;
            }

            var db = DialogueManager.masterDatabase;
            if (db == null)
            {
                return;
            }

            _done = true;
            try
            {
                Dump(DialogueManager.masterDatabase);
                MelonLogger.Msg("Dialogue dump written to " + DumpDir);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Dialogue dump failed: " + ex.Message);
            }
        }

        private static void Dump(DialogueDatabase db)
        {
            Directory.CreateDirectory(DumpDir);
            var actorMap = new Dictionary<int, string>();
            foreach (var actor in db.actors)
            {
                var character = DialogueLineRules.MapActor(actor.Name);
                if (character != null)
                {
                    actorMap[actor.id] = character;
                }
            }

            var grouped = new Dictionary<string, List<string>>();
            foreach (var conversation in db.conversations)
            {
                foreach (var entry in conversation.dialogueEntries)
                {
                    if (!actorMap.TryGetValue(entry.ActorID, out var character))
                    {
                        continue;
                    }

                    if (!DialogueLineRules.ShouldVoice(entry))
                    {
                        continue;
                    }

                    var text = entry.currentLocalizedDialogueText;
                    if (string.IsNullOrEmpty(text))
                    {
                        text = entry.DialogueText;
                    }

                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    if (!grouped.TryGetValue(character, out var lines))
                    {
                        lines = new List<string>();
                        grouped[character] = lines;
                    }

                    lines.Add(DialogueVoiceKeys.Primary(entry, conversation.id) + "\t" + text.Replace("\r\n", " ").Replace("\n", " "));
                }
            }

            foreach (var pair in grouped)
            {
                var path = Path.Combine(DumpDir, "runtime_" + pair.Key + ".tsv");
                var sb = new StringBuilder();
                sb.AppendLine("key\ttext_ru");
                foreach (var line in pair.Value)
                {
                    sb.AppendLine(line);
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                MelonLogger.Msg("Runtime dump " + pair.Key + ": " + pair.Value.Count + " lines");
            }
        }
    }
}
