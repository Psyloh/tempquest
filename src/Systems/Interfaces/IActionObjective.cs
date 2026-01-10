using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public interface IActionObjective
    {
        bool IsCompletable(IPlayer byPlayer, params string[] args);
        List<int> GetProgress(IPlayer byPlayer, params string[] args);
    }
}
