using System;
using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Entity), "GetInfoText")]
    public static class EntityInfoTextPatch
    {
        public static bool Prefix(Entity __instance, ref string __result)
        {
            try
            {
                if (__instance == null) return true;

                if (!ShouldHideInfo(__instance))
                {
                    return true;
                }

                __result = string.Empty;
                return false;
            }
            catch
            {
                return true;
            }
        }

        internal static bool ShouldHideInfo(Entity entity)
        {
            if (entity == null) return false;

            try
            {
                if (entity.Properties?.Attributes != null
                    && entity.Properties.Attributes["alegacyvsquestHideInfoText"].AsBool(false))
                {
                    return true;
                }
            }
            catch
            {
            }

            string domain = entity.Code?.Domain;
            if (!string.Equals(domain, "alstory", StringComparison.OrdinalIgnoreCase)) return false;

            return entity.GetBehavior<EntityBehaviorQuestBoss>() != null
                || entity.GetBehavior<EntityBehaviorBoss>() != null;
        }
    }

    [HarmonyPatch(typeof(EntityAgent), "GetInfoText")]
    public static class EntityAgentInfoTextPatch
    {
        public static bool Prefix(EntityAgent __instance, ref string __result)
        {
            try
            {
                if (__instance == null) return true;

                if (!EntityInfoTextPatch.ShouldHideInfo(__instance))
                {
                    return true;
                }

                __result = string.Empty;
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
