using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestDeathUtil
    {
        public static void HandleEntityDeath(ICoreServerAPI sapi, List<ActiveQuest> quests, EntityPlayer player, Entity killedEntity)
        {
            if (sapi == null || player == null || quests == null) return;

            string killedCode = killedEntity?.Code?.Path;
            var serverPlayer = player.Player as IServerPlayer;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();

            foreach (var quest in quests)
            {
                quest.OnEntityKilled(killedCode, player.Player);

                if (serverPlayer != null)
                {
                    Quest questDef = null;
                    if (questSystem != null) questSystem.QuestRegistry.TryGetValue(quest.questId, out questDef);

                    if (QuestTimeGateUtil.AllowsProgress(serverPlayer, questDef, questSystem?.ActionObjectiveRegistry))
                    {
                        RandomKillQuestUtils.TryHandleKill(sapi, serverPlayer, quest, killedCode);
                    }
                }
            }
        }
    }
}
