using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsQuest.Util;

namespace VsQuest.Harmony
{
    public class ItemAttributePatches
    {
        [HarmonyPatch(typeof(CollectibleObject), "GetAttackPower")]
        public class CollectibleObject_GetAttackPower_Patch
        {
            public static void Postfix(CollectibleObject __instance, IItemStack withItemStack, ref float __result)
            {
                if (withItemStack is ItemStack stack)
                {
                    float bonus = ItemAttributeUtils.GetAttributeFloat(stack, ItemAttributeUtils.AttrAttackPower);
                    __result += bonus;
                }
            }
        }

        [HarmonyPatch(typeof(ItemWearable), "GetWarmth")]
        public class ItemWearable_GetWarmth_Patch
        {
            public static void Postfix(ItemWearable __instance, ItemSlot inslot, ref float __result)
            {
                if (inslot.Itemstack is ItemStack stack)
                {
                    float bonus = ItemAttributeUtils.GetAttributeFloat(stack, ItemAttributeUtils.AttrWarmth);
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
                         flatReduction += ItemAttributeUtils.GetAttributeFloat(slot.Itemstack, ItemAttributeUtils.AttrProtection);
                         percReduction += ItemAttributeUtils.GetAttributeFloat(slot.Itemstack, ItemAttributeUtils.AttrProtectionPerc);
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

                foreach (ItemSlot slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                    {
                        bonusMods.walkSpeed += ItemAttributeUtils.GetAttributeFloat(slot.Itemstack, ItemAttributeUtils.AttrWalkSpeed);
                        bonusMods.hungerrate += ItemAttributeUtils.GetAttributeFloat(slot.Itemstack, ItemAttributeUtils.AttrHungerRate);
                        bonusMods.healingeffectivness += ItemAttributeUtils.GetAttributeFloat(slot.Itemstack, ItemAttributeUtils.AttrHealingEffectiveness);
                        bonusMods.rangedWeaponsAcc += ItemAttributeUtils.GetAttributeFloat(slot.Itemstack, ItemAttributeUtils.AttrRangedAccuracy);
                        bonusMods.rangedWeaponsSpeed += ItemAttributeUtils.GetAttributeFloat(slot.Itemstack, ItemAttributeUtils.AttrRangedSpeed);
                    }
                }

                player.Entity.Stats.Set("walkspeed", "vsquestmod", bonusMods.walkSpeed, true);
                player.Entity.Stats.Set("healingeffectivness", "vsquestmod", bonusMods.healingeffectivness, true);
                player.Entity.Stats.Set("hungerrate", "vsquestmod", bonusMods.hungerrate, true);
                player.Entity.Stats.Set("rangedWeaponsAcc", "vsquestmod", bonusMods.rangedWeaponsAcc, true);
                player.Entity.Stats.Set("rangedWeaponsSpeed", "vsquestmod", bonusMods.rangedWeaponsSpeed, true);
                
                player.Entity.walkSpeed = player.Entity.Stats.GetBlended("walkspeed");
            }
        }
    }
}
