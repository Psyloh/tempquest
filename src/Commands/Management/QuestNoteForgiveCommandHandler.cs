using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestNoteForgiveCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public QuestNoteForgiveCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
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

            int removedNotes = QuestSystemAdminUtils.RemoveNoteJournalEntries(target);
            return TextCommandResult.Success($"Removed {removedNotes} note entry(ies) for '{target.PlayerName}'.");
        }
    }
}
