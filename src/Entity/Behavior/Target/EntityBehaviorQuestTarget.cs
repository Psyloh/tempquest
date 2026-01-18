using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorQuestTarget : EntityBehavior
    {
        protected const string AnchorKeyPrefix = "alegacyvsquest:spawner:";

        protected const double LeashNoDamageGraceHours = 2.0 / 60.0;
        protected const float BossRegenHpPerSecond = 3f;

        protected string id;
        protected float? maxHealthOverride;
        protected float leashRange;
        protected float returnMoveSpeed;
        protected int leashCheckMs;
        protected long lastLeashCheckMs;

        public string TargetId => id;

        public EntityBehaviorQuestTarget(Entity entity) : base(entity)
        {
        }

        protected virtual string JsonIdKey => "targetId";
        protected virtual string WatchedIdKey => "alegacyvsquest:killaction:targetid";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            id = attributes[JsonIdKey].AsString(null);
            maxHealthOverride = attributes.KeyExists("maxHealth") ? attributes["maxHealth"].AsFloat() : (float?)null;

            leashRange = attributes["leashRange"].AsFloat(0);
            returnMoveSpeed = attributes["returnMoveSpeed"].AsFloat(0.04f);
            leashCheckMs = attributes["leashCheckMs"].AsInt(1000);

            if (entity?.WatchedAttributes != null)
            {
                string waId = entity.WatchedAttributes.GetString(WatchedIdKey, null);
                if (!string.IsNullOrWhiteSpace(waId)) id = waId;
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
                        }
                    }
                }
                catch
                {
                }
            }

            // If boss took damage recently, do not leash yet.
            if (noDamageGraceActive) return;

            long nowMs = agent.World.ElapsedMilliseconds;
            if (nowMs - lastLeashCheckMs < leashCheckMs) return;
            lastLeashCheckMs = nowMs;

            if (!TryGetAnchor(out var anchor)) return;

            double dx = agent.ServerPos.X - anchor.X;
            double dy = agent.ServerPos.Y - anchor.Y;
            double dz = agent.ServerPos.Z - anchor.Z;
            if ((dx * dx + dy * dy + dz * dz) <= leashRange * leashRange) return;

            var taskAi = agent.GetBehavior<EntityBehaviorTaskAI>();
            if (taskAi?.PathTraverser != null && taskAi.PathTraverser.Ready)
            {
                taskAi.PathTraverser.NavigateTo_Async(anchor, returnMoveSpeed, 0.5f, null, null, null, 1000, 1, null);
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

            anchor = new Vec3d(x + 0.5, y, z + 0.5);
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
    }
}
