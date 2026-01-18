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
    public class EntityBehaviorBossDash : EntityBehavior
    {
        private const string DashStageKey = "alegacyvsquest:bossdash:stage";
        private const string LastDashStartMsKey = "alegacyvsquest:bossdash:lastStartMs";

        private class DashStage
        {
            public float whenHealthRelBelow;
            public float cooldownSeconds;

            public float minTargetRange;
            public float maxTargetRange;

            public int windupMs;
            public int dashMs;
            public float dashSpeed;

            public string windupAnimation;
            public string dashAnimation;

            public string sound;
            public float soundRange;
            public int soundStartMs;
        }

        private ICoreServerAPI sapi;
        private readonly List<DashStage> stages = new List<DashStage>();

        private bool dashActive;
        private long dashEndsAtMs;
        private long dashStartedAtMs;
        private long dashStartCallbackId;

        private float lockedYaw;
        private bool yawLocked;
        private int activeStageIndex = -1;

        private Vec3d dashDir;

        public EntityBehaviorBossDash(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossdash";

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

                    var stage = new DashStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),

                        minTargetRange = stageObj["minTargetRange"].AsFloat(0f),
                        maxTargetRange = stageObj["maxTargetRange"].AsFloat(30f),

                        windupMs = stageObj["windupMs"].AsInt(350),
                        dashMs = stageObj["dashMs"].AsInt(650),
                        dashSpeed = stageObj["dashSpeed"].AsFloat(0.18f),

                        windupAnimation = stageObj["windupAnimation"].AsString(null),
                        dashAnimation = stageObj["dashAnimation"].AsString(null),

                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),
                    };

                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;
                    if (stage.minTargetRange < 0f) stage.minTargetRange = 0f;
                    if (stage.maxTargetRange < stage.minTargetRange) stage.maxTargetRange = stage.minTargetRange;
                    if (stage.windupMs < 0) stage.windupMs = 0;
                    if (stage.dashMs <= 0) stage.dashMs = 250;
                    if (stage.dashSpeed <= 0f) stage.dashSpeed = 0.08f;

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
                StopDash();
                return;
            }

            if (dashActive)
            {
                BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);

                if (sapi.World.ElapsedMilliseconds >= dashEndsAtMs)
                {
                    StopDash();
                }

                return;
            }

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
            if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastDashStartMsKey, activeStage.cooldownSeconds)) return;

            if (!TryFindTarget(activeStage, out var targetEntity, out float targetDist)) return;
            if (targetDist < activeStage.minTargetRange) return;
            if (targetDist > activeStage.maxTargetRange) return;

            StartDash(activeStage, stageIndex, targetEntity);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopDash();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopDash();
            base.OnEntityDespawn(despawn);
        }

        private bool TryFindTarget(DashStage stage, out Entity target, out float dist)
        {
            target = null;
            dist = 0f;

            if (sapi == null || entity == null) return false;
            if (entity.Pos == null) return false;

            double range = Math.Max(2.0, stage.maxTargetRange > 0 ? stage.maxTargetRange : 30f);
            try
            {
                var own = entity.ServerPos.XYZ;
                float frange = (float)range;
                var found = sapi.World.GetNearestEntity(own, frange, frange, e => e is EntityPlayer);
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

        private void StartDash(DashStage stage, int stageIndex, Entity target)
        {
            if (sapi == null || entity == null || stage == null || target == null) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastDashStartMsKey);

            dashActive = true;
            activeStageIndex = stageIndex;
            dashStartedAtMs = sapi.World.ElapsedMilliseconds;

            BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref dashStartCallbackId);

            BossBehaviorUtils.StopAiAndFreeze(entity);
            BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);

            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            dashDir = new Vec3d(target.ServerPos.X - entity.ServerPos.X, 0, target.ServerPos.Z - entity.ServerPos.Z);
            if (dashDir.Length() < 0.001) dashDir.Set(0, 0, 1);
            dashDir.Normalize();

            lockedYaw = (float)Math.Atan2(dashDir.X, dashDir.Z);
            yawLocked = true;

            int windup = Math.Max(0, stage.windupMs);
            int dashMs = Math.Max(100, stage.dashMs);
            dashEndsAtMs = dashStartedAtMs + windup + dashMs;

            if (windup > 0)
            {
                dashStartCallbackId = sapi.Event.RegisterCallback(_ =>
                {
                    BeginDash(stage);
                }, windup);
            }
            else
            {
                BeginDash(stage);
            }
        }

        private void BeginDash(DashStage stage)
        {
            if (entity == null || stage == null) return;

            TryPlayAnimation(stage.dashAnimation);

            try
            {
                double spd = stage.dashSpeed;
                entity.ServerPos.Motion.X = dashDir.X * spd;
                entity.ServerPos.Motion.Z = dashDir.Z * spd;
            }
            catch
            {
            }
        }

        private void StopDash()
        {
            BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref dashStartCallbackId);

            if (!dashActive && activeStageIndex < 0) return;

            dashActive = false;
            yawLocked = false;

            dashStartedAtMs = 0;
            dashEndsAtMs = 0;

            try
            {
                if (entity != null)
                {
                    entity.ServerPos.Motion.Set(0, 0, 0);
                }
            }
            catch
            {
            }

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                try
                {
                    var stage = stages[activeStageIndex];

                    if (!string.IsNullOrWhiteSpace(stage.windupAnimation))
                    {
                        entity?.AnimManager?.StopAnimation(stage.windupAnimation);
                    }

                    if (!string.IsNullOrWhiteSpace(stage.dashAnimation))
                    {
                        entity?.AnimManager?.StopAnimation(stage.dashAnimation);
                    }
                }
                catch
                {
                }
            }

            activeStageIndex = -1;
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

        private void TryPlaySound(DashStage stage)
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
    }
}
