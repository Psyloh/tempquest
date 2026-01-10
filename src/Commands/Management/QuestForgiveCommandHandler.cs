using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestForgiveCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;

        public QuestForgiveCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem)
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

            bool removed = QuestSystemAdminUtils.ResetQuestForPlayer(questSystem, target, questId, sapi);

            return removed
                ? TextCommandResult.Success($"Quest '{questId}' was reset for '{target.PlayerName}'.")
                : TextCommandResult.Success($"Nothing to reset: '{target.PlayerName}' did not have active quest '{questId}'. Cooldown/completed flags cleared anyway.");
        }
    }
}
