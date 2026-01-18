using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestForgiveAliasCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;
        private readonly string mode;

        public QuestForgiveAliasCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem, string mode)
        {
            this.sapi = sapi;
            this.questSystem = questSystem;
            this.mode = mode;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            var target = ResolveTarget(playerName, args);
            if (target == null)
            {
                return TextCommandResult.Error(string.IsNullOrWhiteSpace(playerName)
                    ? "No player specified and command caller is not a player."
                    : $"Player '{playerName}' not found online.");
            }

            if (string.Equals(mode, "active", StringComparison.OrdinalIgnoreCase))
            {
                bool removedActive = QuestSystemAdminUtils.ForgetActiveQuestForPlayer(questSystem, target, out string activeQuestId);
                return removedActive
                    ? TextCommandResult.Success($"Forgot active quest '{activeQuestId}' for '{target.PlayerName}'.")
                    : TextCommandResult.Success($"Nothing to forget: '{target.PlayerName}' has no active quests.");
            }

            int removedCount = QuestSystemAdminUtils.ResetAllQuestsForPlayer(questSystem, target, sapi);
            return TextCommandResult.Success($"Reset all quests for '{target.PlayerName}'. Removed {removedCount} active quest(s). Cooldowns/completed flags cleared.");
        }

        private IServerPlayer ResolveTarget(string playerName, TextCommandCallingArgs args)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return args.Caller?.Player as IServerPlayer;
            }

            return sapi.World.AllOnlinePlayers
                .FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;
        }
    }
}
