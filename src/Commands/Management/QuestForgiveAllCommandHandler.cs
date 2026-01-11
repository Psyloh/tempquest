using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestForgiveAllCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;

        public QuestForgiveAllCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem)
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

            int removedCount = QuestSystemAdminUtils.ResetAllQuestsForPlayer(questSystem, target, sapi);

            return TextCommandResult.Success($"Reset all quests for '{target.PlayerName}'. Removed {removedCount} active quest(s). Cooldowns/completed flags cleared.");
        }
    }
}
