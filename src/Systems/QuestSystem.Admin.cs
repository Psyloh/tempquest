using System.Linq;
using Vintagestory.API.Server;

namespace VsQuest
{
    public partial class QuestSystem
    {
        public bool ForgiveQuest(IServerPlayer player, string questId)
        {
            var quests = persistenceManager.GetPlayerQuests(player.PlayerUID);
            var activeQuest = quests.Find(q => q.questId == questId);
            bool removed = false;

            if (activeQuest != null)
            {
                quests.Remove(activeQuest);
                removed = true;
            }

            // Clear per-player cooldown marker
            var key = string.Format("vsquest:lastaccepted-{0}", questId);
            if (player.Entity?.WatchedAttributes != null)
            {
                player.Entity.WatchedAttributes.RemoveAttribute(key);
                player.Entity.WatchedAttributes.MarkPathDirty(key);

                // Clear completion flag
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

            persistenceManager.SavePlayerQuests(player.PlayerUID, quests);
            return removed;
        }
    }
}
