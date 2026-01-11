using System.Collections.Generic;
using System.Linq;
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

                    // killnear objectives
                    if (questDef?.actionObjectives != null)
                    {
                        for (int i = 0; i < questDef.actionObjectives.Count; i++)
                        {
                            var ao = questDef.actionObjectives[i];
                            if (ao == null) continue;
                            if (ao.id != "killnear") continue;
                            if (ao.args == null || ao.args.Length < 6) continue;
                            if (string.IsNullOrWhiteSpace(ao.objectiveId)) continue;

                            if (!KillNearObjective.TryParseArgs(ao.args, out string questIdArg, out string objectiveIdArg, out int x, out int y, out int z, out double radius, out string mobCode, out int need))
                            {
                                continue;
                            }

                            if (!string.Equals(questIdArg, quest.questId, System.StringComparison.OrdinalIgnoreCase)) continue;

                            if (!QuestTimeGateUtil.AllowsProgress(serverPlayer, questDef, questSystem?.ActionObjectiveRegistry, "kill", ao.objectiveId)) continue;

                            // Check mob code
                            if (!string.IsNullOrWhiteSpace(mobCode) && mobCode != "*" && !LocalizationUtils.MobCodeMatches(mobCode, killedCode)) continue;

                            // Check distance from killed entity to target point
                            var pos = killedEntity?.Pos;
                            if (pos == null) continue;

                            double dx = pos.X - x;
                            double dy = pos.Y - y;
                            double dz = pos.Z - z;
                            if ((dx * dx + dy * dy + dz * dz) > radius * radius) continue;

                            var wa = serverPlayer.Entity?.WatchedAttributes;
                            if (wa == null) continue;

                            string haveKey = KillNearObjective.HaveKey(quest.questId, ao.objectiveId);
                            int have = wa.GetInt(haveKey, 0);
                            if (have < need)
                            {
                                have++;
                                wa.SetInt(haveKey, have);
                                wa.MarkPathDirty(haveKey);
                            }
                        }
                    }

                    string killObjectiveId = null;
                    if (questDef?.actionObjectives != null)
                    {
                        for (int i = 0; i < questDef.actionObjectives.Count; i++)
                        {
                            var ao = questDef.actionObjectives[i];
                            if (ao == null) continue;
                            if (ao.id != "randomkill") continue;
                            if (string.IsNullOrWhiteSpace(ao.objectiveId)) continue;

                            killObjectiveId = ao.objectiveId;
                            break;
                        }
                    }

                    if (QuestTimeGateUtil.AllowsProgress(serverPlayer, questDef, questSystem?.ActionObjectiveRegistry, "kill", killObjectiveId))
                    {
                        RandomKillQuestUtils.TryHandleKill(sapi, serverPlayer, quest, killedCode);
                    }
                }
            }
        }
    }
}
