using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BossHuntSystem : ModSystem
    {
        public const string LastBossDamageTotalHoursKey = "alegacyvsquest:bosshunt:lastBossDamageTotalHours";

        private const string SaveKey = "alegacyvsquest:bosshunt:state";
        private const bool DebugBossHunt = true;

        private ICoreServerAPI sapi;
        private readonly List<BossHuntConfig> configs = new();
        private BossHuntWorldState state;
        private bool stateDirty;

        private long tickListenerId;

        private readonly Dictionary<string, List<BossHuntAnchorPoint>> orderedAnchorsCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> orderedAnchorsDirty = new(StringComparer.OrdinalIgnoreCase);

        private Entity cachedBossEntity;
        private string cachedBossKey;
        private double nextBossEntityScanTotalHours;
        private double nextDebugLogTotalHours;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            LoadConfigs();
            LoadState();

            tickListenerId = sapi.Event.RegisterGameTickListener(OnTick, 1000);
            sapi.Event.GameWorldSave += OnWorldSave;
            sapi.Event.OnEntityDeath += OnEntityDeath;
        }

        public override void Dispose()
        {
            if (sapi != null)
            {
                if (tickListenerId != 0)
                {
                    sapi.Event.UnregisterGameTickListener(tickListenerId);
                    tickListenerId = 0;
                }

                sapi.Event.GameWorldSave -= OnWorldSave;
                sapi.Event.OnEntityDeath -= OnEntityDeath;
            }

            base.Dispose();
        }

        private void LoadConfigs()
        {
            configs.Clear();

            foreach (var mod in sapi.ModLoader.Mods)
            {
                try
                {
                    var assets = sapi.Assets.GetMany<BossHuntConfig>(sapi.Logger, "config/bosshunt", mod.Info.ModID);
                    foreach (var asset in assets)
                    {
                        if (asset.Value != null)
                        {
                            configs.Add(asset.Value);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private void LoadState()
        {
            try
            {
                state = sapi.WorldManager.SaveGame.GetData<BossHuntWorldState>(SaveKey, new BossHuntWorldState());
            }
            catch
            {
                state = new BossHuntWorldState();
            }

            if (state.entries == null) state.entries = new List<BossHuntStateEntry>();
        }

        private void OnWorldSave()
        {
            SaveStateIfDirty();
        }

        private void SaveStateIfDirty()
        {
            if (!stateDirty) return;
            if (sapi == null) return;

            try
            {
                sapi.WorldManager.SaveGame.StoreData(SaveKey, state);
            }
            catch
            {
            }

            stateDirty = false;
        }

        private BossHuntStateEntry GetOrCreateState(string bossKey)
        {
            if (state == null)
            {
                state = new BossHuntWorldState();
            }

            if (state.entries == null)
            {
                state.entries = new List<BossHuntStateEntry>();
            }

            for (int i = 0; i < state.entries.Count; i++)
            {
                var e = state.entries[i];
                if (e != null && string.Equals(e.bossKey, bossKey, StringComparison.OrdinalIgnoreCase))
                {
                    return e;
                }
            }

            var created = new BossHuntStateEntry
            {
                bossKey = bossKey,
                currentPointIndex = 0,
                nextRelocateAtTotalHours = 0,
                deadUntilTotalHours = 0,
                anchorPoints = new List<BossHuntAnchorPoint>()
            };

            state.entries.Add(created);
            stateDirty = true;
            return created;
        }

        private void NormalizeState(BossHuntConfig cfg, BossHuntStateEntry st)
        {
            if (cfg == null || st == null) return;

            st.anchorPoints ??= new List<BossHuntAnchorPoint>();

            int count = GetPointCount(cfg, st);
            if (count <= 0)
            {
                st.currentPointIndex = 0;
                return;
            }

            if (st.currentPointIndex < 0 || st.currentPointIndex >= count)
            {
                st.currentPointIndex = 0;
                stateDirty = true;
            }
        }

        public string[] GetKnownBossKeys()
        {
            if (configs == null || configs.Count == 0) return Array.Empty<string>();

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null) continue;
                if (string.IsNullOrWhiteSpace(cfg.bossKey)) continue;
                set.Add(cfg.bossKey);
            }

            var list = new List<string>(set);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list.ToArray();
        }

        public void SetAnchorPoint(string bossKey, string anchorId, int pointOrder, BlockPos pos)
        {
            if (sapi == null) return;
            if (string.IsNullOrWhiteSpace(bossKey)) return;
            if (pos == null) return;
            if (string.IsNullOrWhiteSpace(anchorId)) return;

            var cfg = FindConfig(bossKey);
            if (cfg == null) return;

            bossKey = cfg.bossKey;

            var st = GetOrCreateState(bossKey);
            NormalizeState(cfg, st);

            st.anchorPoints ??= new List<BossHuntAnchorPoint>();

            BossHuntAnchorPoint existing = null;
            for (int i = 0; i < st.anchorPoints.Count; i++)
            {
                var ap = st.anchorPoints[i];
                if (ap == null) continue;
                if (string.Equals(ap.anchorId, anchorId, StringComparison.OrdinalIgnoreCase))
                {
                    existing = ap;
                    break;
                }
            }

            if (existing == null)
            {
                existing = new BossHuntAnchorPoint();
                st.anchorPoints.Add(existing);
            }

            existing.anchorId = anchorId;
            existing.order = pointOrder;
            existing.x = pos.X;
            existing.y = pos.Y;
            existing.z = pos.Z;
            existing.dim = pos.dimension;

            stateDirty = true;
            orderedAnchorsDirty.Add(bossKey);
            DebugLog($"Anchor registered: bossKey={bossKey} id={anchorId} order={pointOrder} pos={pos.X},{pos.Y},{pos.Z} dim={pos.dimension}", force: true);
        }

        public void UnsetAnchorPoint(string bossKey, string anchorId, BlockPos pos)
        {
            if (sapi == null) return;
            if (string.IsNullOrWhiteSpace(bossKey)) return;
            if (pos == null) return;
            if (string.IsNullOrWhiteSpace(anchorId)) return;

            var cfg = FindConfig(bossKey);
            if (cfg == null) return;

            bossKey = cfg.bossKey;

            var st = GetOrCreateState(bossKey);
            if (st.anchorPoints == null || st.anchorPoints.Count == 0) return;

            for (int i = st.anchorPoints.Count - 1; i >= 0; i--)
            {
                var cur = st.anchorPoints[i];
                if (cur == null) continue;

                if (!string.Equals(cur.anchorId, anchorId, StringComparison.OrdinalIgnoreCase)) continue;

                if (cur.x == pos.X && cur.y == pos.Y && cur.z == pos.Z && cur.dim == pos.dimension)
                {
                    st.anchorPoints.RemoveAt(i);
                    stateDirty = true;
                    orderedAnchorsDirty.Add(bossKey);
                    break;
                }
            }
        }

        public bool TryGetBossPosition(string bossKey, out Vec3d pos, out int dimension, out bool isLiveEntity)
        {
            pos = null;
            dimension = 0;
            isLiveEntity = false;

            if (sapi == null) return false;
            if (string.IsNullOrWhiteSpace(bossKey)) return false;

            if (!string.Equals(GetActiveBossKey(), bossKey, StringComparison.OrdinalIgnoreCase)) return false;

            var cfg = FindConfig(bossKey);
            if (cfg == null) return false;

            var st = GetOrCreateState(cfg.bossKey);
            NormalizeState(cfg, st);

            double nowHours = sapi.World.Calendar.TotalHours;
            var bossEntity = FindBossEntity(cfg, nowHours);
            if (bossEntity != null && bossEntity.Alive)
            {
                pos = new Vec3d(bossEntity.ServerPos.X, bossEntity.ServerPos.Y, bossEntity.ServerPos.Z);
                dimension = bossEntity.ServerPos.Dimension;
                isLiveEntity = true;
                return true;
            }

            if (!TryGetPoint(cfg, st, st.currentPointIndex, out var p, out int dim)) return false;
            pos = p;
            dimension = dim;
            isLiveEntity = false;
            return true;
        }

        private BossHuntConfig FindConfig(string bossKey)
        {
            if (configs == null) return null;

            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null) continue;
                if (string.Equals(cfg.bossKey, bossKey, StringComparison.OrdinalIgnoreCase)) return cfg;
            }

            return null;
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

        public string GetActiveBossKey()
        {
            return state?.activeBossKey;
        }

        public string GetActiveBossQuestId()
        {
            var cfg = FindConfig(GetActiveBossKey());
            return cfg?.questId;
        }

        public bool ForceRotateToNext(out string bossKey, out string questId)
        {
            bossKey = null;
            questId = null;

            if (sapi == null) return false;

            double nowHours = sapi.World.Calendar.TotalHours;

            if (state == null) state = new BossHuntWorldState();
            if (state.entries == null) state.entries = new List<BossHuntStateEntry>();

            state.nextBossRotationTotalHours = nowHours - 0.01;
            stateDirty = true;

            cachedBossEntity = null;
            cachedBossKey = null;
            nextBossEntityScanTotalHours = 0;

            var cfg = GetActiveBossConfig(nowHours);
            if (cfg == null) return false;

            bossKey = cfg.bossKey;
            questId = cfg.questId;
            return true;
        }

        public bool TryGetBossHuntStatus(out string bossKey, out string questId, out double hoursUntilRotation)
        {
            bossKey = null;
            questId = null;
            hoursUntilRotation = 0;

            if (sapi == null) return false;

            double nowHours = sapi.World.Calendar.TotalHours;
            var cfg = GetActiveBossConfig(nowHours);
            if (cfg == null) return false;

            bossKey = cfg.bossKey;
            questId = cfg.questId;
            hoursUntilRotation = state?.nextBossRotationTotalHours > nowHours
                ? state.nextBossRotationTotalHours - nowHours
                : 0;
            return true;
        }

        private BossHuntConfig GetActiveBossConfig(double nowHours)
        {
            if (configs == null || configs.Count == 0) return null;

            if (state == null) state = new BossHuntWorldState();
            if (state.entries == null) state.entries = new List<BossHuntStateEntry>();

            if (string.IsNullOrWhiteSpace(state.activeBossKey) || nowHours >= state.nextBossRotationTotalHours)
            {
                string previousQuestId = null;
                BossHuntConfig previousCfg = null;
                if (!string.IsNullOrWhiteSpace(state.activeBossKey))
                {
                    previousCfg = FindConfig(state.activeBossKey);
                    previousQuestId = previousCfg?.questId;
                }

                // If the current boss is alive and was damaged recently, postpone rotation.
                // Otherwise the boss can disappear mid-fight or coexist with the next boss.
                if (previousCfg != null && nowHours >= state.nextBossRotationTotalHours)
                {
                    try
                    {
                        var bossEntity = FindBossEntityImmediate(previousCfg.bossKey);
                        if (bossEntity != null && bossEntity.Alive)
                        {
                            double lastDamage = bossEntity.WatchedAttributes.GetDouble(LastBossDamageTotalHoursKey, double.NaN);
                            double lockHours = previousCfg.GetNoRelocateAfterDamageHours();

                            bool shouldPostpone = !double.IsNaN(lastDamage) && lockHours > 0 && nowHours - lastDamage < lockHours;
                            if (shouldPostpone)
                            {
                                state.nextBossRotationTotalHours = nowHours + lockHours;
                                stateDirty = true;
                                return previousCfg;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                var ordered = new List<BossHuntConfig>();
                for (int i = 0; i < configs.Count; i++)
                {
                    var cfg = configs[i];
                    if (cfg == null || !cfg.IsValid()) continue;
                    ordered.Add(cfg);
                }

                if (ordered.Count == 0) return null;

                ordered.Sort((a, b) => string.Compare(a.bossKey, b.bossKey, StringComparison.OrdinalIgnoreCase));

                int nextIndex = 0;
                if (!string.IsNullOrWhiteSpace(state.activeBossKey))
                {
                    int currentIndex = ordered.FindIndex(c => string.Equals(c.bossKey, state.activeBossKey, StringComparison.OrdinalIgnoreCase));
                    if (currentIndex >= 0)
                    {
                        nextIndex = (currentIndex + 1) % ordered.Count;
                    }
                }

                var nextCfg = ordered[nextIndex];
                state.activeBossKey = nextCfg.bossKey;

                if (previousCfg != null
                    && !string.Equals(previousCfg.bossKey, nextCfg.bossKey, StringComparison.OrdinalIgnoreCase))
                {
                    TryDespawnBossOnRotation(previousCfg, nowHours);
                }

                double rotationDays = nextCfg.rotationDays > 0 ? nextCfg.rotationDays : 7;
                state.nextBossRotationTotalHours = nowHours + rotationDays * 24.0;
                stateDirty = true;

                if (!string.IsNullOrWhiteSpace(previousQuestId)
                    && !string.Equals(previousQuestId, nextCfg.questId, StringComparison.OrdinalIgnoreCase))
                {
                    var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                    if (questSystem != null)
                    {
                        foreach (var player in sapi.World.AllOnlinePlayers)
                        {
                            if (player is not IServerPlayer serverPlayer) continue;

                            QuestSystemAdminUtils.ForgetOutdatedQuestsForPlayer(questSystem, serverPlayer, sapi);
                        }
                    }
                }
            }

            return FindConfig(state.activeBossKey);
        }

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

            // If this boss has multi-phase rebirth behavior, we must ensure no leftover phase remains when the
            // active boss rotates away, otherwise two bosses can coexist.
            if (bossEntity.GetBehavior<EntityBehaviorBossRebirth>() != null)
            {
                try
                {
                    sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }
                catch
                {
                }

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

        private void OnTick(float dt)
        {
            if (sapi == null) return;
            if (configs == null || configs.Count == 0) return;

            double nowHours = sapi.World.Calendar.TotalHours;

            var activeCfg = GetActiveBossConfig(nowHours);
            if (activeCfg == null) return;

            var cfg = activeCfg;
            if (!cfg.IsValid()) return;

            var bossKey = cfg.bossKey;
            var st = GetOrCreateState(bossKey);
            NormalizeState(cfg, st);

            if (st.nextRelocateAtTotalHours <= 0)
            {
                st.nextRelocateAtTotalHours = nowHours + cfg.GetRelocateIntervalHours();
                stateDirty = true;
            }
            Entity bossEntity = FindBossEntity(cfg, nowHours);
            bool bossAlive = bossEntity != null && bossEntity.Alive;

            // Handle relocation
            if (nowHours >= st.nextRelocateAtTotalHours)
            {
                if (bossAlive && !IsSafeToRelocate(cfg, bossEntity, nowHours))
                {
                    // Postpone a bit
                    st.nextRelocateAtTotalHours = nowHours + 0.25;
                    stateDirty = true;
                }
                else
                {
                    int nextIndex = PickAnotherIndex(st.currentPointIndex, GetPointCount(cfg, st));
                    st.currentPointIndex = nextIndex;
                    st.nextRelocateAtTotalHours = nowHours + cfg.GetRelocateIntervalHours();
                    stateDirty = true;

                    // If boss is currently alive in the world and it's safe, remove it so it effectively "moves".
                    if (bossAlive)
                    {
                        try
                        {
                            sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        }
                        catch
                        {
                        }
                    }

                    bossEntity = null;
                    bossAlive = false;
                }
            }

            // Handle respawn timer
            if (st.deadUntilTotalHours > nowHours)
            {
                SaveStateIfDirty();
                return;
            }

            // Ensure boss is spawned when a player comes close to its current point.
            if (!bossAlive)
            {
                if (TryGetPoint(cfg, st, st.currentPointIndex, out var point, out int pointDim)
                    && AnyPlayerNear(point.X, point.Y, point.Z, pointDim, cfg.GetActivationRange()))
                {
                    DebugLog($"Spawn attempt: bossKey={bossKey} point={point.X:0.0},{point.Y:0.0},{point.Z:0.0} dim={pointDim} anchors={st.anchorPoints?.Count ?? 0} deadUntil={st.deadUntilTotalHours:0.00} now={nowHours:0.00}");
                    TrySpawnBoss(cfg, point, pointDim);
                }
            }

            SaveStateIfDirty();
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

            nextBossEntityScanTotalHours = nowHours + (1.0 / 60.0);

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

        private void TrySpawnBoss(BossHuntConfig cfg, Vec3d point, int dim)
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

                entity.ServerPos.SetPosWithDimension(new Vec3d(point.X, point.Y + dim * 32768.0, point.Z));
                entity.Pos.SetFrom(entity.ServerPos);

                sapi.World.SpawnEntity(entity);

                // Avoid repeated spawns: FindBossEntity() is throttled to scan loaded entities only once per minute.
                // Cache the newly spawned boss immediately.
                cachedBossEntity = entity;
                cachedBossKey = cfg.bossKey;
                nextBossEntityScanTotalHours = (sapi.World?.Calendar?.TotalHours ?? 0) + (1.0 / 60.0);
            }
            catch
            {
            }
        }

        private void DebugLog(string message, bool force = false)
        {
            if (!DebugBossHunt || sapi == null || string.IsNullOrWhiteSpace(message)) return;

            double nowHours = sapi.World?.Calendar?.TotalHours ?? 0;
            if (!force && nowHours < nextDebugLogTotalHours) return;

            nextDebugLogTotalHours = nowHours + 0.02;
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

        private int GetPointCount(BossHuntConfig cfg, BossHuntStateEntry st)
        {
            if (st?.anchorPoints != null && st.anchorPoints.Count > 0)
            {
                return st.anchorPoints.Count;
            }

            return cfg?.points?.Count ?? 0;
        }

        private bool TryGetPoint(BossHuntConfig cfg, BossHuntStateEntry st, int index, out Vec3d point, out int dim)
        {
            point = null;
            dim = 0;

            if (st?.anchorPoints != null && st.anchorPoints.Count > 0)
            {
                var ordered = GetOrderedAnchorsCached(st);
                if (index >= 0 && index < ordered.Count)
                {
                    var ap = ordered[index];
                    point = new Vec3d(ap.x, ap.y, ap.z);
                    dim = ap.dim;
                    return true;
                }
            }

            EnsureParsedPoints(cfg);
            if (cfg?._parsedPoints == null) return false;
            if (index < 0 || index >= cfg._parsedPoints.Count) return false;

            var p = cfg._parsedPoints[index];
            if (p == null || !p.ok) return false;

            dim = p.dim;
            point = new Vec3d(p.x, p.y, p.z);
            return true;
        }

        private List<BossHuntAnchorPoint> GetOrderedAnchorsCached(BossHuntStateEntry st)
        {
            if (st == null || string.IsNullOrWhiteSpace(st.bossKey) || st.anchorPoints == null || st.anchorPoints.Count == 0)
            {
                return new List<BossHuntAnchorPoint>();
            }

            if (!orderedAnchorsCache.TryGetValue(st.bossKey, out var ordered) || ordered == null || orderedAnchorsDirty.Contains(st.bossKey))
            {
                ordered = GetOrderedAnchors(st.anchorPoints);
                orderedAnchorsCache[st.bossKey] = ordered;
                orderedAnchorsDirty.Remove(st.bossKey);
            }

            return ordered;
        }

        private void EnsureParsedPoints(BossHuntConfig cfg)
        {
            if (cfg == null) return;
            if (cfg.points == null)
            {
                cfg._parsedPoints = null;
                return;
            }

            if (cfg._parsedPoints != null && cfg._parsedPoints.Count == cfg.points.Count)
            {
                return;
            }

            var list = new List<ParsedPoint>(cfg.points.Count);
            for (int i = 0; i < cfg.points.Count; i++)
            {
                var raw = cfg.points[i];
                if (string.IsNullOrWhiteSpace(raw))
                {
                    list.Add(new ParsedPoint { ok = false });
                    continue;
                }

                try
                {
                    var parts = raw.Split(',');
                    if (parts.Length < 3)
                    {
                        list.Add(new ParsedPoint { ok = false });
                        continue;
                    }

                    if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
                    {
                        list.Add(new ParsedPoint { ok = false });
                        continue;
                    }

                    if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                    {
                        list.Add(new ParsedPoint { ok = false });
                        continue;
                    }

                    if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
                    {
                        list.Add(new ParsedPoint { ok = false });
                        continue;
                    }

                    int dim = 0;
                    if (parts.Length >= 4)
                    {
                        int.TryParse(parts[3], out dim);
                    }

                    list.Add(new ParsedPoint { ok = true, x = x, y = y, z = z, dim = dim });
                }
                catch
                {
                    list.Add(new ParsedPoint { ok = false });
                }
            }

            cfg._parsedPoints = list;
        }

        private List<BossHuntAnchorPoint> GetOrderedAnchors(List<BossHuntAnchorPoint> anchors)
        {
            if (anchors == null || anchors.Count == 0) return new List<BossHuntAnchorPoint>();

            var list = new List<BossHuntAnchorPoint>();
            for (int i = 0; i < anchors.Count; i++)
            {
                var ap = anchors[i];
                if (ap == null) continue;
                list.Add(ap);
            }

            list.Sort((a, b) =>
            {
                int c = a.order.CompareTo(b.order);
                if (c != 0) return c;
                return string.Compare(a.anchorId, b.anchorId, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (sapi == null || entity == null) return;

            if (configs == null || configs.Count == 0) return;

            var qt = entity.GetBehavior<EntityBehaviorQuestTarget>();
            if (qt == null) return;

            var rebirth = entity.GetBehavior<EntityBehaviorBossRebirth>();
            if (rebirth != null && !rebirth.IsFinalStage)
            {
                // Prevent duplicate spawns during the short transition where the reborn entity is not yet present.
                for (int i = 0; i < configs.Count; i++)
                {
                    var cfg = configs[i];
                    if (cfg == null) continue;
                    if (!string.Equals(qt.TargetId, cfg.bossKey, StringComparison.OrdinalIgnoreCase)) continue;

                    var st = GetOrCreateState(cfg.bossKey);
                    double nowHours = sapi.World.Calendar.TotalHours;

                    // ~36 seconds grace (0.01h) - enough for rebirth spawnDelayMs, prevents re-spawn loop.
                    double graceUntil = nowHours + 0.01;
                    if (st.deadUntilTotalHours < graceUntil)
                    {
                        st.deadUntilTotalHours = graceUntil;
                        stateDirty = true;
                    }

                    cachedBossEntity = null;
                    nextBossEntityScanTotalHours = 0;
                    return;
                }

                return;
            }

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

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class BossHuntConfig
        {
            public string bossKey;
            public string questId;
            public double rotationDays;
            public List<string> points;

            public double relocateIntervalHours;
            public double respawnInGameHours;
            public double noRelocateAfterDamageMinutes;

            public float activationRange;
            public float playerLockRange;

            public string GetBossEntityCode()
            {
                return DeriveEntityCodeFromBossKey(bossKey);
            }

            private static string DeriveEntityCodeFromBossKey(string key)
            {
                if (string.IsNullOrWhiteSpace(key)) return null;

                var parts = key.Split(':');
                if (parts.Length == 2) return key;
                if (parts.Length < 2) return null;

                var domain = parts[0];
                var bossName = parts[parts.Length - 1];
                return domain + ":" + bossName;
            }

            public bool IsValid()
            {
                if (string.IsNullOrWhiteSpace(bossKey)) return false;
                if (string.IsNullOrWhiteSpace(GetBossEntityCode())) return false;
                if (points == null || points.Count < 1) return false;
                return true;
            }

            public double GetRelocateIntervalHours() => relocateIntervalHours > 0 ? relocateIntervalHours : 72;
            public double GetRespawnHours() => respawnInGameHours > 0 ? respawnInGameHours : 24;
            public double GetNoRelocateAfterDamageHours() => noRelocateAfterDamageMinutes > 0 ? (noRelocateAfterDamageMinutes / 60.0) : (10.0 / 60.0);
            public float GetActivationRange() => activationRange > 0 ? activationRange : 160f;
            public float GetPlayerLockRange() => playerLockRange > 0 ? playerLockRange : 40f;

            [ProtoIgnore]
            public List<ParsedPoint> _parsedPoints;
        }

        public class ParsedPoint
        {
            public bool ok;
            public double x;
            public double y;
            public double z;
            public int dim;
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class BossHuntWorldState
        {
            public List<BossHuntStateEntry> entries = new();
            public string activeBossKey;
            public double nextBossRotationTotalHours;
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class BossHuntStateEntry
        {
            public string bossKey;
            public int currentPointIndex;
            public double nextRelocateAtTotalHours;
            public double deadUntilTotalHours;

            public List<BossHuntAnchorPoint> anchorPoints;
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class BossHuntAnchorPoint
        {
            public string anchorId;
            public int order;
            public int x;
            public int y;
            public int z;
            public int dim;
        }
    }
}
