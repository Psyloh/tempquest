using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using VsQuest;

namespace VsQuest.Harmony
{
    public class ActionItemModePatches
    {
        private const int ModesPerLine = 7;

        private static bool TryGetModes(ITreeAttribute attributes, out List<ActionItemMode> modes)
        {
            modes = null;
            if (attributes == null) return false;

            var modesJson = attributes.GetString(ItemAttributeUtils.ActionItemModesKey);
            if (string.IsNullOrWhiteSpace(modesJson)) return false;

            try
            {
                modes = JsonConvert.DeserializeObject<List<ActionItemMode>>(modesJson);
            }
            catch
            {
                modes = null;
            }

            return modes != null && modes.Count > 0;
        }

        [HarmonyPatch(typeof(CollectibleObject), "GetToolModes")]
        public class CollectibleObject_GetToolModes_ActionItemModes_Patch
        {
            public static void Postfix(CollectibleObject __instance, ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel, ref SkillItem[] __result)
            {
                if (slot?.Itemstack?.Attributes == null) return;
                if (!TryGetModes(slot.Itemstack.Attributes, out var modes)) return;

                if (forPlayer?.Entity?.Api is not ICoreClientAPI capi) return;

                var items = new SkillItem[modes.Count];
                for (int i = 0; i < modes.Count; i++)
                {
                    var mode = modes[i];
                    string name = mode?.name;
                    if (string.IsNullOrWhiteSpace(name)) name = mode?.id ?? $"Mode {i + 1}";

                    var skill = new SkillItem
                    {
                        Name = name,
                        Code = new AssetLocation(mode?.id ?? $"alegacyvsquest:mode-{i}"),
                        Linebreak = i % ModesPerLine == 0
                    };

                    if (!string.IsNullOrWhiteSpace(mode?.icon))
                    {
                        skill.WithIcon(capi, mode.icon);
                    }
                    else
                    {
                        string letter = name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "?";
                        skill.WithLetterIcon(capi, letter);
                    }

                    items[i] = skill;
                }

                __result = items;
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), "GetToolMode")]
        public class CollectibleObject_GetToolMode_ActionItemModes_Patch
        {
            public static void Postfix(CollectibleObject __instance, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, ref int __result)
            {
                if (slot?.Itemstack?.Attributes == null) return;
                if (!TryGetModes(slot.Itemstack.Attributes, out var modes)) return;

                int modeIndex = slot.Itemstack.Attributes.GetInt(ItemAttributeUtils.ActionItemModeIndexKey, 0);
                if (modeIndex < 0) modeIndex = 0;
                if (modeIndex >= modes.Count) modeIndex = modes.Count - 1;

                __result = modeIndex;
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), "SetToolMode")]
        public class CollectibleObject_SetToolMode_ActionItemModes_Patch
        {
            public static bool Prefix(CollectibleObject __instance, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
            {
                if (slot?.Itemstack?.Attributes == null) return true;
                if (!TryGetModes(slot.Itemstack.Attributes, out var modes)) return true;

                int modeIndex = toolMode;
                if (modeIndex < 0) modeIndex = 0;
                if (modeIndex >= modes.Count) modeIndex = modes.Count - 1;

                slot.Itemstack.Attributes.SetInt(ItemAttributeUtils.ActionItemModeIndexKey, modeIndex);
                slot.MarkDirty();
                return false;
            }
        }
    }
}
