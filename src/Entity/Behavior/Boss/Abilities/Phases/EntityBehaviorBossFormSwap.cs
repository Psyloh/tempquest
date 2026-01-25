using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossFormSwap : EntityBehavior
    {
        private const string LastSwapStartMsKey = "alegacyvsquest:bossformswap:lastStartMs";
        private const string AnchorKeyPrefix = "alegacyvsquest:spawner:";
        private const string TargetIdKey = "alegacyvsquest:killaction:targetid";

        private ICoreServerAPI sapi;
        private string alternateEntityCode;
        private float swapChance;
        private float whenHealthRelBelow;
        private float cooldownSeconds;
        private int checkIntervalMs;
        private bool requireTarget;
        private bool keepHealthFraction;

        private string swapSound;
        private float swapSoundRange;
        private int swapSoundStartMs;

        private long lastCheckMs;

        public EntityBehaviorBossFormSwap(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossformswap";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;

            alternateEntityCode = attributes["alternateEntityCode"].AsString(null);
            swapChance = attributes["swapChance"].AsFloat(0.03f);
            whenHealthRelBelow = attributes["whenHealthRelBelow"].AsFloat(1f);
            cooldownSeconds = attributes["cooldownSeconds"].AsFloat(18f);
            checkIntervalMs = attributes["checkIntervalMs"].AsInt(500);
            requireTarget = attributes["requireTarget"].AsBool(true);
            keepHealthFraction = attributes["keepHealthFraction"].AsBool(true);

            swapSound = attributes["sound"].AsString(null);
            swapSoundRange = attributes["soundRange"].AsFloat(24f);
            swapSoundStartMs = attributes["soundStartMs"].AsInt(0);

            if (swapChance < 0f) swapChance = 0f;
            if (swapChance > 1f) swapChance = 1f;
            if (whenHealthRelBelow <= 0f) whenHealthRelBelow = 1f;
            if (whenHealthRelBelow > 1f) whenHealthRelBelow = 1f;
            if (cooldownSeconds < 0f) cooldownSeconds = 0f;
            if (checkIntervalMs < 100) checkIntervalMs = 100;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (sapi == null || entity == null) return;
            if (!entity.Alive) return;
            if (string.IsNullOrWhiteSpace(alternateEntityCode)) return;

            long nowMs = sapi.World.ElapsedMilliseconds;
            if (nowMs - lastCheckMs < checkIntervalMs) return;
            lastCheckMs = nowMs;

            if (requireTarget && !entity.WatchedAttributes.GetBool(BossBehaviorUtils.HasTargetKey, false)) return;

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;
            if (frac > whenHealthRelBelow) return;

            if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastSwapStartMsKey, cooldownSeconds)) return;

            if (sapi.World.Rand.NextDouble() >= swapChance) return;

            TrySwapForm(frac);
        }

        private void TrySwapForm(float healthFraction)
        {
            if (sapi == null || entity == null) return;

            Entity newEntity = null;
            try
            {
                var type = sapi.World.GetEntityType(new AssetLocation(alternateEntityCode));
                if (type == null) return;

                newEntity = sapi.World.ClassRegistry.CreateEntity(type);
                if (newEntity == null) return;

                CopyTargetId(newEntity);
                CopyAnchor(newEntity);

                Vec3d pos = new Vec3d(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
                int dim = entity.ServerPos.Dimension;
                float yaw = entity.ServerPos.Yaw;

                newEntity.ServerPos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + dim * 32768.0, pos.Z));
                newEntity.ServerPos.Yaw = yaw;
                newEntity.Pos.SetFrom(newEntity.ServerPos);

                TryPlaySwapSound();

                sapi.World.SpawnEntity(newEntity);

                if (keepHealthFraction)
                {
                    float fraction = GameMath.Clamp(healthFraction, 0.05f, 1f);
                    sapi.Event.RegisterCallback(_ =>
                    {
                        TryApplyHealthFraction(newEntity, fraction);
                    }, 1);
                }

                BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastSwapStartMsKey);

                sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
            catch
            {
                if (newEntity != null)
                {
                    try
                    {
                        sapi?.World?.DespawnEntity(newEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void TryPlaySwapSound()
        {
            if (sapi == null) return;
            if (string.IsNullOrWhiteSpace(swapSound)) return;

            AssetLocation soundLoc = AssetLocation.Create(swapSound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            if (swapSoundStartMs > 0)
            {
                sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, swapSoundRange);
                    }
                    catch
                    {
                    }
                }, swapSoundStartMs);
            }
            else
            {
                try
                {
                    sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, swapSoundRange);
                }
                catch
                {
                }
            }
        }

        private void TryApplyHealthFraction(Entity target, float fraction)
        {
            if (target == null) return;
            if (!BossBehaviorUtils.TryGetHealth(target, out var healthTree, out float cur, out float maxHealth)) return;

            float newHealth = Math.Max(1f, maxHealth * fraction);
            if (healthTree != null)
            {
                healthTree.SetFloat("currenthealth", newHealth);
                target.WatchedAttributes.MarkPathDirty("health");
            }
        }

        private void CopyTargetId(Entity newEntity)
        {
            try
            {
                string targetId = entity?.WatchedAttributes?.GetString(TargetIdKey, null);
                if (string.IsNullOrWhiteSpace(targetId) || newEntity?.WatchedAttributes == null) return;

                newEntity.WatchedAttributes.SetString(TargetIdKey, targetId);
                newEntity.WatchedAttributes.MarkPathDirty(TargetIdKey);
            }
            catch
            {
            }
        }

        private void CopyAnchor(Entity newEntity)
        {
            try
            {
                if (newEntity?.WatchedAttributes == null || entity?.WatchedAttributes == null) return;

                int dim = entity.WatchedAttributes.GetInt(AnchorKeyPrefix + "dim", int.MinValue);
                int x = entity.WatchedAttributes.GetInt(AnchorKeyPrefix + "x", int.MinValue);
                int y = entity.WatchedAttributes.GetInt(AnchorKeyPrefix + "y", int.MinValue);
                int z = entity.WatchedAttributes.GetInt(AnchorKeyPrefix + "z", int.MinValue);

                if (dim == int.MinValue || x == int.MinValue || y == int.MinValue || z == int.MinValue) return;

                newEntity.WatchedAttributes.SetInt(AnchorKeyPrefix + "x", x);
                newEntity.WatchedAttributes.SetInt(AnchorKeyPrefix + "y", y);
                newEntity.WatchedAttributes.SetInt(AnchorKeyPrefix + "z", z);
                newEntity.WatchedAttributes.SetInt(AnchorKeyPrefix + "dim", dim);

                newEntity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "x");
                newEntity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "y");
                newEntity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "z");
                newEntity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "dim");
            }
            catch
            {
            }
        }
    }
}
