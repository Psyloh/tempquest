using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Entity), "GetPrefixAndCreatureName")]
    public static class EntityPrefixAndCreatureNamePatch
    {
        public static bool Prefix(Entity __instance, ref string __result)
        {
            try
            {
                if (__instance == null) return true;

                if (__instance.GetBehavior<EntityBehaviorQuestBoss>() == null
                    && __instance.GetBehavior<EntityBehaviorQuestTarget>() == null
                    && __instance.GetBehavior<EntityBehaviorBoss>() == null)
                {
                    return true;
                }

                string name = MobLocalizationUtils.GetMobDisplayName(__instance);
                if (string.IsNullOrWhiteSpace(name)) return true;

                __result = name;
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
