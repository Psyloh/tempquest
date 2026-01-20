using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossIntoxicationAura : EntityBehavior
    {
        private const string LastTickMsKey = "alegacyvsquest:bossintoxaura:lastTickMs";

        private class AuraStage
        {
            public float whenHealthRelBelow;
            public float range;
            public float intoxication;
            public int intervalMs;
        }

        private ICoreServerAPI sapi;
        private readonly List<AuraStage> stages = new List<AuraStage>();
        private float maxRange;

        public EntityBehaviorBossIntoxicationAura(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossintoxaura";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;

            stages.Clear();
            maxRange = 0f;
            try
            {
                foreach (var stageObj in attributes["stages"].AsArray())
                {
                    if (stageObj == null || !stageObj.Exists) continue;

                    var stage = new AuraStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        range = stageObj["range"].AsFloat(24f),
                        intoxication = stageObj["intoxication"].AsFloat(0f),
                        intervalMs = stageObj["intervalMs"].AsInt(500)
                    };

                    if (stage.range <= 0f) stage.range = 24f;
                    if (stage.intervalMs < 100) stage.intervalMs = 100;

                    stages.Add(stage);
                    if (stage.range > maxRange) maxRange = stage.range;
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

            float targetIntox = GameMath.Clamp(stage.intoxication, 0f, 1.1f);
            if (targetIntox <= 0f) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            double range = stage.range > 0 ? stage.range : 24f;
            double rangeSq = range * range;
            var selfPos = entity.ServerPos;

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

                float current = playerEntity.WatchedAttributes.GetFloat("intoxication", 0f);
                if (current >= targetIntox) continue;

                playerEntity.WatchedAttributes.SetFloat("intoxication", targetIntox);
                playerEntity.WatchedAttributes.MarkPathDirty("intoxication");
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            ClearIntoxication();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ClearIntoxication();
            base.OnEntityDespawn(despawn);
        }

        private void ClearIntoxication()
        {
            if (sapi == null || entity == null) return;
            if (maxRange <= 0f) maxRange = 24f;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            double range = maxRange * 1.1f;
            double rangeSq = range * range;
            var selfPos = entity.ServerPos;

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

                playerEntity.WatchedAttributes.SetFloat("intoxication", 0f);
                playerEntity.WatchedAttributes.MarkPathDirty("intoxication");
            }
        }
    }
}
