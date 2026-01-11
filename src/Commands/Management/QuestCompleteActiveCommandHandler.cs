using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestCompleteActiveCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;

        public QuestCompleteActiveCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.questSystem = questSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];

            IServerPlayer target;

            if (string.IsNullOrWhiteSpace(playerName))
            {
                target = args.Caller?.Player as IServerPlayer;
                if (target == null)
                {
                    return TextCommandResult.Error("No player specified and command caller is not a player.");
                }
            }
            else
            {
                target = sapi.World.AllOnlinePlayers
                    .FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;

                if (target == null)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found online.");
                }
            }

            bool completed = QuestSystemAdminUtils.ForceCompleteActiveQuestForPlayer(questSystem, target, sapi, out string questId);

            if (!completed)
            {
                return TextCommandResult.Success($"Nothing to complete: '{target.PlayerName}' has no active quests.");
            }

            return TextCommandResult.Success($"Completed active quest '{questId}' for '{target.PlayerName}'.");
        }
    }
}
