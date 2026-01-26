using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VsQuest
{
    public class ActionItemAttributeResolver
    {
        private readonly Dictionary<string, ActionItem> actionItemRegistry;

        public ActionItemAttributeResolver(Dictionary<string, ActionItem> actionItemRegistry)
        {
            this.actionItemRegistry = actionItemRegistry;
        }

        public bool TryGetActionItemActionsFromAttributes(ITreeAttribute attributes, out List<ItemAction> actions, out string sourceQuestId)
        {
            actions = null;
            sourceQuestId = null;

            if (attributes == null) return false;

            var actionsJson = attributes.GetString(ItemAttributeUtils.ActionItemActionsKey);
            if (string.IsNullOrWhiteSpace(actionsJson)) return false;

            try
            {
                actions = JsonConvert.DeserializeObject<List<ItemAction>>(actionsJson);
            }
            catch
            {
                actions = null;
                return false;
            }

            if (actions == null || actions.Count == 0) return false;

            var modesJson = attributes.GetString(ItemAttributeUtils.ActionItemModesKey);
            if (!string.IsNullOrWhiteSpace(modesJson))
            {
                try
                {
                    var modes = JsonConvert.DeserializeObject<List<ActionItemMode>>(modesJson);
                    if (modes != null && modes.Count > 0)
                    {
                        int modeIndex = attributes.GetInt(ItemAttributeUtils.ActionItemModeIndexKey, 0);
                        if (modeIndex < 0) modeIndex = 0;
                        if (modeIndex >= modes.Count) modeIndex = modes.Count - 1;

                        var mode = modes[modeIndex];
                        if (mode?.actions != null && mode.actions.Count > 0)
                        {
                            actions = mode.actions;
                        }
                    }
                }
                catch
                {
                    // fall back to default actions
                }
            }

            sourceQuestId = attributes.GetString(ItemAttributeUtils.ActionItemSourceQuestKey);
            if (string.IsNullOrWhiteSpace(sourceQuestId)) sourceQuestId = ItemAttributeUtils.ActionItemDefaultSourceQuestId;

            return true;
        }

        public bool EnsureActionItemAttributes(ItemSlot slot)
        {
            if (slot?.Itemstack == null) return false;
            if (slot.Itemstack.Attributes == null) return false;

            if (ItemAttributeUtils.IsActionItem(slot.Itemstack)) return true;

            string actionItemId = slot.Itemstack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
            if (string.IsNullOrWhiteSpace(actionItemId)) return false;
            if (actionItemRegistry == null || actionItemRegistry.Count == 0) return false;

            if (!actionItemRegistry.TryGetValue(actionItemId, out var actionItem)) return false;

            ItemAttributeUtils.ApplyActionItemAttributes(slot.Itemstack, actionItem);
            slot.MarkDirty();
            return true;
        }

        private bool TryGetActionItemByStack(ItemStack stack, out ActionItem actionItem)
        {
            actionItem = null;
            if (stack?.Collectible?.Code == null) return false;
            if (actionItemRegistry == null || actionItemRegistry.Count == 0) return false;

            string code = stack.Collectible.Code.ToString();
            ActionItem found = null;
            bool multipleMatches = false;
            foreach (var entry in actionItemRegistry.Values)
            {
                if (entry?.itemCode == null) continue;
                if (!string.Equals(entry.itemCode, code, StringComparison.OrdinalIgnoreCase)) continue;

                if (found == null)
                {
                    found = entry;
                }
                else
                {
                    multipleMatches = true;
                    break;
                }
            }

            if (multipleMatches)
            {
                // Ambiguous base item mapping: do not auto-apply action item attributes.
                // This avoids converting items to the wrong action item when multiple configs share the same itemCode.
                actionItem = null;
                return false;
            }

            actionItem = found;
            return actionItem != null;
        }
    }
}
