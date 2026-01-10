using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public abstract class ActionObjectiveBase : IActionObjective
    {
        public abstract bool IsCompletable(IPlayer byPlayer, params string[] args);
        public abstract List<int> GetProgress(IPlayer byPlayer, params string[] args);
    }
}
