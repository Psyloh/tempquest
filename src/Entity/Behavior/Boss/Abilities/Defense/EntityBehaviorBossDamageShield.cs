using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossDamageShield : EntityBehavior
    {
        private const string ShieldStageKey = "alegacyvsquest:bossdamageshield:stage";
        private const string LastShieldStartMsKey = "alegacyvsquest:bossdamageshield:lastStartMs";

        private class ShieldStage
        {
            public float whenHealthRelBelow;
            public int shieldMs;
            public int windupMs;
            public float cooldownSeconds;

            public bool repeatable;
            public bool immobileDuringShield;
            public bool lockYawDuringShield;

            public float incomingDamageMultiplier;

            public string animation;
            public int animationStopMs;

            public string sound;
            public float soundRange;
            public int soundStartMs;

            public string loopSound;
            public float loopSoundRange;
            public int loopSoundIntervalMs;
        }

        private ICoreServerAPI sapi;
        private readonly List<ShieldStage> stages = new List<ShieldStage>();

        private bool shieldActive;
        private long shieldEndsAtMs;
        private long shieldStartedAtMs;
        private int activeStageIndex = -1;

        private long startShieldCallbackId;
        private readonly BossBehaviorUtils.LoopSound loopSoundPlayer = new BossBehaviorUtils.LoopSound();

        private bool immobileDuringShield;
        private bool lockYawDuringShield;
        private bool yawLocked;
        private float lockedYaw;

        public EntityBehaviorBossDamageShield(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossdamageshield";

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

                    var stage = new ShieldStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        shieldMs = stageObj["shieldMs"].AsInt(2500),
                        windupMs = stageObj["windupMs"].AsInt(0),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),

                        repeatable = stageObj["repeatable"].AsBool(false),
                        immobileDuringShield = stageObj["immobile"].AsBool(false),
                        lockYawDuringShield = stageObj["lockYaw"].AsBool(false),

                        incomingDamageMultiplier = stageObj["incomingDamageMultiplier"].AsFloat(0.25f),

                        animation = stageObj["animation"].AsString(null),
                        animationStopMs = stageObj["animationStopMs"].AsInt(0),

                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),

                        loopSound = stageObj["loopSound"].AsString(null),
                        loopSoundRange = stageObj["loopSoundRange"].AsFloat(24f),
                        loopSoundIntervalMs = stageObj["loopSoundIntervalMs"].AsInt(900),
                    };

                    if (stage.shieldMs <= 0) stage.shieldMs = 500;
                    if (stage.windupMs < 0) stage.windupMs = 0;
                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;

                    if (stage.incomingDamageMultiplier < 0f) stage.incomingDamageMultiplier = 0f;
                    if (stage.incomingDamageMultiplier > 1f) stage.incomingDamageMultiplier = 1f;

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
                StopShield();
                return;
            }

            if (shieldActive)
            {
                if (immobileDuringShield)
                {
                    BossBehaviorUtils.StopAiAndFreeze(entity);
                }

                if (lockYawDuringShield)
                {
                    BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
                }

                if (sapi.World.ElapsedMilliseconds >= shieldEndsAtMs)
                {
                    StopShield();
                }

                return;
            }

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageProgress = entity.WatchedAttributes?.GetInt(ShieldStageKey, 0) ?? 0;
            for (int i = stageProgress; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (frac <= stage.whenHealthRelBelow)
                {
                    if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastShieldStartMsKey, stage.cooldownSeconds)) return;

                    if (!stage.repeatable)
                    {
                        entity.WatchedAttributes.SetInt(ShieldStageKey, i + 1);
                        entity.WatchedAttributes.MarkPathDirty(ShieldStageKey);
                    }

                    StartShield(stage, i);
                    break;
                }
            }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!shieldActive) return;
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count) return;

            float mult = stages[activeStageIndex].incomingDamageMultiplier;
            if (mult >= 0f && mult < 0.9999f)
            {
                damage *= mult;
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopShield();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopShield();
            base.OnEntityDespawn(despawn);
        }

        private void StartShield(ShieldStage stage, int stageIndex)
        {
            if (sapi == null || entity == null || stage == null) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastShieldStartMsKey);

            shieldActive = true;
            activeStageIndex = stageIndex;
            immobileDuringShield = stage.immobileDuringShield;
            lockYawDuringShield = stage.lockYawDuringShield;
            yawLocked = false;

            shieldStartedAtMs = sapi.World.ElapsedMilliseconds;
            shieldEndsAtMs = shieldStartedAtMs + Math.Max(200, stage.windupMs + stage.shieldMs);

            BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref startShieldCallbackId);

            TryPlaySound(stage);
            TryStartLoopSound(stage);

            if (immobileDuringShield)
            {
                BossBehaviorUtils.StopAiAndFreeze(entity);
            }

            if (lockYawDuringShield)
            {
                BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
            }

            if (stage.windupMs > 0)
            {
                startShieldCallbackId = sapi.Event.RegisterCallback(_ =>
                {
                    TryPlayAnimation(stage);
                }, stage.windupMs);
            }
            else
            {
                TryPlayAnimation(stage);
            }
        }

        private void StopShield()
        {
            BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref startShieldCallbackId);

            shieldActive = false;
            immobileDuringShield = false;
            lockYawDuringShield = false;
            yawLocked = false;

            shieldStartedAtMs = 0;
            shieldEndsAtMs = 0;

            loopSoundPlayer.Stop();

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                var stage = stages[activeStageIndex];
                if (!string.IsNullOrWhiteSpace(stage.animation))
                {
                    try
                    {
                        entity?.AnimManager?.StopAnimation(stage.animation);
                    }
                    catch
                    {
                    }
                }
            }

            activeStageIndex = -1;
        }

        private void TryPlayAnimation(ShieldStage stage)
        {
            if (stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.animation)) return;

            try
            {
                entity?.AnimManager?.StartAnimation(stage.animation);
            }
            catch
            {
            }

            int stopMs = stage.animationStopMs;
            if (stopMs <= 0) return;

            try
            {
                if (sapi != null)
                {
                    sapi.Event.RegisterCallback(_ =>
                    {
                        try
                        {
                            entity?.AnimManager?.StopAnimation(stage.animation);
                        }
                        catch
                        {
                        }
                    }, stopMs);
                }
            }
            catch
            {
            }
        }

        private void TryPlaySound(ShieldStage stage)
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
                        sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange);
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
                    sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange);
                }
                catch
                {
                }
            }
        }

        private void TryStartLoopSound(ShieldStage stage)
        {
            if (sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.loopSound)) return;

            loopSoundPlayer.Start(sapi, entity, stage.loopSound, stage.loopSoundRange, stage.loopSoundIntervalMs);
        }
    }
}
