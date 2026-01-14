using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Newtonsoft.Json;

namespace VsQuest
{
    public static class ItemAttributeUtils
    {
        public const string AttrPrefix = "alegacyvsquest:attr:";

        public const string ActionItemActionsKey = "alegacyvsquest:actions";
        public const string ActionItemIdKey = "alegacyvsquest:actionitemid";
        public const string ActionItemSourceQuestKey = "alegacyvsquest:sourcequest";
        public const string ActionItemDefaultSourceQuestId = "item-action";
        public const string ActionItemTriggerOnInvAddKey = "alegacyvsquest:triggerOnInvAdd";
        public const string ActionItemBlockMoveKey = "alegacyvsquest:blockMove";     // restrict movement (hotbar-only)
        public const string ActionItemBlockEquipKey = "alegacyvsquest:blockEquip";   // restrict equipping (character slots)
        public const string ActionItemBlockDropKey = "alegacyvsquest:blockDrop";     // restrict manual drop
        public const string ActionItemBlockDeathKey = "alegacyvsquest:blockDeath";   // restrict drop on death
        public const string ActionItemBlockGroundStorageKey = "alegacyvsquest:blockGroundStorage"; // restrict Shift+RightClick ground storage placement
        public const string ActionItemShowAttrsKey = "alegacyvsquest:showAttrs";
        public const string ActionItemHideVanillaKey = "alegacyvsquest:hideVanilla";

        public const string ItemizerNameKey = "itemizerName";
        public const string ItemizerDescKey = "itemizerDesc";

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

        public static bool IsActionItem(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            var actions = stack.Attributes.GetString(ActionItemActionsKey);
            return !string.IsNullOrWhiteSpace(actions);
        }

        public static bool IsActionItemBlockedMove(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            return stack.Attributes.GetBool(ActionItemBlockMoveKey, false) && IsActionItem(stack);
        }

        public static bool IsActionItemBlockedEquip(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            return stack.Attributes.GetBool(ActionItemBlockEquipKey, false) && IsActionItem(stack);
        }

        public static bool IsActionItemBlockedDrop(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            return stack.Attributes.GetBool(ActionItemBlockDropKey, false) && IsActionItem(stack);
        }

        public static bool IsActionItemBlockedDeath(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            return stack.Attributes.GetBool(ActionItemBlockDeathKey, false) && IsActionItem(stack);
        }

        public static bool IsActionItemBlockedGroundStorage(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            return stack.Attributes.GetBool(ActionItemBlockGroundStorageKey, false) && IsActionItem(stack);
        }

        public static bool TryResolveCollectible(ICoreAPI api, string itemCode, out CollectibleObject collectible)
        {
            collectible = null;
            if (api?.World == null) return false;
            if (string.IsNullOrWhiteSpace(itemCode)) return false;

            collectible = api.World.GetItem(new AssetLocation(itemCode));
            if (collectible == null)
            {
                collectible = api.World.GetBlock(new AssetLocation(itemCode));
            }

            return collectible != null && !collectible.IsMissing;
        }

        public static void ApplyActionItemAttributes(ItemStack stack, ActionItem actionItem)
        {
            if (stack == null || actionItem == null) return;

            if (stack.Attributes == null) return;

            if (!string.IsNullOrWhiteSpace(actionItem.name))
            {
                stack.Attributes.SetString(ItemizerNameKey, actionItem.name);
            }
            if (!string.IsNullOrWhiteSpace(actionItem.description))
            {
                stack.Attributes.SetString(ItemizerDescKey, actionItem.description);
            }

            stack.Attributes.SetString(ActionItemActionsKey, JsonConvert.SerializeObject(actionItem.actions));

            if (!string.IsNullOrWhiteSpace(actionItem.id))
            {
                stack.Attributes.SetString(ActionItemIdKey, actionItem.id);
            }

            if (!string.IsNullOrWhiteSpace(actionItem.sourceQuestId))
            {
                stack.Attributes.SetString(ActionItemSourceQuestKey, actionItem.sourceQuestId);
            }

            if (actionItem.triggerOnInventoryAdd)
            {
                stack.Attributes.SetBool(ActionItemTriggerOnInvAddKey, true);
            }

            if (actionItem.blockMove)
            {
                stack.Attributes.SetBool(ActionItemBlockMoveKey, true);
            }

            if (actionItem.blockEquip)
            {
                stack.Attributes.SetBool(ActionItemBlockEquipKey, true);
            }

            if (actionItem.blockDrop)
            {
                stack.Attributes.SetBool(ActionItemBlockDropKey, true);
            }

            if (actionItem.blockDeath)
            {
                stack.Attributes.SetBool(ActionItemBlockDeathKey, true);
            }

            if (actionItem.blockGroundStorage)
            {
                stack.Attributes.SetBool(ActionItemBlockGroundStorageKey, true);
            }

            if (actionItem.attributes != null)
            {
                foreach (var attr in actionItem.attributes)
                {
                    stack.Attributes.SetFloat(GetKey(attr.Key), attr.Value);
                }
            }

            if (actionItem.showAttributes != null && actionItem.showAttributes.Count > 0)
            {
                stack.Attributes.SetString(ActionItemShowAttrsKey, JsonConvert.SerializeObject(actionItem.showAttributes));
            }

            if (actionItem.hideVanillaTooltips != null && actionItem.hideVanillaTooltips.Count > 0)
            {
                stack.Attributes.SetString(ActionItemHideVanillaKey, JsonConvert.SerializeObject(actionItem.hideVanillaTooltips));
            }
        }
    }
}
