using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class ActionItemPacketHandler
    {
        private readonly QuestSystem questSystem;
        private readonly ActionItemAttributeResolver attributeResolver;
        private readonly ActionItemActionExecutor actionExecutor;

        public ActionItemPacketHandler(QuestSystem questSystem, ActionItemAttributeResolver attributeResolver, ActionItemActionExecutor actionExecutor)
        {
            this.questSystem = questSystem;
            this.attributeResolver = attributeResolver;
            this.actionExecutor = actionExecutor;
        }

        public void HandlePacket(IServerPlayer fromPlayer, ExecuteActionItemPacket packet)
        {
            if (fromPlayer == null) return;
            var slot = fromPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null) return;
            if (attributeResolver == null) return;

            if (!attributeResolver.EnsureActionItemAttributes(slot)) return;

            var attributes = slot.Itemstack.Attributes;
            if (!attributeResolver.TryGetActionItemActionsFromAttributes(attributes, out var actions, out string sourceQuestId)) return;

            bool triggerOnInvAdd = attributes.GetBool(ItemAttributeUtils.ActionItemTriggerOnInvAddKey, false);

            string actionItemId = attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
            var wa = fromPlayer?.Entity?.WatchedAttributes;
            bool enforceOnce = triggerOnInvAdd
                && !string.IsNullOrWhiteSpace(actionItemId)
                && wa != null
                && (string.IsNullOrWhiteSpace(sourceQuestId)
                    || string.Equals(sourceQuestId, ItemAttributeUtils.ActionItemDefaultSourceQuestId, StringComparison.OrdinalIgnoreCase)
                    || questSystem?.QuestRegistry?.ContainsKey(sourceQuestId) != true);
            string onceKey = enforceOnce ? $"alegacyvsquest:itemaction:invadd:{actionItemId}" : null;
            if (enforceOnce && wa.GetBool(onceKey, false))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(sourceQuestId)
                && !string.Equals(sourceQuestId, ItemAttributeUtils.ActionItemDefaultSourceQuestId, StringComparison.OrdinalIgnoreCase)
                && questSystem?.QuestRegistry?.ContainsKey(sourceQuestId) == true)
            {
                var active = questSystem.GetPlayerQuests(fromPlayer.PlayerUID);
                bool isActive = active != null && active.Exists(q => q != null && string.Equals(q.questId, sourceQuestId, StringComparison.OrdinalIgnoreCase));
                if (!isActive)
                {
                    return;
                }
            }

            actionExecutor?.Execute(fromPlayer, attributes, actions, sourceQuestId);

            if (enforceOnce)
            {
                wa.SetBool(onceKey, true);
                wa.MarkPathDirty(onceKey);
            }
        }
    }
}
