using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractAtCoordinateObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (args.Length < 1) return false;

            foreach (var coordString in args)
            {
                var key = $"alegacyvsquest:interactat:{coordString}";
                if (!byPlayer.Entity.WatchedAttributes.GetBool(key, false))
                {
                    return false;
                }
            }
            return true;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (args.Length < 1) return new List<int> { 0, 1 };

            int completedCount = 0;
            foreach (var coordString in args)
            {
                var key = $"alegacyvsquest:interactat:{coordString}";
                if (byPlayer.Entity.WatchedAttributes.GetBool(key, false))
                {
                    completedCount++;
                }
            }
            return new List<int> { completedCount, args.Length };
        }
    }
}
