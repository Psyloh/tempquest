using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    public static class ItemTooltipPatcher
    {
        private static void TrimEndNewlines(StringBuilder sb)
        {
            if (sb == null) return;

            while (sb.Length > 0)
            {
                char c = sb[sb.Length - 1];
                if (c == '\n' || c == '\r') sb.Length--;
                else break;
            }
        }

        private static string GetPatternStart(string keyOrPattern)
        {
            // If this is already a pattern string (contains format braces), do not pass it to Lang.Get()
            // because TranslationService will try to format it and may throw if args are missing.
            string source = keyOrPattern;
            if (keyOrPattern != null && keyOrPattern.IndexOf('{') < 0)
            {
                source = Lang.Get(keyOrPattern);
            }

            int idx = source.IndexOf('{');
            if (idx > 0) return source.Substring(0, idx).Trim();
            return source.Trim();
        }

        public static void ModifyTooltip(ItemSlot inSlot, StringBuilder dsc)
        {
            if (inSlot?.Itemstack?.Attributes == null) return;

            string actionsJson = inSlot.Itemstack.Attributes.GetString("alegacyvsquest:actions");
            if (string.IsNullOrEmpty(actionsJson)) return;

            ITreeAttribute attrs = inSlot.Itemstack.Attributes;

            HashSet<string> hideVanilla = new HashSet<string>();
            string hideVanillaJson = attrs.GetString("alegacyvsquest:hideVanilla");
            if (!string.IsNullOrEmpty(hideVanillaJson))
            {
                try { hideVanilla = new HashSet<string>(JsonConvert.DeserializeObject<List<string>>(hideVanillaJson)); } catch { }
            }

            string customDesc = attrs.GetString(ItemAttributeUtils.QuestDescKey);
            bool hasCustomDesc = !string.IsNullOrEmpty(customDesc);
            bool hideDesc = hasCustomDesc || hideVanilla.Contains("description");

            string currentTooltip = dsc.ToString();


            dsc.Clear();

            bool isConditionOrDurabilityLine(string trimmed)
            {
                if (string.IsNullOrWhiteSpace(trimmed)) return false;
                if (trimmed.StartsWith("Durability:") || trimmed.StartsWith(GetPatternStart("Durability: {0} / {1}"))) return true;
                if (trimmed.StartsWith("Condition:") || trimmed.StartsWith(GetPatternStart("Condition: {0}%"))) return true;
                if (trimmed.StartsWith("Прочность:") || trimmed.StartsWith("Состояние:")) return true;
                return false;
            }

            if (hasCustomDesc && currentTooltip.Contains(customDesc))
            {
                currentTooltip = currentTooltip.Replace(customDesc, "");
            }

            if (hasCustomDesc)
            {
                dsc.AppendLine(customDesc);
            }

            string[] lines = currentTooltip.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            bool lastLineWasEmpty = true;
            bool startedSkippingLeadingDesc = false;
            bool skippedLeadingDescBlock = !hideDesc;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                bool isLineEmpty = string.IsNullOrWhiteSpace(trimmed);

                // If the action item provides its own description, drop the leading vanilla description block
                // (first paragraph). Do not drop if the tooltip begins with durability/condition lines.
                if (!skippedLeadingDescBlock)
                {
                    if (!startedSkippingLeadingDesc)
                    {
                        if (isLineEmpty) continue;
                        if (isConditionOrDurabilityLine(trimmed))
                        {
                            skippedLeadingDescBlock = true;
                        }
                        else
                        {
                            startedSkippingLeadingDesc = true;
                            continue;
                        }
                    }
                    else
                    {
                        // If we reach a durability/condition line without an empty separator, do not skip it.
                        // Stop skipping and let the line be processed normally below.
                        if (isConditionOrDurabilityLine(trimmed))
                        {
                            skippedLeadingDescBlock = true;
                        }
                        else if (isLineEmpty)
                        {
                            // End of the leading description block.
                            skippedLeadingDescBlock = true;
                            continue;
                        }
                        else
                        {
                            // Still skipping vanilla description.
                            continue;
                        }
                    }
                }

                if (isLineEmpty)
                {
                    if (!lastLineWasEmpty)
                    {
                        dsc.AppendLine();
                        lastLineWasEmpty = true;
                    }
                    continue;
                }

                bool shouldHide = false;

                if (hideVanilla.Contains("durability"))
                {
                    if (trimmed.StartsWith("Durability:") || trimmed.StartsWith(GetPatternStart("Durability: {0} / {1}"))) shouldHide = true;
                    else if (trimmed.StartsWith("Condition:") || trimmed.StartsWith(GetPatternStart("Condition: {0}%"))) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("miningspeed"))
                {
                    if (trimmed.StartsWith("Tool Tier:") || trimmed.StartsWith(Lang.Get("Tool Tier: {0}"))) shouldHide = true;
                    else if (trimmed.Contains("mining speed") || trimmed.Contains(Lang.Get("item-tooltip-miningspeed"))) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("attackpower"))
                {
                    if (trimmed.StartsWith("Attack power:") || trimmed.StartsWith(GetPatternStart("Attack power: -{0} hp"))) shouldHide = true;
                    else if (trimmed.StartsWith("Attack tier:") || trimmed.StartsWith(GetPatternStart("Attack tier: {0}"))) shouldHide = true;
                    else if (trimmed.StartsWith("Уровень атаки:")) shouldHide = true;
                    else if (trimmed.StartsWith("Сила атаки:")) shouldHide = true;
                    else if (trimmed.Contains("Attack power:")) shouldHide = true;
                    else if (trimmed.Contains("Attack tier:")) shouldHide = true;
                    else if (trimmed.Contains("Уровень атаки:")) shouldHide = true;
                    else if (trimmed.Contains("Сила атаки:")) shouldHide = true;
                }

                if (!shouldHide && (hideVanilla.Contains("protection") || hideVanilla.Contains("armor")))
                {
                    if (trimmed.StartsWith("Flat damage reduction:") || trimmed.StartsWith(GetPatternStart("Flat damage reduction: {0} hp"))) shouldHide = true;
                    else if (trimmed.StartsWith("Percent protection:") || trimmed.StartsWith(GetPatternStart("Percent protection: {0}%"))) shouldHide = true;
                    else if (trimmed.StartsWith("Protection tier:") || trimmed.StartsWith(GetPatternStart("Protection tier: {0}"))) shouldHide = true;
                    else if (trimmed.Contains("Protection from rain")) shouldHide = true;
                    else if (trimmed.StartsWith("High damage tier resistant")) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("warmth"))
                {
                    // Vanilla wearables show Condition + Warmth on the same line. Do not hide the line if it begins with Condition.
                    bool isConditionLine = trimmed.StartsWith("Condition:")
                        || trimmed.StartsWith(GetPatternStart("Condition:"))
                        || trimmed.StartsWith("Состояние:");

                    if (!isConditionLine && trimmed.Contains("°C") && (trimmed.Contains("+") || trimmed.Contains("Warmth"))) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("temperature"))
                {
                    if (trimmed.StartsWith("Temperature:") || trimmed.StartsWith(GetPatternStart("Temperature: {0}°C"))) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("nutrition"))
                {
                    if (trimmed.StartsWith("Satiety:") || trimmed.StartsWith(GetPatternStart("Satiety: {0}"))) shouldHide = true;
                    else if (trimmed.StartsWith("Nutrients:") || trimmed.StartsWith("Food Category:")) shouldHide = true;
                    else if (trimmed.Contains("sat") && (trimmed.Contains("veg") || trimmed.Contains("fruit") || trimmed.Contains("grain") || trimmed.Contains("prot") || trimmed.Contains("dairy"))) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("storage"))
                {
                    if (trimmed.StartsWith("Slots:") || trimmed.StartsWith("Storage Slots:")) shouldHide = true;
                    else if (trimmed.StartsWith("Containable:")) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("combustible"))
                {
                    if (trimmed.StartsWith("Burn temperature:") || trimmed.StartsWith(GetPatternStart("Burn temperature: {0}°C"))) shouldHide = true;
                    else if (trimmed.StartsWith("Burn duration:") || trimmed.StartsWith(GetPatternStart("Burn duration: {0}s"))) shouldHide = true;
                }

                if (!shouldHide && (hideVanilla.Contains("grinding") || hideVanilla.Contains("crushing")))
                {
                    if (trimmed.StartsWith("Grinds into") || trimmed.StartsWith("Crushes into")) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("modsource"))
                {
                    if (trimmed.StartsWith("Mod:") || trimmed.StartsWith(GetPatternStart("Mod: {0}"))) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("walkspeed"))
                {
                    if (trimmed.StartsWith("Walk speed:")) shouldHide = true;
                }

                if (!shouldHide)
                {
                    dsc.AppendLine(line);
                    lastLineWasEmpty = false;
                }
            }

            HashSet<string> showAttrs = new HashSet<string>();
            string showAttrsJson = attrs.GetString("alegacyvsquest:showAttrs");
            if (!string.IsNullOrEmpty(showAttrsJson))
            {
                try { showAttrs = new HashSet<string>(JsonConvert.DeserializeObject<List<string>>(showAttrsJson)); } catch { }
            }

            string currentDsc = dsc.ToString();
            bool startedAttrBlock = false;

            bool TooltipHasConditionLine(string tooltip)
            {
                if (string.IsNullOrEmpty(tooltip)) return false;
                if (tooltip.Contains(Lang.Get("Condition:"))) return true;
                if (tooltip.Contains("Condition:")) return true;
                if (tooltip.Contains("Состояние:")) return true;
                return false;
            }

            string GetWearableConditionText(float condition01)
            {
                int pct = (int)(GameMath.Clamp(condition01, 0f, 1f) * 100f);

                if (condition01 > 0.5f) return Lang.Get("clothingcondition-good", pct);
                if (condition01 > 0.4f) return Lang.Get("clothingcondition-worn", pct);
                if (condition01 > 0.3f) return Lang.Get("clothingcondition-heavilyworn", pct);
                if (condition01 > 0.2f) return Lang.Get("clothingcondition-tattered", pct);
                if (condition01 > 0.1f) return Lang.Get("clothingcondition-heavilytattered", pct);
                return Lang.Get("clothingcondition-terrible", pct);
            }

            bool wantsConditionLine = !hideVanilla.Contains("durability");

            if (wantsConditionLine && !TooltipHasConditionLine(currentDsc))
            {
                float condition01 = 1f;
                bool canShow = false;

                if (inSlot.Itemstack.Attributes != null && inSlot.Itemstack.Attributes.HasAttribute("condition"))
                {
                    condition01 = GameMath.Clamp(inSlot.Itemstack.Attributes.GetFloat("condition", 1f), 0f, 1f);
                    canShow = true;
                }
                else if (inSlot.Itemstack.Collectible != null)
                {
                    int maxDurability = inSlot.Itemstack.Collectible.GetMaxDurability(inSlot.Itemstack);
                    if (maxDurability > 0)
                    {
                        int remaining = inSlot.Itemstack.Collectible.GetRemainingDurability(inSlot.Itemstack);
                        condition01 = GameMath.Clamp(remaining / (float)maxDurability, 0f, 1f);
                        canShow = true;
                    }
                }

                if (canShow)
                {
                    string condStr = GetWearableConditionText(condition01);
                    string lineToAdd = $"{Lang.Get("Condition:")} {condStr}";
                    if (!string.IsNullOrWhiteSpace(lineToAdd) && !currentDsc.Contains(lineToAdd))
                    {
                        if (!startedAttrBlock)
                        {
                            TrimEndNewlines(dsc);
                            if (dsc.Length > 0) dsc.AppendLine();
                            startedAttrBlock = true;
                            currentDsc = dsc.ToString();
                        }

                        dsc.AppendLine(lineToAdd);
                        currentDsc += "\n" + lineToAdd;
                    }
                }
            }

            foreach (var kvp in attrs)
            {
                if (kvp.Key.StartsWith(ItemAttributeUtils.AttrPrefix))
                {
                    string shortKey = kvp.Key.Substring(ItemAttributeUtils.AttrPrefix.Length);
                    if (!showAttrs.Contains(shortKey)) continue;

                    float value;
                    if (shortKey == ItemAttributeUtils.AttrSecondChanceCharges)
                    {
                        value = ItemAttributeUtils.GetAttributeFloat(inSlot.Itemstack, shortKey, 0f);
                    }
                    else
                    {
                        value = ItemAttributeUtils.GetAttributeFloatScaled(inSlot.Itemstack, shortKey, 0f);
                    }
                    bool showZero = shortKey == ItemAttributeUtils.AttrSecondChanceCharges;
                    if (value != 0f || showZero)
                    {
                        string lineToAdd = ItemAttributeUtils.FormatAttributeForTooltip(kvp.Key, value);
                        if (!currentDsc.Contains(lineToAdd))
                        {
                            if (!startedAttrBlock)
                            {
                                TrimEndNewlines(dsc);
                                if (dsc.Length > 0) dsc.AppendLine();
                                startedAttrBlock = true;
                                currentDsc = dsc.ToString();
                            }

                            dsc.AppendLine(lineToAdd);
                            currentDsc += "\n" + lineToAdd;
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(CollectibleObject), "GetHeldItemInfo")]
    public class CollectibleObject_GetHeldItemInfo_Patch
    {
        public static void Postfix(ItemSlot inSlot, StringBuilder dsc)
        {
            ItemTooltipPatcher.ModifyTooltip(inSlot, dsc);
        }
    }

    [HarmonyPatch(typeof(ItemWearable), "GetHeldItemInfo")]
    public class ItemWearable_GetHeldItemInfo_Patch
    {
        public static void Postfix(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            ItemTooltipPatcher.ModifyTooltip(inSlot, dsc);
        }
    }
}
