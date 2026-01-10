using System.Linq;

namespace VsQuest
{
    public static class QuestObjectiveMatchUtil
    {
        public static bool InteractObjectiveMatches(Objective objective, string code, int[] position)
        {
            if (objective == null) return false;

            if (objective.positions != null && objective.positions.Count > 0)
            {
                var posStr = string.Join(",", position);
                if (!objective.positions.Any(p => p == posStr))
                {
                    return false;
                }
            }

            if (objective.validCodes == null || objective.validCodes.Count == 0)
            {
                return true;
            }

            foreach (var codeCandidate in objective.validCodes)
            {
                if (LocalizationUtils.MobCodeMatches(codeCandidate, code))
                {
                    return true;
                }

                if (codeCandidate.EndsWith("*") && code.StartsWith(codeCandidate.Remove(codeCandidate.Length - 1)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
