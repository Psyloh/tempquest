using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossGrab : EntityBehavior
    {
        private const string GrabStageKey = "alegacyvsquest:bossgrab:stage";
        private const string LastGrabStartMsKey = "alegacyvsquest:bossgrab:lastStartMs";
        private const string WalkSpeedStatCode = "alegacyvsquest:bossgrab";
        private const string WalkSpeedStatCodeLegacy = "alegacyvsquest";

        private class GrabStage
        {
            public float whenHealthRelBelow;
            public float cooldownSeconds;

            public float minTargetRange;
            public float maxTargetRange;

            public int windupMs;
            public int grabMs;
            public float victimWalkSpeedMult;

            public int damageIntervalMs;
            public float damage;
            public int damageTier;
            public string damageType;

            public string windupAnimation;
            public string grabAnimation;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;
        }

        private ICoreServerAPI sapi;
        private readonly List<GrabStage> stages = new List<GrabStage>();

        private bool grabActive;
        private long grabEndsAtMs;
        private long grabStartedAtMs;
        private long grabStartCallbackId;
        private long grabTickListenerId;

        private int activeStageIndex = -1;
        private EntityPlayer targetPlayer;

        private long nextDamageAtMs;

        public EntityBehaviorBossGrab(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossgrab";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;

            stages.Clear();
            try
            {
                foreach (var stageObj in attributes["stages"].AsArray())
                {
                    if (stageObj == null || !stageObj.Exists) continue;

                    var stage = new GrabStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),

                        minTargetRange = stageObj["minTargetRange"].AsFloat(0f),
                        maxTargetRange = stageObj["maxTargetRange"].AsFloat(3.0f),

                        windupMs = stageObj["windupMs"].AsInt(150),
                        grabMs = stageObj["grabMs"].AsInt(2500),
                        victimWalkSpeedMult = stageObj["victimWalkSpeedMult"].AsFloat(0.08f),

                        damageIntervalMs = stageObj["damageIntervalMs"].AsInt(500),
                        damage = stageObj["damage"].AsFloat(2f),
                        damageTier = stageObj["damageTier"].AsInt(3),
                        damageType = stageObj["damageType"].AsString("PiercingAttack"),

                        windupAnimation = stageObj["windupAnimation"].AsString(null),
                        grabAnimation = stageObj["grabAnimation"].AsString(null),

                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),
                        soundVolume = stageObj["soundVolume"].AsFloat(1f),
                    };

                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;
                    if (stage.minTargetRange < 0f) stage.minTargetRange = 0f;
                    if (stage.maxTargetRange < stage.minTargetRange) stage.maxTargetRange = stage.minTargetRange;
                    if (stage.windupMs < 0) stage.windupMs = 0;
                    if (stage.grabMs <= 0) stage.grabMs = 250;
                    if (stage.victimWalkSpeedMult < 0f) stage.victimWalkSpeedMult = 0f;
                    if (stage.damageIntervalMs <= 0) stage.damageIntervalMs = 250;
                    if (stage.damage < 0f) stage.damage = 0f;
                    if (stage.damageTier < 0) stage.damageTier = 0;

                    if (stage.soundVolume <= 0f) stage.soundVolume = 1f;

                    stages.Add(stage);
                }
            }
            catch
            {
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (sapi == null || entity == null) return;
            if (stages.Count == 0) return;

            if (!entity.Alive)
            {
                StopGrab();
                return;
            }

            if (grabActive) return;

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageIndex = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (frac <= stage.whenHealthRelBelow)
                {
                    stageIndex = i;
                }
            }

            if (stageIndex < 0 || stageIndex >= stages.Count) return;

            var activeStage = stages[stageIndex];
            if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastGrabStartMsKey, activeStage.cooldownSeconds)) return;

            if (!TryFindTarget(activeStage, out var targetEntity, out float targetDist)) return;
            if (targetDist < activeStage.minTargetRange) return;
            if (targetDist > activeStage.maxTargetRange) return;

            StartGrab(activeStage, stageIndex, targetEntity);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopGrab();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopGrab();
            base.OnEntityDespawn(despawn);
        }

        private bool TryFindTarget(GrabStage stage, out EntityPlayer target, out float dist)
        {
            target = null;
            dist = 0f;

            if (sapi == null || entity == null) return false;
            if (entity.Pos == null) return false;

            double range = Math.Max(1.5, stage.maxTargetRange > 0 ? stage.maxTargetRange : 3f);
            try
            {
                var own = entity.ServerPos.XYZ;
                float frange = (float)range;
                var found = sapi.World.GetNearestEntity(own, frange, frange, e => e is EntityPlayer) as EntityPlayer;
                if (found == null || !found.Alive) return false;

                if (found.ServerPos.Dimension != entity.ServerPos.Dimension) return false;

                target = found;
                dist = (float)found.ServerPos.DistanceTo(entity.ServerPos);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void StartGrab(GrabStage stage, int stageIndex, EntityPlayer target)
        {
            if (sapi == null || entity == null || stage == null || target == null) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastGrabStartMsKey);

            grabActive = true;
            activeStageIndex = stageIndex;
            grabStartedAtMs = sapi.World.ElapsedMilliseconds;
            targetPlayer = target;

            BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref grabStartCallbackId);
            BossBehaviorUtils.UnregisterGameTickListenerSafe(sapi, ref grabTickListenerId);

            BossBehaviorUtils.StopAiAndFreeze(entity);

            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            int windup = Math.Max(0, stage.windupMs);
            int grabMs = Math.Max(100, stage.grabMs);
            grabEndsAtMs = grabStartedAtMs + windup + grabMs;

            if (windup > 0)
            {
                grabStartCallbackId = sapi.Event.RegisterCallback(_ =>
                {
                    BeginGrab(stage);
                }, windup);
            }
            else
            {
                BeginGrab(stage);
            }
        }

        private void BeginGrab(GrabStage stage)
        {
            if (sapi == null || entity == null || stage == null) return;

            TryPlayAnimation(stage.grabAnimation);

            ApplyVictimMoveSlow(stage);

            nextDamageAtMs = sapi.World.ElapsedMilliseconds;

            grabTickListenerId = sapi.Event.RegisterGameTickListener(_ =>
            {
                try
                {
                    if (!grabActive)
                    {
                        StopGrab();
                        return;
                    }

                    long now = sapi.World.ElapsedMilliseconds;
                    if (now >= grabEndsAtMs)
                    {
                        StopGrab();
                        return;
                    }

                    if (entity == null || !entity.Alive)
                    {
                        StopGrab();
                        return;
                    }

                    if (targetPlayer == null || !targetPlayer.Alive)
                    {
                        StopGrab();
                        return;
                    }

                    if (targetPlayer.ServerPos.Dimension != entity.ServerPos.Dimension)
                    {
                        StopGrab();
                        return;
                    }

                    var stageNow = (activeStageIndex >= 0 && activeStageIndex < stages.Count) ? stages[activeStageIndex] : null;
                    if (stageNow != null)
                    {
                        float dist = (float)targetPlayer.ServerPos.DistanceTo(entity.ServerPos);
                        if (dist > stageNow.maxTargetRange + 2f)
                        {
                            StopGrab();
                            return;
                        }

                        ApplyVictimMoveSlow(stageNow);

                        if (now >= nextDamageAtMs)
                        {
                            DealGrabDamage(stageNow);
                            nextDamageAtMs = now + Math.Max(250, stageNow.damageIntervalMs);
                        }
                    }
                }
                catch
                {
                }
            }, 50);
        }

        private void ApplyVictimMoveSlow(GrabStage stage)
        {
            if (stage == null) return;
            if (targetPlayer == null) return;

            try
            {
                if (targetPlayer.Stats == null) return;

                float mult = GameMath.Clamp(stage.victimWalkSpeedMult, 0f, 1f);
                float modifier = mult - 1f;

                targetPlayer.Stats.Remove("walkspeed", WalkSpeedStatCodeLegacy);
                targetPlayer.Stats.Set("walkspeed", WalkSpeedStatCode, modifier, true);

                targetPlayer.walkSpeed = targetPlayer.Stats.GetBlended("walkspeed");
            }
            catch
            {
            }
        }

        private void DealGrabDamage(GrabStage stage)
        {
            if (stage == null || stage.damage <= 0f) return;
            if (targetPlayer == null || !targetPlayer.Alive) return;
            if (entity == null) return;

            EnumDamageType dmgType = EnumDamageType.PiercingAttack;
            try
            {
                if (!string.IsNullOrWhiteSpace(stage.damageType) && Enum.TryParse(stage.damageType, ignoreCase: true, out EnumDamageType parsed))
                {
                    dmgType = parsed;
                }
            }
            catch
            {
            }

            try
            {
                targetPlayer.ReceiveDamage(new DamageSource()
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = entity,
                    Type = dmgType,
                    DamageTier = stage.damageTier,
                    KnockbackStrength = 0f
                }, stage.damage);
            }
            catch
            {
            }
        }

        private void StopGrab()
        {
            BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref grabStartCallbackId);
            BossBehaviorUtils.UnregisterGameTickListenerSafe(sapi, ref grabTickListenerId);

            if (!grabActive && activeStageIndex < 0) return;

            grabActive = false;

            RestoreVictimMoveSpeed();

            targetPlayer = null;

            grabStartedAtMs = 0;
            grabEndsAtMs = 0;
            nextDamageAtMs = 0;

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                try
                {
                    var stage = stages[activeStageIndex];

                    if (!string.IsNullOrWhiteSpace(stage.windupAnimation))
                    {
                        entity?.AnimManager?.StopAnimation(stage.windupAnimation);
                    }

                    if (!string.IsNullOrWhiteSpace(stage.grabAnimation))
                    {
                        entity?.AnimManager?.StopAnimation(stage.grabAnimation);
                    }
                }
                catch
                {
                }
            }

            activeStageIndex = -1;
        }

        private void RestoreVictimMoveSpeed()
        {
            if (targetPlayer == null) return;

            try
            {
                if (targetPlayer.Stats == null) return;

                targetPlayer.Stats.Remove("walkspeed", WalkSpeedStatCode);
                targetPlayer.Stats.Remove("walkspeed", WalkSpeedStatCodeLegacy);
                targetPlayer.walkSpeed = targetPlayer.Stats.GetBlended("walkspeed");
            }
            catch
            {
            }
        }

        private void TryPlayAnimation(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation)) return;

            try
            {
                entity?.AnimManager?.StartAnimation(animation);
            }
            catch
            {
            }
        }

        private void TryPlaySound(GrabStage stage)
        {
            if (sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.sound)) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            if (stage.soundStartMs > 0)
            {
                sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange, stage.soundVolume);
                    }
                    catch
                    {
                    }
                }, stage.soundStartMs);
            }
            else
            {
                try
                {
                    sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange, stage.soundVolume);
                }
                catch
                {
                }
            }
        }
    }
}
