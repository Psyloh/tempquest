using System;
using System.Globalization;
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

            string shortKey = PlayerAttributeAdminUtils.NormalizeKey(key);

            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fValue))
            {
                return TextCommandResult.Error("Value must be a number (use '.' for decimals).");
            }

            if (PlayerAttributeAdminUtils.IsPlayerAttr(shortKey))
            {
                if (target.Entity == null)
                {
                    return TextCommandResult.Error("Player entity not available.");
                }

                string playerAttrStoreKey = PlayerAttributeAdminUtils.BuildAttrStoreKey(shortKey);
                target.Entity.WatchedAttributes.SetFloat(playerAttrStoreKey, fValue);
                target.Entity.WatchedAttributes.MarkPathDirty(playerAttrStoreKey);
                target.Entity.WatchedAttributes.MarkAllDirty();

                return TextCommandResult.Success($"Set player attribute '{shortKey}' = {fValue.ToString(CultureInfo.InvariantCulture)} for '{target.PlayerName}'.");
            }

            if (!PlayerAttributeAdminUtils.TryMapToPlayerStat(shortKey, out string statKey))
            {
                return TextCommandResult.Error($"Attribute '{shortKey}' is not supported for players. Supported: {string.Join(", ", PlayerAttributeAdminUtils.GetSupportedKeys())}");
            }

            if (target.Entity?.Stats == null)
            {
                return TextCommandResult.Error("Player stats are not available.");
            }

            target.Entity.Stats.Set(statKey, PlayerAttributeAdminUtils.StatSource, fValue, true);
            if (statKey == "walkspeed")
            {
                target.Entity.walkSpeed = target.Entity.Stats.GetBlended("walkspeed");
            }

            string storeKey = PlayerAttributeAdminUtils.BuildStatStoreKey(statKey);
            target.Entity.WatchedAttributes.SetFloat(storeKey, fValue);
            target.Entity.WatchedAttributes.MarkPathDirty(storeKey);
            target.Entity.WatchedAttributes.MarkAllDirty();

            return TextCommandResult.Success($"Set player stat '{statKey}' ({shortKey}) = {fValue.ToString(CultureInfo.InvariantCulture)} for '{target.PlayerName}'.");
        }

    }
}
