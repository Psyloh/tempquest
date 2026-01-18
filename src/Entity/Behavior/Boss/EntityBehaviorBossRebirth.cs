using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossRebirth : EntityBehavior
    {
        private const string AnchorKeyPrefix = "alegacyvsquest:spawner:";
        private const string TargetIdKey = "alegacyvsquest:killaction:targetid";
        private const string RespawnBlockAtHoursKey = "alegacyvsquest:bossrespawnAtTotalHours";

        private ICoreServerAPI sapi;
        private string rebirthEntityCode;
        private bool isFinalStage;
        private bool rebirthTriggered;
        private int spawnDelayMs;

        private string sound;
        private float soundRange;
        private int soundStartMs;

        private string spawnSound;
        private float spawnSoundRange;
        private int spawnSoundStartMs;

        private string loopSound;
        private float loopSoundRange;
        private int loopSoundIntervalMs;

        private readonly BossBehaviorUtils.LoopSound loopSoundPlayer = new BossBehaviorUtils.LoopSound();

        private WeatherSystemBase weatherSystem;

        public bool IsFinalStage => isFinalStage;

        public EntityBehaviorBossRebirth(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossrebirth";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;

            try
            {
                weatherSystem = sapi?.ModLoader?.GetModSystem<WeatherSystemBase>();
            }
            catch
            {
                weatherSystem = null;
            }

            rebirthEntityCode = attributes["rebirthEntityCode"].AsString(null);
            isFinalStage = attributes["isFinalStage"].AsBool(false);
            spawnDelayMs = attributes["spawnDelayMs"].AsInt(600);

            sound = attributes["sound"].AsString(null);
            soundRange = attributes["soundRange"].AsFloat(24f);
            soundStartMs = attributes["soundStartMs"].AsInt(0);

            spawnSound = attributes["spawnSound"].AsString(null);
            spawnSoundRange = attributes["spawnSoundRange"].AsFloat(24f);
            spawnSoundStartMs = attributes["spawnSoundStartMs"].AsInt(0);

            loopSound = attributes["loopSound"].AsString(null);
            loopSoundRange = attributes["loopSoundRange"].AsFloat(24f);
            loopSoundIntervalMs = attributes["loopSoundIntervalMs"].AsInt(900);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            if (sapi != null && entity != null)
            {
                try
                {
                    entity.WatchedAttributes.SetBool("showHealthbar", false);
                    entity.WatchedAttributes.MarkPathDirty("showHealthbar");
                }
                catch
                {
                }

                try
                {
                    if (entity is EntityAgent agent)
                    {
                        agent.AllowDespawn = false;
                    }
                }
                catch
                {
                }
            }

            base.OnEntityDeath(damageSourceForDeath);

            if (sapi == null || entity == null) return;
            if (isFinalStage) return;
            if (rebirthTriggered) return;
            if (string.IsNullOrWhiteSpace(rebirthEntityCode)) return;

            try
            {
                if (entity is EntityAgent agent)
                {
                    agent.AllowDespawn = false;
                }
            }
            catch
            {
            }

            rebirthTriggered = true;
            TriggerRebirth();
        }

        private void TriggerRebirth()
        {
            try
            {
                Vec3d pos = new Vec3d(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
                int dim = entity.ServerPos.Dimension;
                float yaw = entity.ServerPos.Yaw;

                try
                {
                    // Block quest spawners during phase transition to prevent a duplicate spawn on servers.
                    // The spawner checks for a corpse with bossrespawnAtTotalHours and will not spawn additional copies.
                    double nowHours = sapi.World.Calendar.TotalHours;
                    double blockHours = Math.Max(0, spawnDelayMs) / 3600000.0;
                    entity.WatchedAttributes.SetDouble(RespawnBlockAtHoursKey, nowHours + Math.Max(0.01, blockHours));
                    entity.WatchedAttributes.MarkPathDirty(RespawnBlockAtHoursKey);
                }
                catch
                {
                }

                TryPlaySound(pos);
                StartLoopSound();

                int delay = Math.Max(0, spawnDelayMs);
                if (delay > 0)
                {
                    sapi.Event.RegisterCallback(_ =>
                    {
                        StopLoopSound();
                        TrySpawnRebirth(pos, dim, yaw);
                    }, delay);
                }
                else
                {
                    StopLoopSound();
                    TrySpawnRebirth(pos, dim, yaw);
                }
            }
            catch
            {
            }
        }

        private void TryPlaySpawnSound(Vec3d pos)
        {
            if (sapi == null) return;
            if (string.IsNullOrWhiteSpace(spawnSound)) return;

            AssetLocation soundLoc = AssetLocation.Create(spawnSound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            if (spawnSoundStartMs > 0)
            {
                sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        sapi.World.PlaySoundAt(soundLoc, pos.X, pos.Y, pos.Z, null, randomizePitch: true, spawnSoundRange);
                    }
                    catch
                    {
                    }
                }, spawnSoundStartMs);
                return;
            }

            try
            {
                sapi.World.PlaySoundAt(soundLoc, pos.X, pos.Y, pos.Z, null, randomizePitch: true, spawnSoundRange);
            }
            catch
            {
            }
        }

        private void TryPlaySound(Vec3d pos)
        {
            if (sapi == null) return;
            if (string.IsNullOrWhiteSpace(sound)) return;

            AssetLocation soundLoc = AssetLocation.Create(sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            if (soundStartMs > 0)
            {
                sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        sapi.World.PlaySoundAt(soundLoc, pos.X, pos.Y, pos.Z, null, randomizePitch: true, soundRange);
                    }
                    catch
                    {
                    }
                }, soundStartMs);
                return;
            }

            try
            {
                sapi.World.PlaySoundAt(soundLoc, pos.X, pos.Y, pos.Z, null, randomizePitch: true, soundRange);
            }
            catch
            {
            }
        }

        private void StartLoopSound()
        {
            if (sapi == null) return;
            loopSoundPlayer.Start(sapi, entity, loopSound, loopSoundRange, loopSoundIntervalMs);
        }

        private void StopLoopSound()
        {
            loopSoundPlayer.Stop();
        }

        private void TrySpawnRebirth(Vec3d pos, int dim, float yaw)
        {
            try
            {
                try
                {
                    weatherSystem?.SpawnLightningFlash(pos);
                }
                catch
                {
                }

                var type = sapi.World.GetEntityType(new AssetLocation(rebirthEntityCode));
                if (type == null) return;

                Entity newEntity = sapi.World.ClassRegistry.CreateEntity(type);
                if (newEntity == null) return;

                CopyTargetId(newEntity);
                CopyAnchor(newEntity);

                newEntity.ServerPos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + dim * 32768.0, pos.Z));
                newEntity.ServerPos.Yaw = yaw;
                newEntity.Pos.SetFrom(newEntity.ServerPos);

                sapi.World.SpawnEntity(newEntity);

                TryPlaySpawnSound(pos);

                try
                {
                    if (entity != null)
                    {
                        if (entity is EntityAgent agent)
                        {
                            agent.AllowDespawn = true;
                        }
                        sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                }
                catch
                {
                }
            }
            catch
            {
            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopLoopSound();
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
    }
}
