using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    public class PlayerAttributePatches
    {
        [HarmonyPatch(typeof(ModSystemWearableStats), "handleDamaged")]
        public class ModSystemWearableStats_handleDamaged_PlayerAttributes_Patch
        {
            public static void Postfix(ModSystemWearableStats __instance, IPlayer player, float damage, DamageSource dmgSource, ref float __result)
            {
                if (__result <= 0f) return;

                if (player?.Entity?.WatchedAttributes == null) return;

                float playerFlat = player.Entity.WatchedAttributes.GetFloat("vsquestadmin:attr:protection", 0f);
                float playerPerc = player.Entity.WatchedAttributes.GetFloat("vsquestadmin:attr:protectionperc", 0f);

                float newDamage = __result;
                newDamage = System.Math.Max(0f, newDamage - playerFlat);

                playerPerc = System.Math.Max(0f, System.Math.Min(0.95f, playerPerc));
                newDamage *= (1f - playerPerc);

                __result = newDamage;
            }
        }

        [HarmonyPatch(typeof(EntityAgent), "ReceiveDamage")]
        public class EntityAgent_ReceiveDamage_PlayerAttackPower_Patch
        {
            public static void Prefix(EntityAgent __instance, DamageSource damageSource, ref float damage)
            {
                if (__instance?.WatchedAttributes != null)
                {
                    try
                    {
                        if (__instance.WatchedAttributes.GetBool("alegacyvsquest:bossclone:invulnerable", false))
                        {
                            damage = 0f;
                            if (damageSource != null)
                            {
                                damageSource.KnockbackStrength = 0f;
                            }
                            return;
                        }
                    }
                    catch
                    {
                    }
                }

                if (damage <= 0f) return;

                if (damageSource?.SourceEntity?.WatchedAttributes != null)
                {
                    try
                    {
                        float mult = damageSource.SourceEntity.WatchedAttributes.GetFloat("alegacyvsquest:bossclone:damagemult", 0f);
                        if (mult > 0f && mult < 0.999f)
                        {
                            damage *= mult;
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        float growthMult = damageSource.SourceEntity.WatchedAttributes.GetFloat("alegacyvsquest:bossgrowthritual:damagemult", 0f);
                        if (growthMult > 0f && Math.Abs(growthMult - 1f) > 0.001f)
                        {
                            damage *= growthMult;
                        }
                    }
                    catch
                    {
                    }
                }

                if (damageSource?.SourceEntity is not EntityPlayer byEntity) return;
                if (byEntity.WatchedAttributes == null) return;

                float bonus = byEntity.WatchedAttributes.GetFloat("vsquestadmin:attr:attackpower", 0f);
                if (bonus != 0f)
                {
                    damage += bonus;
                }
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "OnGameTick")]
        public class EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth_Patch
        {
            public static void Prefix(EntityBehaviorBodyTemperature __instance)
            {
                var entity = __instance?.entity as EntityPlayer;
                if (entity?.WatchedAttributes == null) return;

                float desiredBonus = entity.WatchedAttributes.GetFloat("vsquestadmin:attr:warmth", 0f);

                const string AppliedKey = "vsquestadmin:attr:warmth:applied";
                const string LastWearableHoursKey = "vsquestadmin:attr:warmth:lastwearablehours";

                try
                {
                    var clothingBonusField = AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "clothingBonus");
                    var lastWearableHoursField = AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "lastWearableHoursTotalUpdate");

                    if (clothingBonusField == null || lastWearableHoursField == null) return;

                    double lastWearableHours = (double)lastWearableHoursField.GetValue(__instance);
                    double storedLastWearableHours = entity.WatchedAttributes.GetDouble(LastWearableHoursKey, double.NaN);

                    if (double.IsNaN(storedLastWearableHours) || storedLastWearableHours != lastWearableHours)
                    {
                        entity.WatchedAttributes.SetDouble(LastWearableHoursKey, lastWearableHours);
                        entity.WatchedAttributes.SetFloat(AppliedKey, 0f);
                    }

                    float appliedBonus = entity.WatchedAttributes.GetFloat(AppliedKey, 0f);
                    float delta = desiredBonus - appliedBonus;
                    if (delta == 0f) return;

                    float cur = (float)clothingBonusField.GetValue(__instance);
                    clothingBonusField.SetValue(__instance, cur + delta);

                    entity.WatchedAttributes.SetFloat(AppliedKey, desiredBonus);
                }
                catch
                {
                }
            }
        }

        [HarmonyPatch(typeof(EntityAgent), "OnGameTick")]
        public class EntityAgent_OnGameTick_BossCloneSpeedMult_Patch
        {
            public static void Prefix(EntityAgent __instance)
            {
                if (__instance?.WatchedAttributes == null) return;
                if (__instance.Stats == null) return;

                const string MultKey = "alegacyvsquest:bossclone:walkspeedmult";
                const string BaseKey = "alegacyvsquest:bossclone:walkspeedbase";
                const string AppliedKey = "alegacyvsquest:bossclone:walkspeedapplied";

                float mult = __instance.WatchedAttributes.GetFloat(MultKey, 0f);
                if (mult <= 0f) return;
                if (mult >= 0.999f && mult <= 1.001f) return;

                float applied = __instance.WatchedAttributes.GetFloat(AppliedKey, 0f);
                if (applied == mult) return;

                float baseWalkSpeed = __instance.WatchedAttributes.GetFloat(BaseKey, 0f);
                if (baseWalkSpeed <= 0f)
                {
                    baseWalkSpeed = __instance.Stats.GetBlended("walkspeed");
                    if (baseWalkSpeed > 0f)
                    {
                        __instance.WatchedAttributes.SetFloat(BaseKey, baseWalkSpeed);
                    }
                }

                if (baseWalkSpeed <= 0f) return;

                __instance.Stats.Set("walkspeed", "alegacyvsquest", baseWalkSpeed * mult, true);
                __instance.WatchedAttributes.SetFloat(AppliedKey, mult);
            }
        }
    }
}
