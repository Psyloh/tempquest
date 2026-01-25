using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossOxygenDrainAura : EntityBehavior
    {
        private const string LastTickMsKey = "alegacyvsquest:bossoxygendrainaura:lastTickMs";

        private class AuraStage
        {
            public float whenHealthRelBelow;
            public float range;
            public float oxygenDrainPerSecond;
            public int intervalMs;
            public float minOxygenRel;
        }

        private ICoreServerAPI sapi;
        private readonly List<AuraStage> stages = new List<AuraStage>();

        public EntityBehaviorBossOxygenDrainAura(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossoxygendrainaura";

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

                    var stage = new AuraStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        range = stageObj["range"].AsFloat(18f),
                        oxygenDrainPerSecond = stageObj["oxygenDrainPerSecond"].AsFloat(0f),
                        intervalMs = stageObj["intervalMs"].AsInt(500),
                        minOxygenRel = stageObj["minOxygenRel"].AsFloat(0f)
                    };

                    if (stage.range <= 0f) stage.range = 18f;
                    if (stage.intervalMs < 100) stage.intervalMs = 100;
                    if (stage.minOxygenRel < 0f) stage.minOxygenRel = 0f;
                    if (stage.minOxygenRel > 1f) stage.minOxygenRel = 1f;

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
            if (!entity.Alive) return;

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageIndex = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                if (frac <= stages[i].whenHealthRelBelow)
                {
                    stageIndex = i;
                }
            }

            if (stageIndex < 0) return;

            var stage = stages[stageIndex];
            long nowMs = sapi.World.ElapsedMilliseconds;
            long lastTickMs = entity.WatchedAttributes.GetLong(LastTickMsKey, 0);
            if (nowMs - lastTickMs < stage.intervalMs) return;

            entity.WatchedAttributes.SetLong(LastTickMsKey, nowMs);
            entity.WatchedAttributes.MarkPathDirty(LastTickMsKey);

            if (stage.oxygenDrainPerSecond <= 0f) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            double range = stage.range > 0 ? stage.range : 18f;
            double rangeSq = range * range;
            var selfPos = entity.ServerPos;
            float drain = stage.oxygenDrainPerSecond * (stage.intervalMs / 1000f);

            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i] as IServerPlayer;
                var playerEntity = player?.Entity;
                if (playerEntity == null) continue;
                if (playerEntity.ServerPos.Dimension != selfPos.Dimension) continue;

                double dx = playerEntity.ServerPos.X - selfPos.X;
                double dy = playerEntity.ServerPos.Y - selfPos.Y;
                double dz = playerEntity.ServerPos.Z - selfPos.Z;
                double distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > rangeSq) continue;

                var breathe = playerEntity.GetBehavior<EntityBehaviorBreathe>();
                if (breathe == null) continue;

                float maxOxygen = breathe.MaxOxygen;
                float minOxygen = maxOxygen * stage.minOxygenRel;
                float current = breathe.Oxygen;
                float newOxygen = GameMath.Clamp(current - drain, minOxygen, maxOxygen);

                if (newOxygen < current)
                {
                    breathe.Oxygen = newOxygen;
                }
            }
        }
    }
}
