using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    public class ItemAttributePatches
    {
        private const string MeleeAttackCooldownKey = "alegacyvsquest:meleeattackspeed:last";
        private const int BaseMeleeAttackCooldownMs = 650;

        [HarmonyPatch(typeof(CollectibleObject), "GetHeldItemName")]
        public class CollectibleObject_GetHeldItemName_ActionItem_ItemizerName_Patch
        {
            public static void Postfix(ItemStack itemStack, ref string __result)
            {
                if (itemStack?.Attributes == null) return;

                string actions = itemStack.Attributes.GetString(ItemAttributeUtils.ActionItemActionsKey);
                if (string.IsNullOrWhiteSpace(actions)) return;

                string customName = itemStack.Attributes.GetString(ItemAttributeUtils.QuestNameKey);
                if (string.IsNullOrWhiteSpace(customName)) return;

                // Preserve VTML/color markup if the stored name already contains it.
                if (customName.IndexOf('<') >= 0)
                {
                    __result = customName;
                    return;
                }

                __result = $"<i>{customName}</i>";
            }
        }

        [HarmonyPatch(typeof(ModSystemWearableStats), "onFootStep")]
        public class ModSystemWearableStats_onFootStep_Patch
        {
            public static bool Prefix(EntityPlayer entity)
            {
                if (entity?.Player?.InventoryManager == null) return true;

                var inv = entity.Player.InventoryManager.GetOwnInventory("character");
                if (inv == null) return true;

                float stealth = 0f;
                foreach (ItemSlot slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                    {
                        stealth += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrStealth);
                    }
                }

                return stealth <= 0f;
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorHealth), "OnFallToGround")]
        public class EntityBehaviorHealth_OnFallToGround_Patch
        {
            public static void Prefix(EntityBehaviorHealth __instance, ref float __state)
            {
                if (__instance?.entity is not EntityPlayer player) return;
                if (player.Player?.InventoryManager == null) return;

                var inv = player.Player.InventoryManager.GetOwnInventory("character");
                if (inv == null) return;

                float reduction = 0f;
                foreach (ItemSlot slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                    {
                        reduction += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrFallDamageMult);
                    }
                }

                if (reduction == 0f) return;

                __state = __instance.entity.Properties.FallDamageMultiplier;
                float mult = GameMath.Clamp(1f - reduction, 0f, 2f);
                __instance.entity.Properties.FallDamageMultiplier = __state * mult;
            }

            public static void Postfix(EntityBehaviorHealth __instance, float __state)
            {
                if (__instance?.entity == null) return;
                if (__state <= 0f) return;

                __instance.entity.Properties.FallDamageMultiplier = __state;
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorTemporalStabilityAffected), "OnGameTick")]
        public class EntityBehaviorTemporalStabilityAffected_OnGameTick_Patch
        {
            public static void Prefix(EntityBehaviorTemporalStabilityAffected __instance, ref double __state)
            {
                __state = __instance?.OwnStability ?? 0.0;
            }

            public static void Postfix(EntityBehaviorTemporalStabilityAffected __instance, double __state)
            {
                if (__instance?.entity is not EntityPlayer player) return;
                if (player.Player?.InventoryManager == null) return;

                var inv = player.Player.InventoryManager.GetOwnInventory("character");
                if (inv == null) return;

                float reduction = 0f;
                foreach (ItemSlot slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                    {
                        reduction += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrTemporalDrainMult);
                    }
                }

                if (reduction <= 0f) return;

                double delta = __instance.OwnStability - __state;
                if (delta >= 0) return;

                float mult = GameMath.Clamp(1f - reduction, 0f, 1f);
                double adjusted = __state + delta * mult;
                __instance.OwnStability = GameMath.Clamp(adjusted, 0.0, 1.0);
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), "GetAttackPower")]
        public class CollectibleObject_GetAttackPower_Patch
        {
            public static void Postfix(CollectibleObject __instance, IItemStack withItemStack, ref float __result)
            {
                if (withItemStack is ItemStack stack)
                {
                    float bonus = ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrAttackPower);
                    __result += bonus;
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), "OnHeldAttackStart")]
        public class CollectibleObject_OnHeldAttackStart_AttackSpeed_Patch
        {
            public static bool Prefix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
            {
                if (byEntity is not EntityPlayer player) return true;
                if (player.Player?.InventoryManager == null) return true;

                var inv = player.Player.InventoryManager.GetOwnInventory("character");
                if (inv == null) return true;

                float bonus = 0f;
                foreach (ItemSlot invSlot in inv)
                {
                    if (!invSlot.Empty && invSlot.Itemstack?.Item is ItemWearable)
                    {
                        bonus += ItemAttributeUtils.GetAttributeFloatScaled(invSlot.Itemstack, ItemAttributeUtils.AttrMeleeAttackSpeed);
                    }
                }

                if (Math.Abs(bonus) < 0.001f) return true;

                float mult = GameMath.Clamp(1f - bonus, 0.15f, 3f);
                long nowMs = byEntity.World.ElapsedMilliseconds;
                long lastMs = byEntity.WatchedAttributes.GetLong(MeleeAttackCooldownKey, 0);
                long cooldownMs = (long)(BaseMeleeAttackCooldownMs * mult);

                if (nowMs - lastMs < cooldownMs)
                {
                    handling = EnumHandHandling.PreventDefault;
                    return false;
                }

                byEntity.WatchedAttributes.SetLong(MeleeAttackCooldownKey, nowMs);
                return true;
            }
        }

        [HarmonyPatch(typeof(ItemWearable), "GetWarmth")]
        public class ItemWearable_GetWarmth_Patch
        {
            public static void Postfix(ItemWearable __instance, ItemSlot inslot, ref float __result)
            {
                if (inslot.Itemstack is ItemStack stack)
                {
                    float bonus = ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrWarmth);
                    __result += bonus;
                }
            }
        }

        [HarmonyPatch(typeof(ModSystemWearableStats), "handleDamaged")]
        public class ModSystemWearableStats_handleDamaged_Patch
        {
            public static void Postfix(ModSystemWearableStats __instance, IPlayer player, float damage, DamageSource dmgSource, ref float __result)
            {
                if (__result <= 0f) return;

                float flatReduction = 0f;
                float percReduction = 0f;

                IInventory inv = player.InventoryManager.GetOwnInventory("character");
                foreach (var slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                    {
                        flatReduction += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrProtection);
                        percReduction += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrProtectionPerc);
                    }
                }


                float newDamage = __result;
                newDamage = System.Math.Max(0f, newDamage - flatReduction);
                newDamage *= (1f - System.Math.Max(0f, percReduction));

                __result = newDamage;
            }
        }

        [HarmonyPatch(typeof(ModSystemWearableStats), "updateWearableStats")]
        public class ModSystemWearableStats_updateWearableStats_Patch
        {
            public static void Postfix(ModSystemWearableStats __instance, IInventory inv, IServerPlayer player)
            {
                if (player == null || player.Entity == null || player.Entity.Stats == null) return;

                StatModifiers bonusMods = new StatModifiers();
                bonusMods.walkSpeed = 0f;
                bonusMods.healingeffectivness = 0f;
                bonusMods.hungerrate = 0f;
                bonusMods.rangedWeaponsAcc = 0f;
                bonusMods.rangedWeaponsSpeed = 0f;
                float miningSpeedMult = 0f;
                float jumpHeightMult = 0f;
                float maxHealthFlat = 0f;
                float maxOxygenBonus = 0f;

                foreach (ItemSlot slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                    {
                        bonusMods.walkSpeed += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrWalkSpeed);
                        bonusMods.hungerrate += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrHungerRate);
                        bonusMods.healingeffectivness += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrHealingEffectiveness);
                        bonusMods.rangedWeaponsAcc += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrRangedAccuracy);
                        bonusMods.rangedWeaponsSpeed += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrRangedSpeed);
                        miningSpeedMult += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrMiningSpeedMult);
                        jumpHeightMult += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrJumpHeightMul);
                        maxHealthFlat += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrMaxHealthFlat);
                        maxOxygenBonus += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrMaxOxygen);
                    }
                }

                player.Entity.Stats.Set("walkspeed", "vsquestmod", bonusMods.walkSpeed, true);
                player.Entity.Stats.Set("healingeffectivness", "vsquestmod", bonusMods.healingeffectivness, true);
                player.Entity.Stats.Set("hungerrate", "vsquestmod", bonusMods.hungerrate, true);
                player.Entity.Stats.Set("rangedWeaponsAcc", "vsquestmod", bonusMods.rangedWeaponsAcc, true);
                player.Entity.Stats.Set("rangedWeaponsSpeed", "vsquestmod", bonusMods.rangedWeaponsSpeed, true);
                player.Entity.Stats.Set("miningSpeedMul", "vsquestmod", miningSpeedMult, true);
                player.Entity.Stats.Set("jumpHeightMul", "vsquestmod", jumpHeightMult, true);

                var healthBehavior = player.Entity.GetBehavior<EntityBehaviorHealth>();
                if (healthBehavior != null)
                {
                    healthBehavior.SetMaxHealthModifiers("vsquestmod:attr:maxhealth", maxHealthFlat);
                }

                var oxygenBehavior = player.Entity.GetBehavior<EntityBehaviorBreathe>();
                if (oxygenBehavior != null)
                {
                    const string AppliedBonusKey = "alegacyvsquest:attr:maxoxygenbonusapplied";

                    float lastAppliedBonus = player.Entity.WatchedAttributes.GetFloat(AppliedBonusKey, 0f);

                    // Calculate a stable base that doesn't stack when updateWearableStats runs multiple times.
                    // We treat the current MaxOxygen as (base + lastAppliedBonus).
                    float baseOxygen = oxygenBehavior.MaxOxygen - lastAppliedBonus;
                    if (baseOxygen < 1f) baseOxygen = 1f;

                    float newMaxOxygen = Math.Max(1f, baseOxygen + maxOxygenBonus);
                    oxygenBehavior.MaxOxygen = newMaxOxygen;

                    // If max oxygen decreased (e.g. accessory removed), ensure current oxygen cannot exceed the new max.
                    float currentOxygen = oxygenBehavior.Oxygen;
                    if (currentOxygen > newMaxOxygen)
                    {
                        oxygenBehavior.Oxygen = newMaxOxygen;
                    }
                    player.Entity.WatchedAttributes.SetFloat(AppliedBonusKey, maxOxygenBonus);
                }

                float weightLimit = GetWeightLimit(inv);
                float weightPenalty = GetInventoryFillRatio(player) * weightLimit;
                if (weightPenalty > 0f)
                {
                    player.Entity.Stats.Set("walkspeed", "vsquestmod:weightlimit", -weightPenalty, true);
                }
                else
                {
                    player.Entity.Stats.Set("walkspeed", "vsquestmod:weightlimit", 0f, true);
                }

                player.Entity.walkSpeed = player.Entity.Stats.GetBlended("walkspeed");
            }

            private static float GetWeightLimit(IInventory inv)
            {
                float total = 0f;
                foreach (ItemSlot slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                    {
                        total += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrWeightLimit);
                    }
                }

                return Math.Max(0f, total);
            }

            private static float GetInventoryFillRatio(IServerPlayer player)
            {
                var invManager = player?.InventoryManager;
                if (invManager?.Inventories == null) return 0f;

                int totalSlots = 0;
                int filledSlots = 0;
                foreach (var kvp in invManager.Inventories)
                {
                    var inventory = kvp.Value;
                    if (inventory == null) continue;
                    if (inventory.ClassName == GlobalConstants.creativeInvClassName) continue;

                    for (int i = 0; i < inventory.Count; i++)
                    {
                        var slot = inventory[i];
                        if (slot == null) continue;
                        totalSlots++;
                        if (!slot.Empty) filledSlots++;
                    }
                }

                if (totalSlots == 0) return 0f;
                return Math.Min(1f, Math.Max(0f, filledSlots / (float)totalSlots));
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), "TryMergeStacks")]
        public class CollectibleObject_TryMergeStacks_SecondChanceCharge_Patch
        {
            public static bool Prefix(CollectibleObject __instance, ItemStackMergeOperation op)
            {
                if (TryHandleSecondChanceCharge(op)) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(ItemWearable), "TryMergeStacks")]
        public class ItemWearable_TryMergeStacks_SecondChanceCharge_Patch
        {
            public static bool Prefix(ItemWearable __instance, ItemStackMergeOperation op)
            {
                if (TryHandleSecondChanceCharge(op)) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(ItemWearable), "GetMergableQuantity")]
        public class ItemWearable_GetMergableQuantity_SecondChanceCharge_Patch
        {
            public static bool Prefix(ItemWearable __instance, ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority, ref int __result)
            {
                if (CanChargeSecondChance(sinkStack, sourceStack))
                {
                    __result = 1;
                    return false;
                }

                return true;
            }
        }

        private static bool TryHandleSecondChanceCharge(ItemStackMergeOperation op)
        {
            if (op?.SinkSlot?.Itemstack == null || op.SourceSlot?.Itemstack == null) return false;

            var sinkStack = op.SinkSlot.Itemstack;
            if (!CanChargeSecondChance(sinkStack, op.SourceSlot.Itemstack)) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            sinkStack.Attributes.SetFloat(chargeKey, 1f);
            op.MovedQuantity = 1;
            op.SourceSlot.TakeOut(1);
            op.SinkSlot.MarkDirty();
            return true;
        }

        private static bool CanChargeSecondChance(ItemStack sinkStack, ItemStack sourceStack)
        {
            if (sinkStack?.Attributes == null || sourceStack?.Collectible?.Code == null) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            if (!sinkStack.Attributes.HasAttribute(chargeKey)) return false;

            if (!IsUranium(sourceStack.Collectible.Code)) return false;

            float charges = ItemAttributeUtils.GetAttributeFloat(sinkStack, ItemAttributeUtils.AttrSecondChanceCharges, 0f);
            return charges < 0.5f;
        }

        private static bool IsUranium(AssetLocation code)
        {
            return code != null
                && string.Equals(code.Domain, "game", StringComparison.OrdinalIgnoreCase)
                && code.Path != null
                && code.Path.IndexOf("uranium", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
