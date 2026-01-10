using System;
using System.Collections.Generic;
using System.Linq;
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

            var chapters = new List<JournalChapter>();
            for (int i = 2; i < args.Length; i++)
            {
                chapters.Add(new JournalChapter() { Text = LocalizationUtils.GetSafe(args[i]) });
            }

            var journalEntry = new JournalEntry()
            {
                Title = title,
                LoreCode = loreCode,
                Chapters = chapters,
                EntryId = entryId
            };

            modJournal.AddOrUpdateJournalEntry(player, journalEntry);
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
