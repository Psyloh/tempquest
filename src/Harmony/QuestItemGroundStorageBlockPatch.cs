using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest.Harmony
{
    public class QuestItemGroundStorageBlockPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("Vintagestory.GameContent.CollectibleBehaviorGroundStorable");
            if (type == null) yield break;

            var m = AccessTools.Method(type, "Interact");
            if (m != null) yield return m;
        }

        [HarmonyPatch]
        public class Interact_Patch
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                return QuestItemGroundStorageBlockPatch.TargetMethods();
            }

            public static bool Prefix(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
            {
                if (itemslot?.Itemstack == null) return true;

                // Only block the special ground storage placement (Shift + right click).
                if (byEntity?.Controls?.ShiftKey != true) return true;

                if (ItemAttributeUtils.IsActionItemBlockedGroundStorage(itemslot.Itemstack))
                {
                    handHandling = EnumHandHandling.PreventDefault;
                    handling = EnumHandling.PreventSubsequent;
                    return false;
                }

                return true;
            }
        }
    }
}
