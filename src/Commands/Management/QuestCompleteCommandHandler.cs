using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestCompleteCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;

        public QuestCompleteCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.questSystem = questSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            string questId = (string)args[0];
            string playerName = (string)args[1];

            if (!questSystem.QuestRegistry.ContainsKey(questId))
            {
                return TextCommandResult.Error($"Quest '{questId}' not found.");
            }

            var target = sapi.World.AllOnlinePlayers
                .FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;

            if (target == null)
            {
                return TextCommandResult.Error($"Player '{playerName}' not found online.");
            }

            bool completed = QuestSystemAdminUtils.ForceCompleteQuestForPlayer(questSystem, target, questId, sapi);

            return completed
                ? TextCommandResult.Success($"Quest '{questId}' was force-completed for '{target.PlayerName}'.")
                : TextCommandResult.Error($"Player '{target.PlayerName}' does not have active quest '{questId}'.");
        }
    }
}
