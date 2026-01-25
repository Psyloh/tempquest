using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class ActionItemDurabilityCommandHandler
    {
        public TextCommandResult Repair(TextCommandCallingArgs args)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player?.InventoryManager == null) return TextCommandResult.Error("This command can only be run by a player.");

            var slot = player.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack?.Collectible == null) return TextCommandResult.Error("No item in active hotbar slot.");

            int maxDurability = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);
            if (maxDurability <= 0)
            {
                if (slot.Itemstack.Collectible is ItemWearable wearable)
                {
                    float current = slot.Itemstack.Attributes?.GetFloat("condition", 1f) ?? 1f;
                    wearable.ChangeCondition(slot, 1f - current);
                    return TextCommandResult.Success($"Repaired '{slot.Itemstack.GetName()}' to 100% condition.");
                }

                return TextCommandResult.Error("Held item has no durability.");
            }

            slot.Itemstack.Collectible.SetDurability(slot.Itemstack, maxDurability);
            slot.MarkDirty();

            return TextCommandResult.Success($"Repaired '{slot.Itemstack.GetName()}' to {maxDurability} durability.");
        }

        public TextCommandResult Destruct(TextCommandCallingArgs args)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player?.InventoryManager == null) return TextCommandResult.Error("This command can only be run by a player.");

            var slot = player.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack?.Collectible == null) return TextCommandResult.Error("No item in active hotbar slot.");

            int amount = (int)args[0];
            if (amount <= 0) return TextCommandResult.Error("Damage amount must be > 0.");

            int maxDurability = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);
            if (maxDurability <= 0)
            {
                if (slot.Itemstack.Collectible is ItemWearable wearable)
                {
                    float current = slot.Itemstack.Attributes?.GetFloat("condition", 1f) ?? 1f;
                    float delta = amount / 100f;
                    float newCondition = Math.Clamp(current - delta, 0f, 1f);
                    wearable.ChangeCondition(slot, newCondition - current);
                    return TextCommandResult.Success($"Damaged '{slot.Itemstack.GetName()}' by {amount}% condition. Remaining: {newCondition * 100f:0.#}%.");
                }

                return TextCommandResult.Error("Held item has no durability.");
            }

            int remaining = slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack);
            int newValue = Math.Max(0, remaining - amount);
            slot.Itemstack.Collectible.SetDurability(slot.Itemstack, newValue);
            slot.MarkDirty();

            return TextCommandResult.Success($"Damaged '{slot.Itemstack.GetName()}' by {amount}. Remaining durability: {newValue}/{maxDurability}.");
        }
    }
}
