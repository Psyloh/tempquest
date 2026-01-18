using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossMusicController : EntityBehavior
    {
        private ICoreClientAPI capi;

        private float range;
        private int combatTimeoutMs;
        private bool requireRecentDamage;

        public EntityBehaviorBossMusicController(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosssong";

        public override void Initialize(EntityProperties properties, Vintagestory.API.Datastructures.JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            capi = entity?.Api as ICoreClientAPI;

            range = attributes?["range"].AsFloat(60f) ?? 60f;
            combatTimeoutMs = attributes?["combatTimeoutMs"].AsInt(12000) ?? 12000;
            requireRecentDamage = attributes?["requireRecentDamage"].AsBool(true) ?? true;

            if (range < 1f) range = 1f;
            if (combatTimeoutMs < 0) combatTimeoutMs = 0;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (capi == null || entity == null || !entity.Alive)
            {
                SetBossMusic(false);
                return;
            }

            var player = capi.World?.Player?.Entity;
            if (player == null)
            {
                SetBossMusic(false);
                return;
            }

            bool inRange;
            try
            {
                inRange = player.Pos.DistanceTo(entity.Pos) <= range;
            }
            catch
            {
                inRange = false;
            }

            if (!inRange)
            {
                SetBossMusic(false);
                return;
            }

            bool inCombat = true;
            if (requireRecentDamage)
            {
                long lastDamageMs = 0;
                try
                {
                    var wa = entity.WatchedAttributes;
                    if (wa != null)
                    {
                        lastDamageMs = wa.GetLong(EntityBehaviorBossHuntCombatMarker.BossHuntLastDamageMsKey, 0);
                        if (lastDamageMs <= 0)
                        {
                            lastDamageMs = wa.GetLong(EntityBehaviorBossCombatMarker.BossCombatLastDamageMsKey, 0);
                        }
                    }
                }
                catch
                {
                    lastDamageMs = 0;
                }

                if (lastDamageMs <= 0)
                {
                    inCombat = false;
                }
                else
                {
                    long now = capi.World.ElapsedMilliseconds;
                    long dtMs = now - lastDamageMs;
                    inCombat = dtMs >= 0 && dtMs <= combatTimeoutMs;
                }
            }

            SetBossMusic(inCombat);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            SetBossMusic(false);
            base.OnEntityDespawn(despawn);
        }

        private void SetBossMusic(bool shouldPlay)
        {
            try
            {
                var bossBh = entity.GetBehavior<EntityBehaviorBoss>();
                if (bossBh != null)
                {
                    bossBh.ShouldPlayTrack = shouldPlay;
                }
            }
            catch
            {
            }
        }
    }
}
