using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace VsQuest
{
    public partial class BossHuntSystem : ModSystem
    {
        public const string LastBossDamageTotalHoursKey = "alegacyvsquest:bosshunt:lastBossDamageTotalHours";

        private const string SaveKey = "alegacyvsquest:bosshunt:state";
        private bool debugBossHunt;

        private double softResetIdleHours = 1.0;
        private double softResetAntiSpamHours = 0.25;
        private double relocatePostponeHours = 0.25;

        private readonly HashSet<string> skipBossKeys = new(StringComparer.OrdinalIgnoreCase);

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

        private double bossEntityScanIntervalHours = 1.0 / 60.0;
        private double debugLogThrottleHours = 0.02;


        private void ApplyCoreConfig()
        {
            if (sapi == null) return;

            AlegacyVsQuestConfig.BossHuntCoreConfig cfg = null;
            try
            {
                var qs = sapi.ModLoader.GetModSystem<QuestSystem>();
                cfg = qs?.CoreConfig?.BossHunt;
            }
            catch
            {
                cfg = null;
            }

            if (cfg == null) return;

            debugBossHunt = cfg.Debug;

            softResetIdleHours = cfg.SoftResetIdleHours > 0 ? cfg.SoftResetIdleHours : 1.0;
            softResetAntiSpamHours = cfg.SoftResetAntiSpamHours >= 0 ? cfg.SoftResetAntiSpamHours : 0.25;
            relocatePostponeHours = cfg.RelocatePostponeHours >= 0 ? cfg.RelocatePostponeHours : 0.25;

            bossEntityScanIntervalHours = cfg.BossEntityScanIntervalHours > 0 ? cfg.BossEntityScanIntervalHours : (1.0 / 60.0);
            debugLogThrottleHours = cfg.DebugLogThrottleHours > 0 ? cfg.DebugLogThrottleHours : 0.02;

            skipBossKeys.Clear();
            if (cfg.SkipBossKeys != null)
            {
                for (int i = 0; i < cfg.SkipBossKeys.Count; i++)
                {
                    var key = cfg.SkipBossKeys[i];
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    skipBossKeys.Add(key);
                }
            }
        }

        private bool IsBossKeySkipped(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return false;
            return skipBossKeys.Contains(bossKey);
        }


        private void OnTick(float dt)
        {
            if (sapi == null) return;
            if (configs == null || configs.Count == 0) return;

            if (!HasAnyRegisteredAnchors()) return;

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

            // Soft reset: if boss is alive but has not been damaged for 1 in-game hour,
            // despawn it so it can respawn cleanly on the same anchor (resetting phases/state).
            if (bossAlive)
            {
                try
                {
                    double lastDamage = bossEntity.WatchedAttributes.GetDouble(LastBossDamageTotalHoursKey, double.NaN);

                    bool hasEverBeenDamaged = !double.IsNaN(lastDamage) && lastDamage > 0;
                    bool idleLongEnough = hasEverBeenDamaged && (nowHours - lastDamage >= softResetIdleHours);

                    bool antiSpamOk = st.lastSoftResetAtTotalHours <= 0 || (nowHours - st.lastSoftResetAtTotalHours >= softResetAntiSpamHours);

                    if (idleLongEnough && antiSpamOk)
                    {
                        st.lastSoftResetAtTotalHours = nowHours;
                        st.deadUntilTotalHours = 0;
                        stateDirty = true;

                        try
                        {
                            sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        }
                        catch
                        {
                        }

                        bossEntity = null;
                        bossAlive = false;

                        // If there is any player nearby, spawn immediately to avoid a visible "missing boss".
                        if (TryGetPoint(cfg, st, st.currentPointIndex, out var point, out int pointDim, out var anchorPoint)
                            && AnyPlayerNear(point.X, point.Y, point.Z, pointDim, cfg.GetActivationRange()))
                        {
                            TrySpawnBoss(cfg, point, pointDim, anchorPoint);
                        }
                    }
                }
                catch
                {
                }
            }

            // Handle relocation
            if (nowHours >= st.nextRelocateAtTotalHours)
            {
                if (bossAlive && !IsSafeToRelocate(cfg, bossEntity, nowHours))
                {
                    // Postpone a bit
                    st.nextRelocateAtTotalHours = nowHours + relocatePostponeHours;
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
                if (TryGetPoint(cfg, st, st.currentPointIndex, out var point, out int pointDim, out var anchorPoint)
                    && AnyPlayerNear(point.X, point.Y, point.Z, pointDim, cfg.GetActivationRange()))
                {
                    DebugLog($"Spawn attempt: bossKey={bossKey} point={point.X:0.0},{point.Y:0.0},{point.Z:0.0} dim={pointDim} anchors={st.anchorPoints?.Count ?? 0} deadUntil={st.deadUntilTotalHours:0.00} now={nowHours:0.00}");
                    TrySpawnBoss(cfg, point, pointDim, anchorPoint);
                }
            }

            SaveStateIfDirty();
        }

    }
}
