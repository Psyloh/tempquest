using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public class EntityBehaviorBossMusicUrlController : EntityBehavior
    {
        private ICoreClientAPI capi;

        private float range;
        private int combatTimeoutMs;
        private bool requireRecentDamage;

        private float startAtSeconds;

        private class MusicPhase
        {
            public float whenHealthRelBelow;
            public string url;
            public float startAtSeconds;
        }

        private readonly System.Collections.Generic.List<MusicPhase> phases = new System.Collections.Generic.List<MusicPhase>();

        private string musicKey;
        private string musicUrl;

        private bool lastShouldPlay;

        private long outOfRangeSinceMs;
        private const int OutOfRangeStopDelayMs = 800;

        public EntityBehaviorBossMusicUrlController(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossmusicurl";

        public override void Initialize(EntityProperties properties, Vintagestory.API.Datastructures.JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            capi = entity?.Api as ICoreClientAPI;

            range = attributes?["range"].AsFloat(60f) ?? 60f;
            combatTimeoutMs = attributes?["combatTimeoutMs"].AsInt(12000) ?? 12000;
            requireRecentDamage = attributes?["requireRecentDamage"].AsBool(true) ?? true;

            startAtSeconds = attributes?["startAtSeconds"].AsFloat(0f) ?? 0f;
            if (startAtSeconds < 0f) startAtSeconds = 0f;

            musicKey = attributes?["musicKey"].AsString(null);
            musicUrl = attributes?["musicUrl"].AsString(null);

            phases.Clear();
            try
            {
                if (attributes?["phases"]?.Exists == true)
                {
                    foreach (var ph in attributes["phases"].AsArray())
                    {
                        if (ph == null || !ph.Exists) continue;
                        phases.Add(new MusicPhase
                        {
                            whenHealthRelBelow = ph["whenHealthRelBelow"].AsFloat(1f),
                            url = ph["musicUrl"].AsString(null),
                            startAtSeconds = ph["startAtSeconds"].AsFloat(0f)
                        });
                    }
                }
            }
            catch
            {
                phases.Clear();
            }

            if (range < 1f) range = 1f;
            if (combatTimeoutMs < 0) combatTimeoutMs = 0;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (capi == null || entity == null || !entity.Alive)
            {
                ApplyShouldPlay(false);
                return;
            }

            var player = capi.World?.Player?.Entity;
            if (player == null || !player.Alive)
            {
                ApplyShouldPlay(false);
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
                long now = 0;
                try
                {
                    now = capi.World.ElapsedMilliseconds;
                }
                catch
                {
                    now = 0;
                }

                if (outOfRangeSinceMs <= 0) outOfRangeSinceMs = now;

                if (now <= 0 || now - outOfRangeSinceMs >= OutOfRangeStopDelayMs)
                {
                    ApplyShouldPlay(false);
                }
                return;
            }

            outOfRangeSinceMs = 0;

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

            ApplyShouldPlay(inCombat);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ApplyShouldPlay(false);
            base.OnEntityDespawn(despawn);
        }

        private void ApplyShouldPlay(bool shouldPlay)
        {
            try
            {
                var sys = capi?.ModLoader?.GetModSystem<BossMusicUrlSystem>();
                if (sys == null) return;

                if (shouldPlay)
                {
                    // Important: phases can depend on current HP, so we must recompute even while already playing.
                    ResolveDesiredMusic(sys, out var url, out var offset);
                    sys.Start(musicKey, url, offset);
                }
                else
                {
                    sys.Stop();
                }
            }
            catch
            {
            }

            lastShouldPlay = shouldPlay;
        }

        private void ResolveDesiredMusic(BossMusicUrlSystem sys, out string url, out float offset)
        {
            url = musicUrl;
            offset = startAtSeconds;

            if (phases.Count > 0)
            {
                float frac = 1f;
                try
                {
                    var ht = entity?.WatchedAttributes?.GetTreeAttribute("health");
                    if (ht != null)
                    {
                        float cur = ht.GetFloat("currenthealth", 0f);
                        float max = ht.GetFloat("maxhealth", 0f);
                        if (max > 0f) frac = cur / max;
                    }
                }
                catch
                {
                    frac = 1f;
                }

                MusicPhase best = null;
                float bestThr = 999f;
                for (int i = 0; i < phases.Count; i++)
                {
                    var ph = phases[i];
                    if (ph == null) continue;
                    if (frac <= ph.whenHealthRelBelow && ph.whenHealthRelBelow < bestThr)
                    {
                        best = ph;
                        bestThr = ph.whenHealthRelBelow;
                    }
                }

                if (best != null)
                {
                    if (!string.IsNullOrWhiteSpace(best.url)) url = best.url;
                    offset = best.startAtSeconds;
                }
            }

            if (offset < 0f) offset = 0f;

            if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(musicKey))
            {
                url = sys.ResolveUrl(musicKey);
            }
        }
    }
}
