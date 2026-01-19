using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossRespawn : EntityBehavior
    {
        private const string AttrRespawnAtHours = "alegacyvsquest:bossrespawnAtTotalHours";
        private const string AttrRespawnX = "alegacyvsquest:bossrespawnX";
        private const string AttrRespawnY = "alegacyvsquest:bossrespawnY";
        private const string AttrRespawnZ = "alegacyvsquest:bossrespawnZ";
        private const string AttrRespawnDim = "alegacyvsquest:bossrespawnDim";

        private ICoreServerAPI sapi;
        private double respawnInGameHours;
        private bool spawnNewBoss;
        private string respawnEntityCode;
        private long tickListenerId;
        private bool scheduled;

        public EntityBehaviorBossRespawn(Entity entity) : base(entity)
        {
        }


        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            sapi = entity?.Api as ICoreServerAPI;
            if (sapi == null) return;

            respawnInGameHours = attributes["respawnInGameHours"].AsDouble(24);
            if (respawnInGameHours < 0) respawnInGameHours = 24;

            spawnNewBoss = attributes["spawnNewBoss"].AsBool(false);
            respawnEntityCode = attributes["respawnEntityCode"].AsString(null);

            tickListenerId = sapi.Event.RegisterGameTickListener(OnTick, 1000);
        }

        private void OnTick(float dt)
        {
            if (sapi == null || entity == null) return;

            if (entity.Alive)
            {
                scheduled = false;
                return;
            }

            if (!scheduled)
            {
                ScheduleRespawn();
            }

            double respawnAt = entity.WatchedAttributes.GetDouble(AttrRespawnAtHours, double.NaN);
            if (double.IsNaN(respawnAt)) return;

            if (sapi.World.Calendar.TotalHours < respawnAt) return;

            if (spawnNewBoss)
            {
                TryRespawnNow();
                return;
            }

            // Default: only remove corpse after timer. Actual respawn can be handled by a separate spawner.
            sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Expire });
        }

        private void ScheduleRespawn()
        {
            if (sapi == null || entity == null) return;

            double respawnAt = entity.WatchedAttributes.GetDouble(AttrRespawnAtHours, double.NaN);
            if (double.IsNaN(respawnAt))
            {
                if (respawnInGameHours == 0)
                {
                    respawnAt = sapi.World.Calendar.TotalHours;
                }
                else
                {
                    respawnAt = sapi.World.Calendar.TotalHours + respawnInGameHours;
                }
                entity.WatchedAttributes.SetDouble(AttrRespawnAtHours, respawnAt);

                entity.WatchedAttributes.SetDouble(AttrRespawnX, entity.ServerPos.X);
                entity.WatchedAttributes.SetDouble(AttrRespawnY, entity.ServerPos.Y);
                entity.WatchedAttributes.SetDouble(AttrRespawnZ, entity.ServerPos.Z);
                entity.WatchedAttributes.SetInt(AttrRespawnDim, entity.ServerPos.Dimension);

                entity.WatchedAttributes.MarkPathDirty(AttrRespawnAtHours);
                entity.WatchedAttributes.MarkPathDirty(AttrRespawnX);
                entity.WatchedAttributes.MarkPathDirty(AttrRespawnY);
                entity.WatchedAttributes.MarkPathDirty(AttrRespawnZ);
                entity.WatchedAttributes.MarkPathDirty(AttrRespawnDim);
            }

            scheduled = true;
        }

        private void TryRespawnNow()
        {
            if (sapi == null || entity == null) return;

            string code = string.IsNullOrWhiteSpace(respawnEntityCode)
                ? entity.Code?.ToShortString()
                : respawnEntityCode;
            if (string.IsNullOrWhiteSpace(code)) return;

            var type = sapi.World.GetEntityType(new AssetLocation(code));
            if (type == null) return;

            double x = entity.WatchedAttributes.GetDouble(AttrRespawnX, entity.ServerPos.X);
            double y = entity.WatchedAttributes.GetDouble(AttrRespawnY, entity.ServerPos.Y);
            double z = entity.WatchedAttributes.GetDouble(AttrRespawnZ, entity.ServerPos.Z);
            int dim = entity.WatchedAttributes.GetInt(AttrRespawnDim, entity.ServerPos.Dimension);

            float yaw = entity.ServerPos.Yaw;

            sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Expire });

            Entity newEntity = sapi.World.ClassRegistry.CreateEntity(type);
            if (newEntity == null) return;

            // EntityPos.SetPosWithDimension(Vec3d) expects Y to include dimension offset (dim*32768)
            newEntity.ServerPos.SetPosWithDimension(new Vec3d(x, y + dim * 32768.0, z));
            newEntity.ServerPos.Yaw = yaw;
            newEntity.Pos.SetFrom(newEntity.ServerPos);

            sapi.World.SpawnEntity(newEntity);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            if (sapi != null && tickListenerId != 0)
            {
                sapi.Event.UnregisterGameTickListener(tickListenerId);
                tickListenerId = 0;
            }

            base.OnEntityDespawn(despawn);
        }

        public override string PropertyName() => "bossrespawn";
    }
}
