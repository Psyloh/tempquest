using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class QuestLandNotificationSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        private long listenerId;

        private List<QuestLandConfig> configs = new List<QuestLandConfig>();
        private QuestSystem questSystem;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            questSystem = api.ModLoader.GetModSystem<QuestSystem>();

            // Load all questland configs from all mods that provide config/questland.json
            // (domain is owned by each mod; this system stays domain-agnostic)
            configs.Clear();
            foreach (var mod in api.ModLoader.Mods)
            {
                var assets = sapi.Assets.GetMany<QuestLandConfig>(sapi.Logger, "config/questland", mod.Info.ModID);
                foreach (var asset in assets)
                {
                    if (asset.Value != null)
                    {
                        configs.Add(asset.Value);
                    }
                }
            }

            listenerId = sapi.Event.RegisterGameTickListener(OnTick, 1000);
        }

        private void OnTick(float dt)
        {
            var players = sapi?.World?.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            for (int i = 0; i < players.Length; i++)
            {
                if (!(players[i] is IServerPlayer sp)) continue;
                var wa = sp.Entity?.WatchedAttributes;
                if (wa == null) continue;

                string currentClaim = GetCurrentClaimName(sp);

                string key = "vsquest:questland:lastclaim";
                string lastClaim = wa.GetString(key, null);

                if (string.Equals(currentClaim, lastClaim, StringComparison.Ordinal))
                {
                    continue;
                }

                wa.SetString(key, currentClaim);
                wa.MarkPathDirty(key);

                // questland is a quest helper: only fire while player has an active quest matching allowed prefixes.
                if (!TryGetRelevantQuestAndConfig(sp, out string questId, out var config))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(currentClaim) && string.IsNullOrWhiteSpace(lastClaim))
                {
                    FireEnter(sp, questId, config, currentClaim);
                }
                else if (string.IsNullOrWhiteSpace(currentClaim) && !string.IsNullOrWhiteSpace(lastClaim))
                {
                    FireExit(sp, questId, config, lastClaim);
                }
                else if (!string.IsNullOrWhiteSpace(currentClaim) && !string.IsNullOrWhiteSpace(lastClaim))
                {
                    FireEnter(sp, questId, config, currentClaim);
                }
            }
        }

        private bool TryGetRelevantQuestAndConfig(IServerPlayer sp, out string questId, out QuestLandConfig config)
        {
            questId = null;
            config = null;
            if (questSystem == null) return false;
            if (configs == null || configs.Count == 0) return false;

            var active = questSystem.GetPlayerQuests(sp.PlayerUID);
            if (active == null || active.Count == 0) return false;

            // Prefer the config with the most specific (longest) prefix match.
            string bestQuestId = null;
            QuestLandConfig bestConfig = null;
            int bestPrefixLen = -1;

            foreach (var aq in active)
            {
                var qid = aq?.questId;
                if (string.IsNullOrWhiteSpace(qid)) continue;

                for (int ci = 0; ci < configs.Count; ci++)
                {
                    var cfg = configs[ci];
                    var prefixes = cfg?.allowedQuestPrefixes;
                    if (prefixes == null || prefixes.Length == 0) continue;

                    for (int pi = 0; pi < prefixes.Length; pi++)
                    {
                        var p = prefixes[pi];
                        if (string.IsNullOrWhiteSpace(p)) continue;
                        if (qid.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        {
                            int len = p.Length;
                            if (len > bestPrefixLen)
                            {
                                bestPrefixLen = len;
                                bestQuestId = qid;
                                bestConfig = cfg;
                            }
                        }
                    }
                }
            }

            if (bestConfig == null || string.IsNullOrWhiteSpace(bestQuestId)) return false;

            questId = bestQuestId;
            config = bestConfig;
            return true;
        }

        private void FireEnter(IServerPlayer sp, string questId, QuestLandConfig config, string claimName)
        {
            string msg = GetEnterMessage(config, claimName);
            FireActionTemplate(sp, questId, config?.enterAction, msg, claimName, null);
        }

        private void FireExit(IServerPlayer sp, string questId, QuestLandConfig config, string lastClaimName)
        {
            string msg = GetExitMessage(config, lastClaimName);
            FireActionTemplate(sp, questId, config?.exitAction, msg, null, lastClaimName);
        }

        private void FireActionTemplate(IServerPlayer sp, string questId, string actionTemplate, string message, string claimName, string lastClaimName)
        {
            if (sapi == null || sp == null) return;

            if (string.IsNullOrWhiteSpace(actionTemplate))
            {
                // Safe default: just notify
                actionTemplate = "notify '{message}'";
            }

            // Replace placeholders
            string safeMsg = (message ?? "").Replace("'", "\\'");
            string safeClaim = (claimName ?? "").Replace("'", "\\'");
            string safeLast = (lastClaimName ?? "").Replace("'", "\\'");

            string actionString = actionTemplate
                .Replace("{message}", safeMsg)
                .Replace("{claim}", safeClaim)
                .Replace("{lastclaim}", safeLast);

            var msg = new QuestAcceptedMessage { questGiverId = sp.Entity.EntityId, questId = questId };
            ActionStringExecutor.Execute(sapi, msg, sp, actionString);
        }

        private string GetEnterMessage(QuestLandConfig config, string claimName)
        {
            if (!string.IsNullOrWhiteSpace(claimName) && config?.enterMessages != null)
            {
                foreach (var kvp in config.enterMessages)
                {
                    if (string.Equals(kvp.Key, claimName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(kvp.Value)) return kvp.Value;
                    }
                }
            }

            return $"Вход: {claimName}";
        }

        private string GetExitMessage(QuestLandConfig config, string lastClaimName)
        {
            if (!string.IsNullOrWhiteSpace(config?.defaultExitMessage))
            {
                return config.defaultExitMessage;
            }

            return $"Выход: {lastClaimName}";
        }

        private string GetCurrentClaimName(IServerPlayer sp)
        {
            if (sp?.Entity?.Pos == null) return null;

            var claimsApi = sp.Entity.World?.Claims;
            if (claimsApi == null) return null;

            BlockPos pos = sp.Entity.Pos.AsBlockPos;
            var claims = claimsApi.Get(pos);
            if (claims == null || claims.Length == 0) return null;

            for (int i = 0; i < claims.Length; i++)
            {
                var desc = claims[i]?.Description;
                if (!string.IsNullOrWhiteSpace(desc)) return desc;

                var ownerName = claims[i]?.LastKnownOwnerName;
                if (!string.IsNullOrWhiteSpace(ownerName)) return ownerName;
            }

            return null;
        }

        private class QuestLandConfig
        {
            public string[] allowedQuestPrefixes { get; set; }
            public string enterAction { get; set; }
            public string exitAction { get; set; }
            public string defaultExitMessage { get; set; }
            public Dictionary<string, string> enterMessages { get; set; }
        }
    }
}
