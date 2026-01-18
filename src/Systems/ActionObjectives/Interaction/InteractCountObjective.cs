using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractCountObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (args == null || args.Length == 0) return false;

            var coordArgs = GetCoordArgs(args);
            if (coordArgs.Length == 0)
            {
                try
                {
                    byPlayer?.Entity?.Api?.Logger?.Warning($"[alegacyvsquest] interactcount objective has 0 coordinates (args='{string.Join("|", args ?? Array.Empty<string>())}'). Treating as completable to avoid stuck quest.");
                }
                catch
                {
                }

                // Fail-open: an invalid objective should not permanently block quest turn-in.
                return true;
            }

            return QuestInteractAtUtil.CountCompleted(byPlayer, coordArgs) >= coordArgs.Length;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            var coordArgs = GetCoordArgs(args);
            int need = coordArgs.Length;
            if (need == 0) return new List<int>(new int[] { 0, 1 });

            int have = QuestInteractAtUtil.CountCompleted(byPlayer, coordArgs);
            if (have > need) have = need;

            return new List<int>(new int[] { have, need });
        }

        private static string[] GetCoordArgs(string[] args)
        {
            if (args == null) return Array.Empty<string>();

            return args.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
        }

        
    }
}
