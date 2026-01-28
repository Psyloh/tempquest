using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossPeriodicSpawn : EntityBehavior
    {
        private const string LastSpawnStartMsKey = "alegacyvsquest:bossperiodicspawn:lastStartMs";
        private const string SummonedByEntityIdKey = "alegacyvsquest:bosssummonritual:summonedByEntityId";
        private const string SummonedByEntityCodeKey = "alegacyvsquest:bosssummonritual:summonedByEntityCode";

        private class SpawnStage
        {
            public float whenHealthRelBelow;
            public float cooldownSeconds;

            public float minTargetRange;
            public float maxTargetRange;

            public string entityCode;
            public int minCount;
            public int maxCount;
            public float chance;

            public int maxNearby;
            public float nearbyRange;

            public float spawnRange;

            public bool requireHasTarget;
        }

        private ICoreServerAPI sapi;
        private readonly List<SpawnStage> stages = new List<SpawnStage>();

        public EntityBehaviorBossPeriodicSpawn(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossperiodicspawn";

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

                    var stage = new SpawnStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),

                        minTargetRange = stageObj["minTargetRange"].AsFloat(0f),
                        maxTargetRange = stageObj["maxTargetRange"].AsFloat(40f),

                        entityCode = stageObj["entityCode"].AsString(null),
                        minCount = stageObj["minCount"].AsInt(1),
                        maxCount = stageObj["maxCount"].AsInt(1),
                        chance = stageObj["chance"].AsFloat(1f),

                        maxNearby = stageObj["maxNearby"].AsInt(0),
                        nearbyRange = stageObj["nearbyRange"].AsFloat(0f),

                        spawnRange = stageObj["spawnRange"].AsFloat(8f),

                        requireHasTarget = stageObj["requireHasTarget"].AsBool(true)
                    };

                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;
                    if (stage.minTargetRange < 0f) stage.minTargetRange = 0f;
                    if (stage.maxTargetRange < stage.minTargetRange) stage.maxTargetRange = stage.minTargetRange;
                    if (stage.minCount < 1) stage.minCount = 1;
                    if (stage.maxCount < stage.minCount) stage.maxCount = stage.minCount;
                    if (stage.spawnRange <= 0f) stage.spawnRange = 4f;

                    if (!string.IsNullOrWhiteSpace(stage.entityCode) && stage.chance > 0f)
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
            if (entity.Api?.Side != EnumAppSide.Server) return;

            if (!entity.Alive) return;

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageIndex = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                if (frac <= stages[i].whenHealthRelBelow)
                {
                    stageIndex = i;
                }
            }

            if (stageIndex < 0 || stageIndex >= stages.Count) return;
            var stage = stages[stageIndex];

            if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastSpawnStartMsKey, stage.cooldownSeconds)) return;

            Entity target = null;
            float dist = 0f;
            if (!TryFindTarget(stage, out target, out dist))
            {
                if (stage.requireHasTarget) return;
            }
            else
            {
                if (dist < stage.minTargetRange) return;
                if (dist > stage.maxTargetRange) return;
            }

            if (stage.chance < 1f && sapi.World.Rand.NextDouble() > stage.chance) return;

            if (stage.maxNearby > 0)
            {
                float range = stage.nearbyRange > 0f ? stage.nearbyRange : Math.Max(1f, stage.spawnRange);
                int aliveNearby = CountAliveNearby(stage.entityCode, range);
                if (aliveNearby >= stage.maxNearby) return;
            }

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastSpawnStartMsKey);

            int count = stage.minCount;
            if (stage.maxCount > stage.minCount)
            {
                count = stage.minCount + sapi.World.Rand.Next(stage.maxCount - stage.minCount + 1);
            }

            Spawn(stage, count);
        }

        private bool TryFindTarget(SpawnStage stage, out Entity target, out float dist)
        {
            target = null;
            dist = 0f;

            if (sapi == null || entity == null || stage == null) return false;

            try
            {
                var own = entity.ServerPos.XYZ;
                float range = Math.Max(4f, stage.maxTargetRange > 0f ? stage.maxTargetRange : 40f);
                var found = sapi.World.GetNearestEntity(own, range, range, e => e is EntityPlayer);
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

        private int CountAliveNearby(string entityCode, float range)
        {
            if (sapi == null || entity == null) return 0;
            if (string.IsNullOrWhiteSpace(entityCode)) return 0;
            if (range <= 0f) return 0;

            try
            {
                int dim = entity.ServerPos.Dimension;
                var center = new Vec3d(entity.ServerPos.X, entity.ServerPos.Y + dim * 32768.0, entity.ServerPos.Z);
                var entities = sapi.World.GetEntitiesAround(center, range, range, e => e != null && e.Alive);
                if (entities == null) return 0;

                int alive = 0;
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    var code = e?.Code?.ToString();
                    if (string.IsNullOrWhiteSpace(code)) continue;

                    if (string.Equals(code, entityCode, StringComparison.OrdinalIgnoreCase))
                    {
                        alive++;
                    }
                }

                return alive;
            }
            catch
            {
                return 0;
            }
        }

        private void Spawn(SpawnStage stage, int count)
        {
            if (sapi == null || entity == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.entityCode)) return;
            if (count <= 0) return;

            EntityProperties type = null;
            AssetLocation codeLoc = null;
            try
            {
                codeLoc = new AssetLocation(stage.entityCode);
                type = sapi.World.GetEntityType(codeLoc);
            }
            catch
            {
                type = null;
            }

            if (type == null)
            {
                try
                {
                    if (codeLoc != null && string.Equals(codeLoc.Domain, "game", StringComparison.OrdinalIgnoreCase))
                    {
                        type = sapi.World.GetEntityType(new AssetLocation("survival", codeLoc.Path));
                    }
                }
                catch
                {
                    type = null;
                }
            }

            if (type == null) return;

            int dim = entity.ServerPos.Dimension;
            for (int i = 0; i < count; i++)
            {
                double angle = sapi.World.Rand.NextDouble() * Math.PI * 2.0;
                double dist = stage.spawnRange * (0.5 + sapi.World.Rand.NextDouble() * 0.5);
                double x = entity.ServerPos.X + Math.Cos(angle) * dist;
                double z = entity.ServerPos.Z + Math.Sin(angle) * dist;
                double y = entity.ServerPos.Y;

                float yaw = (float)(sapi.World.Rand.NextDouble() * Math.PI * 2.0);
                var spawnPos = new Vec3d(x, y + dim * 32768.0, z);

                Entity spawned = sapi.World.ClassRegistry.CreateEntity(type);
                if (spawned == null) continue;

                try
                {
                    if (entity.EntityId != 0)
                    {
                        spawned.WatchedAttributes.SetLong(SummonedByEntityIdKey, entity.EntityId);
                        spawned.WatchedAttributes.MarkPathDirty(SummonedByEntityIdKey);
                    }
                }
                catch
                {
                }

                try
                {
                    var summonerCode = entity?.Code?.ToString();
                    if (!string.IsNullOrWhiteSpace(summonerCode))
                    {
                        spawned.WatchedAttributes.SetString(SummonedByEntityCodeKey, summonerCode);
                        spawned.WatchedAttributes.MarkPathDirty(SummonedByEntityCodeKey);
                    }
                }
                catch
                {
                }

                spawned.ServerPos.SetPosWithDimension(spawnPos);
                spawned.Pos.SetFrom(spawned.ServerPos);
                spawned.ServerPos.Yaw = yaw;

                sapi.World.SpawnEntity(spawned);

                TryDisableFleeForSummonedWolves(spawned);
            }
        }

        private void TryDisableFleeForSummonedWolves(Entity spawned)
        {
            if (sapi == null || spawned == null) return;

            try
            {
                var code = spawned.Code?.ToString() ?? "";
                if (code.IndexOf("wolf", StringComparison.OrdinalIgnoreCase) < 0) return;

                long summonerId = spawned.WatchedAttributes?.GetLong(SummonedByEntityIdKey, 0) ?? 0;
                if (summonerId <= 0) return;

                sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        if (spawned == null || !spawned.Alive) return;

                        var taskAi = spawned.GetBehavior<EntityBehaviorTaskAI>();
                        if (taskAi?.TaskManager == null) return;

                        taskAi.TaskManager.StopTasks();

                        var tasks = taskAi.TaskManager.AllTasks;
                        for (int i = tasks.Count - 1; i >= 0; i--)
                        {
                            var t = tasks[i];
                            var tn = t?.GetType()?.Name ?? "";
                            if (tn.IndexOf("AiTaskFlee", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                taskAi.TaskManager.RemoveTask(t);
                            }
                        }
                    }
                    catch
                    {
                    }
                }, 1);
            }
            catch
            {
            }
        }
    }
}
