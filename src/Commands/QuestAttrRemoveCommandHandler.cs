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

            string shortKey = key.StartsWith(ItemAttributeUtils.AttrPrefix, StringComparison.OrdinalIgnoreCase)
                ? key.Substring(ItemAttributeUtils.AttrPrefix.Length)
                : key;

            if (IsPlayerAttr(shortKey))
            {
                if (target.Entity == null)
                {
                    return TextCommandResult.Error("Player entity not available.");
                }

                string playerAttrStoreKey = $"vsquestadmin:attr:{shortKey}";
                target.Entity.WatchedAttributes.RemoveAttribute(playerAttrStoreKey);
                target.Entity.WatchedAttributes.MarkPathDirty(playerAttrStoreKey);
                target.Entity.WatchedAttributes.MarkAllDirty();

                return TextCommandResult.Success($"Removed player attribute '{shortKey}' for '{target.PlayerName}'.");
            }

            if (!TryMapToPlayerStat(shortKey, out string statKey))
            {
                return TextCommandResult.Error($"Attribute '{shortKey}' is not supported for players. Supported: {string.Join(", ", GetSupportedKeys())}");
            }

            if (target.Entity?.Stats == null)
            {
                return TextCommandResult.Error("Player stats are not available.");
            }

            target.Entity.Stats.Set(statKey, "vsquestadmin", 0f, true);
            if (statKey == "walkspeed")
            {
                target.Entity.walkSpeed = target.Entity.Stats.GetBlended("walkspeed");
            }

            string storeKey = $"vsquestadmin:stat:{statKey}";
            target.Entity.WatchedAttributes.RemoveAttribute(storeKey);
            target.Entity.WatchedAttributes.MarkPathDirty(storeKey);
            target.Entity.WatchedAttributes.MarkAllDirty();

            return TextCommandResult.Success($"Removed player stat '{statKey}' ({shortKey}) for '{target.PlayerName}'.");
        }

        private static bool TryMapToPlayerStat(string shortKey, out string statKey)
        {
            statKey = null;
            if (string.IsNullOrEmpty(shortKey)) return false;

            switch (shortKey)
            {
                case ItemAttributeUtils.AttrWalkSpeed:
                    statKey = "walkspeed";
                    return true;
                case ItemAttributeUtils.AttrHungerRate:
                    statKey = "hungerrate";
                    return true;
                case ItemAttributeUtils.AttrHealingEffectiveness:
                    statKey = "healingeffectivness";
                    return true;
                case ItemAttributeUtils.AttrRangedAccuracy:
                    statKey = "rangedWeaponsAcc";
                    return true;
                case ItemAttributeUtils.AttrRangedSpeed:
                    statKey = "rangedWeaponsSpeed";
                    return true;
                default:
                    return false;
            }
        }

        private static string[] GetSupportedKeys()
        {
            return new[]
            {
                ItemAttributeUtils.AttrAttackPower,
                ItemAttributeUtils.AttrWarmth,
                ItemAttributeUtils.AttrProtection,
                ItemAttributeUtils.AttrProtectionPerc,
                ItemAttributeUtils.AttrWalkSpeed,
                ItemAttributeUtils.AttrHungerRate,
                ItemAttributeUtils.AttrHealingEffectiveness,
                ItemAttributeUtils.AttrRangedAccuracy,
                ItemAttributeUtils.AttrRangedSpeed
            };
        }

        private static bool IsPlayerAttr(string shortKey)
        {
            switch (shortKey)
            {
                case ItemAttributeUtils.AttrAttackPower:
                case ItemAttributeUtils.AttrWarmth:
                case ItemAttributeUtils.AttrProtection:
                case ItemAttributeUtils.AttrProtectionPerc:
                    return true;
            }

            return false;
        }
    }
}
