using System.Collections.Generic;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestTickUtil
    {
        public static void HandleQuestTick(float dt, Dictionary<string, Quest> questRegistry, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, IServerPlayer[] players, System.Func<string, List<ActiveQuest>> getPlayerQuests, ICoreServerAPI sapi)
        {
            foreach (var serverPlayer in players)
            {
                var activeQuests = getPlayerQuests(serverPlayer.PlayerUID);
                foreach (var activeQuest in activeQuests)
                {
                    if (!questRegistry.ContainsKey(activeQuest.questId))
                    {
                        sapi.Logger.Error($"[vsquest] Active quest with id '{activeQuest.questId}' for player '{serverPlayer.PlayerUID}' not found in QuestRegistry. Skipping tick update. This might happen if a quest was removed but player data was not updated.");
                        continue;
                    }
                    var quest = questRegistry[activeQuest.questId];

                    for (int i = 0; i < quest.actionObjectives.Count; i++)
                    {
                        var objective = quest.actionObjectives[i];
                        if (objective.id == "walkdistance")
                        {
                            if (!QuestTimeGateUtil.AllowsProgress(serverPlayer, quest, actionObjectiveRegistry, "tick", objective.objectiveId)) continue;

                            var objectiveImplementation = actionObjectiveRegistry[objective.id] as WalkDistanceObjective;
                            objectiveImplementation?.OnTick(serverPlayer, activeQuest, i, objective.args, sapi, dt);
                        }
                        else if (objective.id == "temporalstorm")
                        {
                            if (!QuestTimeGateUtil.AllowsProgress(serverPlayer, quest, actionObjectiveRegistry, "tick", objective.objectiveId)) continue;

                            var objectiveImplementation = actionObjectiveRegistry[objective.id] as TemporalStormObjective;
                            objectiveImplementation?.OnTick(serverPlayer, activeQuest, objective, sapi);
                        }
                    }
                }
            }
        }
    }
}
