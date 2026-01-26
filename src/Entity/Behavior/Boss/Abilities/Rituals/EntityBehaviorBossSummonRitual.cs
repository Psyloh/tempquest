using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossSummonRitual : EntityBehavior
    {
        private const string SummonStageKey = "alegacyvsquest:bosssummonstage";
        private const string LastRitualStartMsKey = "alegacyvsquest:bosssummonritual:lastStartMs";

        private class SummonSpawn
        {
            public string entityCode;
            public int maxNearby;
            public float nearbyRange;
            public int minCount;
            public int maxCount;
            public float chance;
            public int spawnDelayMs;
        }

        private class SummonStage
        {
            public float whenHealthRelBelow;
            public string entityCode;
            public int maxNearby;
            public float nearbyRange;
            public int minCount;
            public int maxCount;
            public List<SummonSpawn> spawns;
            public int ritualMs;
            public float healPerSecond;
            public float healRelPerSecond;
            public float cooldownSeconds;
            public float spawnRange;
            public float circleRadius;
            public float circleMoveSpeed;
            public int circleStartDelayMs;
            public string animation;
            public string sound;
            public float soundRange;
            public int soundStartMs;
            public string loopSound;
            public float loopSoundRange;
            public int loopSoundIntervalMs;
            public int spawnDelayMs;
        }

        private ICoreServerAPI sapi;
        private readonly List<SummonStage> stages = new List<SummonStage>();
        private bool ritualActive;
        private long ritualEndsAtMs;
        private long ritualStartedAtMs;
        private readonly BossBehaviorUtils.LoopSound soundLoop = new BossBehaviorUtils.LoopSound();
        private int activeStageIndex = -1;
        private float lockedYaw;
        private bool yawLocked;

        private Vec3d ritualCenter;
        private float ritualCircleAngle;

        public EntityBehaviorBossSummonRitual(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosssummonritual";

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

                    var stage = new SummonStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        entityCode = stageObj["entityCode"].AsString(null),
                        maxNearby = stageObj["maxNearby"].AsInt(0),
                        nearbyRange = stageObj["nearbyRange"].AsFloat(0f),
                        minCount = stageObj["minCount"].AsInt(1),
                        maxCount = stageObj["maxCount"].AsInt(1),
                        ritualMs = stageObj["ritualMs"].AsInt(4000),
                        healPerSecond = stageObj["healPerSecond"].AsFloat(0f),
                        healRelPerSecond = stageObj["healRelPerSecond"].AsFloat(0f),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),
                        spawnRange = stageObj["spawnRange"].AsFloat(6f),
                        circleRadius = stageObj["circleRadius"].AsFloat(0f),
                        circleMoveSpeed = stageObj["circleMoveSpeed"].AsFloat(0f),
                        circleStartDelayMs = stageObj["circleStartDelayMs"].AsInt(0),
                        animation = stageObj["animation"].AsString(null),
                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(16f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),
                        loopSound = stageObj["loopSound"].AsString(null),
                        loopSoundRange = stageObj["loopSoundRange"].AsFloat(16f),
                        loopSoundIntervalMs = stageObj["loopSoundIntervalMs"].AsInt(900),
                        spawnDelayMs = stageObj["spawnDelayMs"].AsInt(600),
                    };

                    stage.spawns = new List<SummonSpawn>();
                    foreach (var spawnObj in stageObj["spawns"].AsArray())
                    {
                        if (spawnObj == null || !spawnObj.Exists) continue;

                        var spawn = new SummonSpawn
                        {
                            entityCode = spawnObj["entityCode"].AsString(null),
                            maxNearby = spawnObj["maxNearby"].AsInt(stage.maxNearby),
                            nearbyRange = spawnObj["nearbyRange"].AsFloat(stage.nearbyRange),
                            minCount = spawnObj["minCount"].AsInt(stage.minCount),
                            maxCount = spawnObj["maxCount"].AsInt(stage.maxCount),
                            chance = spawnObj["chance"].AsFloat(1f),
                            spawnDelayMs = spawnObj["spawnDelayMs"].AsInt(stage.spawnDelayMs),
                        };

                        if (!string.IsNullOrWhiteSpace(spawn.entityCode))
                        {
                            stage.spawns.Add(spawn);
                        }
                    }

                    if (stage.spawns.Count > 0 || !string.IsNullOrWhiteSpace(stage.entityCode))
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
            if (!entity.Alive)
            {
                StopRitual();
                return;
            }

            if (ritualActive)
            {
                BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
                if (stages.Count > activeStageIndex && activeStageIndex >= 0)
                {
                    var activeStage = stages[activeStageIndex];
                    HealDuringRitual(activeStage, dt);
                    ApplyCircleMovement(activeStage, dt);
                }

                if (sapi.World.ElapsedMilliseconds >= ritualEndsAtMs)
                {
                    StopRitual();
                }

                return;
            }

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageIndex = SelectStageIndex(frac);
            if (stageIndex < 0 || stageIndex >= stages.Count) return;

            var selectedStage = stages[stageIndex];
            if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastRitualStartMsKey, selectedStage.cooldownSeconds)) return;

            entity.WatchedAttributes.SetInt(SummonStageKey, stageIndex + 1);
            entity.WatchedAttributes.MarkPathDirty(SummonStageKey);

            StartRitual(selectedStage, stageIndex);
        }

        private int SelectStageIndex(float healthFraction)
        {
            if (stages == null || stages.Count == 0) return -1;

            int chosen = -1;
            float chosenThreshold = float.MaxValue;
            for (int i = 0; i < stages.Count; i++)
            {
                var candidateStage = stages[i];
                if (candidateStage == null) continue;

                if (healthFraction <= candidateStage.whenHealthRelBelow)
                {
                    if (candidateStage.whenHealthRelBelow < chosenThreshold)
                    {
                        chosenThreshold = candidateStage.whenHealthRelBelow;
                        chosen = i;
                    }
                }
            }

            return chosen;
        }

        private void TryStartLoopSound(SummonStage stage)
        {
            if (string.IsNullOrWhiteSpace(stage.loopSound)) return;
            soundLoop.Start(sapi, entity, stage.loopSound, stage.loopSoundRange, stage.loopSoundIntervalMs);
        }

        private void StartRitual(SummonStage stage, int index)
        {
            ritualActive = true;
            activeStageIndex = index;
            ritualStartedAtMs = sapi.World.ElapsedMilliseconds;
            ritualEndsAtMs = ritualStartedAtMs + Math.Max(500, stage.ritualMs);

            ritualCircleAngle = (float)(sapi.World.Rand.NextDouble() * Math.PI * 2.0);
            ritualCenter = new Vec3d(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            if (stage.circleRadius > 0f)
            {
                double radius = Math.Max(0.25, stage.circleRadius);
                ritualCenter.X = entity.ServerPos.X - Math.Cos(ritualCircleAngle) * radius;
                ritualCenter.Z = entity.ServerPos.Z - Math.Sin(ritualCircleAngle) * radius;
            }

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastRitualStartMsKey);

            BossBehaviorUtils.StopAiAndFreeze(entity);
            BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
            SpawnMinions(stage);
            TryPlaySound(stage);
            TryStartLoopSound(stage);
            TryPlayAnimation(stage.animation);
        }

        private void StopRitual()
        {
            ritualActive = false;
            yawLocked = false;

            ritualStartedAtMs = 0;

            ritualCenter = null;

            soundLoop.Stop();

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                var animation = stages[activeStageIndex].animation;
                if (!string.IsNullOrWhiteSpace(animation))
                {
                    try
                    {
                        entity?.AnimManager?.StopAnimation(animation);
                    }
                    catch
                    {
                    }
                }
            }

            activeStageIndex = -1;
        }

        private void ApplyCircleMovement(SummonStage stage, float dt)
        {
            if (stage == null) return;
            if (ritualCenter == null) return;
            if (stage.circleRadius <= 0f) return;
            if (stage.circleMoveSpeed <= 0f) return;

            if (stage.circleStartDelayMs > 0 && sapi != null && ritualStartedAtMs > 0)
            {
                if (sapi.World.ElapsedMilliseconds - ritualStartedAtMs < stage.circleStartDelayMs) return;
            }

            try
            {
                float radius = Math.Max(0.25f, stage.circleRadius);
                float moveSpeed = Math.Max(0.001f, stage.circleMoveSpeed);
                float angSpeed = moveSpeed / radius;

                ritualCircleAngle += angSpeed * dt;

                double x = ritualCenter.X + Math.Cos(ritualCircleAngle) * radius;
                double z = ritualCenter.Z + Math.Sin(ritualCircleAngle) * radius;

                int dim = entity.ServerPos.Dimension;
                double y = entity.ServerPos.Y;
                entity.ServerPos.SetPosWithDimension(new Vec3d(x, y + dim * 32768.0, z));
                entity.Pos.SetFrom(entity.ServerPos);
            }
            catch
            {
            }
        }

        private void HealDuringRitual(SummonStage stage, float dt)
        {
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

        private void SpawnMinions(SummonStage stage)
        {
            if (sapi == null) return;
            if (stage == null) return;

            if (stage.spawns != null && stage.spawns.Count > 0)
            {
                for (int i = 0; i < stage.spawns.Count; i++)
                {
                    SpawnMinions(stage, stage.spawns[i]);
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(stage.entityCode)) return;
            SpawnMinions(stage, new SummonSpawn
            {
                entityCode = stage.entityCode,
                maxNearby = stage.maxNearby,
                nearbyRange = stage.nearbyRange,
                minCount = stage.minCount,
                maxCount = stage.maxCount,
                chance = 1f,
                spawnDelayMs = stage.spawnDelayMs
            });
        }

        private void SpawnMinions(SummonStage stage, SummonSpawn spawn)
        {
            if (sapi == null || entity == null) return;
            if (stage == null || spawn == null) return;
            if (string.IsNullOrWhiteSpace(spawn.entityCode)) return;

            float chance = spawn.chance;
            if (chance <= 0f) return;
            if (chance < 1f && sapi.World.Rand.NextDouble() > chance) return;

            int min = Math.Max(1, spawn.minCount);
            int max = Math.Max(min, spawn.maxCount);
            int count = min;
            if (max > min)
            {
                count = min + sapi.World.Rand.Next(max - min + 1);
            }

            if (spawn.maxNearby > 0)
            {
                float range = spawn.nearbyRange > 0f ? spawn.nearbyRange : Math.Max(1f, stage.spawnRange);
                int aliveNearby = CountAliveNearby(spawn.entityCode, range);
                int remaining = spawn.maxNearby - aliveNearby;
                if (remaining <= 0) return;
                if (count > remaining) count = remaining;
            }

            var type = sapi.World.GetEntityType(new AssetLocation(spawn.entityCode));
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

                if (spawn.spawnDelayMs > 0)
                {
                    sapi.Event.RegisterCallback(_ =>
                    {
                        SpawnEntityAt(type, spawnPos, yaw);
                    }, spawn.spawnDelayMs);
                }
                else
                {
                    SpawnEntityAt(type, spawnPos, yaw);
                }
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

        private void SpawnEntityAt(EntityProperties type, Vec3d spawnPos, float yaw)
        {
            if (sapi == null || type == null) return;

            Entity spawned = sapi.World.ClassRegistry.CreateEntity(type);
            if (spawned == null) return;

            spawned.ServerPos.SetPosWithDimension(spawnPos);
            spawned.Pos.SetFrom(spawned.ServerPos);
            spawned.ServerPos.Yaw = yaw;

            sapi.World.SpawnEntity(spawned);
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

        private void TryPlaySound(SummonStage stage)
        {
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

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopRitual();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopRitual();
            base.OnEntityDespawn(despawn);
        }
    }
}
