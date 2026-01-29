using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public class EntityBehaviorBossMusicUrlController : EntityBehavior
    {
        private const string QuestlandMusicAttributeKey = "alegacyvsquest:questlandmusic";
        private ICoreClientAPI capi;

        private float range;
        private float startRange;
        private float keepRange;
        private int combatTimeoutMs;
        private bool usePhases;
        private bool phaseSwitching;

        private float fadeOutSeconds;

        private float startAtSeconds;

        private class MusicPhase
        {
            public float whenHealthRelBelow;
            public string url;
            public float startAtSeconds;
            public float startAtRel;
        }

        private readonly System.Collections.Generic.List<MusicPhase> phases = new System.Collections.Generic.List<MusicPhase>();

        private string musicKey;
        private string musicUrl;

        private bool lastShouldPlay;

        private bool wasInCombat;
        private long combatEndGraceUntilMs;
        private const int CombatEndGraceMs = 2000;

        private long outOfRangeSinceMs;
        private const int OutOfRangeStopDelayMs = 800;

        private long lastResolveMs;
        private const int ResolveThrottleMs = 400;
        private string lastResolvedUrl;
        private float lastResolvedOffset;

        public EntityBehaviorBossMusicUrlController(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossmusicurl";

        public override void Initialize(EntityProperties properties, Vintagestory.API.Datastructures.JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            capi = entity?.Api as ICoreClientAPI;

            range = attributes?["range"].AsFloat(60f) ?? 60f;
            startRange = attributes?["startRange"].AsFloat(0f) ?? 0f;
            keepRange = attributes?["keepRange"].AsFloat(45f) ?? 45f;
            combatTimeoutMs = attributes?["combatTimeoutMs"].AsInt(20000) ?? 20000;
            usePhases = attributes?["usePhases"].AsBool(true) ?? true;
            phaseSwitching = attributes?["phaseSwitching"].AsBool(true) ?? true;

            fadeOutSeconds = attributes?["fadeOutSeconds"].AsFloat(4f) ?? 4f;
            if (fadeOutSeconds < 0f) fadeOutSeconds = 0f;

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
                            startAtSeconds = ph["startAtSeconds"].AsFloat(0f),
                            startAtRel = ph["startAtRel"].AsFloat(-1f)
                        });
                    }
                }
            }
            catch
            {
                phases.Clear();
            }

            if (range < 1f) range = 1f;
            if (startRange < 0f) startRange = 0f;
            if (startRange > 0f && startRange < 1f) startRange = 1f;
            if (keepRange < 1f) keepRange = 1f;
            if (combatTimeoutMs < 0) combatTimeoutMs = 0;

            if (keepRange > range) keepRange = range;

            wasInCombat = false;
            combatEndGraceUntilMs = 0;
        }

        private const float DeathFadeOutSeconds = 2f;

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (capi == null || entity == null || !entity.Alive)
            {
                ApplyShouldPlay(false, fadeOutSeconds > 0f ? fadeOutSeconds : DeathFadeOutSeconds);
                return;
            }

            var player = capi.World?.Player?.Entity;
            if (player == null || !player.Alive)
            {
                ApplyShouldPlay(false, fadeOutSeconds);
                return;
            }

            bool inRange;
            double distanceToPlayer = 0;
            try
            {
                distanceToPlayer = player.Pos.DistanceTo(entity.Pos);
                inRange = (float)distanceToPlayer <= range;
            }
            catch
            {
                distanceToPlayer = 0;
                inRange = false;
            }

            if (!inRange)
            {
                bool inQuestland = false;
                try
                {
                    inQuestland = player?.WatchedAttributes?.HasAttribute(QuestlandMusicAttributeKey) == true;
                }
                catch
                {
                    inQuestland = false;
                }

                if (inQuestland)
                {
                    return;
                }

                long now = Environment.TickCount64;
                if (outOfRangeSinceMs <= 0) outOfRangeSinceMs = now;

                if (now - outOfRangeSinceMs >= OutOfRangeStopDelayMs)
                {
                    ApplyShouldPlay(false, fadeOutSeconds);
                }

                wasInCombat = false;
                combatEndGraceUntilMs = 0;
                return;
            }

            outOfRangeSinceMs = 0;

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

            bool hasTrigger = lastDamageMs > 0;
            bool inStartRange = startRange > 0f ? distanceToPlayer <= startRange : inRange;
            bool inKeepRange = distanceToPlayer <= keepRange;

            bool aiHasTarget = false;
            try
            {
                aiHasTarget = entity?.WatchedAttributes?.GetBool(BossBehaviorUtils.HasTargetKey, false) ?? false;
            }
            catch
            {
                aiHasTarget = false;
            }

            bool recentDamage = false;
            if (combatTimeoutMs <= 0)
            {
                recentDamage = hasTrigger;
            }
            else if (hasTrigger)
            {
                try
                {
                    long now = capi.World.ElapsedMilliseconds;
                    long dtMs = now - lastDamageMs;
                    recentDamage = dtMs >= 0 && dtMs <= combatTimeoutMs;
                }
                catch
                {
                    recentDamage = false;
                }
            }

            bool combatCore = aiHasTarget || recentDamage;

            // If combat has already started, keep playing as long as player stays in keepRange.
            // This prevents brief target/LoS drops from killing the music.
            if (wasInCombat && inKeepRange)
            {
                combatCore = true;
            }

            // Start only when entering combat, optionally gated by startRange. After combat has started,
            // keep playing while combat remains active (has target OR recent damage).
            bool combatNow = combatCore && (wasInCombat || inStartRange);

            long tickNow = Environment.TickCount64;
            if (wasInCombat && !combatNow)
            {
                if (combatEndGraceUntilMs <= 0)
                {
                    combatEndGraceUntilMs = tickNow + CombatEndGraceMs;
                }

                if (tickNow < combatEndGraceUntilMs)
                {
                    combatNow = true;
                }
            }
            else if (combatNow)
            {
                combatEndGraceUntilMs = 0;
            }

            ApplyShouldPlay(combatNow);
            wasInCombat = combatNow;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ApplyShouldPlay(false, fadeOutSeconds);
            base.OnEntityDespawn(despawn);
        }

        private void ApplyShouldPlay(bool shouldPlay, float fadeOutSeconds = -1f)
        {
            // We intentionally allow resolving desired music while playing (for HP phases),
            // but we must not spam Stop/Start each tick.

            try
            {
                var sys = capi?.ModLoader?.GetModSystem<BossMusicUrlSystem>();
                if (sys == null) return;

                if (shouldPlay)
                {
                    bool playbackStopped = sys.IsActive && !sys.IsPlaybackRunning;

                    long now = Environment.TickCount64;
                    bool allowResolve = !lastShouldPlay || phaseSwitching || playbackStopped;
                    if (allowResolve && (!lastShouldPlay || now - lastResolveMs >= ResolveThrottleMs))
                    {
                        lastResolveMs = now;

                        // Important: phases can depend on current HP, so we recompute while playing.
                        ResolveDesiredMusic(sys, out var url, out var offset);

                        bool changed = !string.Equals(lastResolvedUrl ?? "", url ?? "", StringComparison.OrdinalIgnoreCase)
                                       || Math.Abs(lastResolvedOffset - offset) > 0.01f;

                        if (!lastShouldPlay || changed || playbackStopped)
                        {
                            lastResolvedUrl = url;
                            lastResolvedOffset = offset;
                            sys.Start(musicKey, url, offset);
                        }
                    }
                }
                else
                {
                    if (lastShouldPlay)
                    {
                        lastResolvedUrl = null;
                        lastResolvedOffset = 0f;
                        sys.Stop(fadeOutSeconds);
                    }
                }
            }
            catch
            {
            }

            lastShouldPlay = shouldPlay;
        }

        private void ResolveDesiredMusic(BossMusicUrlSystem sys, out string url, out float offset)
        {
            bool preferKey = !string.IsNullOrWhiteSpace(musicKey);
            url = preferKey ? null : musicUrl;
            offset = startAtSeconds;

            if (usePhases && phases.Count > 0)
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
                    if (!preferKey && !string.IsNullOrWhiteSpace(best.url)) url = best.url;
                    offset = best.startAtSeconds;

                    if (best.startAtRel >= 0f)
                    {
                        float rel = best.startAtRel;
                        if (rel < 0f) rel = 0f;
                        if (rel > 1f) rel = 1f;

                        if (!string.IsNullOrWhiteSpace(url) && sys != null && sys.TryGetDurationSeconds(url, out var seconds) && seconds > 0.01f)
                        {
                            offset = seconds * rel;
                        }
                    }
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
