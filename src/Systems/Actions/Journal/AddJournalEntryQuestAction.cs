using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class AddJournalEntryQuestAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                throw new QuestException("The 'addjournalentry' action requires at least 2 arguments.");
            }

            // Supported formats:
            // - Legacy: addjournalentry <loreCode> <chapter...>
            // - Legacy: addjournalentry <loreCode> <title> <chapter...>
            // - New:    addjournalentry <groupId> <loreCode> <title> <chapter...>
            string groupId = null;
            string loreCode;
            string title;
            bool overwriteMode = false;
            int chapterStartIndex;

            if (args.Length >= 4)
            {
                groupId = args[0];
                loreCode = args[1];
                title = LocalizationUtils.GetSafe(args[2]);

                if (args.Length >= 5 && string.Equals(args[3], "overwrite", StringComparison.OrdinalIgnoreCase))
                {
                    overwriteMode = true;
                    chapterStartIndex = 4;
                }
                else
                {
                    chapterStartIndex = 3;
                }
            }
            else if (args.Length >= 3)
            {
                loreCode = args[0];
                title = LocalizationUtils.GetSafe(args[1]);
                chapterStartIndex = 2;
            }
            else
            {
                loreCode = args[0];
                title = loreCode;
                chapterStartIndex = 1;
            }

            // Build incoming chapters (dedupe empty)
            var incomingChapters = new List<JournalChapter>();
            for (int i = chapterStartIndex; i < args.Length; i++)
            {
                var txt = LocalizationUtils.GetSafe(args[i]);
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    incomingChapters.Add(new JournalChapter() { Text = txt });
                }
            }

            if (incomingChapters.Count == 0)
            {
                return;
            }

            if (player?.Entity?.WatchedAttributes == null) return;

            var wa = player.Entity.WatchedAttributes;
            var entries = QuestJournalEntry.Load(wa);

            QuestJournalEntry entry = null;
            foreach (var existing in entries)
            {
                if (existing == null) continue;
                if (string.Equals(existing.LoreCode, loreCode, StringComparison.OrdinalIgnoreCase))
                {
                    entry = existing;
                    break;
                }
            }

            bool changed = false;
            string resolvedQuestId = !string.IsNullOrWhiteSpace(groupId)
                ? groupId
                : message?.questId;

            if (string.IsNullOrWhiteSpace(resolvedQuestId)
                || string.Equals(resolvedQuestId, ItemAttributeUtils.ActionItemDefaultSourceQuestId, StringComparison.OrdinalIgnoreCase))
            {
                resolvedQuestId = loreCode;
            }

            if (!string.IsNullOrWhiteSpace(resolvedQuestId) && !string.IsNullOrWhiteSpace(loreCode))
            {
                string key = $"alegacyvsquest:journal:{resolvedQuestId}:lorecodes";
                var loreCodes = wa.GetStringArray(key, new string[0]).ToList();
                if (!loreCodes.Contains(loreCode, StringComparer.OrdinalIgnoreCase))
                {
                    loreCodes.Add(loreCode);
                    wa.SetStringArray(key, loreCodes.ToArray());
                    wa.MarkPathDirty(key);
                }
            }

            if (entry == null)
            {
                entry = new QuestJournalEntry
                {
                    QuestId = resolvedQuestId,
                    LoreCode = loreCode,
                    Title = title,
                    Chapters = new List<string>()
                };
                entries.Add(entry);
                changed = true;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(entry.QuestId) && !string.IsNullOrWhiteSpace(resolvedQuestId))
                {
                    entry.QuestId = resolvedQuestId;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(title) && !string.Equals(entry.Title, title, StringComparison.Ordinal))
            {
                entry.Title = title;
                changed = true;
            }

            if (entry.Chapters == null)
            {
                entry.Chapters = new List<string>();
                changed = true;
            }

            if (overwriteMode && entry.Chapters.Count == 1 && incomingChapters.Count == 1)
            {
                string newText = incomingChapters[0]?.Text;
                if (!string.IsNullOrWhiteSpace(newText) && !string.Equals(entry.Chapters[0], newText, StringComparison.Ordinal))
                {
                    entry.Chapters[0] = newText;
                    changed = true;
                }
            }
            else
            {

                var existingTexts = new HashSet<string>(entry.Chapters.Where(c => !string.IsNullOrWhiteSpace(c)), StringComparer.Ordinal);
                foreach (var ch in incomingChapters)
                {
                    if (ch == null || string.IsNullOrWhiteSpace(ch.Text)) continue;
                    if (existingTexts.Contains(ch.Text)) continue;

                    entry.Chapters.Add(ch.Text);
                    existingTexts.Add(ch.Text);
                    changed = true;
                }
            }

            if (changed)
            {
                // Treat updated entries as "newest" for UI ordering by moving them to the end.
                // This way the journal can auto-select the most recently updated entry.
                if (entry != null)
                {
                    entries.Remove(entry);
                    entries.Add(entry);
                }

                QuestJournalEntry.Save(wa, entries);
                wa.MarkPathDirty(QuestJournalEntry.JournalEntriesKey);
            }

            sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, LocalizationUtils.GetSafe("alegacyvsquest:journal-updated"), EnumChatType.Notification);

            try
            {
                sapi.World.PlaySoundFor(new AssetLocation("sounds/effect/writing"), player);
            }
            catch (Exception e)
            {
                sapi.Logger.Warning($"[alegacyvsquest] Could not play sound 'sounds/effect/writing' for journal update in quest '{message?.questId}': {e.Message}");
            }
        }
    }
}
