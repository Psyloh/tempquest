using Vintagestory.API.Server;

namespace VsQuest
{
    public class CheckObjectiveAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (!questSystem.QuestRegistry.TryGetValue(message.questId, out var quest))
            {
                return;
            }

            var activeQuests = questSystem.GetPlayerQuests(byPlayer.PlayerUID);
            var activeQuest = activeQuests.Find(q => q.questId == message.questId);
            if (activeQuest == null) return;

            for (int i = 0; i < quest.actionObjectives.Count; i++)
            {                
                var objective = quest.actionObjectives[i];
                if (objective.id == "checkvariable")
                {
                    if (questSystem.ActionObjectiveRegistry.TryGetValue(objective.id, out var objectiveImplementation) && objectiveImplementation is CheckVariableObjective checkVariableObjective)
                    {
                        checkVariableObjective.CheckAndFire(byPlayer, quest, activeQuest, i, sapi);
                    }
                }
            }
        }
    }
}
