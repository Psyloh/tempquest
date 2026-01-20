using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossCloning : EntityBehavior
    {
        private const string CloneStageKey = "alegacyvsquest:bossclonestage";
        private const string LastCloneStartMsKey = "alegacyvsquest:bossclone:lastStartMs";
        private const string CloneOwnerIdKey = "alegacyvsquest:bossclone:ownerid";

        private const string TargetIdKey = "alegacyvsquest:killaction:targetid";
        private const string AnchorKeyPrefix = "alegacyvsquest:spawner:";

        private class CloneStage
        {
            public float whenHealthRelBelow;
            public int cloneCount;
            public int durationMs;
            public float cooldownSeconds;
            public float spawnRange;
            public float cloneDamageMult;
            public float cloneWalkSpeedMult;
            public bool cloneInvulnerable;
        }

        private ICoreServerAPI sapi;
        private readonly List<CloneStage> stages = new List<CloneStage>();

        private bool cloningActive;
        private long cloningEndsAtMs;
        private readonly List<long> activeCloneEntityIds = new List<long>();

        public EntityBehaviorBossCloning(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosscloning";

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

                    var stage = new CloneStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        cloneCount = stageObj["cloneCount"].AsInt(2),
                        durationMs = stageObj["durationMs"].AsInt(12000),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),
                        spawnRange = stageObj["spawnRange"].AsFloat(6f),
                        cloneDamageMult = stageObj["cloneDamageMult"].AsFloat(0.35f),
                        cloneWalkSpeedMult = stageObj["cloneWalkSpeedMult"].AsFloat(0.85f),
                        cloneInvulnerable = stageObj["cloneInvulnerable"].AsBool(true)
                    };

                    if (stage.cloneCount > 0)
                    {
                        stages.Add(stage);
                    }
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

            if (IsCloneEntity())
            {
                DespawnIfOwnerMissing();
                return;
            }

            if (!entity.Alive)
            {
                CleanupClones();
                return;
            }

            if (cloningActive)
            {
                if (sapi.World.ElapsedMilliseconds >= cloningEndsAtMs)
                {
                    CleanupClones();
                }
                return;
            }

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageProgress = entity.WatchedAttributes?.GetInt(CloneStageKey, 0) ?? 0;
            for (int i = stageProgress; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (frac <= stage.whenHealthRelBelow)
                {
                    if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastCloneStartMsKey, stage.cooldownSeconds)) return;

                    entity.WatchedAttributes.SetInt(CloneStageKey, i + 1);
                    entity.WatchedAttributes.MarkPathDirty(CloneStageKey);

                    StartCloning(stage);
                    break;
                }
            }
        }

        private void StartCloning(CloneStage stage)
        {
            cloningActive = true;
            cloningEndsAtMs = sapi.World.ElapsedMilliseconds + Math.Max(500, stage.durationMs);

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastCloneStartMsKey);

            CleanupClones();
            SpawnClones(stage);
        }

        private void SpawnClones(CloneStage stage)
        {
            if (sapi == null || entity == null) return;

            string code = entity.Code?.ToShortString();
            if (string.IsNullOrWhiteSpace(code)) return;

            var type = sapi.World.GetEntityType(new AssetLocation(code));
            if (type == null) return;

            Vec3d basePos = new Vec3d(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            int dim = entity.ServerPos.Dimension;
            float yaw = entity.ServerPos.Yaw;

            int count = Math.Max(1, stage.cloneCount);
            for (int i = 0; i < count; i++)
            {
                try
                {
                    Entity clone = sapi.World.ClassRegistry.CreateEntity(type);
                    if (clone == null) continue;

                    CopyTargetId(clone);
                    CopyAnchor(clone);
                    ApplyCloneAttributes(clone, stage);

                    Vec3d offset = RandomOffset(stage.spawnRange);
                    clone.ServerPos.SetPosWithDimension(new Vec3d(basePos.X + offset.X, basePos.Y + dim * 32768.0, basePos.Z + offset.Z));
                    clone.ServerPos.Yaw = yaw + (float)((sapi.World.Rand.NextDouble() - 0.5) * 0.4);
                    clone.Pos.SetFrom(clone.ServerPos);

                    sapi.World.SpawnEntity(clone);

                    activeCloneEntityIds.Add(clone.EntityId);
                }
                catch
                {
                }
            }
        }

        private Vec3d RandomOffset(float range)
        {
            double r = range;
            if (r < 0.5) r = 0.5;

            double angle = sapi.World.Rand.NextDouble() * Math.PI * 2.0;
            double dist = sapi.World.Rand.NextDouble() * r;
            return new Vec3d(Math.Cos(angle) * dist, 0, Math.Sin(angle) * dist);
        }

        private void ApplyCloneAttributes(Entity clone, CloneStage stage)
        {
            if (clone?.WatchedAttributes == null) return;

            try
            {
                clone.WatchedAttributes.SetBool("showHealthbar", false);
                clone.WatchedAttributes.MarkPathDirty("showHealthbar");
            }
            catch
            {
            }

            try
            {
                clone.WatchedAttributes.SetBool("alegacyvsquest:bossclone", true);
                clone.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone");
            }
            catch
            {
            }

            try
            {
                clone.WatchedAttributes.SetLong(CloneOwnerIdKey, entity.EntityId);
                clone.WatchedAttributes.MarkPathDirty(CloneOwnerIdKey);
            }
            catch
            {
            }

            try
            {
                clone.WatchedAttributes.SetBool("alegacyvsquest:bossclone:invulnerable", stage.cloneInvulnerable);
                clone.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:invulnerable");
            }
            catch
            {
            }

            try
            {
                if (stage.cloneDamageMult > 0f)
                {
                    clone.WatchedAttributes.SetFloat("alegacyvsquest:bossclone:damagemult", stage.cloneDamageMult);
                    clone.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:damagemult");
                }
            }
            catch
            {
            }

            try
            {
                if (stage.cloneWalkSpeedMult > 0f)
                {
                    clone.WatchedAttributes.SetFloat("alegacyvsquest:bossclone:walkspeedmult", stage.cloneWalkSpeedMult);
                    clone.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:walkspeedmult");
                }
            }
            catch
            {
            }
        }

        private void CleanupClones()
        {
            cloningActive = false;

            if (sapi == null) return;

            for (int i = 0; i < activeCloneEntityIds.Count; i++)
            {
                long id = activeCloneEntityIds[i];
                if (id <= 0) continue;

                try
                {
                    var e = sapi.World.GetEntityById(id);
                    if (e != null)
                    {
                        sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                }
                catch
                {
                }
            }

            activeCloneEntityIds.Clear();
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            CleanupClones();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            CleanupClones();
            base.OnEntityDespawn(despawn);
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

        private bool IsCloneEntity()
        {
            try
            {
                return entity?.WatchedAttributes?.GetBool("alegacyvsquest:bossclone", false) ?? false;
            }
            catch
            {
                return false;
            }
        }

        private void DespawnIfOwnerMissing()
        {
            if (sapi == null || entity == null) return;

            long ownerId = 0;
            try
            {
                ownerId = entity.WatchedAttributes.GetLong(CloneOwnerIdKey, 0);
            }
            catch
            {
            }

            if (ownerId <= 0)
            {
                sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                return;
            }

            var owner = sapi.World.GetEntityById(ownerId);
            if (owner == null || !owner.Alive)
            {
                sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
        }
    }
}
