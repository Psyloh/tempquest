using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class ConsumeActionItemAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.InventoryManager?.Inventories == null) return;

            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                throw new QuestException("The 'consumeactionitem' action requires at least 1 argument: actionItemId.");
            }

            string actionItemId = args[0];

            int amount = 1;
            if (args.Length >= 2 && int.TryParse(args[1], out int parsedAmount))
            {
                amount = parsedAmount;
            }
            if (amount <= 0) return;

            int remaining = amount;

            foreach (var inv in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inv == null) continue;
                if (inv.ClassName == GlobalConstants.creativeInvClassName) continue;

                int slotCount;
                try
                {
                    slotCount = inv.Count;
                }
                catch
                {
                    continue;
                }

                for (int i = 0; i < slotCount; i++)
                {
                    if (remaining <= 0) return;

                    var slot = inv[i];
                    if (slot?.Empty != false) continue;

                    var stack = slot.Itemstack;
                    if (stack?.Attributes == null) continue;

                    string id = stack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
                    if (!string.Equals(id, actionItemId, StringComparison.OrdinalIgnoreCase)) continue;

                    int take = Math.Min(remaining, stack.StackSize);
                    slot.TakeOut(take);
                    slot.MarkDirty();

                    remaining -= take;
                }
            }
        }
    }
}
