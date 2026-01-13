using HarmonyLib;
using Vintagestory.API.Common;

namespace VsQuest.Harmony
{
    public class QuestItemEquipBlockPatch
    {
        private static bool IsBlockedQuestItem(ItemSlot sourceSlot)
        {
            if (sourceSlot?.Itemstack == null) return false;
            return ItemAttributeUtils.IsActionItemBlockedEquip(sourceSlot.Itemstack);
        }

        [HarmonyPatch(typeof(ItemSlotCharacter), nameof(ItemSlotCharacter.CanHold))]
        public class ItemSlotCharacter_CanHold_Patch
        {
            public static bool Prefix(ItemSlotCharacter __instance, ItemSlot itemstackFromSourceSlot, ref bool __result)
            {
                if (IsBlockedQuestItem(itemstackFromSourceSlot))
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(ItemSlotCharacter), nameof(ItemSlotCharacter.CanTakeFrom))]
        public class ItemSlotCharacter_CanTakeFrom_Patch
        {
            public static bool Prefix(ItemSlotCharacter __instance, ItemSlot sourceSlot, EnumMergePriority priority, ref bool __result)
            {
                if (IsBlockedQuestItem(sourceSlot))
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }
    }
}
