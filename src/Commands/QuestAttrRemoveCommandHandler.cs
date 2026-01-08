using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VsQuest.Util;

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

            string shortKey = PlayerAttributeAdminUtils.NormalizeKey(key);

            if (PlayerAttributeAdminUtils.IsPlayerAttr(shortKey))
            {
                if (target.Entity == null)
                {
                    return TextCommandResult.Error("Player entity not available.");
                }

                string playerAttrStoreKey = PlayerAttributeAdminUtils.BuildAttrStoreKey(shortKey);
                target.Entity.WatchedAttributes.RemoveAttribute(playerAttrStoreKey);
                target.Entity.WatchedAttributes.MarkPathDirty(playerAttrStoreKey);
                target.Entity.WatchedAttributes.MarkAllDirty();

                return TextCommandResult.Success($"Removed player attribute '{shortKey}' for '{target.PlayerName}'.");
            }

            if (!PlayerAttributeAdminUtils.TryMapToPlayerStat(shortKey, out string statKey))
            {
                return TextCommandResult.Error($"Attribute '{shortKey}' is not supported for players. Supported: {string.Join(", ", PlayerAttributeAdminUtils.GetSupportedKeys())}");
            }

            if (target.Entity?.Stats == null)
            {
                return TextCommandResult.Error("Player stats are not available.");
            }

            target.Entity.Stats.Set(statKey, PlayerAttributeAdminUtils.StatSource, 0f, true);
            if (statKey == "walkspeed")
            {
                target.Entity.walkSpeed = target.Entity.Stats.GetBlended("walkspeed");
            }

            string storeKey = PlayerAttributeAdminUtils.BuildStatStoreKey(statKey);
            target.Entity.WatchedAttributes.RemoveAttribute(storeKey);
            target.Entity.WatchedAttributes.MarkPathDirty(storeKey);
            target.Entity.WatchedAttributes.MarkAllDirty();

            return TextCommandResult.Success($"Removed player stat '{statKey}' ({shortKey}) for '{target.PlayerName}'.");
        }

    }
}
