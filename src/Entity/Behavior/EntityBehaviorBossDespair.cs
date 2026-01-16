using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossDespair : EntityBehavior
    {
        private const string DespairStageKey = "alegacyvsquest:bossdespairstage";

        private ICoreServerAPI sapi;
        private float durationMultiplier = 3f;
        private bool despairActive;
        private long soundCallbackId;
        private long soundLoopListenerId;
        private float lockedYaw;
        private bool yawLocked;
        private long healListenerId;

        public EntityBehaviorBossDespair(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity.Api as ICoreServerAPI;
            durationMultiplier = attributes["despairDurationMultiplier"].AsFloat(3f);
            if (durationMultiplier <= 0f) durationMultiplier = 3f;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (sapi == null || entity == null) return;
            if (!entity.Alive)
            {
                StopDespairEffects();
                return;
            }
            if (entity.AnimManager == null) return;

            if (despairActive)
            {
                ApplyRotationLock();
                return;
            }

            if (!TryGetHealthFraction(out float frac)) return;

            int stage = entity.WatchedAttributes.GetInt(DespairStageKey, 0);
            if (stage == 0 && frac <= 0.75f)
            {
                TriggerDespair(1);
            }
            else if (stage == 1 && frac <= 0.25f)
            {
                TriggerDespair(2);
            }
        }

        public override string PropertyName() => "bossdespair";

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

        private void TriggerDespair(int nextStage)
        {
            entity.WatchedAttributes.SetInt(DespairStageKey, nextStage);
            entity.WatchedAttributes.MarkPathDirty(DespairStageKey);

            despairActive = true;
            StopAiAndFreeze();
            ApplyRotationLock();
            entity.AnimManager.StartAnimation("despair");

            if (soundCallbackId != 0)
            {
                sapi.Event.UnregisterCallback(soundCallbackId);
                soundCallbackId = 0;
            }

            if (soundLoopListenerId != 0)
            {
                sapi.Event.UnregisterGameTickListener(soundLoopListenerId);
                soundLoopListenerId = 0;
            }

            if (healListenerId != 0)
            {
                sapi.Event.UnregisterGameTickListener(healListenerId);
                healListenerId = 0;
            }

            soundLoopListenerId = sapi.Event.RegisterGameTickListener(_ =>
            {
                try
                {
                    sapi.World.PlaySoundAt(new AssetLocation("sounds/creature/shiver/aggro"), entity, null, randomizePitch: true, 16f);
                }
                catch { }
            }, 1000);

            healListenerId = sapi.Event.RegisterGameTickListener(_ =>
            {
                try
                {
                    HealDuringDespair();
                }
                catch { }
            }, 500);

            int baseSeconds = (int)(sapi.World.Rand.NextDouble() * 3.0 + 3.0);
            int durationMs = (int)(baseSeconds * 1000 * durationMultiplier);

            sapi.Event.RegisterCallback(_ =>
            {
                try
                {
                    entity.AnimManager.StopAnimation("despair");
                }
                catch { }

                sapi.Event.RegisterCallback(__ =>
                {
                    try
                    {
                        StopDespairEffects();
                        Unfreeze();
                    }
                    catch { }
                }, 200);

            }, durationMs);
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

        private void Unfreeze()
        {
            entity.ServerPos.Motion.Set(0, 0, 0);
        }

        private void HealDuringDespair()
        {
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

            float newHealth = Math.Min(maxHealth, curHealth + 5.0f);
            healthTree.SetFloat("currenthealth", newHealth);
            wa.MarkPathDirty("health");
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            StopDespairEffects();
        }

        private void StopDespairEffects()
        {
            despairActive = false;
            yawLocked = false;

            if (soundCallbackId != 0)
            {
                sapi.Event.UnregisterCallback(soundCallbackId);
                soundCallbackId = 0;
            }

            if (soundLoopListenerId != 0)
            {
                sapi.Event.UnregisterGameTickListener(soundLoopListenerId);
                soundLoopListenerId = 0;
            }

            if (healListenerId != 0)
            {
                sapi.Event.UnregisterGameTickListener(healListenerId);
                healListenerId = 0;
            }
        }
    }
}
