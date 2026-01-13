using System;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class MarkEntityInteractionAction : IQuestAction
    {
        // Args:
        // [0] questId
        // [1] target entity id or entity code (must match objective args[1])
        // [2] objectiveId (must match objective.objectiveId)
        // Optional gate:
        // [3] requiredIntKey (player watched attribute int key)
        // [4] requiredMinValue (int)
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer == null) return;
            if (args == null || args.Length < 3) return;

            string questId = args[0];
            string target = args[1];
            string objectiveId = args[2];

            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(objectiveId)) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var questDef) || questDef?.actionObjectives == null) return;

            var activeQuests = questSystem.GetPlayerQuests(byPlayer.PlayerUID);
            var activeQuest = activeQuests?.Find(q => string.Equals(q.questId, questId, StringComparison.OrdinalIgnoreCase));
            if (activeQuest == null) return;

            // Find matching interactwithentity objective
            ActionWithArgs objective = null;
            for (int i = 0; i < questDef.actionObjectives.Count; i++)
            {
                var ao = questDef.actionObjectives[i];
                if (ao == null) continue;
                if (!string.Equals(ao.id, "interactwithentity", StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(ao.objectiveId, objectiveId, StringComparison.OrdinalIgnoreCase)) continue;
                if (ao.args == null || ao.args.Length < 3) continue;
                if (!string.Equals(ao.args[0], questId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(ao.args[1]?.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

                objective = ao;
                break;
            }

            if (objective == null) return;

            var wa = byPlayer.Entity?.WatchedAttributes;
            if (wa == null) return;

            // Optional gating: only allow counting when player has required int >= min value.
            if (args.Length >= 5 && !string.IsNullOrWhiteSpace(args[3]))
            {
                string requiredKey = args[3];
                if (!int.TryParse(args[4], out int requiredMin))
                {
                    return;
                }

                int have = wa.GetInt(requiredKey, 0);
                if (have < requiredMin)
                {
                    return;
                }
            }

            string key = InteractWithEntityObjective.CountKey(questId, target);
            int cur = wa.GetInt(key, 0);
            wa.SetInt(key, cur + 1);
            wa.MarkPathDirty(key);

            // If this objective is now complete, fire onCompleteActions
            if (questSystem.ActionObjectiveRegistry != null
                && questSystem.ActionObjectiveRegistry.TryGetValue("interactwithentity", out var impl)
                && impl != null
                && impl.IsCompletable(byPlayer, objective.args))
            {
                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, byPlayer, activeQuest, objective, objective.objectiveId, true);
            }
        }
    }
}
