using System;

namespace VsQuest
{
    public static class PlayerAttributeAdminUtils
    {
        public const string StatSource = "vsquestadmin";
        public const string StatStorePrefix = "vsquestadmin:stat:";
        public const string AttrStorePrefix = "vsquestadmin:attr:";

        public static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return key;

            return key.StartsWith(ItemAttributeUtils.AttrPrefix, StringComparison.OrdinalIgnoreCase)
                ? key.Substring(ItemAttributeUtils.AttrPrefix.Length)
                : key;
        }

        public static string BuildStatStoreKey(string statKey) => $"{StatStorePrefix}{statKey}";

        public static string BuildAttrStoreKey(string attrKey) => $"{AttrStorePrefix}{attrKey}";

        public static bool TryMapToPlayerStat(string shortKey, out string statKey)
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
                    statKey = "healingeffectiveness";
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

        public static string[] GetSupportedKeys()
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

        public static bool IsPlayerAttr(string shortKey)
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
