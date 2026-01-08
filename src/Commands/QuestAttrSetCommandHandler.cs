using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestAttrSetCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public QuestAttrSetCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string key = (string)args[1];
            string value = (string)args[2];

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

            target.Entity.WatchedAttributes.SetString(key, value ?? "");
            target.Entity.WatchedAttributes.MarkPathDirty(key);

            return TextCommandResult.Success($"Set attribute '{key}'='{value}' for '{target.PlayerName}'.");
        }
    }
}
