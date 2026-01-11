using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class PlayerHasAttributeActionObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            return byPlayer.Entity.WatchedAttributes.GetString(args[0]) == args[1];
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            return IsCompletable(byPlayer, args) ? new List<int>(new int[] { 1 }) : new List<int>(new int[] { 0 });
        }
    }
}