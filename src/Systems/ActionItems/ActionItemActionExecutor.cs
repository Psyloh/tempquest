using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class ActionItemActionExecutor
    {
        private readonly QuestSystem questSystem;
        private readonly ICoreServerAPI sapi;

        public ActionItemActionExecutor(QuestSystem questSystem, ICoreServerAPI sapi)
        {
            this.questSystem = questSystem;
            this.sapi = sapi;
        }

        public bool Execute(IServerPlayer player, ITreeAttribute attributes, List<ItemAction> actions, string sourceQuestId)
        {
            if (player == null || actions == null || actions.Count == 0) return false;

            bool executed = false;
            foreach (var action in actions)
            {
                if (action == null) continue;
                if (questSystem.ActionRegistry.TryGetValue(action.id, out var registeredAction))
                {
                    var message = new QuestAcceptedMessage { questGiverId = player.Entity.EntityId, questId = sourceQuestId };
                    registeredAction.Execute(sapi, message, player, action.args);
                    executed = true;
                }
            }

            return executed;
        }
    }
}
