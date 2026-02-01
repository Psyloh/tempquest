using System;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public partial class BossHuntSystem
    {
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

        public string GetActiveBossKey()
        {
            return state?.activeBossKey;
        }

        public string GetActiveBossQuestId()
        {
            if (!HasAnyRegisteredAnchors()) return null;

            var activeBossKey = GetActiveBossKey();
            if (!HasRegisteredAnchorsForBoss(activeBossKey)) return null;

            var cfg = FindConfig(activeBossKey);
            return cfg?.questId;
        }

        public bool ForceRotateToNext(out string bossKey, out string questId)
        {
            bossKey = null;
            questId = null;

            if (sapi == null) return false;
            if (!HasAnyRegisteredAnchors()) return false;

            double nowHours = sapi.World.Calendar.TotalHours;

            if (state == null) state = new BossHuntWorldState();
            if (state.entries == null) state.entries = new System.Collections.Generic.List<BossHuntStateEntry>();

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
            if (!HasAnyRegisteredAnchors()) return false;

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
    }
}
