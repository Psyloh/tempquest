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
    public class EntityBehaviorBossRandomLightning : EntityBehavior
    {
        private const string LastStrikeStartKeyPrefix = "alegacyvsquest:bossrandomlightning:lastStartMs:";

        private class LightningStage
        {
            public float whenHealthRelBelow;
            public int minCount;
            public int maxCount;
            public float cooldownSeconds;
            public float minRadius;
            public float maxRadius;
            public string warningSound;
            public float warningSoundRange;
            public int warningDelayMs;
            public float chance;
        }

        private readonly List<LightningStage> stages = new List<LightningStage>();
        private ICoreServerAPI sapi;
        private WeatherSystemBase weatherSystem;

        public EntityBehaviorBossRandomLightning(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossrandomlightning";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;

            if (sapi != null)
            {
                try
                {
                    weatherSystem = sapi.ModLoader?.GetModSystem<WeatherSystemBase>();
                }
                catch
                {
                    weatherSystem = null;
                }
            }

            stages.Clear();
            try
            {
                foreach (var stageObj in attributes["stages"].AsArray())
                {
                    if (stageObj == null || !stageObj.Exists) continue;

                    var stage = new LightningStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        minCount = stageObj["minCount"].AsInt(1),
                        maxCount = stageObj["maxCount"].AsInt(1),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(8f),
                        minRadius = stageObj["minRadius"].AsFloat(0f),
                        maxRadius = stageObj["maxRadius"].AsFloat(14f),
                        warningSound = stageObj["warningSound"].AsString("weather/lightning-verynear"),
                        warningSoundRange = stageObj["warningSoundRange"].AsFloat(32f),
                        warningDelayMs = stageObj["warningDelayMs"].AsInt(3000),
                        chance = stageObj["chance"].AsFloat(1f)
                    };

                    if (stage.maxRadius <= 0f)
                    {
                        stage.maxRadius = 14f;
                    }

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

            int stageIndex = GetActiveStageIndex(frac);
            if (stageIndex < 0) return;

            var stage = stages[stageIndex];
            if (stage == null) return;

            if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastStrikeStartKeyPrefix + stageIndex, stage.cooldownSeconds)) return;

            if (stage.chance < 1f && sapi.World.Rand.NextDouble() > stage.chance) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastStrikeStartKeyPrefix + stageIndex);

            int minCount = Math.Max(1, stage.minCount);
            int maxCount = Math.Max(minCount, stage.maxCount);
            int count = minCount;
            if (maxCount > minCount)
            {
                count = minCount + sapi.World.Rand.Next(maxCount - minCount + 1);
            }

            for (int i = 0; i < count; i++)
            {
                Vec3d strikePos = GetRandomStrikePosition(stage);
                TriggerStrike(stage, strikePos);
            }
        }

        private int GetActiveStageIndex(float healthFraction)
        {
            int activeIndex = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                if (healthFraction <= stages[i].whenHealthRelBelow)
                {
                    activeIndex = i;
                }
            }

            return activeIndex;
        }

        private Vec3d GetRandomStrikePosition(LightningStage stage)
        {
            double minRadius = Math.Max(0, stage.minRadius);
            double maxRadius = Math.Max(minRadius + 0.1, stage.maxRadius);

            double angle = sapi.World.Rand.NextDouble() * Math.PI * 2.0;
            double dist = minRadius + sapi.World.Rand.NextDouble() * (maxRadius - minRadius);

            double x = entity.ServerPos.X + Math.Cos(angle) * dist;
            double z = entity.ServerPos.Z + Math.Sin(angle) * dist;
            int dim = entity.ServerPos.Dimension;
            double y = entity.ServerPos.Y + dim * 32768.0;

            return new Vec3d(x, y, z);
        }

        private void TriggerStrike(LightningStage stage, Vec3d strikePos)
        {
            if (sapi == null || stage == null || strikePos == null) return;

            TryPlayWarningSound(stage, strikePos);

            int delayMs = Math.Max(0, stage.warningDelayMs);
            sapi.Event.RegisterCallback(_ =>
            {
                try
                {
                    weatherSystem?.SpawnLightningFlash(strikePos);
                }
                catch
                {
                }
            }, delayMs);
        }

        private void TryPlayWarningSound(LightningStage stage, Vec3d strikePos)
        {
            if (string.IsNullOrWhiteSpace(stage.warningSound)) return;

            try
            {
                AssetLocation soundLoc = AssetLocation.Create(stage.warningSound, "game").WithPathPrefixOnce("sounds/");
                sapi.World.PlaySoundAt(soundLoc, strikePos.X, strikePos.Y, strikePos.Z, null, randomizePitch: true, stage.warningSoundRange);
            }
            catch
            {
            }
        }
    }
}
