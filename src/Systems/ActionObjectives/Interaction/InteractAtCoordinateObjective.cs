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

            string coordString = args[0];
            if (!QuestInteractAtUtil.TryParsePos(coordString, out int targetX, out int targetY, out int targetZ)) return false;

            return QuestInteractAtUtil.HasInteraction(byPlayer, targetX, targetY, targetZ);
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            bool completed = IsCompletable(byPlayer, args);
            return new List<int>(new int[] { completed ? 1 : 0 });
        }
    }
}
