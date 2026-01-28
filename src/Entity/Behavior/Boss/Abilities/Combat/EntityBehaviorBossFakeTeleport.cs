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
    public class EntityBehaviorBossFakeTeleport : EntityBehavior
    {
        private const string LastStartMsKey = "alegacyvsquest:bossfaketeleport:lastStartMs";
        private const string FakeFlagKey = "alegacyvsquest:bossfaketeleport:fake";
        private const string FakeOwnerIdKey = "alegacyvsquest:bossfaketeleport:ownerid";
        private const string FakeDespawnAtMsKey = "alegacyvsquest:bossfaketeleport:despawnat";

        private class Stage
        {
            public float whenHealthRelBelow;
            public float cooldownSeconds;

            public float minTargetRange;
            public float maxTargetRange;

            public float minRadius;
            public float maxRadius;
            public int tries;
            public bool requireSolidGround;

            public int windupMs;
            public string windupAnimation;
            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public string fakeEntityCode;
            public int fakeDurationMs;
            public bool fakeInvulnerable;

            public bool teleportBoss;
            public float bossTeleportMinRadius;
            public float bossTeleportMaxRadius;
            public int bossTeleportTries;
        }

        private ICoreServerAPI sapi;
        private readonly List<Stage> stages = new List<Stage>();

        private const int CheckIntervalMs = 200;
        private long lastCheckMs;

        private long callbackId;
        private bool pending;
        private int pendingStageIndex;

        public EntityBehaviorBossFakeTeleport(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossfaketeleport";

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

                        minTargetRange = stageObj["minTargetRange"].AsFloat(0f),
                        maxTargetRange = stageObj["maxTargetRange"].AsFloat(40f),

                        minRadius = stageObj["minRadius"].AsFloat(3f),
                        maxRadius = stageObj["maxRadius"].AsFloat(8f),
                        tries = stageObj["tries"].AsInt(10),
                        requireSolidGround = stageObj["requireSolidGround"].AsBool(true),

                        windupMs = stageObj["windupMs"].AsInt(250),
                        windupAnimation = stageObj["windupAnimation"].AsString(null),
                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),
                        soundVolume = stageObj["soundVolume"].AsFloat(1f),

                        fakeEntityCode = stageObj["fakeEntityCode"].AsString(null),
                        fakeDurationMs = stageObj["fakeDurationMs"].AsInt(2500),
                        fakeInvulnerable = stageObj["fakeInvulnerable"].AsBool(true),

                        teleportBoss = stageObj["teleportBoss"].AsBool(false),
                        bossTeleportMinRadius = stageObj["bossTeleportMinRadius"].AsFloat(3f),
                        bossTeleportMaxRadius = stageObj["bossTeleportMaxRadius"].AsFloat(7f),
                        bossTeleportTries = stageObj["bossTeleportTries"].AsInt(10),
                    };

                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;
                    if (stage.minTargetRange < 0f) stage.minTargetRange = 0f;
                    if (stage.maxTargetRange < stage.minTargetRange) stage.maxTargetRange = stage.minTargetRange;

                    if (stage.minRadius < 0f) stage.minRadius = 0f;
                    if (stage.maxRadius < stage.minRadius) stage.maxRadius = stage.minRadius;
                    if (stage.tries <= 0) stage.tries = 1;

                    if (stage.windupMs < 0) stage.windupMs = 0;
                    if (stage.soundVolume <= 0f) stage.soundVolume = 1f;

                    if (stage.fakeDurationMs <= 0) stage.fakeDurationMs = 500;

                    if (stage.bossTeleportMinRadius < 0f) stage.bossTeleportMinRadius = 0f;
                    if (stage.bossTeleportMaxRadius < stage.bossTeleportMinRadius) stage.bossTeleportMaxRadius = stage.bossTeleportMinRadius;
                    if (stage.bossTeleportTries <= 0) stage.bossTeleportTries = 1;

                    if (!string.IsNullOrWhiteSpace(stage.fakeEntityCode))
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
            if (entity.Api?.Side != EnumAppSide.Server) return;

            if (IsFakeEntity())
            {
                DespawnIfExpiredOrOwnerMissing();
                return;
            }

            if (stages.Count == 0) return;

            if (!entity.Alive)
            {
                CancelPending();
                return;
            }

            if (pending) return;

            long now;
            try
            {
                now = sapi.World.ElapsedMilliseconds;
            }
            catch
            {
                now = 0;
            }

            if (now > 0)
            {
                if (lastCheckMs != 0 && now - lastCheckMs < CheckIntervalMs) return;
                lastCheckMs = now;
            }

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
            if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastStartMsKey, activeStage.cooldownSeconds)) return;

            if (!TryFindTarget(activeStage, out var target, out float dist)) return;
            if (dist < activeStage.minTargetRange) return;
            if (dist > activeStage.maxTargetRange) return;

            if (!TryFindPosNear(activeStage.minRadius, activeStage.maxRadius, activeStage.tries, activeStage.requireSolidGround, target.ServerPos.XYZ, entity.ServerPos.Dimension, out var fakePos)) return;

            Vec3d bossTpPos = null;
            if (activeStage.teleportBoss)
            {
                TryFindPosNear(activeStage.bossTeleportMinRadius, activeStage.bossTeleportMaxRadius, activeStage.bossTeleportTries, activeStage.requireSolidGround, target.ServerPos.XYZ, entity.ServerPos.Dimension, out bossTpPos);
            }

            Start(activeStage, stageIndex, fakePos, bossTpPos);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            CancelPending();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            CancelPending();
            base.OnEntityDespawn(despawn);
        }

        private void Start(Stage stage, int stageIndex, Vec3d fakePos, Vec3d bossTpPos)
        {
            if (sapi == null || entity == null || stage == null || fakePos == null) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastStartMsKey);

            pending = true;
            pendingStageIndex = stageIndex;

            BossBehaviorUtils.StopAiAndFreeze(entity);

            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            int delay = Math.Max(0, stage.windupMs);
            BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref callbackId);
            callbackId = sapi.Event.RegisterCallback(_ =>
            {
                try
                {
                    SpawnFake(stage, fakePos);
                    if (stage.teleportBoss && bossTpPos != null)
                    {
                        TeleportEntity(entity, bossTpPos, entity.ServerPos.Dimension);
                    }
                }
                catch
                {
                }

                pending = false;
                pendingStageIndex = -1;
                callbackId = 0;

            }, delay);
        }

        private void SpawnFake(Stage stage, Vec3d pos)
        {
            if (sapi == null || entity == null || stage == null || pos == null) return;
            if (string.IsNullOrWhiteSpace(stage.fakeEntityCode)) return;

            var type = sapi.World.GetEntityType(new AssetLocation(stage.fakeEntityCode));
            if (type == null) return;

            Entity fake = null;
            try
            {
                fake = sapi.World.ClassRegistry.CreateEntity(type);
                if (fake == null) return;

                ApplyFakeFlags(fake, stage);

                int dim = entity.ServerPos.Dimension;
                fake.ServerPos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + dim * 32768.0, pos.Z));
                fake.ServerPos.Yaw = entity.ServerPos.Yaw;
                fake.Pos.SetFrom(fake.ServerPos);

                sapi.World.SpawnEntity(fake);
            }
            catch
            {
                if (fake != null)
                {
                    try
                    {
                        sapi.World.DespawnEntity(fake, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void ApplyFakeFlags(Entity fake, Stage stage)
        {
            if (fake?.WatchedAttributes == null || stage == null) return;

            try
            {
                fake.WatchedAttributes.SetBool(FakeFlagKey, true);
                fake.WatchedAttributes.MarkPathDirty(FakeFlagKey);
            }
            catch
            {
            }

            try
            {
                fake.WatchedAttributes.SetLong(FakeOwnerIdKey, entity.EntityId);
                fake.WatchedAttributes.MarkPathDirty(FakeOwnerIdKey);
            }
            catch
            {
            }

            try
            {
                fake.WatchedAttributes.SetLong(FakeDespawnAtMsKey, sapi.World.ElapsedMilliseconds + Math.Max(250, stage.fakeDurationMs));
                fake.WatchedAttributes.MarkPathDirty(FakeDespawnAtMsKey);
            }
            catch
            {
            }

            try
            {
                fake.WatchedAttributes.SetBool("alegacyvsquest:bossclone:invulnerable", stage.fakeInvulnerable);
                fake.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:invulnerable");
            }
            catch
            {
            }

            try
            {
                fake.WatchedAttributes.SetBool("showHealthbar", false);
                fake.WatchedAttributes.MarkPathDirty("showHealthbar");
            }
            catch
            {
            }
        }

        private bool IsFakeEntity()
        {
            try
            {
                return entity?.WatchedAttributes?.GetBool(FakeFlagKey, false) ?? false;
            }
            catch
            {
                return false;
            }
        }

        private void DespawnIfExpiredOrOwnerMissing()
        {
            if (sapi == null || entity == null) return;

            long despawnAt = 0;
            long ownerId = 0;
            try
            {
                var wa = entity.WatchedAttributes;
                despawnAt = wa.GetLong(FakeDespawnAtMsKey, 0);
                ownerId = wa.GetLong(FakeOwnerIdKey, 0);
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
                return;
            }

            if (despawnAt > 0 && sapi.World.ElapsedMilliseconds >= despawnAt)
            {
                sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
        }

        private void CancelPending()
        {
            if (sapi != null)
            {
                BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref callbackId);
            }

            pending = false;
            pendingStageIndex = -1;
            callbackId = 0;
        }

        private bool TryFindTarget(Stage stage, out EntityPlayer target, out float dist)
        {
            target = null;
            dist = 0f;

            if (sapi == null || entity == null) return false;

            double range = Math.Max(2.0, stage.maxTargetRange > 0 ? stage.maxTargetRange : 40f);
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

        private bool TryFindPosNear(float minRadius, float maxRadius, int tries, bool requireSolidGround, Vec3d center, int dim, out Vec3d pos)
        {
            pos = null;
            if (sapi == null || entity == null || center == null) return false;

            double minR = Math.Max(0.0, minRadius);
            double maxR = Math.Max(minR, maxRadius);
            if (maxR <= 0.01) maxR = 0.01;

            var world = sapi.World;
            var ba = world?.BlockAccessor;
            if (ba == null) return false;

            int attemptCount = Math.Max(1, tries);
            for (int attempt = 0; attempt < attemptCount; attempt++)
            {
                double ang = world.Rand.NextDouble() * Math.PI * 2.0;
                double dist = minR + world.Rand.NextDouble() * (maxR - minR);

                double x = center.X + Math.Cos(ang) * dist;
                double z = center.Z + Math.Sin(ang) * dist;

                int baseY = (int)Math.Round(center.Y);
                var tmp = new BlockPos((int)Math.Floor(x), baseY, (int)Math.Floor(z), dim);

                if (TryFindFreeSpotNear(tmp, requireSolidGround, out var found))
                {
                    pos = found;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindFreeSpotNear(BlockPos basePos, bool requireSolidGround, out Vec3d pos)
        {
            pos = null;
            if (sapi == null || entity == null || basePos == null) return false;

            var world = sapi.World;
            var ba = world?.BlockAccessor;
            if (ba == null) return false;

            var ct = world.CollisionTester;
            if (ct == null) return false;

            var selBox = entity.SelectionBox;
            if (selBox == null) return false;

            for (int dy = 0; dy <= 6; dy++)
            {
                int y = basePos.Y + dy;
                var testPos = new Vec3d(basePos.X + 0.5, y + 1.0, basePos.Z + 0.5);

                bool colliding;
                try
                {
                    colliding = ct.IsColliding(ba, selBox, testPos, alsoCheckTouch: false);
                }
                catch
                {
                    colliding = true;
                }

                if (colliding) continue;

                if (requireSolidGround)
                {
                    try
                    {
                        var belowPos = basePos.Copy();
                        belowPos.Y = basePos.Y + dy - 1;
                        var below = ba.GetBlock(belowPos);
                        if (below == null) continue;
                        if (!below.SideSolid[BlockFacing.UP.Index]) continue;
                    }
                    catch
                    {
                        continue;
                    }
                }

                pos = new Vec3d(testPos.X, testPos.Y - 1.0, testPos.Z);
                return true;
            }

            for (int dy = 1; dy <= 6; dy++)
            {
                int y = basePos.Y - dy;
                if (y < 0) break;

                var testPos = new Vec3d(basePos.X + 0.5, y + 1.0, basePos.Z + 0.5);

                bool colliding;
                try
                {
                    colliding = ct.IsColliding(ba, selBox, testPos, alsoCheckTouch: false);
                }
                catch
                {
                    colliding = true;
                }

                if (colliding) continue;

                if (requireSolidGround)
                {
                    try
                    {
                        var belowPos = basePos.Copy();
                        belowPos.Y = basePos.Y - dy - 1;
                        var below = ba.GetBlock(belowPos);
                        if (below == null) continue;
                        if (!below.SideSolid[BlockFacing.UP.Index]) continue;
                    }
                    catch
                    {
                        continue;
                    }
                }

                pos = new Vec3d(testPos.X, testPos.Y - 1.0, testPos.Z);
                return true;
            }

            return false;
        }

        private void TeleportEntity(Entity target, Vec3d pos, int dim)
        {
            if (target == null || pos == null) return;

            try
            {
                target.IsTeleport = true;
            }
            catch
            {
            }

            target.ServerPos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + dim * 32768.0, pos.Z));
            target.Pos.SetFrom(target.ServerPos);

            try
            {
                target.ServerPos.Motion.Set(0, 0, 0);
            }
            catch
            {
            }
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

        private void TryPlaySound(Stage stage)
        {
            if (sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.sound)) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            float volume = stage.soundVolume;
            if (volume <= 0f) volume = 1f;

            if (stage.soundStartMs > 0)
            {
                sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange, volume);
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
                    sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange, volume);
                }
                catch
                {
                }
            }
        }
    }
}
