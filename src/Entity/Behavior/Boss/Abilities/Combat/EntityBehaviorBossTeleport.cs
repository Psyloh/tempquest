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
    public class EntityBehaviorBossTeleport : EntityBehavior
    {
        private const string TeleportStageKey = "alegacyvsquest:bossteleport:stage";
        private const string LastTeleportStartMsKey = "alegacyvsquest:bossteleport:lastStartMs";
        private const string CloneOwnerIdKey = "alegacyvsquest:bossclone:ownerid";
        private const string CloneFlagKey = "alegacyvsquest:bossclone";

        private class TeleportStage
        {
            public float whenHealthRelBelow;
            public float cooldownSeconds;

            public float minTargetRange;
            public float maxTargetRange;

            public float minRadius;
            public float maxRadius;
            public int tries;

            public int windupMs;
            public string windupAnimation;
            public string arriveAnimation;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public bool requireSolidGround;

            public bool teleportClones;
            public bool swapWithClones;
        }

        private ICoreServerAPI sapi;
        private readonly List<TeleportStage> stages = new List<TeleportStage>();

        private long teleportCallbackId;
        private bool teleportPending;
        private int pendingStageIndex = -1;

        public EntityBehaviorBossTeleport(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossteleport";

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

                    var stage = new TeleportStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),

                        minTargetRange = stageObj["minTargetRange"].AsFloat(0f),
                        maxTargetRange = stageObj["maxTargetRange"].AsFloat(40f),

                        minRadius = stageObj["minRadius"].AsFloat(3f),
                        maxRadius = stageObj["maxRadius"].AsFloat(7f),
                        tries = stageObj["tries"].AsInt(10),

                        windupMs = stageObj["windupMs"].AsInt(250),
                        windupAnimation = stageObj["windupAnimation"].AsString(null),
                        arriveAnimation = stageObj["arriveAnimation"].AsString(null),

                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),

                        soundVolume = stageObj["soundVolume"].AsFloat(1f),

                        requireSolidGround = stageObj["requireSolidGround"].AsBool(true),

                        teleportClones = stageObj["teleportClones"].AsBool(false),
                        swapWithClones = stageObj["swapWithClones"].AsBool(false),
                    };

                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;
                    if (stage.minTargetRange < 0f) stage.minTargetRange = 0f;
                    if (stage.maxTargetRange < stage.minTargetRange) stage.maxTargetRange = stage.minTargetRange;

                    if (stage.minRadius < 0f) stage.minRadius = 0f;
                    if (stage.maxRadius < stage.minRadius) stage.maxRadius = stage.minRadius;

                    if (stage.tries <= 0) stage.tries = 1;
                    if (stage.windupMs < 0) stage.windupMs = 0;

                    if (stage.soundVolume <= 0f) stage.soundVolume = 1f;

                    stages.Add(stage);
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
                CancelPending();
                return;
            }

            if (teleportPending) return;

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
            if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastTeleportStartMsKey, activeStage.cooldownSeconds)) return;

            if (!TryFindTarget(activeStage, out var target, out float dist)) return;
            if (dist < activeStage.minTargetRange) return;
            if (dist > activeStage.maxTargetRange) return;

            if (!TryFindTeleportPos(activeStage, target, out var tpPos)) return;

            StartTeleport(activeStage, stageIndex, tpPos);
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

        private void StartTeleport(TeleportStage stage, int stageIndex, Vec3d targetPos)
        {
            if (sapi == null || entity == null || stage == null || targetPos == null) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastTeleportStartMsKey);

            teleportPending = true;
            pendingStageIndex = stageIndex;

            BossBehaviorUtils.StopAiAndFreeze(entity);

            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            int delay = Math.Max(0, stage.windupMs);
            teleportCallbackId = sapi.Event.RegisterCallback(_ =>
            {
                try
                {
                    DoTeleport(stage, targetPos);
                    TryPlayAnimation(stage.arriveAnimation);
                }
                catch
                {
                }

                teleportPending = false;
                pendingStageIndex = -1;
                teleportCallbackId = 0;

            }, delay);
        }

        private void DoTeleport(TeleportStage stage, Vec3d pos)
        {
            if (sapi == null || entity == null || pos == null) return;

            int dim = entity.ServerPos.Dimension;

            var clones = (stage != null && (stage.teleportClones || stage.swapWithClones)) ? FindClones() : null;
            Vec3d bossOldPos = new Vec3d(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

            if (stage?.swapWithClones == true && clones != null && clones.Count > 0)
            {
                var chosen = clones[sapi.World.Rand.Next(clones.Count)];
                var clonePos = new Vec3d(chosen.ServerPos.X, chosen.ServerPos.Y, chosen.ServerPos.Z);

                TeleportEntity(chosen, bossOldPos, dim);
                TeleportEntity(entity, clonePos, dim);

                clones.Remove(chosen);

                if (stage.teleportClones && clones.Count > 0)
                {
                    TeleportClonesAround(clones, entity.ServerPos.XYZ, stage, dim);
                }

                return;
            }

            TeleportEntity(entity, pos, dim);

            if (stage?.teleportClones == true && clones != null && clones.Count > 0)
            {
                TeleportClonesAround(clones, entity.ServerPos.XYZ, stage, dim);
            }
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

        private List<Entity> FindClones()
        {
            var result = new List<Entity>();
            if (sapi == null || entity == null) return result;

            var loaded = sapi.World?.LoadedEntities;
            if (loaded == null) return result;

            long ownerId = entity.EntityId;
            foreach (var entry in loaded)
            {
                var e = entry.Value;
                if (e == null || !e.Alive) continue;
                if (e.ServerPos.Dimension != entity.ServerPos.Dimension) continue;

                try
                {
                    var wa = e.WatchedAttributes;
                    if (wa == null) continue;
                    if (!wa.GetBool(CloneFlagKey, false)) continue;
                    long cloneOwner = wa.GetLong(CloneOwnerIdKey, 0);
                    if (cloneOwner != ownerId) continue;
                }
                catch
                {
                    continue;
                }

                result.Add(e);
            }

            return result;
        }

        private void TeleportClonesAround(List<Entity> clones, Vec3d center, TeleportStage stage, int dim)
        {
            if (clones == null || clones.Count == 0 || stage == null || center == null) return;

            for (int i = 0; i < clones.Count; i++)
            {
                var clone = clones[i];
                if (clone == null || !clone.Alive) continue;

                if (TryFindTeleportPos(stage, center, dim, out var pos))
                {
                    TeleportEntity(clone, pos, dim);
                }
            }
        }

        private void CancelPending()
        {
            if (sapi != null)
            {
                BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref teleportCallbackId);
            }

            teleportPending = false;
            pendingStageIndex = -1;

            teleportCallbackId = 0;
        }

        private bool TryFindTarget(TeleportStage stage, out Entity target, out float dist)
        {
            target = null;
            dist = 0f;

            if (sapi == null || entity == null) return false;
            if (entity.Pos == null) return false;

            double range = Math.Max(2.0, stage.maxTargetRange > 0 ? stage.maxTargetRange : 40f);
            try
            {
                var own = entity.ServerPos.XYZ;
                float frange = (float)range;
                var found = sapi.World.GetNearestEntity(own, frange, frange, e => e is EntityPlayer);
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

        private bool TryFindTeleportPos(TeleportStage stage, Entity target, out Vec3d pos)
        {
            pos = null;
            if (sapi == null || entity == null || target == null) return false;

            double minR = Math.Max(0.0, stage.minRadius);
            double maxR = Math.Max(minR, stage.maxRadius);
            if (maxR <= 0.01) maxR = 0.01;

            var world = sapi.World;
            if (world == null) return false;

            var ba = world.BlockAccessor;
            if (ba == null) return false;

            int tries = Math.Max(1, stage.tries);

            int dim = entity.ServerPos.Dimension;
            var targetPos = new Vec3d(target.ServerPos.X, target.ServerPos.Y, target.ServerPos.Z);
            return TryFindTeleportPos(stage, targetPos, dim, out pos);
        }

        private bool TryFindTeleportPos(TeleportStage stage, Vec3d targetPos, int dim, out Vec3d pos)
        {
            pos = null;
            if (sapi == null || entity == null || targetPos == null || stage == null) return false;

            double minR = Math.Max(0.0, stage.minRadius);
            double maxR = Math.Max(minR, stage.maxRadius);
            if (maxR <= 0.01) maxR = 0.01;

            var world = sapi.World;
            if (world == null) return false;

            var ba = world.BlockAccessor;
            if (ba == null) return false;

            int tries = Math.Max(1, stage.tries);

            for (int attempt = 0; attempt < tries; attempt++)
            {
                double ang = world.Rand.NextDouble() * Math.PI * 2.0;
                double dist = minR + world.Rand.NextDouble() * (maxR - minR);

                double x = targetPos.X + Math.Cos(ang) * dist;
                double z = targetPos.Z + Math.Sin(ang) * dist;

                int baseY = (int)Math.Round(targetPos.Y);

                var tmp = new BlockPos((int)Math.Floor(x), baseY, (int)Math.Floor(z), dim);

                if (TryFindFreeSpotNear(tmp, stage.requireSolidGround, out var found))
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

            for (int dy = 0; dy <= 4; dy++)
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

            for (int dy = 1; dy <= 4; dy++)
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

        private void TryPlaySound(TeleportStage stage)
        {
            if (sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.sound)) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            float volume = stage.soundVolume;
            try
            {
                Dictionary<string, float> volumeBySound = null;
                try
                {
                    volumeBySound = entity?.Properties?.Attributes?["SoundVolumeMulBySound"]?.AsObject<Dictionary<string, float>>();
                }
                catch
                {
                }

                if (volumeBySound != null && volumeBySound.Count > 0)
                {
                    string fullKey = soundLoc.ToString();
                    string pathKey = soundLoc.Path;

                    foreach (var entry in volumeBySound)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Key)) continue;

                        if (string.Equals(entry.Key, fullKey, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(entry.Key, pathKey, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(entry.Key, stage.sound, StringComparison.OrdinalIgnoreCase))
                        {
                            if (entry.Value > 0f)
                            {
                                volume *= entry.Value;
                            }
                            break;
                        }
                    }
                }
            }
            catch
            {
            }

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
