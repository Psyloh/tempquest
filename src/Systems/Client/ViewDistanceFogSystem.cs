using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class ViewDistanceFogSystem : ModSystem
    {
        private const string ModifierKey = "alegacyvsquest:viewdistancefog";
        private ICoreClientAPI capi;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterGameTickListener(OnTick, 100);
        }

        private void OnTick(float dt)
        {
            if (capi?.World?.Player == null) return;

            float viewDistance = GetViewDistanceAttr(capi.World.Player);
            ApplyFog(viewDistance);
        }

        private float GetViewDistanceAttr(IClientPlayer player)
        {
            IInventory inv = player.InventoryManager?.GetOwnInventory("character");
            if (inv == null) return 0f;

            float total = 0f;
            foreach (ItemSlot slot in inv)
            {
                if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                {
                    total += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrViewDistance);
                }
            }

            return Math.Max(0f, total);
        }

        private void ApplyFog(float strength)
        {
            var modifiers = capi?.Ambient?.CurrentModifiers;
            if (modifiers == null) return;

            if (!modifiers.TryGetValue(ModifierKey, out AmbientModifier modifier))
            {
                modifier = new AmbientModifier().EnsurePopulated();
                modifiers[ModifierKey] = modifier;
            }

            if (strength <= 0f)
            {
                modifier.FogDensity.Weight = 0f;
                modifier.FogMin.Weight = 0f;
                return;
            }

            strength = GameMath.Clamp(strength, 0f, 1f);

            const float baseDensity = 0.00125f;
            modifier.FogDensity.Value = baseDensity + strength * 0.004f;
            modifier.FogDensity.Weight = strength * 0.6f;

            modifier.FogMin.Value = strength * 0.02f;
            modifier.FogMin.Weight = strength * 0.6f;
        }
    }
}
