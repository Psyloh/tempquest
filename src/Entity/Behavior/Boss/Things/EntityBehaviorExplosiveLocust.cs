using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorExplosiveLocust : EntityBehavior
    {
        private const string ExplodeAtMsKey = "alegacyvsquest:explosivelocust:explodeat";
        private const string SummonedByEntityIdKey = "alegacyvsquest:bosssummonritual:summonedByEntityId";

        private const int ExplodeAtRescheduleGraceMs = 2000;

        private const int ProximityCheckIntervalDefaultMs = 250;
        private const int ProximityCheckIntervalNearExplodeMs = 125;
        private const int ProximityNearExplodeWindowMs = 1000;

        private const float ProximityExplodeDistanceSq = 1.0f;

        private ICoreServerAPI sapi;
        private readonly BossBehaviorUtils.LoopSound loopSound = new BossBehaviorUtils.LoopSound();

        private int fuseMs;
        private float explosionRadius;
        private float explosionDamage;
        private int damageTier;
        private string damageType;

        private string loopSoundCode;
        private float loopSoundRange;
        private int loopSoundIntervalMs;
        private float loopSoundVolume;

        private string explodeSound;
        private float explodeSoundRange;
        private float explodeSoundVolume;

        private bool loopStarted;

        private float leapCooldownSeconds;
        private float leapTriggerRange;
        private float leapHorizontalSpeed;
        private float leapVerticalSpeed;
        private long lastLeapAtMs;

        private long spawnedAtMs;

        private long lastExplodeScheduleCheckMs;
        private long lastProximityCheckMs;

        public EntityBehaviorExplosiveLocust(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "explosivelocust";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;

            loopStarted = false;

            fuseMs = attributes?["fuseMs"].AsInt(3000) ?? 3000;
            if (fuseMs <= 0) fuseMs = 3000;

            explosionRadius = attributes?["explosionRadius"].AsFloat(3f) ?? 3f;
            if (explosionRadius <= 0f) explosionRadius = 0.5f;

            explosionDamage = attributes?["explosionDamage"].AsFloat(8f) ?? 8f;
            if (explosionDamage < 0f) explosionDamage = 0f;

            damageTier = attributes?["damageTier"].AsInt(3) ?? 3;
            if (damageTier < 0) damageTier = 0;

            damageType = attributes?["damageType"].AsString("PiercingAttack") ?? "PiercingAttack";

            loopSoundCode = attributes?["loopSound"].AsString("albase:mechanical/mecha switch") ?? "albase:mechanical/mecha switch";
            loopSoundRange = attributes?["loopSoundRange"].AsFloat(10f) ?? 10f;
            loopSoundIntervalMs = attributes?["loopSoundIntervalMs"].AsInt(900) ?? 900;
            loopSoundVolume = attributes?["loopSoundVolume"].AsFloat(0.6f) ?? 0.6f;
            if (loopSoundIntervalMs <= 0) loopSoundIntervalMs = 900;
            if (loopSoundVolume <= 0f) loopSoundVolume = 0.6f;

            explodeSound = attributes?["explodeSound"].AsString("effect/smallexplosion") ?? "effect/smallexplosion";
            explodeSoundRange = attributes?["explodeSoundRange"].AsFloat(18f) ?? 18f;
            explodeSoundVolume = attributes?["explodeSoundVolume"].AsFloat(0.9f) ?? 0.9f;
            if (explodeSoundVolume <= 0f) explodeSoundVolume = 0.9f;

            leapCooldownSeconds = attributes?["leapCooldownSeconds"].AsFloat(1.0f) ?? 1.0f;
            if (leapCooldownSeconds < 0.1f) leapCooldownSeconds = 0.1f;

            leapTriggerRange = attributes?["leapTriggerRange"].AsFloat(12f) ?? 12f;
            if (leapTriggerRange < 1f) leapTriggerRange = 1f;

            leapHorizontalSpeed = attributes?["leapHorizontalSpeed"].AsFloat(1.3f) ?? 1.3f;
            if (leapHorizontalSpeed < 0.1f) leapHorizontalSpeed = 0.1f;

            leapVerticalSpeed = attributes?["leapVerticalSpeed"].AsFloat(0.55f) ?? 0.55f;
            if (leapVerticalSpeed < 0.05f) leapVerticalSpeed = 0.05f;

            lastLeapAtMs = 0;

            try
            {
                spawnedAtMs = sapi?.World?.ElapsedMilliseconds ?? 0;
            }
            catch
            {
                spawnedAtMs = 0;
            }

            long nowMs = spawnedAtMs;
            if (nowMs <= 0)
            {
                try
                {
                    nowMs = sapi?.World?.ElapsedMilliseconds ?? 0;
                }
                catch
                {
                    nowMs = 0;
                }
            }

            EnsureExplodeAtScheduled(nowMs);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (sapi == null || entity == null) return;
            if (entity.Api?.Side != EnumAppSide.Server) return;

            if (!entity.Alive)
            {
                loopSound.Stop();
                loopStarted = false;
                return;
            }

            long nowMs;
            try
            {
                nowMs = sapi.World.ElapsedMilliseconds;
            }
            catch
            {
                nowMs = 0;
            }

            if (nowMs > 0)
            {
                if (lastExplodeScheduleCheckMs == 0 || nowMs - lastExplodeScheduleCheckMs >= 500)
                {
                    EnsureExplodeAtScheduled(nowMs);
                    lastExplodeScheduleCheckMs = nowMs;
                }

                long explodeAtIntervalMs = 0;
                try
                {
                    explodeAtIntervalMs = entity.WatchedAttributes.GetLong(ExplodeAtMsKey, 0);
                }
                catch
                {
                    explodeAtIntervalMs = 0;
                }

                int proximityInterval = ProximityCheckIntervalDefaultMs;
                if (explodeAtIntervalMs > 0)
                {
                    long msLeft = explodeAtIntervalMs - nowMs;
                    if (msLeft <= ProximityNearExplodeWindowMs)
                    {
                        proximityInterval = ProximityCheckIntervalNearExplodeMs;
                    }
                }

                if (lastProximityCheckMs == 0 || nowMs - lastProximityCheckMs >= proximityInterval)
                {
                    TryLeapAtTarget(nowMs);
                    TryExplodeOnProximity(nowMs);
                    lastProximityCheckMs = nowMs;
                }
            }

            EnsureLoopSoundStarted();

            long explodeAtMs;
            try
            {
                explodeAtMs = entity.WatchedAttributes.GetLong(ExplodeAtMsKey, 0);
            }
            catch
            {
                explodeAtMs = 0;
            }
            if (explodeAtMs <= 0) return;
            if (nowMs > 0 && nowMs < explodeAtMs) return;

            Explode();
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            loopSound.Stop();
            loopStarted = false;
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            loopSound.Stop();
            loopStarted = false;
            base.OnEntityDespawn(despawn);
        }

        private void EnsureExplodeAtScheduled(long now)
        {
            if (sapi == null || entity == null) return;
            if (now <= 0) return;

            try
            {
                long explodeAt = entity.WatchedAttributes.GetLong(ExplodeAtMsKey, 0);

                if (explodeAt > 0)
                {
                    long maxValid = now + Math.Max(ExplodeAtRescheduleGraceMs, fuseMs * 3);
                    if (explodeAt > maxValid)
                    {
                        entity.WatchedAttributes.SetLong(ExplodeAtMsKey, now + fuseMs);
                        entity.WatchedAttributes.MarkPathDirty(ExplodeAtMsKey);
                    }

                    return;
                }

                entity.WatchedAttributes.SetLong(ExplodeAtMsKey, now + fuseMs);
                entity.WatchedAttributes.MarkPathDirty(ExplodeAtMsKey);
            }
            catch
            {
            }
        }

        private void TryExplodeOnProximity(long now)
        {
            if (sapi == null || entity == null) return;
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (now <= 0) return;

            try
            {
                if (spawnedAtMs > 0)
                {
                    long maxLife = (long)Math.Max(1500, fuseMs * 3);
                    if (now - spawnedAtMs > maxLife)
                    {
                        Explode();
                        return;
                    }
                }
            }
            catch
            {
            }

            EntityPlayer target = null;
            try
            {
                float range = Math.Max(1.2f, Math.Min(3.5f, explosionRadius));
                target = sapi.World.GetNearestEntity(entity.ServerPos.XYZ, range, range, e => e is EntityPlayer plr && plr.Alive) as EntityPlayer;
            }
            catch
            {
                target = null;
            }

            if (target == null) return;

            try
            {
                float dx = (float)(target.ServerPos.X - entity.ServerPos.X);
                float dy = (float)(target.ServerPos.Y - entity.ServerPos.Y);
                float dz = (float)(target.ServerPos.Z - entity.ServerPos.Z);
                float distSq = dx * dx + dy * dy + dz * dz;

                if (distSq <= ProximityExplodeDistanceSq)
                {
                    Explode();
                }
            }
            catch
            {
            }
        }

        private void EnsureLoopSoundStarted()
        {
            if (sapi == null || entity == null) return;
            if (string.IsNullOrWhiteSpace(loopSoundCode)) return;

            if (loopStarted) return;

            try
            {
                loopSound.Start(sapi, entity, loopSoundCode, loopSoundRange, loopSoundIntervalMs, loopSoundVolume);
                loopStarted = true;
            }
            catch
            {
            }
        }

        private void Explode()
        {
            if (sapi == null || entity == null) return;

            loopSound.Stop();
            loopStarted = false;

            EntityAgent owner = null;
            try
            {
                long ownerId = entity.WatchedAttributes.GetLong(SummonedByEntityIdKey, 0);
                if (ownerId > 0)
                {
                    owner = sapi.World.GetEntityById(ownerId) as EntityAgent;
                }
            }
            catch
            {
                owner = null;
            }

            EnumDamageType dmgType = EnumDamageType.PiercingAttack;
            try
            {
                if (!string.IsNullOrWhiteSpace(damageType) && Enum.TryParse(damageType, ignoreCase: true, out EnumDamageType parsed))
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

            TrySpawnExplosionParticles();

            try
            {
                int dim = entity.ServerPos.Dimension;
                var center = new Vec3d(entity.ServerPos.X, entity.ServerPos.Y + dim * 32768.0, entity.ServerPos.Z);
                var entities = sapi.World.GetEntitiesAround(center, explosionRadius, explosionRadius, e => e is EntityPlayer);
                if (entities != null)
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        if (entities[i] is not EntityPlayer plr) continue;
                        if (!plr.Alive) continue;
                        if (plr.ServerPos.Dimension != entity.ServerPos.Dimension) continue;

                        plr.ReceiveDamage(new DamageSource()
                        {
                            Source = EnumDamageSource.Entity,
                            SourceEntity = owner ?? entity,
                            Type = dmgType,
                            DamageTier = damageTier,
                            KnockbackStrength = 0f
                        }, explosionDamage);
                    }
                }
            }
            catch
            {
            }

            try
            {
                sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
            catch
            {
            }
        }

        private void TrySpawnExplosionParticles()
        {
            if (sapi == null || entity == null) return;

            try
            {
                int dim = entity.ServerPos.Dimension;
                var center = new Vec3d(entity.ServerPos.X, entity.ServerPos.Y + dim * 32768.0, entity.ServerPos.Z);

                float radius = explosionRadius;
                if (radius <= 0.1f) radius = 1f;

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
        }

        private void TryLeapAtTarget(long now)
        {
            if (sapi == null || entity == null) return;
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (now <= 0) return;
            long cdMs = (long)(leapCooldownSeconds * 1000f);
            if (cdMs < 50) cdMs = 50;

            if (lastLeapAtMs > 0 && now - lastLeapAtMs < cdMs) return;

            EntityPlayer target = null;
            try
            {
                float range = leapTriggerRange;
                if (range <= 0.1f) range = 6f;

                var own = entity.ServerPos.XYZ;
                var found = sapi.World.GetNearestEntity(own, range, range, e => e is EntityPlayer plr && plr.Alive);
                target = found as EntityPlayer;
            }
            catch
            {
                target = null;
            }

            if (target == null) return;
            if (target.ServerPos.Dimension != entity.ServerPos.Dimension) return;

            Vec3d from = entity.ServerPos.XYZ;
            Vec3d to = target.ServerPos.XYZ;

            Vec3d dir = to.SubCopy(from);
            dir.Y = 0;
            double len = dir.Length();
            if (len < 0.001) return;
            dir.Mul(1.0 / len);

            try
            {
                var motion = entity.ServerPos.Motion;
                motion.X = dir.X * leapHorizontalSpeed;
                motion.Z = dir.Z * leapHorizontalSpeed;
                motion.Y = Math.Max(motion.Y, leapVerticalSpeed);
                entity.ServerPos.Motion = motion;
                entity.Pos.Motion = motion;

                entity.ServerPos.Yaw = (float)Math.Atan2(dir.X, dir.Z);
            }
            catch
            {
            }

            try
            {
                entity.AnimManager?.StartAnimation("jump");
            }
            catch
            {
            }

            lastLeapAtMs = now;
        }
    }
}
