using System;
using System.Linq;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestSystemAdminUtils
    {
        private static void ClearPerQuestPlayerState(IServerPlayer player, string questId)
        {
            if (player?.Entity?.WatchedAttributes == null) return;

            string cooldownKey = string.Format("vsquest:lastaccepted-{0}", questId);
            player.Entity.WatchedAttributes.RemoveAttribute(cooldownKey);
            player.Entity.WatchedAttributes.MarkPathDirty(cooldownKey);

            // Clear randkill state (legacy + multi-slot)
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:code");
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:need");
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:have");
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:slots");
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:onprogress");
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:oncomplete");

            // We don't know how many slots were used, just clear a reasonable range
            for (int slot = 0; slot < 16; slot++)
            {
                player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:slot{slot}:code");
                player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:slot{slot}:need");
                player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:slot{slot}:have");
            }
        }

        private static void RemoveQuestFromCompletedList(IServerPlayer player, string questId)
        {
            if (player?.Entity?.WatchedAttributes == null) return;

            var completed = player.Entity.WatchedAttributes.GetStringArray("vsquest:playercompleted", new string[0]);
            if (completed == null || completed.Length == 0) return;

            var filtered = completed.Where(id => id != questId).ToArray();
            if (filtered.Length == completed.Length) return;

            player.Entity.WatchedAttributes.SetStringArray("vsquest:playercompleted", filtered);
            player.Entity.WatchedAttributes.MarkAllDirty();
        }

        public static bool ForceCompleteQuestForPlayer(QuestSystem questSystem, IServerPlayer player, string questId, ICoreServerAPI sapi)
        {
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            var activeQuest = quests.Find(q => q.questId == questId);
            if (activeQuest == null)
            {
                return false;
            }

            var message = new QuestCompletedMessage { questGiverId = activeQuest.questGiverId, questId = questId };
            bool completed = questSystem.ForceCompleteQuestInternal(player, message, sapi);
            if (completed)
            {
                questSystem.SavePlayerQuests(player.PlayerUID, quests);
            }
            return completed;
        }

        public static bool ForceCompleteActiveQuestForPlayer(QuestSystem questSystem, IServerPlayer player, ICoreServerAPI sapi, out string questId)
        {
            questId = null;
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            if (quests == null || quests.Count == 0)
            {
                return false;
            }

            var activeQuest = quests[0];
            if (activeQuest == null || string.IsNullOrEmpty(activeQuest.questId))
            {
                return false;
            }

            questId = activeQuest.questId;
            var message = new QuestCompletedMessage { questGiverId = activeQuest.questGiverId, questId = questId };
            bool completed = questSystem.ForceCompleteQuestInternal(player, message, sapi);
            if (completed)
            {
                questSystem.SavePlayerQuests(player.PlayerUID, quests);
            }
            return completed;
        }

        public static bool ResetQuestForPlayer(QuestSystem questSystem, IServerPlayer player, string questId)
        {
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            var activeQuest = quests.Find(q => q.questId == questId);
            bool removed = false;

            if (activeQuest != null)
            {
                quests.Remove(activeQuest);
                removed = true;
            }

            ClearPerQuestPlayerState(player, questId);
            RemoveQuestFromCompletedList(player, questId);

            questSystem.SavePlayerQuests(player.PlayerUID, quests);
            return removed;
        }

        public static int ResetAllQuestsForPlayer(QuestSystem questSystem, IServerPlayer player)
        {
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            int removedCount = quests?.Count ?? 0;

            if (quests != null)
            {
                quests.Clear();
            }

            if (player.Entity?.WatchedAttributes != null)
            {
                // Clear completed list
                player.Entity.WatchedAttributes.RemoveAttribute("vsquest:playercompleted");
                player.Entity.WatchedAttributes.MarkPathDirty("vsquest:playercompleted");

                // Clear cooldowns and any per-quest state we store on the player
                foreach (var questId in questSystem.QuestRegistry.Keys.ToList())
                {
                    ClearPerQuestPlayerState(player, questId);
                }

                player.Entity.WatchedAttributes.MarkAllDirty();
            }

            questSystem.SavePlayerQuests(player.PlayerUID, quests ?? new System.Collections.Generic.List<ActiveQuest>());
            return removedCount;
        }
    }
}
