using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossFormSwapList : EntityBehavior
    {
        private const string StageKey = "alegacyvsquest:bossformswaplist:stage";
        private const string LastSwapStartMsKey = "alegacyvsquest:bossformswaplist:lastStartMs";
        private const string AnchorKeyPrefix = "alegacyvsquest:spawner:";
        private const string TargetIdKey = "alegacyvsquest:killaction:targetid";

        private const string CloneFlagKey = "alegacyvsquest:bossplayerclone";
        private const string CloneOwnerIdKey = "alegacyvsquest:bossplayerclone:ownerid";

        private class SwapStage
        {
            public float whenHealthRelBelow;
            public string entityCode;
            public float cooldownSeconds;
            public bool requireTarget;
            public bool keepHealthFraction;
            public string sound;
            public float soundRange;
            public int soundStartMs;
        }

        private ICoreServerAPI sapi;
        private readonly List<SwapStage> stages = new List<SwapStage>();

        public EntityBehaviorBossFormSwapList(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossformswaplist";

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

                    var stage = new SwapStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        entityCode = stageObj["entityCode"].AsString(null),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),
                        requireTarget = stageObj["requireTarget"].AsBool(true),
                        keepHealthFraction = stageObj["keepHealthFraction"].AsBool(true),
                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0)
                    };

                    if (stage.whenHealthRelBelow <= 0f) stage.whenHealthRelBelow = 1f;
                    if (stage.whenHealthRelBelow > 1f) stage.whenHealthRelBelow = 1f;
                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;

                    if (!string.IsNullOrWhiteSpace(stage.entityCode))
                    {
                        stages.Add(stage);
                    }
                }
            }
            catch
            {
            }
        }

        private void TryRebindPlayerClones(long oldOwnerId, long newOwnerId)
        {
            if (sapi == null) return;
            if (oldOwnerId <= 0 || newOwnerId <= 0) return;

            var loaded = sapi.World?.LoadedEntities;
            if (loaded == null) return;

            try
            {
                foreach (var e in loaded.Values)
                {
                    if (e == null || !e.Alive) continue;

                    var wa = e.WatchedAttributes;
                    if (wa == null) continue;

                    if (!wa.GetBool(CloneFlagKey, false)) continue;

                    long owner = wa.GetLong(CloneOwnerIdKey, 0);
                    if (owner != oldOwnerId) continue;

                    wa.SetLong(CloneOwnerIdKey, newOwnerId);
                    wa.MarkPathDirty(CloneOwnerIdKey);
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
            if (!entity.Alive) return;
            if (stages.Count == 0) return;

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageProgress = entity.WatchedAttributes?.GetInt(StageKey, 0) ?? 0;
            for (int i = stageProgress; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (stage == null) continue;
                if (frac > stage.whenHealthRelBelow) continue;

                if (stage.requireTarget && !entity.WatchedAttributes.GetBool(BossBehaviorUtils.HasTargetKey, false)) return;
                if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastSwapStartMsKey, stage.cooldownSeconds)) return;

                entity.WatchedAttributes.SetInt(StageKey, i + 1);
                entity.WatchedAttributes.MarkPathDirty(StageKey);

                TrySwapForm(stage, frac);
                break;
            }
        }

        private void TrySwapForm(SwapStage stage, float healthFraction)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.entityCode)) return;

            Entity newEntity = null;
            try
            {
                long oldEntityId = entity?.EntityId ?? 0;
                var type = sapi.World.GetEntityType(new AssetLocation(stage.entityCode));
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

                TryPlaySwapSound(stage);

                sapi.World.SpawnEntity(newEntity);

                // Keep any existing player clones alive by rebinding them to the newly spawned boss entity.
                TryRebindPlayerClones(oldEntityId, newEntity.EntityId);

                if (stage.keepHealthFraction)
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
                        sapi.World.DespawnEntity(newEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void TryPlaySwapSound(SwapStage stage)
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
