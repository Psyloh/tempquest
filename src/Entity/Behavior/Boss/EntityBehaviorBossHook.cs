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
    public class EntityBehaviorBossHook : EntityBehavior
    {
        private const string HookStageKey = "alegacyvsquest:bosshook:stage";
        private const string LastHookStartMsKey = "alegacyvsquest:bosshook:lastStartMs";

        private const string WalkSpeedStatCodeHook = "alegacyvsquest:bosshook";
        private const float HookVictimWalkSpeedMult = 0.05f;

        private const long PullLogIntervalMs = 250;

        private class HookStage
        {
            public float whenHealthRelBelow;
            public float cooldownSeconds;

            public float minTargetRange;
            public float maxTargetRange;

            public int windupMs;
            public int pullMs;
            public float pullSpeed;
            public float maxPlayerMotion;

            public string windupAnimation;
            public string pullAnimation;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;
        }

        private ICoreServerAPI sapi;
        private readonly List<HookStage> stages = new List<HookStage>();

        private bool hookActive;
        private long hookEndsAtMs;
        private long hookStartedAtMs;
        private long hookStartCallbackId;
        private long hookTickListenerId;

        private long lastPullLogAtMs;

        private int activeStageIndex = -1;
        private EntityPlayer targetPlayer;

        public EntityBehaviorBossHook(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosshook";

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

                    var stage = new HookStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),

                        minTargetRange = stageObj["minTargetRange"].AsFloat(0f),
                        maxTargetRange = stageObj["maxTargetRange"].AsFloat(28f),

                        windupMs = stageObj["windupMs"].AsInt(250),
                        pullMs = stageObj["pullMs"].AsInt(850),
                        pullSpeed = stageObj["pullSpeed"].AsFloat(0.12f),
                        maxPlayerMotion = stageObj["maxPlayerMotion"].AsFloat(0.22f),

                        windupAnimation = stageObj["windupAnimation"].AsString(null),
                        pullAnimation = stageObj["pullAnimation"].AsString(null),

                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),
                        soundVolume = stageObj["soundVolume"].AsFloat(1f),
                    };

                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;
                    if (stage.minTargetRange < 0f) stage.minTargetRange = 0f;
                    if (stage.maxTargetRange < stage.minTargetRange) stage.maxTargetRange = stage.minTargetRange;
                    if (stage.windupMs < 0) stage.windupMs = 0;
                    if (stage.pullMs <= 0) stage.pullMs = 250;
                    if (stage.pullSpeed <= 0f) stage.pullSpeed = 0.08f;
                    if (stage.maxPlayerMotion <= 0f) stage.maxPlayerMotion = 0.18f;

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
                StopHook();
                return;
            }

            if (hookActive) return;

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
            if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastHookStartMsKey, activeStage.cooldownSeconds)) return;

            if (!TryFindTarget(activeStage, out var targetEntity, out float targetDist)) return;
            // Do not hard-block casting when too close. We slow the victim during hook,
            // so using minTargetRange as a hard gate can permanently prevent re-casting.
            if (targetDist < 0.75f) return;
            if (targetDist > activeStage.maxTargetRange) return;

            StartHook(activeStage, stageIndex, targetEntity);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopHook();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopHook();
            base.OnEntityDespawn(despawn);
        }

        private bool TryFindTarget(HookStage stage, out EntityPlayer target, out float dist)
        {
            target = null;
            dist = 0f;

            if (sapi == null || entity == null) return false;
            if (entity.Pos == null) return false;

            double range = Math.Max(2.0, stage.maxTargetRange > 0 ? stage.maxTargetRange : 28f);
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

        private void StartHook(HookStage stage, int stageIndex, EntityPlayer target)
        {
            if (sapi == null || entity == null || stage == null || target == null) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastHookStartMsKey);

            hookActive = true;
            activeStageIndex = stageIndex;
            hookStartedAtMs = sapi.World.ElapsedMilliseconds;
            lastPullLogAtMs = 0;
            targetPlayer = target;

            try
            {
                if (targetPlayer?.Stats != null)
                {
                    float modifier = HookVictimWalkSpeedMult - 1f;
                    targetPlayer.Stats.Remove("walkspeed", WalkSpeedStatCodeHook);
                    targetPlayer.Stats.Set("walkspeed", WalkSpeedStatCodeHook, modifier, true);
                    targetPlayer.walkSpeed = targetPlayer.Stats.GetBlended("walkspeed");
                }
            }
            catch
            {
            }

            BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref hookStartCallbackId);
            BossBehaviorUtils.UnregisterGameTickListenerSafe(sapi, ref hookTickListenerId);

            BossBehaviorUtils.StopAiAndFreeze(entity);

            try
            {
                float dist = (float)target.ServerPos.DistanceTo(entity.ServerPos);
                sapi.Logger.VerboseDebug($"[alegacyvsquest] bosshook start stage={stageIndex} target={target?.Player?.PlayerName ?? "?"} entId={target?.EntityId} dist={dist:0.00} windupMs={stage.windupMs} pullMs={stage.pullMs} pullSpeed={stage.pullSpeed:0.###} maxMotion={stage.maxPlayerMotion:0.###}");
            }
            catch
            {
            }

            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            int windup = Math.Max(0, stage.windupMs);
            int pullMs = Math.Max(100, stage.pullMs);
            hookEndsAtMs = hookStartedAtMs + windup + pullMs;

            if (windup > 0)
            {
                hookStartCallbackId = sapi.Event.RegisterCallback(_ =>
                {
                    BeginPull(stage);
                }, windup);
            }
            else
            {
                BeginPull(stage);
            }
        }

        private void BeginPull(HookStage stage)
        {
            if (sapi == null || entity == null || stage == null) return;

            TryPlayAnimation(stage.pullAnimation);

            hookTickListenerId = sapi.Event.RegisterGameTickListener(_ =>
            {
                try
                {
                    if (!hookActive)
                    {
                        StopHook();
                        return;
                    }

                    if (sapi.World.ElapsedMilliseconds >= hookEndsAtMs)
                    {
                        StopHook();
                        return;
                    }

                    if (entity == null || !entity.Alive)
                    {
                        StopHook();
                        return;
                    }

                    if (targetPlayer == null || !targetPlayer.Alive)
                    {
                        StopHook();
                        return;
                    }

                    if (targetPlayer.ServerPos.Dimension != entity.ServerPos.Dimension)
                    {
                        StopHook();
                        return;
                    }

                    var dir = new Vec3d(entity.ServerPos.X - targetPlayer.ServerPos.X, 0, entity.ServerPos.Z - targetPlayer.ServerPos.Z);
                    if (dir.Length() < 0.001) return;

                    float dist = (float)targetPlayer.ServerPos.DistanceTo(entity.ServerPos);
                    dir.Normalize();

                    double pull = stage.pullSpeed;
                    if (pull <= 0.0001) pull = 0.05;
                    if (pull < 0.18) pull = 0.18;

                    double max = stage.maxPlayerMotion;
                    if (max <= 0.0001) max = 0.25;
                    if (max < 0.35) max = 0.35;

                    float maxRange = stage.maxTargetRange > 0 ? stage.maxTargetRange : 28f;
                    float denom = Math.Max(1f, maxRange - 1f);
                    float distNorm = GameMath.Clamp((dist - 1f) / denom, 0f, 1f);
                    double scale = 1.0 + distNorm * 2.0;
                    pull *= scale;
                    max *= scale;

                    double kbX = GameMath.Clamp(dir.X * pull, -max, max);
                    double kbZ = GameMath.Clamp(dir.Z * pull, -max, max);

                    long nowMs = sapi.World.ElapsedMilliseconds;
                    if (lastPullLogAtMs == 0 || nowMs - lastPullLogAtMs >= PullLogIntervalMs)
                    {
                        lastPullLogAtMs = nowMs;
                        sapi.Logger.VerboseDebug($"[alegacyvsquest] bosshook pull target={targetPlayer?.Player?.PlayerName ?? "?"} kbX={kbX:0.###} kbZ={kbZ:0.###} dist={dist:0.00}");
                    }

                    // Use vanilla knockback module so the pull cannot be overridden by player client controls.
                    // PModuleKnockback reads kbdirX/Y/Z from WatchedAttributes and applies when Attributes.dmgkb == 1.
                    targetPlayer.WatchedAttributes.SetDouble("kbdirX", kbX);
                    targetPlayer.WatchedAttributes.SetDouble("kbdirY", 0.0);
                    targetPlayer.WatchedAttributes.SetDouble("kbdirZ", kbZ);

                    // Entity.Attributes is not synced to the client. Vanilla sets Attributes["dmgkb"] client-side
                    // via a WatchedAttributes["onHurt"] modified listener. We replicate that trigger with a tiny value.
                    targetPlayer.WatchedAttributes.SetFloat("onHurt", 0.01f);
                    targetPlayer.WatchedAttributes.SetInt("onHurtCounter", targetPlayer.WatchedAttributes.GetInt("onHurtCounter") + 1);

                    targetPlayer.WatchedAttributes.MarkPathDirty("kbdirX");
                    targetPlayer.WatchedAttributes.MarkPathDirty("kbdirY");
                    targetPlayer.WatchedAttributes.MarkPathDirty("kbdirZ");
                    targetPlayer.WatchedAttributes.MarkPathDirty("onHurt");
                    targetPlayer.WatchedAttributes.MarkPathDirty("onHurtCounter");
                }
                catch
                {
                }
            }, 25);
        }

        private void StopHook()
        {
            BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref hookStartCallbackId);
            BossBehaviorUtils.UnregisterGameTickListenerSafe(sapi, ref hookTickListenerId);

            if (!hookActive && activeStageIndex < 0) return;

            hookActive = false;

            try
            {
                sapi?.Logger?.VerboseDebug($"[alegacyvsquest] bosshook stop target={(targetPlayer?.Player?.PlayerName ?? "?")}");
            }
            catch
            {
            }

            try
            {
                // Ensure no leftover knockback impulse.
                if (targetPlayer != null)
                {
                    try
                    {
                        if (targetPlayer.Stats != null)
                        {
                            targetPlayer.Stats.Remove("walkspeed", WalkSpeedStatCodeHook);
                            targetPlayer.walkSpeed = targetPlayer.Stats.GetBlended("walkspeed");
                        }
                    }
                    catch
                    {
                    }

                    targetPlayer.WatchedAttributes.SetDouble("kbdirX", 0.0);
                    targetPlayer.WatchedAttributes.SetDouble("kbdirY", 0.0);
                    targetPlayer.WatchedAttributes.SetDouble("kbdirZ", 0.0);
                    targetPlayer.WatchedAttributes.MarkPathDirty("kbdirX");
                    targetPlayer.WatchedAttributes.MarkPathDirty("kbdirY");
                    targetPlayer.WatchedAttributes.MarkPathDirty("kbdirZ");

                    targetPlayer.WatchedAttributes.SetFloat("onHurt", 0f);
                    targetPlayer.WatchedAttributes.MarkPathDirty("onHurt");
                }
            }
            catch
            {
            }

            targetPlayer = null;

            hookStartedAtMs = 0;
            hookEndsAtMs = 0;
            lastPullLogAtMs = 0;

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                try
                {
                    var stage = stages[activeStageIndex];

                    if (!string.IsNullOrWhiteSpace(stage.windupAnimation))
                    {
                        entity?.AnimManager?.StopAnimation(stage.windupAnimation);
                    }

                    if (!string.IsNullOrWhiteSpace(stage.pullAnimation))
                    {
                        entity?.AnimManager?.StopAnimation(stage.pullAnimation);
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

        private void TryPlaySound(HookStage stage)
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
