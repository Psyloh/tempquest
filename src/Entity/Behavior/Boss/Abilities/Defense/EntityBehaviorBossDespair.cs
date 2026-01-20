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
        private const string LastDespairStartMsKey = "alegacyvsquest:bossdespair:lastStartMs";

        private ICoreServerAPI sapi;
        private float durationMultiplier = 3f;
        private float cooldownSeconds;
        private bool despairActive;
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

            cooldownSeconds = attributes["cooldownSeconds"].AsFloat(0f);
            if (cooldownSeconds < 0f) cooldownSeconds = 0f;
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
                BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
                return;
            }

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stage = entity.WatchedAttributes.GetInt(DespairStageKey, 0);
            if (stage == 0 && frac <= 0.75f)
            {
                if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastDespairStartMsKey, cooldownSeconds)) return;
                TriggerDespair(1);
            }
            else if (stage == 1 && frac <= 0.25f)
            {
                if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastDespairStartMsKey, cooldownSeconds)) return;
                TriggerDespair(2);
            }
        }

        public override string PropertyName() => "bossdespair";

        private void TriggerDespair(int nextStage)
        {
            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastDespairStartMsKey);

            entity.WatchedAttributes.SetInt(DespairStageKey, nextStage);
            entity.WatchedAttributes.MarkPathDirty(DespairStageKey);

            despairActive = true;
            BossBehaviorUtils.StopAiAndFreeze(entity);
            BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
            entity.AnimManager.StartAnimation("despair");

            BossBehaviorUtils.UnregisterGameTickListenerSafe(sapi, ref soundLoopListenerId);
            BossBehaviorUtils.UnregisterGameTickListenerSafe(sapi, ref healListenerId);

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

            BossBehaviorUtils.UnregisterGameTickListenerSafe(sapi, ref soundLoopListenerId);
            BossBehaviorUtils.UnregisterGameTickListenerSafe(sapi, ref healListenerId);
        }
    }
}
