using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    internal static class ReputationUiHelper
    {
        private static string ToRewardKeyPart(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            return code.Replace(':', '-').Replace('/', '-').Replace(' ', '-');
        }

        private static string GetCustomRewardTitle(string langKey)
        {
            string value = LocalizationUtils.GetSafe(langKey);
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, langKey, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return value;
        }

        internal static string GetReputationHeaderText(string reputationNpcId, int repValue, string reputationNpcRankLangKey, string reputationFactionRankLangKey)
        {
            string rankKey = !string.IsNullOrWhiteSpace(reputationNpcId)
                ? reputationNpcRankLangKey
                : reputationFactionRankLangKey;

            if (!string.IsNullOrWhiteSpace(rankKey))
            {
                string rankText = Lang.Get(rankKey);
                if (!string.IsNullOrWhiteSpace(rankText))
                {
                    return $"{rankText}: {repValue}";
                }
            }

            return Lang.Get("alegacyvsquest:reputation-value-template", repValue);
        }

        internal static string GetRankRewardTitle(ICoreClientAPI capi, string reputationNpcId, ReputationRankRewardStatus rr)
        {
            if (rr == null) return string.Empty;

            if (!string.IsNullOrWhiteSpace(reputationNpcId))
            {
                string npcMinKey = $"albase:reputation-reward-title-{ToRewardKeyPart(reputationNpcId)}-{rr.min}";
                string npcMinTitle = GetCustomRewardTitle(npcMinKey);
                if (!string.IsNullOrWhiteSpace(npcMinTitle))
                {
                    return npcMinTitle;
                }
            }

            if (!string.IsNullOrWhiteSpace(rr.iconItemCode))
            {
                try
                {
                    ItemStack iconStack = null;
                    var loc = new AssetLocation(rr.iconItemCode);
                    var item = capi.World.GetItem(loc);
                    if (item != null)
                    {
                        iconStack = new ItemStack(item);
                    }
                    else
                    {
                        var block = capi.World.GetBlock(loc);
                        if (block != null)
                        {
                            iconStack = new ItemStack(block);
                        }
                        else
                        {
                            var itemSystem = capi.ModLoader.GetModSystem<ItemSystem>();
                            if (itemSystem?.ActionItemRegistry != null && itemSystem.ActionItemRegistry.TryGetValue(rr.iconItemCode, out var actionItem))
                            {
                                if (!string.IsNullOrWhiteSpace(actionItem?.itemCode))
                                {
                                    var baseLoc = new AssetLocation(actionItem.itemCode);
                                    var baseItem = capi.World.GetItem(baseLoc);
                                    if (baseItem != null)
                                    {
                                        iconStack = new ItemStack(baseItem);
                                    }
                                    else
                                    {
                                        var baseBlock = capi.World.GetBlock(baseLoc);
                                        if (baseBlock != null)
                                        {
                                            iconStack = new ItemStack(baseBlock);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (iconStack != null)
                    {
                        return iconStack.GetName();
                    }
                }
                catch
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(rr.rankLangKey))
            {
                return Lang.Get(rr.rankLangKey);
            }

            return rr.min.ToString();
        }
    }
}
