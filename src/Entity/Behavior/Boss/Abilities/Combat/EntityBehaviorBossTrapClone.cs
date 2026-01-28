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
    public class EntityBehaviorBossTrapClone : EntityBehavior
    {
        private const string LastStartMsKey = "alegacyvsquest:bosstrapclone:lastStartMs";

        private const string TrapFlagKey = "alegacyvsquest:bosstrapclone:trap";
        private const string TrapOwnerIdKey = "alegacyvsquest:bosstrapclone:ownerid";
        private const string TrapExplodeAtMsKey = "alegacyvsquest:bosstrapclone:explodeat";
        private const string TrapRadiusKey = "alegacyvsquest:bosstrapclone:radius";
        private const string TrapDamageKey = "alegacyvsquest:bosstrapclone:damage";
        private const string TrapDamageTierKey = "alegacyvsquest:bosstrapclone:damagetier";
        private const string TrapDamageTypeKey = "alegacyvsquest:bosstrapclone:damagetype";
        private const string TrapExplodeSoundKey = "alegacyvsquest:bosstrapclone:explodesound";
        private const string TrapExplodeSoundRangeKey = "alegacyvsquest:bosstrapclone:explodesoundrange";
        private const string TrapExplodeSoundVolumeKey = "alegacyvsquest:bosstrapclone:explodesoundvolume";

        private class Stage
        {
            public float whenHealthRelBelow;
            public float cooldownSeconds;

            public float minTargetRange;
            public float maxTargetRange;

            public string trapEntityCode;
            public float spawnRange;
            public int trapCount;

            public int fuseMs;
            public float explosionRadius;
            public float explosionDamage;
            public int damageTier;
            public string damageType;

            public bool trapInvulnerable;

            public string windupAnimation;
            public int windupMs;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public string explodeSound;
            public float explodeSoundRange;
            public float explodeSoundVolume;
        }

        private ICoreServerAPI sapi;
        private readonly List<Stage> stages = new List<Stage>();

        private const int CheckIntervalMs = 200;
        private const int TrapOwnerCheckIntervalMs = 500;
        private const int ExplodeRetryDelayMs = 250;
        private const int ExplodeSoundLimiterMs = 400;
        private const int SpawnSoundLimiterMs = 500;
        private const float DefaultMaxTargetRange = 40f;
        private const float DefaultSpawnRange = 6f;
        private const int DefaultTrapCount = 1;
        private const int DefaultFuseMs = 1500;
        private const float DefaultExplosionRadius = 3f;
        private const float DefaultExplosionDamage = 6f;
        private const int DefaultDamageTier = 3;
        private const string DefaultDamageType = "PiercingAttack";
        private const float DefaultSoundRange = 24f;
        private const int DefaultSoundStartMs = 0;
        private const float DefaultSoundVolume = 1f;
        private const float DefaultExplodeSoundRange = 24f;
        private const float DefaultExplodeSoundVolume = 1f;

        private const float MinSpawnRange = 0.5f;
        private const int MinTrapCount = 1;
        private const int MinFuseMs = 250;
        private const float MinExplosionRadius = 0.5f;
        private const int MinDamageTier = 0;
        private const int SpawnTries = 14;
        private const bool SpawnRequireSolidGround = true;
        private const float SpawnMinRingFrac = 0.4f;
        private const float SpawnMinSeparationMin = 1.5f;
        private const float SpawnMinSeparationSpawnRangeFrac = 0.5f;
        private const float SpawnYawRandomRange = 0.4f;
        private const int FindFreeSpotMaxVerticalSteps = 6;

        private long lastCheckMs;

        private long lastTrapOwnerCheckMs;

        private long callbackId;
        private bool pending;

        public EntityBehaviorBossTrapClone(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosstrapclone";

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
                        maxTargetRange = stageObj["maxTargetRange"].AsFloat(DefaultMaxTargetRange),

                        trapEntityCode = stageObj["trapEntityCode"].AsString(null),
                        spawnRange = stageObj["spawnRange"].AsFloat(DefaultSpawnRange),
                        trapCount = stageObj["trapCount"].AsInt(DefaultTrapCount),

                        fuseMs = stageObj["fuseMs"].AsInt(DefaultFuseMs),
                        explosionRadius = stageObj["explosionRadius"].AsFloat(DefaultExplosionRadius),
                        explosionDamage = stageObj["explosionDamage"].AsFloat(DefaultExplosionDamage),
                        damageTier = stageObj["damageTier"].AsInt(DefaultDamageTier),
                        damageType = stageObj["damageType"].AsString(DefaultDamageType),

                        trapInvulnerable = stageObj["trapInvulnerable"].AsBool(false),

                        windupAnimation = stageObj["windupAnimation"].AsString(null),
                        windupMs = stageObj["windupMs"].AsInt(0),

                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(DefaultSoundRange),
                        soundStartMs = stageObj["soundStartMs"].AsInt(DefaultSoundStartMs),
                        soundVolume = stageObj["soundVolume"].AsFloat(DefaultSoundVolume),

                        explodeSound = stageObj["explodeSound"].AsString(null),
                        explodeSoundRange = stageObj["explodeSoundRange"].AsFloat(DefaultExplodeSoundRange),
                        explodeSoundVolume = stageObj["explodeSoundVolume"].AsFloat(DefaultExplodeSoundVolume),
                    };

                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;
                    if (stage.minTargetRange < 0f) stage.minTargetRange = 0f;
                    if (stage.maxTargetRange < stage.minTargetRange) stage.maxTargetRange = stage.minTargetRange;

                    if (stage.spawnRange <= 0f) stage.spawnRange = MinSpawnRange;
                    if (stage.trapCount <= 0) stage.trapCount = MinTrapCount;

                    if (stage.fuseMs <= 0) stage.fuseMs = MinFuseMs;
                    if (stage.explosionRadius <= 0f) stage.explosionRadius = MinExplosionRadius;
                    if (stage.explosionDamage < 0f) stage.explosionDamage = 0f;
                    if (stage.damageTier < MinDamageTier) stage.damageTier = MinDamageTier;

                    if (stage.windupMs < 0) stage.windupMs = 0;
                    if (stage.soundVolume <= 0f) stage.soundVolume = 1f;
                    if (stage.explodeSoundVolume <= 0f) stage.explodeSoundVolume = 1f;

                    if (!string.IsNullOrWhiteSpace(stage.trapEntityCode))
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

            if (IsTrapEntity())
            {
                TickTrap();
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

            Start(activeStage, target);
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

        private void Start(Stage stage, EntityPlayer target)
        {
            if (sapi == null || entity == null || stage == null || target == null) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastStartMsKey);

            pending = true;

            BossBehaviorUtils.StopAiAndFreeze(entity);
            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            int delay = Math.Max(0, stage.windupMs);
            BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref callbackId);
            callbackId = sapi.Event.RegisterCallback(_ =>
            {
                try
                {
                    SpawnTraps(stage, target);
                }
                catch
                {
                }

                pending = false;
                callbackId = 0;

            }, delay);
        }

        private void SpawnTraps(Stage stage, EntityPlayer target)
        {
            if (sapi == null || entity == null || stage == null || target == null) return;
            if (string.IsNullOrWhiteSpace(stage.trapEntityCode)) return;

            var type = sapi.World.GetEntityType(new AssetLocation(stage.trapEntityCode));
            if (type == null) return;

            int dim = entity.ServerPos.Dimension;
            float yaw = entity.ServerPos.Yaw;

            int count = Math.Max(1, stage.trapCount);
            var placed = new List<Vec3d>();

            float minRingFrac = SpawnMinRingFrac;
            float minSeparation = Math.Max(SpawnMinSeparationMin, stage.spawnRange * SpawnMinSeparationSpawnRangeFrac);

            for (int i = 0; i < count; i++)
            {
                Entity trap = null;
                try
                {
                    trap = sapi.World.ClassRegistry.CreateEntity(type);
                    if (trap == null) continue;

                    ApplyTrapFlags(trap, stage);

                    var spawnPos = TryFindSpawnPositionNear(trap, target.ServerPos.XYZ, stage.spawnRange, tries: SpawnTries, requireSolidGround: SpawnRequireSolidGround, minRingFrac: minRingFrac, avoidPositions: placed, minSeparation: minSeparation);
                    trap.ServerPos.SetPosWithDimension(new Vec3d(spawnPos.X, spawnPos.Y + dim * 32768.0, spawnPos.Z));
                    double yawRand = (sapi.World.Rand.NextDouble() - 0.5) * SpawnYawRandomRange;
                    trap.ServerPos.Yaw = yaw + (float)yawRand;
                    trap.Pos.SetFrom(trap.ServerPos);

                    sapi.World.SpawnEntity(trap);

                    try
                    {
                        placed.Add(spawnPos);
                    }
                    catch
                    {
                    }
                }
                catch
                {
                    if (trap != null)
                    {
                        try
                        {
                            sapi.World.DespawnEntity(trap, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        private Vec3d TryFindSpawnPositionNear(Entity trap, Vec3d center, float range, int tries, bool requireSolidGround)
        {
            return TryFindSpawnPositionNear(trap, center, range, tries, requireSolidGround, minRingFrac: 0f, avoidPositions: null, minSeparation: 0f);
        }

        private Vec3d TryFindSpawnPositionNear(Entity trap, Vec3d center, float range, int tries, bool requireSolidGround, float minRingFrac, List<Vec3d> avoidPositions, float minSeparation)
        {
            if (sapi == null || entity == null || center == null) return entity.ServerPos.XYZ.Clone();

            var world = sapi.World;
            var ba = world?.BlockAccessor;
            var ct = world?.CollisionTester;
            if (ba == null || ct == null) return entity.ServerPos.XYZ.Clone();

            var selBox = trap?.SelectionBox ?? entity.SelectionBox;
            if (selBox == null) return entity.ServerPos.XYZ.Clone();

            int dim = entity.ServerPos.Dimension;
            double r = Math.Max(0.5, range);
            double minR = r * Math.Clamp(minRingFrac, 0f, 0.95f);

            int attemptCount = Math.Max(1, tries);
            for (int attempt = 0; attempt < attemptCount; attempt++)
            {
                double ang = world.Rand.NextDouble() * Math.PI * 2.0;
                double dist = minR + world.Rand.NextDouble() * (r - minR);

                double x = center.X + Math.Cos(ang) * dist;
                double z = center.Z + Math.Sin(ang) * dist;

                int baseY = (int)Math.Round(center.Y);
                var basePos = new BlockPos((int)Math.Floor(x), baseY, (int)Math.Floor(z), dim);

                if (TryFindFreeSpotNearForSelectionBox(selBox, basePos, requireSolidGround, out var found))
                {
                    if (avoidPositions != null && avoidPositions.Count > 0 && minSeparation > 0f)
                    {
                        bool tooClose = false;
                        for (int i = 0; i < avoidPositions.Count; i++)
                        {
                            var p = avoidPositions[i];
                            if (p == null) continue;

                            double dx = found.X - p.X;
                            double dz = found.Z - p.Z;
                            if (dx * dx + dz * dz < (double)minSeparation * (double)minSeparation)
                            {
                                tooClose = true;
                                break;
                            }
                        }

                        if (tooClose)
                        {
                            continue;
                        }
                    }

                    return found;
                }
            }

            return entity.ServerPos.XYZ.Clone();
        }

        private bool TryFindFreeSpotNearForSelectionBox(Cuboidf selBox, BlockPos basePos, bool requireSolidGround, out Vec3d pos)
        {
            pos = null;
            if (sapi == null || entity == null || basePos == null || selBox == null) return false;

            var world = sapi.World;
            var ba = world?.BlockAccessor;
            if (ba == null) return false;

            var ct = world.CollisionTester;
            if (ct == null) return false;

            for (int dy = 0; dy <= FindFreeSpotMaxVerticalSteps; dy++)
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

            for (int dy = 1; dy <= FindFreeSpotMaxVerticalSteps; dy++)
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

        private void ApplyTrapFlags(Entity trap, Stage stage)
        {
            if (trap?.WatchedAttributes == null || stage == null) return;

            try
            {
                trap.WatchedAttributes.SetBool(TrapFlagKey, true);
                trap.WatchedAttributes.MarkPathDirty(TrapFlagKey);
            }
            catch
            {
            }

            try
            {
                trap.WatchedAttributes.SetLong(TrapOwnerIdKey, entity.EntityId);
                trap.WatchedAttributes.MarkPathDirty(TrapOwnerIdKey);
            }
            catch
            {
            }

            try
            {
                trap.WatchedAttributes.SetLong(TrapExplodeAtMsKey, sapi.World.ElapsedMilliseconds + Math.Max(MinFuseMs, stage.fuseMs));
                trap.WatchedAttributes.MarkPathDirty(TrapExplodeAtMsKey);
            }
            catch
            {
            }

            try
            {
                trap.WatchedAttributes.SetFloat(TrapRadiusKey, stage.explosionRadius);
                trap.WatchedAttributes.MarkPathDirty(TrapRadiusKey);
            }
            catch
            {
            }

            try
            {
                trap.WatchedAttributes.SetFloat(TrapDamageKey, stage.explosionDamage);
                trap.WatchedAttributes.MarkPathDirty(TrapDamageKey);
            }
            catch
            {
            }

            try
            {
                trap.WatchedAttributes.SetInt(TrapDamageTierKey, stage.damageTier);
                trap.WatchedAttributes.MarkPathDirty(TrapDamageTierKey);
            }
            catch
            {
            }

            try
            {
                trap.WatchedAttributes.SetString(TrapDamageTypeKey, stage.damageType);
                trap.WatchedAttributes.MarkPathDirty(TrapDamageTypeKey);
            }
            catch
            {
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(stage.explodeSound))
                {
                    trap.WatchedAttributes.SetString(TrapExplodeSoundKey, stage.explodeSound);
                    trap.WatchedAttributes.MarkPathDirty(TrapExplodeSoundKey);
                }
            }
            catch
            {
            }

            try
            {
                trap.WatchedAttributes.SetFloat(TrapExplodeSoundRangeKey, stage.explodeSoundRange);
                trap.WatchedAttributes.MarkPathDirty(TrapExplodeSoundRangeKey);
            }
            catch
            {
            }

            try
            {
                trap.WatchedAttributes.SetFloat(TrapExplodeSoundVolumeKey, stage.explodeSoundVolume);
                trap.WatchedAttributes.MarkPathDirty(TrapExplodeSoundVolumeKey);
            }
            catch
            {
            }

            try
            {
                trap.WatchedAttributes.SetBool("alegacyvsquest:bossclone:invulnerable", stage.trapInvulnerable);
                trap.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:invulnerable");
            }
            catch
            {
            }

            try
            {
                trap.WatchedAttributes.SetBool("showHealthbar", false);
                trap.WatchedAttributes.MarkPathDirty("showHealthbar");
            }
            catch
            {
            }
        }

        private bool IsTrapEntity()
        {
            try
            {
                return entity?.WatchedAttributes?.GetBool(TrapFlagKey, false) ?? false;
            }
            catch
            {
                return false;
            }
        }

        private void TickTrap()
        {
            if (sapi == null || entity == null) return;
            if (!entity.Alive) return;

            long now;
            try
            {
                now = sapi.World.ElapsedMilliseconds;
            }
            catch
            {
                now = 0;
            }

            if (now <= 0) return;

            long explodeAt = 0;
            long ownerId = 0;
            try
            {
                var wa = entity.WatchedAttributes;
                explodeAt = wa.GetLong(TrapExplodeAtMsKey, 0);
                ownerId = wa.GetLong(TrapOwnerIdKey, 0);
            }
            catch
            {
                explodeAt = 0;
                ownerId = 0;
            }

            if (explodeAt <= 0) return;

            if (now < explodeAt)
            {
                // While far from explosion, avoid doing expensive work each tick.
                // Only validate owner occasionally.
                if (ownerId <= 0)
                {
                    sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    return;
                }

                if (lastTrapOwnerCheckMs == 0 || now - lastTrapOwnerCheckMs >= TrapOwnerCheckIntervalMs)
                {
                    lastTrapOwnerCheckMs = now;
                    try
                    {
                        var ownerCheck = sapi.World.GetEntityById(ownerId);
                        if (ownerCheck == null || !ownerCheck.Alive)
                        {
                            sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        }
                    }
                    catch
                    {
                    }
                }

                return;
            }

            // Explosion time reached: read remaining parameters and attempt explode.
            float radius = 0f;
            float damage = 0f;
            int tier = 0;
            string dmgTypeStr = null;
            string explodeSound = null;
            float explodeSoundRange = 0f;
            float explodeSoundVolume = 1f;

            try
            {
                var wa = entity.WatchedAttributes;
                radius = wa.GetFloat(TrapRadiusKey, 0f);
                damage = wa.GetFloat(TrapDamageKey, 0f);
                tier = wa.GetInt(TrapDamageTierKey, 0);
                dmgTypeStr = wa.GetString(TrapDamageTypeKey, null);
                explodeSound = wa.GetString(TrapExplodeSoundKey, null);
                explodeSoundRange = wa.GetFloat(TrapExplodeSoundRangeKey, 0f);
                explodeSoundVolume = wa.GetFloat(TrapExplodeSoundVolumeKey, 1f);
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

            if (!TryExplode(owner as EntityAgent, radius, damage, tier, dmgTypeStr, explodeSound, explodeSoundRange, explodeSoundVolume))
            {
                try
                {
                    var wa = entity.WatchedAttributes;
                    wa.SetLong(TrapExplodeAtMsKey, now + ExplodeRetryDelayMs);
                    wa.MarkPathDirty(TrapExplodeAtMsKey);
                }
                catch
                {
                }

                return;
            }

            sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
        }

        private bool TryExplode(EntityAgent owner, float radius, float damage, int tier, string dmgTypeStr, string explodeSound, float explodeSoundRange, float explodeSoundVolume)
        {
            if (sapi == null || entity == null) return false;
            if (radius <= 0f) return false;
            if (damage <= 0f) return false;

            EnumDamageType dmgType = EnumDamageType.PiercingAttack;
            try
            {
                if (!string.IsNullOrWhiteSpace(dmgTypeStr) && Enum.TryParse(dmgTypeStr, ignoreCase: true, out EnumDamageType parsed))
                {
                    dmgType = parsed;
                }
            }
            catch
            {
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(explodeSound))
                {
                    // Prevent stacking when several traps explode at nearly the same time.
                    long limiterEntityId = 0;
                    try
                    {
                        limiterEntityId = owner?.EntityId ?? 0;
                    }
                    catch
                    {
                        limiterEntityId = 0;
                    }

                    string limiterKey = $"ent:{limiterEntityId}:{explodeSound}";
                    if (!BossBehaviorUtils.ShouldPlaySoundLimited(limiterKey, ExplodeSoundLimiterMs))
                    {
                        explodeSound = null;
                    }

                    var soundLoc = AssetLocation.Create(explodeSound, "game").WithPathPrefixOnce("sounds/");
                    if (soundLoc != null)
                    {
                        float range = explodeSoundRange > 0f ? explodeSoundRange : 24f;
                        float volume = explodeSoundVolume;
                        if (volume <= 0f) volume = 1f;
                        sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, range, volume);
                    }
                }
            }
            catch
            {
            }

            try
            {
                int dim = entity.ServerPos.Dimension;
                var center = new Vec3d(entity.ServerPos.X, entity.ServerPos.Y + dim * 32768.0, entity.ServerPos.Z);
                var entities = sapi.World.GetEntitiesAround(center, radius, radius, e => e is EntityPlayer);
                if (entities == null) return false;

                for (int i = 0; i < entities.Length; i++)
                {
                    if (entities[i] is not EntityPlayer plr) continue;
                    if (!plr.Alive) continue;
                    if (plr.ServerPos.Dimension != entity.ServerPos.Dimension) continue;

                    plr.ReceiveDamage(new DamageSource()
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = owner,
                        Type = dmgType,
                        DamageTier = tier,
                        KnockbackStrength = 0f
                    }, damage);
                }
            }
            catch
            {
                return false;
            }

            try
            {
                int dim = entity.ServerPos.Dimension;
                var center = new Vec3d(entity.ServerPos.X, entity.ServerPos.Y + dim * 32768.0, entity.ServerPos.Z);

                int smokeMin = Math.Max(45, (int)(radius * 40f));
                int smokeMax = Math.Max(smokeMin + 15, (int)(radius * 70f));

                SimpleParticleProperties smoke = new SimpleParticleProperties(
                    smokeMin, smokeMax,
                    ColorUtil.ToRgba(140, 30, 30, 30),
                    new Vec3d(),
                    new Vec3d(radius, Math.Max(1.0, radius * 0.6), radius),
                    new Vec3f(-0.6f, 0.05f, -0.6f),
                    new Vec3f(0.6f, 0.35f, 0.6f),
                    0.0875f,
                    -0.06f,
                    0.5f,
                    0.5f,
                    EnumParticleModel.Quad
                );
                smoke.MinPos = center.AddCopy(-radius * 0.5, -0.25, -radius * 0.5);
                sapi.World.SpawnParticles(smoke);

                SimpleParticleProperties flash = new SimpleParticleProperties(
                    10, 16,
                    ColorUtil.ToRgba(255, 255, 220, 120),
                    new Vec3d(),
                    new Vec3d(Math.Max(0.5, radius * 0.35), 0.25, Math.Max(0.5, radius * 0.35)),
                    new Vec3f(-0.2f, 0.2f, -0.2f),
                    new Vec3f(0.2f, 0.6f, 0.2f),
                    0.03f,
                    0f,
                    0.08f,
                    0.045f,
                    EnumParticleModel.Quad
                );
                flash.MinPos = center.AddCopy(-0.1, 0.2, -0.1);
                sapi.World.SpawnParticles(flash);
            }
            catch
            {
            }

            return true;
        }

        private void CancelPending()
        {
            if (sapi != null)
            {
                BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref callbackId);
            }

            pending = false;
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

            // Prevent stacking when multiple traps are spawned in the same moment.
            if (!BossBehaviorUtils.ShouldPlaySoundLimited(entity, stage.sound, SpawnSoundLimiterMs)) return;

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
