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
    public class EntityBehaviorBossIntermissionDispel : EntityBehavior
    {
        private const string StageKey = "alegacyvsquest:bossintermissiondispel:stage";
        private const string LastStartMsKey = "alegacyvsquest:bossintermissiondispel:lastStartMs";

        private const string DispelFlagKey = "alegacyvsquest:bossintermissiondispel:dispel";
        private const string DispelOwnerIdKey = "alegacyvsquest:bossintermissiondispel:ownerid";

        private class Spawn
        {
            public string entityCode;
            public int maxNearby;
            public float nearbyRange;
            public int minCount;
            public int maxCount;
            public float chance;
            public int spawnDelayMs;
        }

        private class Stage
        {
            public float whenHealthRelBelow;
            public float cooldownSeconds;

            public int intermissionMaxMs;
            public bool freezeBoss;
            public bool lockYaw;
            public float incomingDamageMultiplier;

            public float spawnRange;

            public List<Spawn> adds;

            public string dispelEntityCode;
            public int dispelCount;
            public bool dispelInvulnerable;

            public string animation;
            public int animationStopMs;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public string loopSound;
            public float loopSoundRange;
            public int loopSoundIntervalMs;
            public float loopSoundVolume;
        }

        private ICoreServerAPI sapi;
        private readonly List<Stage> stages = new List<Stage>();

        private bool active;
        private long endsAtMs;
        private long startedAtMs;
        private int activeStageIndex = -1;

        private bool yawLocked;
        private float lockedYaw;

        private readonly BossBehaviorUtils.LoopSound loopSoundPlayer = new BossBehaviorUtils.LoopSound();

        private readonly List<long> spawnedAddIds = new List<long>();
        private readonly List<long> spawnedDispelIds = new List<long>();

        public EntityBehaviorBossIntermissionDispel(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossintermissiondispel";

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

                    var stage = new Stage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),

                        intermissionMaxMs = stageObj["intermissionMaxMs"].AsInt(20000),
                        freezeBoss = stageObj["freezeBoss"].AsBool(true),
                        lockYaw = stageObj["lockYaw"].AsBool(true),
                        incomingDamageMultiplier = stageObj["incomingDamageMultiplier"].AsFloat(0f),

                        spawnRange = stageObj["spawnRange"].AsFloat(8f),

                        dispelEntityCode = stageObj["dispelEntityCode"].AsString(null),
                        dispelCount = stageObj["dispelCount"].AsInt(1),
                        dispelInvulnerable = stageObj["dispelInvulnerable"].AsBool(false),

                        animation = stageObj["animation"].AsString(null),
                        animationStopMs = stageObj["animationStopMs"].AsInt(0),

                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),
                        soundVolume = stageObj["soundVolume"].AsFloat(1f),

                        loopSound = stageObj["loopSound"].AsString(null),
                        loopSoundRange = stageObj["loopSoundRange"].AsFloat(24f),
                        loopSoundIntervalMs = stageObj["loopSoundIntervalMs"].AsInt(900),
                        loopSoundVolume = stageObj["loopSoundVolume"].AsFloat(1f),
                    };

                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;

                    if (stage.intermissionMaxMs <= 0) stage.intermissionMaxMs = 5000;

                    if (stage.spawnRange <= 0f) stage.spawnRange = 0.5f;

                    if (stage.incomingDamageMultiplier < 0f) stage.incomingDamageMultiplier = 0f;
                    if (stage.incomingDamageMultiplier > 1f) stage.incomingDamageMultiplier = 1f;

                    if (stage.soundVolume <= 0f) stage.soundVolume = 1f;
                    if (stage.loopSoundVolume <= 0f) stage.loopSoundVolume = 1f;

                    stage.adds = new List<Spawn>();
                    foreach (var addObj in stageObj["adds"].AsArray())
                    {
                        if (addObj == null || !addObj.Exists) continue;

                        var add = new Spawn
                        {
                            entityCode = addObj["entityCode"].AsString(null),
                            maxNearby = addObj["maxNearby"].AsInt(0),
                            nearbyRange = addObj["nearbyRange"].AsFloat(0f),
                            minCount = addObj["minCount"].AsInt(1),
                            maxCount = addObj["maxCount"].AsInt(1),
                            chance = addObj["chance"].AsFloat(1f),
                            spawnDelayMs = addObj["spawnDelayMs"].AsInt(0),
                        };

                        if (!string.IsNullOrWhiteSpace(add.entityCode))
                        {
                            stage.adds.Add(add);
                        }
                    }

                    if (stage.adds.Count > 0 || !string.IsNullOrWhiteSpace(stage.dispelEntityCode))
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

            if (IsDispelEntity())
            {
                DespawnIfOwnerMissing();
                return;
            }

            if (stages.Count == 0) return;

            if (!entity.Alive)
            {
                StopIntermission();
                return;
            }

            if (active)
            {
                var stage = (activeStageIndex >= 0 && activeStageIndex < stages.Count) ? stages[activeStageIndex] : null;
                if (stage != null)
                {
                    if (stage.freezeBoss)
                    {
                        BossBehaviorUtils.StopAiAndFreeze(entity);
                    }

                    if (stage.lockYaw)
                    {
                        BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
                    }

                    if (AllObjectivesCleared())
                    {
                        StopIntermission();
                        return;
                    }

                    if (sapi.World.ElapsedMilliseconds >= endsAtMs)
                    {
                        StopIntermission();
                        return;
                    }
                }

                return;
            }

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageProgress = entity.WatchedAttributes?.GetInt(StageKey, 0) ?? 0;
            for (int i = stageProgress; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (frac <= stage.whenHealthRelBelow)
                {
                    if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastStartMsKey, stage.cooldownSeconds)) return;

                    entity.WatchedAttributes.SetInt(StageKey, i + 1);
                    entity.WatchedAttributes.MarkPathDirty(StageKey);

                    StartIntermission(stage, i);
                    break;
                }
            }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!active) return;
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count) return;

            float mult = stages[activeStageIndex].incomingDamageMultiplier;
            if (mult >= 0f && mult < 0.9999f)
            {
                damage *= mult;
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopIntermission();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopIntermission();
            base.OnEntityDespawn(despawn);
        }

        private void StartIntermission(Stage stage, int stageIndex)
        {
            if (sapi == null || entity == null || stage == null) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastStartMsKey);

            active = true;
            activeStageIndex = stageIndex;
            yawLocked = false;

            startedAtMs = sapi.World.ElapsedMilliseconds;
            endsAtMs = startedAtMs + Math.Max(500, stage.intermissionMaxMs);

            spawnedAddIds.Clear();
            spawnedDispelIds.Clear();

            if (stage.freezeBoss)
            {
                BossBehaviorUtils.StopAiAndFreeze(entity);
            }

            if (stage.lockYaw)
            {
                BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
            }

            TryPlaySound(stage);
            TryStartLoopSound(stage);
            TryPlayAnimation(stage);

            SpawnAdds(stage);
            SpawnDispelObjects(stage);
        }

        private void StopIntermission()
        {
            if (!active && activeStageIndex < 0) return;

            active = false;
            yawLocked = false;

            startedAtMs = 0;
            endsAtMs = 0;

            loopSoundPlayer.Stop();

            DespawnSpawnedAdds();
            DespawnSpawnedDispels();

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

        private void DespawnSpawnedAdds()
        {
            if (sapi == null) return;

            for (int i = spawnedAddIds.Count - 1; i >= 0; i--)
            {
                try
                {
                    var e = sapi.World.GetEntityById(spawnedAddIds[i]);
                    if (e == null) continue;
                    sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }
                catch
                {
                }
            }

            spawnedAddIds.Clear();
        }

        private void DespawnSpawnedDispels()
        {
            if (sapi == null) return;

            for (int i = spawnedDispelIds.Count - 1; i >= 0; i--)
            {
                try
                {
                    var e = sapi.World.GetEntityById(spawnedDispelIds[i]);
                    if (e == null) continue;
                    sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }
                catch
                {
                }
            }

            spawnedDispelIds.Clear();
        }

        private bool AllObjectivesCleared()
        {
            for (int i = spawnedDispelIds.Count - 1; i >= 0; i--)
            {
                var e = sapi.World.GetEntityById(spawnedDispelIds[i]);
                if (e == null || !e.Alive)
                {
                    spawnedDispelIds.RemoveAt(i);
                }
            }

            for (int i = spawnedAddIds.Count - 1; i >= 0; i--)
            {
                var e = sapi.World.GetEntityById(spawnedAddIds[i]);
                if (e == null || !e.Alive)
                {
                    spawnedAddIds.RemoveAt(i);
                }
            }

            if (spawnedDispelIds.Count > 0) return false;
            if (spawnedAddIds.Count > 0) return false;

            return true;
        }

        private void SpawnAdds(Stage stage)
        {
            if (sapi == null || entity == null || stage == null) return;
            if (stage.adds == null || stage.adds.Count == 0) return;

            for (int i = 0; i < stage.adds.Count; i++)
            {
                SpawnAdds(stage, stage.adds[i]);
            }
        }

        private void SpawnAdds(Stage stage, Spawn spawn)
        {
            if (sapi == null || entity == null || stage == null || spawn == null) return;
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
                        SpawnAddEntityAt(type, spawnPos, yaw);
                    }, spawn.spawnDelayMs);
                }
                else
                {
                    SpawnAddEntityAt(type, spawnPos, yaw);
                }
            }
        }

        private void SpawnAddEntityAt(EntityProperties type, Vec3d spawnPos, float yaw)
        {
            if (sapi == null || type == null) return;

            Entity spawned = sapi.World.ClassRegistry.CreateEntity(type);
            if (spawned == null) return;

            spawned.ServerPos.SetPosWithDimension(spawnPos);
            spawned.Pos.SetFrom(spawned.ServerPos);
            spawned.ServerPos.Yaw = yaw;

            sapi.World.SpawnEntity(spawned);

            spawnedAddIds.Add(spawned.EntityId);
        }

        private void SpawnDispelObjects(Stage stage)
        {
            if (sapi == null || entity == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.dispelEntityCode)) return;

            int count = Math.Max(1, stage.dispelCount);
            var type = sapi.World.GetEntityType(new AssetLocation(stage.dispelEntityCode));
            if (type == null) return;

            int dim = entity.ServerPos.Dimension;
            for (int i = 0; i < count; i++)
            {
                Entity dispel = null;
                try
                {
                    dispel = sapi.World.ClassRegistry.CreateEntity(type);
                    if (dispel == null) continue;

                    ApplyDispelFlags(dispel, stage);

                    double angle = sapi.World.Rand.NextDouble() * Math.PI * 2.0;
                    double dist = stage.spawnRange * (0.5 + sapi.World.Rand.NextDouble() * 0.5);
                    double x = entity.ServerPos.X + Math.Cos(angle) * dist;
                    double z = entity.ServerPos.Z + Math.Sin(angle) * dist;
                    double y = entity.ServerPos.Y;

                    dispel.ServerPos.SetPosWithDimension(new Vec3d(x, y + dim * 32768.0, z));
                    dispel.Pos.SetFrom(dispel.ServerPos);
                    dispel.ServerPos.Yaw = (float)(sapi.World.Rand.NextDouble() * Math.PI * 2.0);

                    sapi.World.SpawnEntity(dispel);

                    spawnedDispelIds.Add(dispel.EntityId);
                }
                catch
                {
                    if (dispel != null)
                    {
                        try
                        {
                            sapi.World.DespawnEntity(dispel, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        private void ApplyDispelFlags(Entity dispel, Stage stage)
        {
            if (dispel?.WatchedAttributes == null || stage == null) return;

            try
            {
                dispel.WatchedAttributes.SetBool(DispelFlagKey, true);
                dispel.WatchedAttributes.MarkPathDirty(DispelFlagKey);
            }
            catch
            {
            }

            try
            {
                dispel.WatchedAttributes.SetLong(DispelOwnerIdKey, entity.EntityId);
                dispel.WatchedAttributes.MarkPathDirty(DispelOwnerIdKey);
            }
            catch
            {
            }

            try
            {
                dispel.WatchedAttributes.SetBool("alegacyvsquest:bossclone:invulnerable", stage.dispelInvulnerable);
                dispel.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:invulnerable");
            }
            catch
            {
            }

            try
            {
                dispel.WatchedAttributes.SetBool("showHealthbar", false);
                dispel.WatchedAttributes.MarkPathDirty("showHealthbar");
            }
            catch
            {
            }
        }

        private bool IsDispelEntity()
        {
            try
            {
                return entity?.WatchedAttributes?.GetBool(DispelFlagKey, false) ?? false;
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
                ownerId = entity.WatchedAttributes.GetLong(DispelOwnerIdKey, 0);
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

        private void TryPlayAnimation(Stage stage)
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
            catch
            {
            }
        }

        private void TryPlaySound(Stage stage)
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

        private void TryStartLoopSound(Stage stage)
        {
            if (sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.loopSound)) return;

            loopSoundPlayer.Start(sapi, entity, stage.loopSound, stage.loopSoundRange, stage.loopSoundIntervalMs, stage.loopSoundVolume);
        }
    }
}
