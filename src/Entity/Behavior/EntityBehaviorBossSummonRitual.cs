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
    public class EntityBehaviorBossSummonRitual : EntityBehavior
    {
        private const string SummonStageKey = "alegacyvsquest:bosssummonstage";

        private class SummonStage
        {
            public float whenHealthRelBelow;
            public string entityCode;
            public int minCount;
            public int maxCount;
            public int ritualMs;
            public float healPerSecond;
            public float spawnRange;
            public string animation;
            public string sound;
            public float soundRange;
            public int soundStartMs;
        }

        private ICoreServerAPI sapi;
        private readonly List<SummonStage> stages = new List<SummonStage>();
        private bool ritualActive;
        private long ritualEndsAtMs;
        private int activeStageIndex = -1;
        private float lockedYaw;
        private bool yawLocked;

        public EntityBehaviorBossSummonRitual(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosssummonritual";

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

                    var stage = new SummonStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        entityCode = stageObj["entityCode"].AsString(null),
                        minCount = stageObj["minCount"].AsInt(1),
                        maxCount = stageObj["maxCount"].AsInt(1),
                        ritualMs = stageObj["ritualMs"].AsInt(4000),
                        healPerSecond = stageObj["healPerSecond"].AsFloat(0f),
                        spawnRange = stageObj["spawnRange"].AsFloat(6f),
                        animation = stageObj["animation"].AsString(null),
                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(16f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0)
                    };

                    if (!string.IsNullOrWhiteSpace(stage.entityCode))
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
            if (stages.Count == 0) return;
            if (!entity.Alive)
            {
                StopRitual();
                return;
            }

            if (ritualActive)
            {
                ApplyRotationLock();
                if (stages.Count > activeStageIndex && activeStageIndex >= 0)
                {
                    HealDuringRitual(stages[activeStageIndex], dt);
                }

                if (sapi.World.ElapsedMilliseconds >= ritualEndsAtMs)
                {
                    StopRitual();
                }

                return;
            }

            if (!TryGetHealthFraction(out float frac)) return;

            int stageProgress = entity.WatchedAttributes?.GetInt(SummonStageKey, 0) ?? 0;
            for (int i = stageProgress; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (frac <= stage.whenHealthRelBelow)
                {
                    entity.WatchedAttributes.SetInt(SummonStageKey, i + 1);
                    entity.WatchedAttributes.MarkPathDirty(SummonStageKey);
                    StartRitual(stage, i);
                    break;
                }
            }
        }

        private void StartRitual(SummonStage stage, int index)
        {
            ritualActive = true;
            activeStageIndex = index;
            ritualEndsAtMs = sapi.World.ElapsedMilliseconds + Math.Max(500, stage.ritualMs);

            StopAiAndFreeze();
            ApplyRotationLock();
            SpawnMinions(stage);
            TryPlaySound(stage);
            TryPlayAnimation(stage.animation);
        }

        private void StopRitual()
        {
            ritualActive = false;
            yawLocked = false;

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                var animation = stages[activeStageIndex].animation;
                if (!string.IsNullOrWhiteSpace(animation))
                {
                    try
                    {
                        entity?.AnimManager?.StopAnimation(animation);
                    }
                    catch
                    {
                    }
                }
            }

            activeStageIndex = -1;
        }

        private void StopAiAndFreeze()
        {
            var taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
            taskAi?.TaskManager?.StopTasks();

            entity.ServerPos.Motion.Set(0, 0, 0);
            if (entity is EntityAgent agent)
            {
                agent.Controls.StopAllMovement();
            }
        }

        private void ApplyRotationLock()
        {
            if (!yawLocked)
            {
                lockedYaw = entity.ServerPos.Yaw;
                yawLocked = true;
            }

            entity.ServerPos.Yaw = lockedYaw;
            entity.Pos.Yaw = lockedYaw;
            if (entity is EntityAgent agent)
            {
                agent.BodyYaw = lockedYaw;
            }
        }

        private void HealDuringRitual(SummonStage stage, float dt)
        {
            if (stage.healPerSecond <= 0f) return;

            var wa = entity?.WatchedAttributes;
            if (wa == null) return;

            var healthTree = wa.GetTreeAttribute("health");
            if (healthTree == null) return;

            float maxHealth = healthTree.GetFloat("maxhealth", 0f);
            if (maxHealth <= 0f)
            {
                maxHealth = healthTree.GetFloat("basemaxhealth", 0f);
            }

            float curHealth = healthTree.GetFloat("currenthealth", 0f);
            if (maxHealth <= 0f || curHealth <= 0f) return;

            float newHealth = Math.Min(maxHealth, curHealth + stage.healPerSecond * dt);
            healthTree.SetFloat("currenthealth", newHealth);
            wa.MarkPathDirty("health");
        }

        private void SpawnMinions(SummonStage stage)
        {
            if (sapi == null) return;
            if (string.IsNullOrWhiteSpace(stage.entityCode)) return;

            int min = Math.Max(1, stage.minCount);
            int max = Math.Max(min, stage.maxCount);
            int count = min;
            if (max > min)
            {
                count = min + sapi.World.Rand.Next(max - min + 1);
            }

            var type = sapi.World.GetEntityType(new AssetLocation(stage.entityCode));
            if (type == null) return;

            int dim = entity.ServerPos.Dimension;
            for (int i = 0; i < count; i++)
            {
                Entity spawned = sapi.World.ClassRegistry.CreateEntity(type);
                if (spawned == null) continue;

                double angle = sapi.World.Rand.NextDouble() * Math.PI * 2.0;
                double dist = stage.spawnRange * (0.5 + sapi.World.Rand.NextDouble() * 0.5);
                double x = entity.ServerPos.X + Math.Cos(angle) * dist;
                double z = entity.ServerPos.Z + Math.Sin(angle) * dist;
                double y = entity.ServerPos.Y;

                spawned.ServerPos.SetPosWithDimension(new Vec3d(x, y + dim * 32768.0, z));
                spawned.Pos.SetFrom(spawned.ServerPos);
                spawned.ServerPos.Yaw = (float)(sapi.World.Rand.NextDouble() * Math.PI * 2.0);

                sapi.World.SpawnEntity(spawned);
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

        private void TryPlaySound(SummonStage stage)
        {
            if (string.IsNullOrWhiteSpace(stage.sound)) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, entity.Code?.Domain ?? "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            if (stage.soundStartMs > 0)
            {
                sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange);
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
                    sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange);
                }
                catch
                {
                }
            }
        }

        private bool TryGetHealthFraction(out float fraction)
        {
            fraction = 1f;
            var wa = entity?.WatchedAttributes;
            if (wa == null) return false;

            var healthTree = wa.GetTreeAttribute("health");
            if (healthTree == null) return false;

            float maxHealth = healthTree.GetFloat("maxhealth", 0f);
            if (maxHealth <= 0f)
            {
                maxHealth = healthTree.GetFloat("basemaxhealth", 0f);
            }

            float curHealth = healthTree.GetFloat("currenthealth", 0f);
            if (maxHealth <= 0f || curHealth <= 0f) return false;

            fraction = curHealth / maxHealth;
            return true;
        }
    }
}
