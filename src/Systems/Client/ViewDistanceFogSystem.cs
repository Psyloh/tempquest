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

            return total;
        }

        private void ApplyFog(float viewDistance)
        {
            var modifiers = capi?.Ambient?.CurrentModifiers;
            if (modifiers == null) return;

            if (!modifiers.TryGetValue(ModifierKey, out AmbientModifier modifier))
            {
                modifier = new AmbientModifier().EnsurePopulated();
                modifiers[ModifierKey] = modifier;
            }

            float strength = Math.Abs(viewDistance);
            if (strength <= 0f)
            {
                modifier.FogDensity.Weight = 0f;
                modifier.FogMin.Weight = 0f;
                return;
            }

            strength = GameMath.Clamp(strength, 0f, 1f);

            const float baseDensity = 0.00125f;

            // FogMin controls how close the fog starts.
            // Positive viewDistance => allow the fog to start further away.
            // Negative viewDistance => do not increase FogMin (avoid improving visibility).
            modifier.FogMin.Value = viewDistance > 0f ? strength * 0.03f : 0f;
            modifier.FogMin.Weight = strength;

            // Negative viewDistance => worse visibility (more fog)
            if (viewDistance < 0f)
            {
                modifier.FogDensity.Value = baseDensity + strength * 0.006f;
                modifier.FogDensity.Weight = strength;
                return;
            }

            // Positive viewDistance => better visibility (less fog)
            modifier.FogDensity.Value = Math.Max(0f, baseDensity - strength * 0.0009f);
            modifier.FogDensity.Weight = strength;
        }
    }
}
