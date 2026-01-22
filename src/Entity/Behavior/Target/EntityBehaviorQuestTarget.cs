using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorQuestTarget : EntityBehavior
    {
        protected const string AnchorKeyPrefix = "alegacyvsquest:spawner:";
        protected const string ReturningToAnchorKey = "alegacyvsquest:spawner:returningToAnchor";

        protected const double LeashNoDamageGraceHours = 2.0 / 60.0;
        protected const float BossRegenHpPerSecond = 3f;
        protected const float DefaultBossOutOfCombatLeashRange = 10f;
        protected const float LeashReturnStopDistance = 5f;
        public const string LeashRangeKey = "alegacyvsquest:spawner:leashRange";
        public const string OutOfCombatLeashRangeKey = "alegacyvsquest:spawner:outOfCombatLeashRange";
        protected string id;
        protected float? maxHealthOverride;
        protected float leashRange;
        protected float bossOutOfCombatLeashRange;
        protected float returnMoveSpeed;
        protected int leashCheckMs;
        protected long lastLeashCheckMs;
        protected ICoreServerAPI sapi;

        public string TargetId => id;

        public EntityBehaviorQuestTarget(Entity entity) : base(entity)
        {
        }

        protected virtual string JsonIdKey => "targetId";
        protected virtual string WatchedIdKey => "alegacyvsquest:killaction:targetid";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            sapi = entity?.Api as ICoreServerAPI;
            id = attributes[JsonIdKey].AsString(null);
            maxHealthOverride = attributes.KeyExists("maxHealth") ? attributes["maxHealth"].AsFloat() : (float?)null;

            leashRange = attributes["leashRange"].AsFloat(0);
            returnMoveSpeed = attributes["returnMoveSpeed"].AsFloat(0.04f);
            leashCheckMs = attributes["leashCheckMs"].AsInt(1000);
            bossOutOfCombatLeashRange = DefaultBossOutOfCombatLeashRange;

            if (entity?.WatchedAttributes != null)
            {
                var wa = entity.WatchedAttributes;
                string waId = wa.GetString(WatchedIdKey, null);
                if (!string.IsNullOrWhiteSpace(waId)) id = waId;

                float waLeashRange = wa.GetFloat(LeashRangeKey, float.NaN);
                if (!float.IsNaN(waLeashRange) && waLeashRange > 0f)
                {
                    leashRange = waLeashRange;
                }

                float waOutOfCombatLeashRange = wa.GetFloat(OutOfCombatLeashRangeKey, float.NaN);
                if (!float.IsNaN(waOutOfCombatLeashRange))
                {
                    bossOutOfCombatLeashRange = waOutOfCombatLeashRange;
                }
            }

            if (entity?.Api?.Side == EnumAppSide.Server)
            {
                TryApplyHealthOverride();
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if (entity?.Api?.Side != EnumAppSide.Server) return;
            if (entity is not EntityAgent agent) return;
            if (!agent.Alive) return;
            if (leashRange <= 0) return;

            double nowHours = agent.World.Calendar?.TotalHours ?? 0;
            bool noDamageGraceActive = false;
            if (nowHours > 0)
            {
                try
                {
                    double lastDamageHours = agent.WatchedAttributes.GetDouble(BossHuntSystem.LastBossDamageTotalHoursKey, double.NaN);
                    if (!double.IsNaN(lastDamageHours) && nowHours - lastDamageHours < LeashNoDamageGraceHours)
                    {
                        noDamageGraceActive = true;
                    }
                }
                catch
                {
                }
            }

            // Slow out-of-combat regen for bosses.
            // Only when no damage was received recently (same window as leash grace), and only if bosscombatmarker exists.
            if (!noDamageGraceActive && BossRegenHpPerSecond > 0f && agent.HasBehavior<EntityBehaviorBossCombatMarker>())
            {
                try
                {
                    var wa = agent.WatchedAttributes;
                    var healthTree = wa?.GetTreeAttribute("health");
                    if (healthTree != null)
                    {
                        float maxHealth = healthTree.GetFloat("maxhealth", 0f);
                        if (maxHealth <= 0f) maxHealth = healthTree.GetFloat("basemaxhealth", 0f);

                        float curHealth = healthTree.GetFloat("currenthealth", 0f);
                        if (maxHealth > 0f && curHealth > 0f && curHealth < maxHealth)
                        {
                            float newHealth = Math.Min(maxHealth, curHealth + BossRegenHpPerSecond * deltaTime);
                            healthTree.SetFloat("currenthealth", newHealth);
                            wa.MarkPathDirty("health");

                            if (newHealth >= maxHealth)
                            {
                                ResetBossCombatProgress(wa);
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            long nowMs = agent.World.ElapsedMilliseconds;
            if (nowMs - lastLeashCheckMs < leashCheckMs) return;
            lastLeashCheckMs = nowMs;

            if (!TryGetAnchor(out var anchor)) return;

            float effectiveLeashRange = leashRange;
            if (!noDamageGraceActive && agent.HasBehavior<EntityBehaviorBossCombatMarker>() && bossOutOfCombatLeashRange > 0f)
            {
                effectiveLeashRange = Math.Min(effectiveLeashRange, bossOutOfCombatLeashRange);
            }

            double dx = agent.ServerPos.X - anchor.X;
            double dz = agent.ServerPos.Z - anchor.Z;
            bool inRange = (dx * dx + dz * dz) <= effectiveLeashRange * effectiveLeashRange;
            if (inRange)
            {
                try
                {
                    var wa = agent.WatchedAttributes;
                    if (wa != null && wa.GetBool(ReturningToAnchorKey, false))
                    {
                        var taskAiLocal = agent.GetBehavior<EntityBehaviorTaskAI>();
                        try
                        {
                            taskAiLocal?.PathTraverser?.Stop();
                        }
                        catch
                        {
                        }

                        try
                        {
                            taskAiLocal?.TaskManager?.StopTasks();
                        }
                        catch
                        {
                        }

                        agent.ServerPos?.Motion?.Set(0, 0, 0);
                        agent.Controls.StopAllMovement();

                        wa.SetBool(ReturningToAnchorKey, false);
                        wa.MarkPathDirty(ReturningToAnchorKey);
                    }
                }
                catch
                {
                }

                return;
            }

            // If boss took damage recently, do not start/refresh a leash return yet.
            // Note: we still allow the inRange/stop logic above to run so a previously started return can be cleared.
            if (noDamageGraceActive) return;

            var taskAi = agent.GetBehavior<EntityBehaviorTaskAI>();
            if (taskAi?.PathTraverser != null && taskAi.PathTraverser.Ready)
            {
                try
                {
                    var wa = agent.WatchedAttributes;
                    if (wa != null)
                    {
                        wa.SetBool(ReturningToAnchorKey, true);
                        wa.MarkPathDirty(ReturningToAnchorKey);
                    }
                }
                catch
                {
                }

                float stopDistance = Math.Min(LeashReturnStopDistance, effectiveLeashRange);
                taskAi.PathTraverser.NavigateTo_Async(anchor, returnMoveSpeed, stopDistance, null, null, null, 1000, 1, null);
            }
        }

        protected bool TryGetAnchor(out Vec3d anchor)
        {
            anchor = null;
            var wa = entity?.WatchedAttributes;
            if (wa == null) return false;

            int dim = wa.GetInt(AnchorKeyPrefix + "dim", int.MinValue);
            if (dim == int.MinValue) return false;
            if (entity.Pos.Dimension != dim) return false;

            int x = wa.GetInt(AnchorKeyPrefix + "x", int.MinValue);
            int y = wa.GetInt(AnchorKeyPrefix + "y", int.MinValue);
            int z = wa.GetInt(AnchorKeyPrefix + "z", int.MinValue);
            if (x == int.MinValue || y == int.MinValue || z == int.MinValue) return false;

            anchor = new Vec3d(x + 0.5, y + 1, z + 0.5);
            return true;
        }

        protected void TryApplyHealthOverride()
        {
            if (!maxHealthOverride.HasValue) return;
            var healthBh = entity.GetBehavior<EntityBehaviorHealth>();
            if (healthBh == null) return;

            healthBh.BaseMaxHealth = maxHealthOverride.Value;
            healthBh.UpdateMaxHealth();
            healthBh.Health = healthBh.MaxHealth;
        }

        protected static void SetSpawnerAnchorStatic(Entity entity, BlockPos pos)
        {
            if (entity?.WatchedAttributes == null || pos == null) return;

            entity.WatchedAttributes.SetInt(AnchorKeyPrefix + "x", pos.X);
            entity.WatchedAttributes.SetInt(AnchorKeyPrefix + "y", pos.Y);
            entity.WatchedAttributes.SetInt(AnchorKeyPrefix + "z", pos.Z);
            entity.WatchedAttributes.SetInt(AnchorKeyPrefix + "dim", pos.dimension);

            entity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "x");
            entity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "y");
            entity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "z");
            entity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "dim");
        }

        public override string PropertyName() => "questtarget";

        public static void SetSpawnerAnchor(Entity entity, BlockPos pos)
        {
            SetSpawnerAnchorStatic(entity, pos);
        }

        protected static void ResetBossCombatProgress(ITreeAttribute wa)
        {
            if (wa == null) return;

            try
            {
                wa[EntityBehaviorBossCombatMarker.BossCombatAttackersKey] = new StringArrayAttribute(Array.Empty<string>());
                if (wa is SyncedTreeAttribute synced)
                {
                    synced.MarkPathDirty(EntityBehaviorBossCombatMarker.BossCombatAttackersKey);
                }

                wa[EntityBehaviorBossCombatMarker.BossCombatDamageByPlayerKey] = new TreeAttribute();
                if (wa is SyncedTreeAttribute synced2)
                {
                    synced2.MarkPathDirty(EntityBehaviorBossCombatMarker.BossCombatDamageByPlayerKey);
                }
            }
            catch
            {
            }

            try
            {
                wa[EntityBehaviorBossHuntCombatMarker.BossHuntAttackersKey] = new StringArrayAttribute(Array.Empty<string>());
                if (wa is SyncedTreeAttribute synced)
                {
                    synced.MarkPathDirty(EntityBehaviorBossHuntCombatMarker.BossHuntAttackersKey);
                }

                wa[EntityBehaviorBossHuntCombatMarker.BossHuntDamageByPlayerKey] = new TreeAttribute();
                if (wa is SyncedTreeAttribute synced2)
                {
                    synced2.MarkPathDirty(EntityBehaviorBossHuntCombatMarker.BossHuntDamageByPlayerKey);
                }
            }
            catch
            {
            }
        }
    }
}
