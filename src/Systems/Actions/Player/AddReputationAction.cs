using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class AddReputationAction : IQuestAction
    {
        private const string OnceKeysListKey = "alegacyvsquest:rep:oncekeys";

        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;

            if (args.Length < 3)
            {
                sapi.Logger.Error($"[vsquest] 'addreputation' action requires at least 3 arguments (scope, id, delta) but got {args.Length} in quest '{message?.questId}'.");
                return;
            }

            string scopeRaw = args[0]?.ToLowerInvariant();
            string id = args[1];
            if (string.IsNullOrWhiteSpace(scopeRaw) || string.IsNullOrWhiteSpace(id)) return;

            var repSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
            if (repSystem == null)
            {
                return;
            }

            if (!repSystem.TryParseScope(scopeRaw, out var scope))
            {
                sapi.Logger.Error($"[vsquest] 'addreputation' action scope must be 'npc' or 'faction', got '{scopeRaw}' in quest '{message?.questId}'.");
                return;
            }

            if (!int.TryParse(args[2], out int delta)) delta = 0;

            int? max = null;
            if (args.Length >= 4 && int.TryParse(args[3], out int parsedMax))
            {
                max = parsedMax;
            }

            string onceKey = null;
            if (args.Length >= 5 && !string.IsNullOrWhiteSpace(args[4]))
            {
                onceKey = args[4];
            }

            var wa = byPlayer.Entity.WatchedAttributes;
            if (!string.IsNullOrWhiteSpace(onceKey) && wa.GetBool(onceKey, false))
            {
                return;
            }

            int current = repSystem.GetReputationValue(byPlayer as IPlayer, scope, id);
            int next = current + delta;

            if (max.HasValue && next > max.Value) next = max.Value;

            repSystem.ApplyReputationChange(sapi, byPlayer, scope, id, next, false);

            if (!string.IsNullOrWhiteSpace(onceKey))
            {
                try
                {
                    var list = wa.GetStringArray(OnceKeysListKey, null);
                    if (list == null)
                    {
                        wa.SetStringArray(OnceKeysListKey, new[] { onceKey });
                        wa.MarkPathDirty(OnceKeysListKey);
                    }
                    else
                    {
                        bool exists = false;
                        for (int i = 0; i < list.Length; i++)
                        {
                            if (list[i] == onceKey)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            var nextList = new string[list.Length + 1];
                            for (int i = 0; i < list.Length; i++) nextList[i] = list[i];
                            nextList[list.Length] = onceKey;
                            wa.SetStringArray(OnceKeysListKey, nextList);
                            wa.MarkPathDirty(OnceKeysListKey);
                        }
                    }
                }
                catch
                {
                }

                wa.SetBool(onceKey, true);
                wa.MarkPathDirty(onceKey);
            }
        }
    }
}
