using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VsQuest;

namespace VsQuest.Harmony
{
    public class PlayerAttributePatches
    {
        private const string SecondChanceDebuffUntilKey = "alegacyvsquest:secondchance:debuffuntil";
        private const string SecondChanceDebuffStatKey = "alegacyvsquest:secondchance:debuff";

        private const string BossGrabNoSneakUntilKey = "alegacyvsquest:bossgrab:nosneakuntil";

        private const string AshFloorNoJumpUntilKey = "alegacyvsquest:ashfloor:nojumpuntil";
        private const string AshFloorNoShiftUntilKey = "alegacyvsquest:ashfloor:noshiftuntil";
        private const string AshFloorUntilKey = "alegacyvsquest:ashfloor:until";
        private const string AshFloorWalkSpeedMultKey = "alegacyvsquest:ashfloor:walkspeedmult";
        private const string AshFloorWalkSpeedStatKey = "alegacyvsquest:ashfloor";

        private const string SecondChanceProcSound = "albase:sounds/atmospheric-metallic-swipe";
        private const float SecondChanceProcSoundRange = 24f;

        private const float SecondChanceDebuffWalkspeed = -0.2f;
        private const float SecondChanceDebuffHungerRate = 0.4f;
        private const float SecondChanceDebuffHealing = -0.3f;

        private static bool IsBossTarget(EntityAgent target)
        {
            if (target == null) return false;

            return target.HasBehavior<EntityBehaviorBossHuntCombatMarker>()
                || target.HasBehavior<EntityBehaviorBossCombatMarker>()
                || target.HasBehavior<EntityBehaviorBossRespawn>()
                || target.HasBehavior<EntityBehaviorBossDespair>()
                || target.HasBehavior<EntityBehaviorQuestBoss>()
                || target.HasBehavior<EntityBehaviorBoss>();
        }

        [HarmonyPatch(typeof(ModSystemWearableStats), "handleDamaged")]
        public class ModSystemWearableStats_handleDamaged_PlayerAttributes_Patch
        {
            public static void Postfix(ModSystemWearableStats __instance, IPlayer player, float damage, DamageSource dmgSource, ref float __result)
            {
                if (__result <= 0f) return;

                if (player?.Entity?.WatchedAttributes == null) return;

                if (!IsProtectionApplicable(dmgSource)) return;

                float playerFlat = player.Entity.WatchedAttributes.GetFloat("vsquestadmin:attr:protection", 0f);
                float playerPerc = player.Entity.WatchedAttributes.GetFloat("vsquestadmin:attr:protectionperc", 0f);

                float newDamage = __result;
                newDamage = System.Math.Max(0f, newDamage - playerFlat);

                playerPerc = System.Math.Max(0f, System.Math.Min(0.95f, playerPerc));
                newDamage *= (1f - playerPerc);

                __result = newDamage;
            }
        }

        private static bool IsProtectionApplicable(DamageSource dmgSource)
        {
            EnumDamageType type;
            try
            {
                type = dmgSource?.Type ?? EnumDamageType.Injury;
            }
            catch
            {
                type = EnumDamageType.Injury;
            }

            // Apply custom armor only to direct physical damage.
            // Do not reduce suffocation/drowning, hunger, poison, fire, etc.
            return type == EnumDamageType.BluntAttack
                || type == EnumDamageType.SlashingAttack
                || type == EnumDamageType.PiercingAttack
                || type == EnumDamageType.Crushing
                || type == EnumDamageType.Injury;
        }

        [HarmonyPatch(typeof(EntityBehaviorHealth), "OnEntityReceiveDamage")]
        public class EntityBehaviorHealth_OnEntityReceiveDamage_SecondChance_Patch
        {
            public static void Prefix(EntityBehaviorHealth __instance, DamageSource damageSource, ref float damage)
            {
                if (damageSource?.Type == EnumDamageType.Heal) return;
                if (__instance?.entity is not EntityPlayer player) return;
                if (player.World?.Side == EnumAppSide.Client) return;
                if (player.Player?.InventoryManager == null) return;
                if (damage <= 0f) return;

                float health = __instance.Health;
                if (health - damage > 0f) return;

                if (!TryGetSecondChanceSlot(player, out var slot)) return;

                float charges = GetSecondChanceCharges(slot.Itemstack);
                if (charges < 1f) return;

                float targetHealth = Math.Max(0.1f, __instance.MaxHealth * 0.7f);
                __instance.Health = Math.Max(targetHealth, __instance.Health);
                damage = 0f;

                SetSecondChanceCharges(slot.Itemstack, charges - 1f);
                slot.MarkDirty();

                ApplySecondChanceDebuff(player);
                TryPlaySecondChanceSound(player);
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorHealth), "OnEntityDeath")]
        public class EntityBehaviorHealth_OnEntityDeath_SecondChanceReset_Patch
        {
            public static void Prefix(EntityBehaviorHealth __instance, DamageSource damageSourceForDeath)
            {
                if (__instance?.entity is not EntityPlayer player) return;
                if (player.Player?.InventoryManager == null) return;

                try
                {
                    if (player.Api?.Side != EnumAppSide.Server) return;
                    var sapi = player.Api as Vintagestory.API.Server.ICoreServerAPI;
                    var system = sapi?.ModLoader?.GetModSystem<VsQuest.BossHuntArenaSystem>();
                    system?.TryHandlePlayerDeath(player);
                }
                catch
                {
                }

                if (!TryGetSecondChanceSlot(player, out var slot)) return;
                SetSecondChanceCharges(slot.Itemstack, 0f);
                slot.MarkDirty();
            }
        }

        [HarmonyPatch(typeof(EntityAgent), "OnGameTick")]
        public class EntityAgent_OnGameTick_SecondChanceDebuff_Patch
        {
            public static void Prefix(EntityAgent __instance)
            {
                if (__instance is not EntityPlayer player) return;
                if (player.World.Side == EnumAppSide.Client) return;
                if (player.Stats == null) return;

                double until = player.WatchedAttributes.GetDouble(SecondChanceDebuffUntilKey, 0);
                if (until <= 0)
                {
                    ClearDebuff(player);
                    return;
                }

                double nowHours = player.World.Calendar.TotalHours;
                if (nowHours >= until)
                {
                    player.WatchedAttributes.SetDouble(SecondChanceDebuffUntilKey, 0);
                    ClearDebuff(player);
                    return;
                }

                ApplyDebuffStats(player);
            }
        }

        private static bool TryGetSecondChanceSlot(EntityPlayer player, out ItemSlot slot)
        {
            slot = null;
            var inv = player.Player?.InventoryManager?.GetOwnInventory("character");
            if (inv == null) return false;

            foreach (ItemSlot s in inv)
            {
                if (s?.Empty != false) continue;
                var stack = s.Itemstack;
                if (!ItemAttributeUtils.IsActionItem(stack)) continue;

                string key = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
                if (stack.Attributes.HasAttribute(key))
                {
                    slot = s;
                    return true;
                }
            }

            return false;
        }

        private static float GetSecondChanceCharges(ItemStack stack)
        {
            if (stack?.Attributes == null) return 0f;
            string key = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            return stack.Attributes.GetFloat(key, 0f);
        }

        private static void SetSecondChanceCharges(ItemStack stack, float value)
        {
            if (stack?.Attributes == null) return;
            string key = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            stack.Attributes.SetFloat(key, Math.Clamp(value, 0f, 3f));
        }

        private static void ApplySecondChanceDebuff(EntityPlayer player)
        {
            double until = player.World.Calendar.TotalHours + (2d / 60d);
            player.WatchedAttributes.SetDouble(SecondChanceDebuffUntilKey, until);
            ApplyDebuffStats(player);
        }

        private static void TryPlaySecondChanceSound(EntityPlayer player)
        {
            if (player?.World == null) return;
            if (string.IsNullOrWhiteSpace(SecondChanceProcSound)) return;

            try
            {
                AssetLocation soundLoc = AssetLocation.Create(SecondChanceProcSound, "game").WithPathPrefixOnce("sounds/");
                player.World.PlaySoundAt(soundLoc, player.ServerPos.X, player.ServerPos.Y, player.ServerPos.Z, null, randomizePitch: true, SecondChanceProcSoundRange);
            }
            catch
            {
            }
        }

        private static void ApplyDebuffStats(EntityPlayer player)
        {
            player.Stats.Set("walkspeed", SecondChanceDebuffStatKey, SecondChanceDebuffWalkspeed, true);
            player.Stats.Set("hungerrate", SecondChanceDebuffStatKey, SecondChanceDebuffHungerRate, true);
            player.Stats.Set("healingeffectivness", SecondChanceDebuffStatKey, SecondChanceDebuffHealing, true);
            player.walkSpeed = player.Stats.GetBlended("walkspeed");
        }

        private static void ClearDebuff(EntityPlayer player)
        {
            player.Stats.Set("walkspeed", SecondChanceDebuffStatKey, 0f, true);
            player.Stats.Set("hungerrate", SecondChanceDebuffStatKey, 0f, true);
            player.Stats.Set("healingeffectivness", SecondChanceDebuffStatKey, 0f, true);
            player.walkSpeed = player.Stats.GetBlended("walkspeed");
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
                        if (damageSource != null && IsBossTarget(__instance))
                        {
                            damageSource.KnockbackStrength = 0f;
                        }

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

                    try
                    {
                        var sourceEntity = damageSource.SourceEntity;
                        var causeEntity = damageSource.GetCauseEntity() ?? sourceEntity;
                        if (causeEntity is EntityPlayer attacker && attacker.Player?.InventoryManager != null)
                        {
                            var inv = attacker.Player.InventoryManager.GetOwnInventory("character");
                            if (inv != null)
                            {
                                float knockbackBonus = 0f;
                                foreach (ItemSlot slot in inv)
                                {
                                    if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                                    {
                                        knockbackBonus += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrKnockbackMult);
                                    }
                                }

                                if (knockbackBonus != 0f && damageSource.KnockbackStrength > 0f)
                                {
                                    float mult = GameMath.Clamp(1f + knockbackBonus, 0f, 5f);
                                    damageSource.KnockbackStrength *= mult;
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                if (damage <= 0f) return;

                if (damageSource != null)
                {
                    try
                    {
                        var sourceEntity = damageSource.SourceEntity;
                        var causeEntity = damageSource.GetCauseEntity() ?? sourceEntity;
                        var sourceAttrs = sourceEntity?.WatchedAttributes ?? causeEntity?.WatchedAttributes;
                        if (sourceAttrs != null && sourceAttrs.GetBool("alegacyvsquest:bossclone", false))
                        {
                            long ownerId = sourceAttrs.GetLong("alegacyvsquest:bossclone:ownerid", 0);
                            if (ownerId > 0 && __instance != null && __instance.EntityId == ownerId)
                            {
                                damage = 0f;
                                if (damageSource != null)
                                {
                                    damageSource.KnockbackStrength = 0f;
                                }
                                return;
                            }
                        }

                        if (sourceEntity?.WatchedAttributes != null)
                        {
                            long firedById = sourceEntity.WatchedAttributes.GetLong("firedBy", 0);
                            if (firedById > 0 && __instance?.World != null)
                            {
                                var firedByEntity = __instance.World.GetEntityById(firedById);
                                var firedByAttrs = firedByEntity?.WatchedAttributes;
                                if (firedByAttrs != null && firedByAttrs.GetBool("alegacyvsquest:bossclone", false))
                                {
                                    long ownerId = firedByAttrs.GetLong("alegacyvsquest:bossclone:ownerid", 0);
                                    if (ownerId > 0 && __instance.EntityId == ownerId)
                                    {
                                        damage = 0f;
                                        if (damageSource != null)
                                        {
                                            damageSource.KnockbackStrength = 0f;
                                        }
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        var sourceEntity = damageSource.SourceEntity;
                        var causeEntity = damageSource.GetCauseEntity() ?? sourceEntity;
                        var sourceAttrs = sourceEntity?.WatchedAttributes ?? causeEntity?.WatchedAttributes;
                        if (sourceAttrs == null) return;

                        float mult = sourceAttrs.GetFloat("alegacyvsquest:bossclone:damagemult", 0f);
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

        [HarmonyPatch(typeof(EntityAgent), "OnGameTick")]
        public class EntityAgent_OnGameTick_BossGrabDisableSneak_Client_Patch
        {
            public static void Prefix(EntityAgent __instance)
            {
                if (__instance is not EntityPlayer player) return;
                if (player.World?.Side != EnumAppSide.Client) return;
                if (player.WatchedAttributes == null) return;

                long until;
                try
                {
                    until = player.WatchedAttributes.GetLong(BossGrabNoSneakUntilKey, 0);
                }
                catch
                {
                    until = 0;
                }

                if (until <= 0) return;

                long now;
                try
                {
                    now = player.World.ElapsedMilliseconds;
                }
                catch
                {
                    now = 0;
                }

                // World.ElapsedMilliseconds resets on relog/server restart, but WatchedAttributes persist.
                // If 'until' is far in the future compared to 'now', it is almost certainly stale data.
                // In that case, clear the effect so players don't get stuck with permanent disabled sneak.
                if (until > 0 && now > 0)
                {
                    const long MaxFutureMs = 5L * 60L * 1000L;
                    if (until - now > MaxFutureMs)
                    {
                        try
                        {
                            player.WatchedAttributes.SetLong(BossGrabNoSneakUntilKey, 0);
                        }
                        catch
                        {
                        }

                        return;
                    }
                }

                if (now > 0 && now < until)
                {
                    try
                    {
                        player.Controls.Sneak = false;
                    }
                    catch
                    {
                    }
                }
            }
        }

        [HarmonyPatch(typeof(EntityAgent), "OnGameTick")]
        public class EntityAgent_OnGameTick_AshFloorDisableControls_Client_Patch
        {
            public static void Prefix(EntityAgent __instance)
            {
                if (__instance is not EntityPlayer player) return;
                if (player.World?.Side != EnumAppSide.Client) return;
                if (player.WatchedAttributes == null) return;

                double nowHours;
                try
                {
                    nowHours = player.World.Calendar.TotalHours;
                }
                catch
                {
                    nowHours = 0;
                }

                if (nowHours <= 0) return;

                try
                {
                    double untilJump = player.WatchedAttributes.GetDouble(AshFloorNoJumpUntilKey, 0);
                    if (untilJump > 0 && nowHours < untilJump)
                    {
                        player.Controls.Jump = false;
                    }
                }
                catch
                {
                }

                try
                {
                    double untilShift = player.WatchedAttributes.GetDouble(AshFloorNoShiftUntilKey, 0);
                    if (untilShift > 0 && nowHours < untilShift)
                    {
                        player.Controls.ShiftKey = false;
                        player.Controls.Sneak = false;
                    }
                }
                catch
                {
                }
            }
        }

        [HarmonyPatch(typeof(EntityAgent), "OnGameTick")]
        public class EntityAgent_OnGameTick_AshFloorWalkSpeed_Server_Patch
        {
            public static void Prefix(EntityAgent __instance)
            {
                if (__instance is not EntityPlayer player) return;
                if (player.World?.Side != EnumAppSide.Server) return;
                if (player.Stats == null) return;
                if (player.WatchedAttributes == null) return;

                double nowHours;
                try
                {
                    nowHours = (player.World as Vintagestory.API.Common.IWorldAccessor)?.Calendar?.TotalHours ?? player.World.Calendar.TotalHours;
                }
                catch
                {
                    nowHours = 0;
                }

                if (nowHours <= 0) return;

                double until;
                try
                {
                    until = player.WatchedAttributes.GetDouble(AshFloorUntilKey, 0);
                }
                catch
                {
                    until = 0;
                }

                if (until <= 0 || nowHours >= until)
                {
                    try
                    {
                        player.Stats.Set("walkspeed", AshFloorWalkSpeedStatKey, 0f, true);
                        float blended = player.Stats.GetBlended("walkspeed");
                        player.walkSpeed = float.IsNaN(blended) ? 0f : blended;
                    }
                    catch
                    {
                    }

                    return;
                }

                float mult;
                try
                {
                    mult = GameMath.Clamp(player.WatchedAttributes.GetFloat(AshFloorWalkSpeedMultKey, 0.35f), 0f, 1f);
                }
                catch
                {
                    mult = 0.35f;
                }

                if (float.IsNaN(mult)) mult = 0.35f;

                try
                {
                    float modifier = mult - 1f;
                    player.Stats.Set("walkspeed", AshFloorWalkSpeedStatKey, modifier, true);
                    float blended = player.Stats.GetBlended("walkspeed");
                    player.walkSpeed = float.IsNaN(blended) ? 0f : blended;
                }
                catch
                {
                }
            }
        }
    }
}
