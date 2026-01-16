using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossRebirth : EntityBehavior
    {
        private const string AnchorKeyPrefix = "alegacyvsquest:spawner:";
        private const string TargetIdKey = "alegacyvsquest:killaction:targetid";

        private ICoreServerAPI sapi;
        private string rebirthEntityCode;
        private bool isFinalStage;
        private bool rebirthTriggered;

        public bool IsFinalStage => isFinalStage;

        public EntityBehaviorBossRebirth(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossrebirth";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;
            rebirthEntityCode = attributes["rebirthEntityCode"].AsString(null);
            isFinalStage = attributes["isFinalStage"].AsBool(false);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);

            if (sapi == null || entity == null) return;
            if (isFinalStage) return;
            if (rebirthTriggered) return;
            if (string.IsNullOrWhiteSpace(rebirthEntityCode)) return;

            rebirthTriggered = true;
            TrySpawnRebirth();
        }

        private void TrySpawnRebirth()
        {
            try
            {
                var type = sapi.World.GetEntityType(new AssetLocation(rebirthEntityCode));
                if (type == null) return;

                Entity newEntity = sapi.World.ClassRegistry.CreateEntity(type);
                if (newEntity == null) return;

                CopyTargetId(newEntity);
                CopyAnchor(newEntity);

                newEntity.ServerPos.SetPosWithDimension(new Vec3d(entity.ServerPos.X, entity.ServerPos.Y + entity.ServerPos.Dimension * 32768.0, entity.ServerPos.Z));
                newEntity.ServerPos.Yaw = entity.ServerPos.Yaw;
                newEntity.Pos.SetFrom(newEntity.ServerPos);

                sapi.World.SpawnEntity(newEntity);
            }
            catch
            {
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
