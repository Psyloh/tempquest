using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public static class QuestJournalMigration
    {
        public static int MigrateFromVanilla(ICoreServerAPI sapi, IServerPlayer player, string lorePrefix = "alegacyvsquest")
        {
            if (sapi == null || player == null) return 0;
            if (player.Entity?.WatchedAttributes == null) return 0;

            var modJournal = sapi.ModLoader.GetModSystem<ModJournal>();
            if (modJournal == null) return 0;

            var t = modJournal.GetType();
            var journalsField = t.GetField("journalsByPlayerUid", BindingFlags.Instance | BindingFlags.NonPublic);

            var journals = journalsField?.GetValue(modJournal) as Dictionary<string, Journal>;
            if (journals == null) return 0;

            if (!journals.TryGetValue(player.PlayerUID, out var journal) || journal?.Entries == null) return 0;

            var wa = player.Entity.WatchedAttributes;
            var entries = QuestJournalEntry.Load(wa);
            bool changed = false;
            int importedCount = 0;

            foreach (var legacyEntry in journal.Entries)
            {
                if (legacyEntry == null) continue;
                if (string.IsNullOrWhiteSpace(legacyEntry.LoreCode)) continue;
                if (!legacyEntry.LoreCode.StartsWith(lorePrefix, StringComparison.OrdinalIgnoreCase)) continue;

                var existing = entries.FirstOrDefault(e => e != null && string.Equals(e.LoreCode, legacyEntry.LoreCode, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    existing = new QuestJournalEntry
                    {
                        LoreCode = legacyEntry.LoreCode,
                        Title = legacyEntry.Title,
                        Chapters = new List<string>()
                    };
                    entries.Add(existing);
                    changed = true;
                    importedCount++;
                }

                if (!string.IsNullOrWhiteSpace(legacyEntry.Title) && !string.Equals(existing.Title, legacyEntry.Title, StringComparison.Ordinal))
                {
                    existing.Title = legacyEntry.Title;
                    changed = true;
                }

                if (existing.Chapters == null)
                {
                    existing.Chapters = new List<string>();
                    changed = true;
                }

                var chapterTexts = legacyEntry.Chapters?.Select(c => c?.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (chapterTexts != null && chapterTexts.Count > 0)
                {
                    var existingTexts = new HashSet<string>(existing.Chapters.Where(c => !string.IsNullOrWhiteSpace(c)), StringComparer.Ordinal);
                    foreach (var text in chapterTexts)
                    {
                        if (existingTexts.Contains(text)) continue;
                        existing.Chapters.Add(text);
                        existingTexts.Add(text);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                QuestJournalEntry.Save(wa, entries);
                wa.MarkPathDirty(QuestJournalEntry.JournalEntriesKey);
            }

            return importedCount;
        }
    }
}
