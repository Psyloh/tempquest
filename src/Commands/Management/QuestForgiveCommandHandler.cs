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
            string modeOrQuestId = (string)args[0];
            string playerName = (string)args[1];

            if (string.IsNullOrWhiteSpace(modeOrQuestId)
                || string.Equals(modeOrQuestId, "help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(modeOrQuestId, "?", StringComparison.OrdinalIgnoreCase))
            {
                return TextCommandResult.Success(
                    "Forgive command usage:\n" +
                    "/vsq qforgive <questId> <playerName> - reset one quest\n" +
                    "/vsq qforgive all [playerName] - reset all quests\n" +
                    "/vsq qforgive notes [playerName] - remove all notes\n" +
                    "/vsq qforgive active [playerName] - forget active quest\n" +
                    "\nExamples:\n" +
                    "/vsq qforgive albase:bosshunt-ossuarywarden PlayerName\n" +
                    "/vsq qforgive notes PlayerName"
                );
            }

            if (string.Equals(modeOrQuestId, "all", StringComparison.OrdinalIgnoreCase)
                || string.Equals(modeOrQuestId, "notes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(modeOrQuestId, "active", StringComparison.OrdinalIgnoreCase))
            {
                var target = ResolveTarget(playerName, args);
                if (target == null)
                {
                    return TextCommandResult.Error(string.IsNullOrWhiteSpace(playerName)
                        ? "No player specified and command caller is not a player."
                        : $"Player '{playerName}' not found online.");
                }

                if (string.Equals(modeOrQuestId, "notes", StringComparison.OrdinalIgnoreCase))
                {
                    int removedNotes = QuestSystemAdminUtils.RemoveNoteJournalEntries(target);
                    return TextCommandResult.Success($"Removed {removedNotes} note entry(ies) for '{target.PlayerName}'.");
                }

                if (string.Equals(modeOrQuestId, "active", StringComparison.OrdinalIgnoreCase))
                {
                    bool removedActive = QuestSystemAdminUtils.ForgetActiveQuestForPlayer(questSystem, target, out string activeQuestId);
                    return removedActive
                        ? TextCommandResult.Success($"Forgot active quest '{activeQuestId}' for '{target.PlayerName}'.")
                        : TextCommandResult.Success($"Nothing to forget: '{target.PlayerName}' has no active quests.");
                }

                int removedCount = QuestSystemAdminUtils.ResetAllQuestsForPlayer(questSystem, target, sapi);
                return TextCommandResult.Success($"Reset all quests for '{target.PlayerName}'. Removed {removedCount} active quest(s). Cooldowns/completed flags cleared.");
            }

            string questId = modeOrQuestId;
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return TextCommandResult.Error("Player name is required when forgiving a specific quest.");
            }

            if (!questSystem.QuestRegistry.ContainsKey(questId))
            {
                return TextCommandResult.Error($"Quest '{questId}' not found.");
            }

            var targetQuest = ResolveTarget(playerName, args);
            if (targetQuest == null)
            {
                return TextCommandResult.Error($"Player '{playerName}' not found online.");
            }

            bool removed = QuestSystemAdminUtils.ResetQuestForPlayer(questSystem, targetQuest, questId, sapi);

            return removed
                ? TextCommandResult.Success($"Quest '{questId}' was reset for '{targetQuest.PlayerName}'.")
                : TextCommandResult.Success($"Nothing to reset: '{targetQuest.PlayerName}' did not have active quest '{questId}'. Cooldown/completed flags cleared anyway.");
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
