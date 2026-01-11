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
            if (coordArgs.Length == 0) return false;

            return CountCompleted(byPlayer, coordArgs) >= coordArgs.Length;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            var coordArgs = GetCoordArgs(args);
            int need = coordArgs.Length;
            if (need == 0) return new List<int>(new int[] { 0, 0 });

            int have = CountCompleted(byPlayer, coordArgs);
            if (have > need) have = need;

            return new List<int>(new int[] { have, need });
        }

        private static string[] GetCoordArgs(string[] args)
        {
            if (args == null) return Array.Empty<string>();

            return args.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
        }

        private static int CountCompleted(IPlayer byPlayer, string[] coordArgs)
        {
            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return 0;

            // Legacy storage (comma-separated list)
            string completedInteractions = wa.GetString("completedInteractions", "");
            var completed = completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            int count = 0;
            foreach (var coordString in coordArgs)
            {
                if (string.IsNullOrWhiteSpace(coordString)) continue;

                var coords = coordString.Split(',');
                if (coords.Length != 3) continue;

                if (!int.TryParse(coords[0], out int x) ||
                    !int.TryParse(coords[1], out int y) ||
                    !int.TryParse(coords[2], out int z))
                {
                    continue;
                }

                string interactionKey = $"interactat_{x}_{y}_{z}";
                if (completed.Contains(interactionKey)) count++;
            }

            return count;
        }
    }
}
