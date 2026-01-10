using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Newtonsoft.Json;

namespace VsQuest
{
    public static class ItemAttributeUtils
    {
        public const string AttrPrefix = "alegacyvsquest:attr:";
        public const string AttrAttackPower = "attackpower";
        public const string AttrWarmth = "warmth";
        public const string AttrProtection = "protection";
        public const string AttrProtectionPerc = "protectionperc";
        public const string AttrWalkSpeed = "walkspeed";
        public const string AttrHungerRate = "hungerrate";
        public const string AttrHealingEffectiveness = "healingeffectiveness";
        public const string AttrRangedAccuracy = "rangedaccuracy";
        public const string AttrRangedSpeed = "rangedchargspeed";

        public static string GetKey(string attributeName)
        {
            return AttrPrefix + attributeName;
        }

        public static float GetAttributeFloat(ItemStack stack, string attributeName, float defaultValue = 0f)
        {
            if (stack == null || stack.Attributes == null) return defaultValue;

            string key = GetKey(attributeName);
            if (stack.Attributes.HasAttribute(key))
            {
                return stack.Attributes.GetFloat(key, defaultValue);
            }

            return defaultValue;
        }

        public static string GetDisplayName(string shortKey)
        {
            string langKey = $"alegacyvsquest:attr-{shortKey}";
            string result = Lang.Get(langKey);

            if (result == langKey)
            {
                return shortKey;
            }
            return result;
        }

        public static string FormatAttributeForTooltip(string attrKey, float value)
        {
            string shortKey = attrKey.StartsWith(AttrPrefix) ? attrKey.Substring(AttrPrefix.Length) : attrKey;
            string displayName = GetDisplayName(shortKey);

            string prefix = value >= 0 ? "+" : "";

            if (shortKey == AttrProtectionPerc || shortKey == AttrWalkSpeed ||
                shortKey == AttrHungerRate || shortKey == AttrHealingEffectiveness ||
                shortKey == AttrRangedAccuracy || shortKey == AttrRangedSpeed)
            {
                return $"{displayName}: {prefix}{value * 100:0.#}%";
            }
            else if (shortKey == AttrWarmth)
            {
                return $"{displayName}: {prefix}{value:0.#}°C";
            }
            else if (shortKey == AttrAttackPower)
            {
                return $"{displayName}: {prefix}{value:0.#} hp";
            }
            else if (shortKey == AttrProtection)
            {
                return $"{displayName}: {prefix}{value:0.#} dmg";
            }

            return $"{displayName}: {prefix}{value:0.##}";
        }

        public static void ApplyActionItemAttributes(ItemStack stack, ActionItem actionItem)
        {
            if (stack == null || actionItem == null) return;

            stack.Attributes.SetString("itemizerName", actionItem.name);
            stack.Attributes.SetString("itemizerDesc", actionItem.description);
            stack.Attributes.SetString("alegacyvsquest:actions", JsonConvert.SerializeObject(actionItem.actions));

            if (actionItem.attributes != null)
            {
                foreach (var attr in actionItem.attributes)
                {
                    stack.Attributes.SetFloat(GetKey(attr.Key), attr.Value);
                }
            }

            if (actionItem.showAttributes != null && actionItem.showAttributes.Count > 0)
            {
                stack.Attributes.SetString("alegacyvsquest:showAttrs", JsonConvert.SerializeObject(actionItem.showAttributes));
            }

            if (actionItem.hideVanillaTooltips != null && actionItem.hideVanillaTooltips.Count > 0)
            {
                stack.Attributes.SetString("alegacyvsquest:hideVanilla", JsonConvert.SerializeObject(actionItem.hideVanillaTooltips));
            }
        }
    }
}
