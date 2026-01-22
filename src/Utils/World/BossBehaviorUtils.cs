using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public static class BossBehaviorUtils
    {
        public const string HasTargetKey = "alegacyvsquest:boss:hasTarget";

        public static void SetWatchedBoolDirty(Entity entity, string key, bool value)
        {
            try
            {
                var wa = entity?.WatchedAttributes;
                if (wa == null) return;

                bool prev = wa.GetBool(key, false);
                if (prev == value) return;

                wa.SetBool(key, value);
                wa.MarkPathDirty(key);
            }
            catch
            {
            }
        }

        public static bool TryGetHealthFraction(Entity entity, out float fraction)
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

        public static bool TryGetHealth(Entity entity, out ITreeAttribute healthTree, out float currentHealth, out float maxHealth)
        {
            healthTree = null;
            currentHealth = 0f;
            maxHealth = 0f;

            var wa = entity?.WatchedAttributes;
            if (wa == null) return false;

            healthTree = wa.GetTreeAttribute("health");
            if (healthTree == null) return false;

            maxHealth = healthTree.GetFloat("maxhealth", 0f);
            if (maxHealth <= 0f)
            {
                maxHealth = healthTree.GetFloat("basemaxhealth", 0f);
            }

            currentHealth = healthTree.GetFloat("currenthealth", 0f);
            return maxHealth > 0f && currentHealth > 0f;
        }

        public static void StopAiAndFreeze(Entity entity)
        {
            var taskAi = entity?.GetBehavior<EntityBehaviorTaskAI>();
            taskAi?.TaskManager?.StopTasks();

            entity?.ServerPos?.Motion?.Set(0, 0, 0);
            if (entity is EntityAgent agent)
            {
                agent.Controls.StopAllMovement();
            }
        }

        public static void ApplyRotationLock(Entity entity, ref bool yawLocked, ref float lockedYaw)
        {
            if (entity == null) return;

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

        public static void UnregisterCallbackSafe(ICoreServerAPI sapi, ref long callbackId)
        {
            if (sapi != null && callbackId != 0)
            {
                sapi.Event.UnregisterCallback(callbackId);
                callbackId = 0;
            }
        }

        public static void UnregisterGameTickListenerSafe(ICoreServerAPI sapi, ref long listenerId)
        {
            if (sapi != null && listenerId != 0)
            {
                sapi.Event.UnregisterGameTickListener(listenerId);
                listenerId = 0;
            }
        }

        public static bool IsCooldownReady(ICoreServerAPI sapi, Entity entity, string lastStartKey, float cooldownSeconds)
        {
            if (sapi == null || entity == null) return false;
            if (string.IsNullOrWhiteSpace(lastStartKey)) return true;

            if (cooldownSeconds <= 0f) return true;

            long cooldownMs;
            try
            {
                cooldownMs = (long)Math.Round(cooldownSeconds * 1000.0);
            }
            catch
            {
                cooldownMs = 0;
            }

            if (cooldownMs <= 0) return true;

            long nowMs = sapi.World.ElapsedMilliseconds;
            long lastStartMs = 0;
            try
            {
                lastStartMs = entity.WatchedAttributes?.GetLong(lastStartKey, 0) ?? 0;
            }
            catch
            {
                lastStartMs = 0;
            }

            return nowMs - lastStartMs >= cooldownMs;
        }

        public static void MarkCooldownStart(ICoreServerAPI sapi, Entity entity, string lastStartKey)
        {
            if (sapi == null || entity == null) return;
            if (string.IsNullOrWhiteSpace(lastStartKey)) return;

            try
            {
                entity.WatchedAttributes.SetLong(lastStartKey, sapi.World.ElapsedMilliseconds);
                entity.WatchedAttributes.MarkPathDirty(lastStartKey);
            }
            catch
            {
            }
        }

        public sealed class LoopSound : IDisposable
        {
            private long listenerId;
            private ICoreServerAPI sapi;
            private Entity entity;
            private AssetLocation soundLoc;
            private float range;

            public void Start(ICoreServerAPI sapi, Entity entity, string sound, float range, int intervalMs)
            {
                Stop();

                if (sapi == null || entity == null || string.IsNullOrWhiteSpace(sound)) return;

                this.sapi = sapi;
                this.entity = entity;
                this.range = range;

                int interval = Math.Max(250, intervalMs);
                soundLoc = AssetLocation.Create(sound, "game").WithPathPrefixOnce("sounds/");
                if (soundLoc == null) return;

                listenerId = sapi.Event.RegisterGameTickListener(_ =>
                {
                    try
                    {
                        sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, range);
                    }
                    catch
                    {
                    }
                }, interval);
            }

            public void Stop()
            {
                if (sapi != null && listenerId != 0)
                {
                    sapi.Event.UnregisterGameTickListener(listenerId);
                    listenerId = 0;
                }

                sapi = null;
                entity = null;
                soundLoc = null;
            }

            public void Dispose()
            {
                Stop();
            }
        }
    }
}
