using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossCastPhase : EntityBehavior
    {
        private const string CastStageKey = "alegacyvsquest:bosscastphase:stage";
        private const string LastCastStartMsKey = "alegacyvsquest:bosscastphase:lastStartMs";

        private class CastStage
        {
            public float whenHealthRelBelow;
            public int castMs;
            public int windupMs;
            public float cooldownSeconds;

            public float healPerSecond;
            public float healRelPerSecond;
            public float incomingDamageMultiplier;

            public string animation;
            public string sound;
            public float soundRange;
            public int soundStartMs;

            public string loopSound;
            public float loopSoundRange;
            public int loopSoundIntervalMs;
        }

        private ICoreServerAPI sapi;
        private readonly List<CastStage> stages = new List<CastStage>();

        private bool castActive;
        private long castEndsAtMs;
        private long castStartedAtMs;
        private float lockedYaw;
        private bool yawLocked;
        private int activeStageIndex = -1;

        private readonly BossBehaviorUtils.LoopSound loopSoundPlayer = new BossBehaviorUtils.LoopSound();

        public EntityBehaviorBossCastPhase(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosscastphase";

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

                    var stage = new CastStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        castMs = stageObj["castMs"].AsInt(2500),
                        windupMs = stageObj["windupMs"].AsInt(0),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),

                        healPerSecond = stageObj["healPerSecond"].AsFloat(0f),
                        healRelPerSecond = stageObj["healRelPerSecond"].AsFloat(0f),
                        incomingDamageMultiplier = stageObj["incomingDamageMultiplier"].AsFloat(1f),

                        animation = stageObj["animation"].AsString(null),
                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),

                        loopSound = stageObj["loopSound"].AsString(null),
                        loopSoundRange = stageObj["loopSoundRange"].AsFloat(24f),
                        loopSoundIntervalMs = stageObj["loopSoundIntervalMs"].AsInt(900),
                    };

                    if (stage.castMs <= 0) stage.castMs = 500;
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
                StopCast();
                return;
            }

            if (castActive)
            {
                BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
                ApplyHealing(dt);

                if (sapi.World.ElapsedMilliseconds >= castEndsAtMs)
                {
                    StopCast();
                }

                return;
            }

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageProgress = entity.WatchedAttributes?.GetInt(CastStageKey, 0) ?? 0;
            for (int i = stageProgress; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (frac <= stage.whenHealthRelBelow)
                {
                    if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastCastStartMsKey, stage.cooldownSeconds)) return;

                    entity.WatchedAttributes.SetInt(CastStageKey, i + 1);
                    entity.WatchedAttributes.MarkPathDirty(CastStageKey);

                    StartCast(stage, i);
                    break;
                }
            }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!castActive) return;
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count) return;

            float mult = stages[activeStageIndex].incomingDamageMultiplier;
            if (mult >= 0f && mult < 0.9999f)
            {
                damage *= mult;
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopCast();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopCast();
            base.OnEntityDespawn(despawn);
        }

        private void StartCast(CastStage stage, int stageIndex)
        {
            if (sapi == null || entity == null || stage == null) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastCastStartMsKey);

            castActive = true;
            activeStageIndex = stageIndex;
            castStartedAtMs = sapi.World.ElapsedMilliseconds;

            BossBehaviorUtils.StopAiAndFreeze(entity);
            BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);

            TryPlaySound(stage);
            TryStartLoopSound(stage);

            if (stage.windupMs > 0)
            {
                castEndsAtMs = castStartedAtMs + stage.windupMs + stage.castMs;
                sapi.Event.RegisterCallback(_ =>
                {
                    TryPlayAnimation(stage.animation);
                }, stage.windupMs);
            }
            else
            {
                castEndsAtMs = castStartedAtMs + stage.castMs;
                TryPlayAnimation(stage.animation);
            }
        }

        private void StopCast()
        {
            if (!castActive && activeStageIndex < 0) return;

            castActive = false;
            yawLocked = false;

            castStartedAtMs = 0;
            castEndsAtMs = 0;

            loopSoundPlayer.Stop();

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                var anim = stages[activeStageIndex].animation;
                if (!string.IsNullOrWhiteSpace(anim))
                {
                    try
                    {
                        entity?.AnimManager?.StopAnimation(anim);
                    }
                    catch
                    {
                    }
                }
            }

            activeStageIndex = -1;
        }

        private void ApplyHealing(float dt)
        {
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count) return;
            var stage = stages[activeStageIndex];

            if (stage.healPerSecond <= 0f && stage.healRelPerSecond <= 0f) return;
            if (!BossBehaviorUtils.TryGetHealth(entity, out var healthTree, out float curHealth, out float maxHealth)) return;

            float absHeal = stage.healPerSecond > 0f ? stage.healPerSecond * dt : 0f;
            float relHeal = stage.healRelPerSecond > 0f ? stage.healRelPerSecond * maxHealth * dt : 0f;
            float heal = absHeal + relHeal;
            if (heal <= 0f) return;

            float newHealth = Math.Min(maxHealth, curHealth + heal);
            healthTree.SetFloat("currenthealth", newHealth);
            entity.WatchedAttributes.MarkPathDirty("health");
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

        private void TryPlaySound(CastStage stage)
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

        private void TryStartLoopSound(CastStage stage)
        {
            if (sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.loopSound)) return;

            loopSoundPlayer.Start(sapi, entity, stage.loopSound, stage.loopSoundRange, stage.loopSoundIntervalMs);
        }
    }
}
