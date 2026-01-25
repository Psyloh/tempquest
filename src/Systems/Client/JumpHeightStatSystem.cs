using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class JumpHeightStatSystem : ModSystem
    {
        private const string ModifierKey = "alegacyvsquest:jumpheight";
        private ICoreClientAPI capi;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterGameTickListener(OnTick, 100);
        }

        private void OnTick(float dt)
        {
            if (capi?.World?.Player?.Entity?.Stats == null) return;

            float total = GetJumpHeightAttr(capi.World.Player);
            ApplyJumpHeight(total);
        }

        private float GetJumpHeightAttr(IClientPlayer player)
        {
            IInventory inv = player.InventoryManager?.GetOwnInventory("character");
            if (inv == null) return 0f;

            float total = 0f;
            foreach (ItemSlot slot in inv)
            {
                if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                {
                    total += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrJumpHeightMul);
                }
            }

            return total;
        }

        private void ApplyJumpHeight(float bonus)
        {
            var playerEntity = capi?.World?.Player?.Entity;
            if (playerEntity?.Stats == null) return;

            if (Math.Abs(bonus) < 0.0001f)
            {
                playerEntity.Stats.Remove("jumpHeightMul", ModifierKey);
                return;
            }

            playerEntity.Stats.Set("jumpHeightMul", ModifierKey, bonus, true);
        }
    }
}
