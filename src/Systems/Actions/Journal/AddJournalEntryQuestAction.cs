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
            if (args.Length < 3)
            {
                throw new QuestException("The 'addjournalentry' action requires at least 3 arguments: loreCode, title, and at least one chapter.");
            }

            var modJournal = sapi.ModLoader.GetModSystem<ModJournal>();
            if (modJournal == null)
            {
                throw new QuestException("ModJournal system not found.");
            }

            var loreCode = args[0];
            var title = LocalizationUtils.GetSafe(args[1]);

            // Build incoming chapters (dedupe empty)
            var incomingChapters = new List<JournalChapter>();
            for (int i = 2; i < args.Length; i++)
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

            int entryId = 0;
            if (player?.Entity?.WatchedAttributes != null && !string.IsNullOrWhiteSpace(loreCode))
            {
                string idKey = $"alegacyvsquest:journal:entryid:{loreCode}";
                int storedId = player.Entity.WatchedAttributes.GetInt(idKey, -1);
                if (storedId >= 0)
                {
                    entryId = storedId;
                }
                else
                {
                    string nextKey = "alegacyvsquest:journal:nextentryid";
                    int nextId = player.Entity.WatchedAttributes.GetInt(nextKey, 0);
                    if (nextId < 0) nextId = 0;

                    entryId = nextId;
                    player.Entity.WatchedAttributes.SetInt(nextKey, nextId + 1);
                    player.Entity.WatchedAttributes.MarkPathDirty(nextKey);

                    player.Entity.WatchedAttributes.SetInt(idKey, entryId);
                    player.Entity.WatchedAttributes.MarkPathDirty(idKey);
                }
            }

            if (!string.IsNullOrWhiteSpace(message?.questId) && player?.Entity?.WatchedAttributes != null && !string.IsNullOrWhiteSpace(loreCode))
            {
                string key = $"alegacyvsquest:journal:{message.questId}:lorecodes";
                var loreCodes = player.Entity.WatchedAttributes.GetStringArray(key, new string[0]).ToList();
                if (!loreCodes.Contains(loreCode, StringComparer.OrdinalIgnoreCase))
                {
                    loreCodes.Add(loreCode);
                    player.Entity.WatchedAttributes.SetStringArray(key, loreCodes.ToArray());
                    player.Entity.WatchedAttributes.MarkPathDirty(key);
                }
            }

            bool changed = false;
            bool foundExisting = false;

            // Try to append to existing entry if present
            try
            {
                var t = modJournal.GetType();
                var journalsField = t.GetField("journalsByPlayerUid", BindingFlags.Instance | BindingFlags.NonPublic);
                var channelField = t.GetField("serverChannel", BindingFlags.Instance | BindingFlags.NonPublic);

                var journals = journalsField?.GetValue(modJournal) as Dictionary<string, Journal>;
                var serverChannel = channelField?.GetValue(modJournal) as IServerNetworkChannel;

                if (journals != null && serverChannel != null
                    && journals.TryGetValue(player.PlayerUID, out var journal)
                    && journal?.Entries != null)
                {
                    var existing = journal.Entries.FirstOrDefault(e => e != null && string.Equals(e.LoreCode, loreCode, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        foundExisting = true;

                        if (!string.IsNullOrWhiteSpace(title) && !string.Equals(existing.Title, title, StringComparison.Ordinal))
                        {
                            existing.Title = title;
                            changed = true;
                        }

                        if (existing.Chapters == null)
                        {
                            existing.Chapters = new List<JournalChapter>();
                            changed = true;
                        }

                        var existingTexts = new HashSet<string>(
                            existing.Chapters.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Text)).Select(c => c.Text),
                            StringComparer.Ordinal
                        );

                        foreach (var ch in incomingChapters)
                        {
                            if (ch == null || string.IsNullOrWhiteSpace(ch.Text)) continue;
                            if (existingTexts.Contains(ch.Text)) continue;

                            ch.EntryId = existing.EntryId;
                            existing.Chapters.Add(ch);
                            existingTexts.Add(ch.Text);
                            changed = true;
                        }

                        if (changed)
                        {
                            // Resync full journal to the client
                            serverChannel.SendPacket(journal, player);
                        }
                    }
                }
            }
            catch
            {
                // Fall back to old behavior below
            }

            // If the entry already exists and nothing new was added, do nothing (no spam)
            if (foundExisting && !changed)
            {
                return;
            }

            if (!changed)
            {
                // Fallback: create/update entry via ModJournal (may overwrite chapters)
                var journalEntry = new JournalEntry()
                {
                    Title = title,
                    LoreCode = loreCode,
                    Chapters = incomingChapters,
                    EntryId = entryId
                };

                modJournal.AddOrUpdateJournalEntry(player, journalEntry);
                changed = true;
            }

            if (!changed) return;

            sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, LocalizationUtils.GetSafe("alegacyvsquest:journal-updated"), EnumChatType.Notification);

            try
            {
                sapi.World.PlaySoundFor(new AssetLocation("sounds/effect/writing"), player);
            }
            catch (Exception e)
            {
                sapi.Logger.Warning($"[vsquest] Could not play sound 'sounds/effect/writing' for journal update in quest '{message?.questId}': {e.Message}");
            }
        }
    }
}
