using Vintagestory.API.Server;

namespace VsQuest
{
    public class AddPlayerIntAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;

            if (args.Length < 2)
            {
                sapi.Logger.Error($"[vsquest] 'addplayerint' action requires at least 2 arguments (key, delta) but got {args.Length} in quest '{message?.questId}'.");
                return;
            }

            string key = args[0];
            if (string.IsNullOrWhiteSpace(key)) return;

            if (!int.TryParse(args[1], out int delta)) delta = 0;

            int? max = null;
            if (args.Length >= 3 && int.TryParse(args[2], out int parsedMax))
            {
                max = parsedMax;
            }

            string onceKey = null;
            if (args.Length >= 4 && !string.IsNullOrWhiteSpace(args[3]))
            {
                onceKey = args[3];
            }

            var wa = byPlayer.Entity.WatchedAttributes;

            if (!string.IsNullOrWhiteSpace(onceKey) && wa.GetBool(onceKey, false))
            {
                return;
            }

            int cur = wa.GetInt(key, 0);
            int next = cur + delta;

            if (max.HasValue && next > max.Value) next = max.Value;
            if (next < 0) next = 0;

            wa.SetInt(key, next);
            wa.MarkPathDirty(key);

            if (!string.IsNullOrWhiteSpace(onceKey))
            {
                wa.SetBool(onceKey, true);
                wa.MarkPathDirty(onceKey);
            }
        }
    }
}
