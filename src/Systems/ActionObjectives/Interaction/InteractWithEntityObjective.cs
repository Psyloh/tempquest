using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractWithEntityObjective : ActionObjectiveBase
    {
        public static string CountKey(string questId, long entityId) => $"vsquest:interactentity:{questId}:{entityId}:count";

        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out long entityId, out int need)) return false;
            int have = GetHave(byPlayer, questId, entityId);
            return have >= need;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out long entityId, out int need)) return new List<int>(new int[] { 0, 0 });

            int have = GetHave(byPlayer, questId, entityId);
            if (have > need) have = need;
            if (need < 0) need = 0;

            return new List<int>(new int[] { have, need });
        }

        private static int GetHave(IPlayer byPlayer, string questId, long entityId)
        {
            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return 0;

            return wa.GetInt(CountKey(questId, entityId), 0);
        }

        private static bool TryParseArgs(string[] args, out string questId, out long entityId, out int need)
        {
            questId = null;
            entityId = 0;
            need = 0;

            if (args == null || args.Length < 3) return false;

            questId = args[0];
            if (string.IsNullOrWhiteSpace(questId)) return false;

            if (!long.TryParse(args[1], out entityId)) return false;
            if (!int.TryParse(args[2], out need)) need = 0;
            if (need < 0) need = 0;

            return true;
        }
    }
}
