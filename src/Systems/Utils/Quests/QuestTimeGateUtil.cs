using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public static class QuestTimeGateUtil
    {
        public static bool AllowsProgress(IPlayer player, Quest questDef, Dictionary<string, IActionObjective> actionObjectiveRegistry)
        {
            if (player == null || questDef == null) return true;
            if (questDef.actionObjectives == null) return true;

            for (int i = 0; i < questDef.actionObjectives.Count; i++)
            {
                var ao = questDef.actionObjectives[i];
                if (ao?.id != "timeofday") continue;

                if (actionObjectiveRegistry != null && actionObjectiveRegistry.TryGetValue("timeofday", out var impl) && impl != null)
                {
                    return impl.IsCompletable(player, ao.args);
                }

                return true;
            }

            return true;
        }
    }
}
