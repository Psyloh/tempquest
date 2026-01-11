using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public static class QuestTimeGateUtil
    {
        public static bool AllowsProgress(IPlayer player, Quest questDef, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry)
        {
            return AllowsProgress(player, questDef, actionObjectiveRegistry, null, null);
        }

        public static bool AllowsProgress(IPlayer player, Quest questDef, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, string scope)
        {
            return AllowsProgress(player, questDef, actionObjectiveRegistry, scope, null);
        }

        public static bool AllowsProgress(IPlayer player, Quest questDef, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, string scope, string objectiveId)
        {
            if (player == null || questDef == null) return true;
            if (questDef.actionObjectives == null) return true;

            var timeOfDayImpl = actionObjectiveRegistry != null && actionObjectiveRegistry.TryGetValue("timeofday", out var tod) ? tod : null;
            var landGateImpl = actionObjectiveRegistry != null && actionObjectiveRegistry.TryGetValue("landgate", out var lg) ? lg : null;

            bool foundMatchingGate = false;
            bool allows = true;

            for (int i = 0; i < questDef.actionObjectives.Count; i++)
            {
                var ao = questDef.actionObjectives[i];

                // Time-of-day gate
                if (ao?.id == "timeofday")
                {
                    if (timeOfDayImpl == null) continue;

                    string[] args = ao.args;
                    ParseTimeGateArgs(args, questDef, out string gateScope, out string gateObjectiveId);

                    if (!AppliesToScope(gateScope, scope)) continue;
                    if (!AppliesToObjectiveId(gateObjectiveId, objectiveId)) continue;

                    foundMatchingGate = true;
                    allows &= timeOfDayImpl.IsCompletable(player, args);
                }

                // Land claim gate
                if (ao?.id == "landgate")
                {
                    if (landGateImpl == null) continue;

                    string[] args = ao.args;
                    if (!LandGateObjective.TryParseArgs(args, out _, out string gateObjectiveId, out _, out _)) continue;

                    // If objectiveId specified on the gate: only apply when caller is progressing that objectiveId.
                    // If not specified: apply to all progress in the quest.
                    if (!string.IsNullOrWhiteSpace(gateObjectiveId) && !AppliesToObjectiveId(gateObjectiveId, objectiveId))
                    {
                        continue;
                    }

                    foundMatchingGate = true;
                    allows &= landGateImpl.IsCompletable(player, args);
                }
            }

            return !foundMatchingGate || allows;
        }

        private static bool AppliesToScope(string gateScope, string requestedScope)
        {
            if (string.IsNullOrWhiteSpace(gateScope)) return false;
            if (string.IsNullOrWhiteSpace(requestedScope)) return false;

            return string.Equals(gateScope.Trim(), requestedScope.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool AppliesToObjectiveId(string gateObjectiveId, string requestedObjectiveId)
        {
            if (string.IsNullOrWhiteSpace(gateObjectiveId)) return false;
            if (string.IsNullOrWhiteSpace(requestedObjectiveId)) return false;

            return string.Equals(gateObjectiveId.Trim(), requestedObjectiveId.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        private static void ParseTimeGateArgs(string[] args, Quest questDef, out string gateScope, out string gateObjectiveId)
        {
            gateScope = null;
            gateObjectiveId = null;

            // Required format: [mode, objectiveId]
            if (args == null || args.Length != 2) return;

            gateObjectiveId = args[1];
            gateScope = InferScopeFromObjectiveId(questDef, gateObjectiveId);
        }

        private static string InferScopeFromObjectiveId(Quest questDef, string objectiveId)
        {
            if (questDef?.actionObjectives == null) return null;
            if (string.IsNullOrWhiteSpace(objectiveId)) return null;

            for (int i = 0; i < questDef.actionObjectives.Count; i++)
            {
                var ao = questDef.actionObjectives[i];
                if (ao == null) continue;
                if (!string.Equals(ao.objectiveId, objectiveId, System.StringComparison.OrdinalIgnoreCase)) continue;

                // Map actionObjective type -> scope
                if (string.Equals(ao.id, "walkdistance", System.StringComparison.OrdinalIgnoreCase)) return "tick";
                if (string.Equals(ao.id, "randomkill", System.StringComparison.OrdinalIgnoreCase)) return "kill";
                if (string.Equals(ao.id, "killnear", System.StringComparison.OrdinalIgnoreCase)) return "kill";
                if (string.Equals(ao.id, "temporalstorm", System.StringComparison.OrdinalIgnoreCase)) return "tick";
            }

            return null;
        }
    }
}
