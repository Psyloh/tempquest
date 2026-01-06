using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractCountObjective : ActiveActionObjective
    {
        public bool isCompletable(IPlayer byPlayer, params string[] args)
        {
            if (args == null || args.Length == 0) return false;
            return CountCompleted(byPlayer, args) >= args.Length;
        }

        public List<int> progress(IPlayer byPlayer, params string[] args)
        {
            int total = args?.Length ?? 0;
            if (total == 0) return new List<int>(new int[] { 0, 0 });

            int count = CountCompleted(byPlayer, args);
            if (count > total) count = total;

            return new List<int>(new int[] { count, total });
        }

        private static int CountCompleted(IPlayer byPlayer, string[] coordArgs)
        {
            string completedInteractions = byPlayer.Entity.WatchedAttributes.GetString("completedInteractions", "");
            if (string.IsNullOrEmpty(completedInteractions)) return 0;

            var completed = new HashSet<string>(
                completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            );

            int count = 0;
            foreach (var coordString in coordArgs)
            {
                if (string.IsNullOrWhiteSpace(coordString)) continue;
                var coords = coordString.Split(',');
                if (coords.Length != 3) continue;

                if (int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y) && int.TryParse(coords[2], out int z))
                {
                    string key = $"interactat_{x}_{y}_{z}";
                    if (completed.Contains(key)) count++;
                }
            }

            return count;
        }
    }
}
