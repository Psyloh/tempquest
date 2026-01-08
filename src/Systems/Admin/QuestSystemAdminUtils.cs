using System;
using System.Linq;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestSystemAdminUtils
    {
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

            var key = string.Format("vsquest:lastaccepted-{0}", questId);
            if (player.Entity?.WatchedAttributes != null)
            {
                player.Entity.WatchedAttributes.RemoveAttribute(key);
                player.Entity.WatchedAttributes.MarkPathDirty(key);

                var completed = player.Entity.WatchedAttributes.GetStringArray("vsquest:playercompleted", new string[0]);
                if (completed != null && completed.Length > 0)
                {
                    var filtered = completed.Where(id => id != questId).ToArray();
                    if (filtered.Length != completed.Length)
                    {
                        player.Entity.WatchedAttributes.SetStringArray("vsquest:playercompleted", filtered);
                        player.Entity.WatchedAttributes.MarkAllDirty();
                    }
                }
            }

            questSystem.SavePlayerQuests(player.PlayerUID, quests);
            return removed;
        }
    }
}
