using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public partial class BossHuntSystem
    {
        private void TryDespawnBossOnRotation(BossHuntConfig cfg, double nowHours)
        {
            if (sapi == null || cfg == null) return;

            var bossEntity = FindBossEntityImmediateAny(cfg.bossKey);
            if (bossEntity == null) return;

            if (!bossEntity.Alive)
            {
                TryDespawnBossCorpse(bossEntity);
                return;
            }

            try
            {
                sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
            catch
            {
            }
        }

        private void TryDespawnBossCorpse(Entity bossEntity)
        {
            if (sapi == null || bossEntity == null || bossEntity.Alive) return;

            try
            {
                sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
            catch
            {
            }
        }

        private Entity FindBossEntityImmediateAny(string bossTargetId)
        {
            if (sapi == null) return null;
            if (string.IsNullOrWhiteSpace(bossTargetId)) return null;

            var loaded = sapi.World?.LoadedEntities;
            if (loaded == null) return null;

            try
            {
                foreach (var e in loaded.Values)
                {
                    if (e == null) continue;
                    var qt = e.GetBehavior<EntityBehaviorQuestTarget>();
                    if (qt == null) continue;

                    if (string.Equals(qt.TargetId, bossTargetId, StringComparison.OrdinalIgnoreCase))
                    {
                        return e;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private Entity FindBossEntityImmediate(string bossTargetId)
        {
            if (sapi == null) return null;
            if (string.IsNullOrWhiteSpace(bossTargetId)) return null;

            var loaded = sapi.World?.LoadedEntities;
            if (loaded == null) return null;

            try
            {
                foreach (var e in loaded.Values)
                {
                    if (e == null || !e.Alive) continue;
                    var qt = e.GetBehavior<EntityBehaviorQuestTarget>();
                    if (qt == null) continue;

                    if (string.Equals(qt.TargetId, bossTargetId, StringComparison.OrdinalIgnoreCase))
                    {
                        return e;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private Entity FindBossEntity(BossHuntConfig cfg, double nowHours)
        {
            if (cfg == null) return null;

            var bossTargetId = cfg.bossKey;

            if (cachedBossEntity != null && cachedBossEntity.Alive)
            {
                var qtCached = cachedBossEntity.GetBehavior<EntityBehaviorQuestTarget>();
                if (qtCached != null && string.Equals(qtCached.TargetId, bossTargetId, StringComparison.OrdinalIgnoreCase))
                {
                    return cachedBossEntity;
                }

                cachedBossEntity = null;
            }

            if (cachedBossKey == null || !string.Equals(cachedBossKey, bossTargetId, StringComparison.OrdinalIgnoreCase))
            {
                cachedBossKey = bossTargetId;
                nextBossEntityScanTotalHours = 0;
            }

            if (nowHours < nextBossEntityScanTotalHours)
            {
                return null;
            }

            double scanInterval = bossEntityScanIntervalHours;
            if (scanInterval <= 0) scanInterval = 1.0 / 60.0;
            nextBossEntityScanTotalHours = nowHours + scanInterval;

            var loaded = sapi?.World?.LoadedEntities;
            if (loaded == null) return null;

            try
            {
                Entity deadMatch = null;
                foreach (var e in loaded.Values)
                {
                    if (e == null) continue;
                    var qt = e.GetBehavior<EntityBehaviorQuestTarget>();
                    if (qt == null) continue;

                    if (string.Equals(qt.TargetId, bossTargetId, StringComparison.OrdinalIgnoreCase))
                    {
                        // Prefer a live entity if one exists. During multi-phase rebirth there can be a short
                        // overlap where a corpse and the reborn phase share the same targetId.
                        if (e.Alive)
                        {
                            cachedBossEntity = e;
                            return e;
                        }

                        // Keep the corpse as a fallback only if no living boss is found.
                        deadMatch ??= e;
                    }
                }

                if (deadMatch != null)
                {
                    cachedBossEntity = deadMatch;
                    return deadMatch;
                }
            }
            catch
            {
            }

            return null;
        }

        private void TrySpawnBoss(BossHuntConfig cfg, Vec3d point, int dim, BossHuntAnchorPoint anchorPoint)
        {
            if (cfg == null) return;
            if (point == null) return;

            // Safety: do not spawn a new boss if a living one with the same targetId already exists.
            // This can happen during multi-phase rebirth or if a corpse is detected first.
            try
            {
                var existing = FindBossEntityImmediate(cfg.bossKey);
                if (existing != null && existing.Alive)
                {
                    return;
                }
            }
            catch
            {
            }

            try
            {
                var type = sapi.World.GetEntityType(new AssetLocation(cfg.GetBossEntityCode()));
                if (type == null)
                {
                    DebugLog($"Spawn failed: entity type not found for code '{cfg.GetBossEntityCode()}'.", force: true);
                    return;
                }

                Entity entity = sapi.World.ClassRegistry.CreateEntity(type);
                if (entity == null)
                {
                    DebugLog($"Spawn failed: entity create returned null for code '{cfg.GetBossEntityCode()}'.", force: true);
                    return;
                }

                if (entity.WatchedAttributes != null)
                {
                    entity.WatchedAttributes.SetString("alegacyvsquest:killaction:targetid", cfg.bossKey);
                    entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:killaction:targetid");
                }

                EntityBehaviorQuestTarget.SetSpawnerAnchor(entity, new BlockPos((int)point.X, (int)point.Y, (int)point.Z, dim));
                if (anchorPoint != null && entity.WatchedAttributes != null)
                {
                    if (anchorPoint.leashRange > 0f)
                    {
                        entity.WatchedAttributes.SetFloat(EntityBehaviorQuestTarget.LeashRangeKey, anchorPoint.leashRange);
                        entity.WatchedAttributes.MarkPathDirty(EntityBehaviorQuestTarget.LeashRangeKey);
                    }

                    entity.WatchedAttributes.SetFloat(EntityBehaviorQuestTarget.OutOfCombatLeashRangeKey, anchorPoint.outOfCombatLeashRange);
                    entity.WatchedAttributes.MarkPathDirty(EntityBehaviorQuestTarget.OutOfCombatLeashRangeKey);
                }

                entity.ServerPos.SetPosWithDimension(new Vec3d(point.X, point.Y + dim * 32768.0, point.Z));
                entity.Pos.SetFrom(entity.ServerPos);

                sapi.World.SpawnEntity(entity);

                // Avoid repeated spawns: FindBossEntity() is throttled to scan loaded entities only once per minute.
                // Cache the newly spawned boss immediately.
                cachedBossEntity = entity;
                cachedBossKey = cfg.bossKey;
                double scanInterval = bossEntityScanIntervalHours;
                if (scanInterval <= 0) scanInterval = 1.0 / 60.0;
                nextBossEntityScanTotalHours = (sapi.World?.Calendar?.TotalHours ?? 0) + scanInterval;
            }
            catch
            {
            }
        }

        private void DebugLog(string message, bool force = false)
        {
            if (!debugBossHunt || sapi == null || string.IsNullOrWhiteSpace(message)) return;

            double nowHours = sapi.World?.Calendar?.TotalHours ?? 0;
            if (!force && nowHours < nextDebugLogTotalHours) return;

            double throttle = debugLogThrottleHours;
            if (throttle <= 0) throttle = 0.02;
            nextDebugLogTotalHours = nowHours + throttle;
            sapi.Logger.Notification("[BossHunt] " + message);
        }

        private int PickAnotherIndex(int current, int count)
        {
            if (count <= 1) return 0;

            int next = current;
            try
            {
                for (int i = 0; i < 5 && next == current; i++)
                {
                    next = sapi.World.Rand.Next(0, count);
                }
            }
            catch
            {
                next = (current + 1) % count;
            }

            if (next == current)
            {
                next = (current + 1) % count;
            }

            return next;
        }

        private bool AnyPlayerNear(Vec3d point, int dim, float range)
        {
            if (point == null) return false;

            return AnyPlayerNear(point.X, point.Y, point.Z, dim, range);
        }

        private bool AnyPlayerNear(double x, double y, double z, int dim, float range)
        {
            if (range <= 0) range = 160f;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return false;

            double rangeSq = range * (double)range;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe?.Pos == null) continue;
                if (pe.Pos.Dimension != dim) continue;

                double dx = pe.Pos.X - x;
                double dy = pe.Pos.Y - y;
                double dz = pe.Pos.Z - z;

                if (dx * dx + dy * dy + dz * dz <= rangeSq) return true;
            }

            return false;
        }

        private bool IsSafeToRelocate(BossHuntConfig cfg, Entity bossEntity, double nowHours)
        {
            if (bossEntity == null) return true;

            double lastDamage = bossEntity.WatchedAttributes.GetDouble(LastBossDamageTotalHoursKey, double.NaN);
            if (!double.IsNaN(lastDamage))
            {
                double lockHours = cfg.GetNoRelocateAfterDamageHours();
                if (lockHours > 0 && nowHours - lastDamage < lockHours)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (sapi == null || entity == null) return;

            if (configs == null || configs.Count == 0) return;

            var qt = entity.GetBehavior<EntityBehaviorQuestTarget>();
            if (qt == null) return;

            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null) continue;

                var bossTargetId = cfg.bossKey;

                if (!string.Equals(qt.TargetId, bossTargetId, StringComparison.OrdinalIgnoreCase)) continue;

                var st = GetOrCreateState(cfg.bossKey);

                double nowHours = sapi.World.Calendar.TotalHours;
                st.deadUntilTotalHours = nowHours + cfg.GetRespawnHours();

                // Rotate location on death to prevent camping.
                st.currentPointIndex = PickAnotherIndex(st.currentPointIndex, GetPointCount(cfg, st));
                st.nextRelocateAtTotalHours = nowHours + cfg.GetRelocateIntervalHours();

                stateDirty = true;
                return;
            }
        }
    }
}
