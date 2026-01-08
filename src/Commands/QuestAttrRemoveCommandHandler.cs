using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestAttrRemoveCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public QuestAttrRemoveCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string key = (string)args[1];

            var target = sapi.World.AllOnlinePlayers
                .FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;

            if (target == null)
            {
                return TextCommandResult.Error($"Player '{playerName}' not found online.");
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                return TextCommandResult.Error("Key must not be empty.");
            }

            target.Entity.WatchedAttributes.RemoveAttribute(key);
            target.Entity.WatchedAttributes.MarkPathDirty(key);

            return TextCommandResult.Success($"Removed attribute '{key}' for '{target.PlayerName}'.");
        }
    }
}
