using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class ActionItemInventoryScanner
    {
        private const int MaxSlotsPerTick = 64;

        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;
        private readonly global::System.Func<IServerPlayer, ITreeAttribute, List<ItemAction>, string, bool> onActionItem;
        private readonly Dictionary<string, (string invKey, int slot)> inventoryScanCursorByPlayerUid;
        private readonly ActionItemAttributeResolver attributeResolver;

        public ActionItemInventoryScanner(
            ICoreServerAPI sapi,
            QuestSystem questSystem,
            ActionItemAttributeResolver attributeResolver,
            Dictionary<string, (string invKey, int slot)> inventoryScanCursorByPlayerUid,
            global::System.Func<IServerPlayer, ITreeAttribute, List<ItemAction>, string, bool> onActionItem)
        {
            this.sapi = sapi;
            this.questSystem = questSystem;
            this.attributeResolver = attributeResolver;
            this.inventoryScanCursorByPlayerUid = inventoryScanCursorByPlayerUid;
            this.onActionItem = onActionItem;
        }

        public void Tick(float dt)
        {
            if (sapi == null || questSystem == null) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            foreach (var p in players)
            {
                if (!(p is IServerPlayer sp)) continue;
                var inv = sp?.InventoryManager;
                if (inv == null) continue;

                string uid = sp.PlayerUID;
                if (string.IsNullOrWhiteSpace(uid)) continue;

                int scanned = 0;

                inventoryScanCursorByPlayerUid.TryGetValue(uid, out var cursor);
                string resumeInvKey = cursor.invKey;
                int resumeSlot = cursor.slot;
                if (resumeSlot < 0) resumeSlot = 0;

                bool resuming = !string.IsNullOrWhiteSpace(resumeInvKey);
                bool resumeInvReached = !resuming;

                // Walk all inventories/slots; trigger only for stacks that request auto-processing.
                foreach (var iinv in inv.Inventories)
                {
                    // If we have a resume inventory key, skip inventories until we reach it.
                    if (!resumeInvReached)
                    {
                        if (!string.Equals(iinv.Key, resumeInvKey, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        resumeInvReached = true;
                    }

                    var inventory = iinv.Value;
                    if (inventory == null) continue;

                    if (inventory.ClassName == GlobalConstants.creativeInvClassName) continue;

                    int slotCount;
                    try
                    {
                        slotCount = inventory.Count;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (slotCount <= 0) continue;

                    int startSlot = (resuming && string.Equals(iinv.Key, resumeInvKey, StringComparison.Ordinal)) ? resumeSlot : 0;
                    if (startSlot < 0) startSlot = 0;
                    if (startSlot >= slotCount) startSlot = 0;

                    for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                    {
                        if (slotIndex < startSlot) continue;

                        var slot = inventory[slotIndex];
                        var stack = slot?.Itemstack;
                        if (stack?.Attributes == null) continue;

                        // Ensure action item attributes are present before checking auto-trigger flags.
                        // Otherwise newly obtained action items (with triggerOnInventoryAdd=true in config)
                        // would never get processed.
                        attributeResolver?.EnsureActionItemAttributes(slot);

                        if (!stack.Attributes.GetBool(ItemAttributeUtils.ActionItemTriggerOnInvAddKey, false)) continue;

                        string actionItemId = stack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
                        if (string.IsNullOrWhiteSpace(actionItemId)) continue;

                        string onceKey = $"alegacyvsquest:itemaction:invadd:{actionItemId}";
                        var wa = sp?.Entity?.WatchedAttributes;
                        if (wa == null) break;
                        if (wa.GetBool(onceKey, false)) continue;

                        if (!attributeResolver.TryGetActionItemActionsFromAttributes(stack.Attributes, out var actions, out string sourceQuestId))
                        {
                            continue;
                        }

                        // If this action item is tied to a quest, only auto-trigger it while that quest is active.
                        // This prevents pre-collecting quest items and consuming their one-time trigger too early.
                        if (!string.Equals(sourceQuestId, ItemAttributeUtils.ActionItemDefaultSourceQuestId, StringComparison.OrdinalIgnoreCase)
                            && questSystem?.QuestRegistry?.ContainsKey(sourceQuestId) == true)
                        {
                            var active = questSystem.GetPlayerQuests(sp.PlayerUID);
                            bool isActive = active != null && active.Exists(q => q != null && string.Equals(q.questId, sourceQuestId, StringComparison.OrdinalIgnoreCase));
                            if (!isActive)
                            {
                                continue;
                            }
                        }

                        if (onActionItem != null)
                        {
                            if (onActionItem(sp, stack.Attributes, actions, sourceQuestId))
                            {
                                wa.SetBool(onceKey, true);
                                wa.MarkPathDirty(onceKey);
                            }
                        }

                        scanned++;
                        if (scanned >= MaxSlotsPerTick)
                        {
                            // Continue from next slot on next tick.
                            inventoryScanCursorByPlayerUid[uid] = (iinv.Key, slotIndex + 1);
                            goto nextPlayer;
                        }
                    }
                }

                // Finished full scan => reset cursor.
                inventoryScanCursorByPlayerUid.Remove(uid);

            nextPlayer:
                continue;
            }
        }
    }
}
