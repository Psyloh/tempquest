using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    public static class QuestProgressTextUtil
    {
        private static string LocalizeMobName(string code)
        {
            return MobLocalizationUtils.GetMobDisplayName(code);
        }

        public static bool TryBuildRandomKillProgressText(ICoreClientAPI capi, IClientPlayer player, ActiveQuest activeQuest, out string progressText)
        {
            progressText = null;
            if (capi == null || player == null || activeQuest == null) return false;

            try
            {
                var questSystem = capi.ModLoader.GetModSystem<QuestSystem>();
                if (questSystem == null) return false;
                if (!questSystem.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef)) return false;

                var wa = player.Entity?.WatchedAttributes;
                if (wa == null) return false;

                if (questDef.actionObjectives == null || !questDef.actionObjectives.Exists(obj => obj.id == "randomkill")) return false;

                string questId = activeQuest.questId;
                int slots = wa.GetInt($"vsquest:randkill:{questId}:slots", 0);

                string BuildLine(int slot)
                {
                    string code = wa.GetString($"vsquest:randkill:{questId}:slot{slot}:code", "?");
                    int have = wa.GetInt($"vsquest:randkill:{questId}:slot{slot}:have", 0);
                    int need = wa.GetInt($"vsquest:randkill:{questId}:slot{slot}:need", 0);

                    if (need < 0) need = 0;
                    if (have < 0) have = 0;
                    if (need > 0 && have > need) have = need;

                    return $"- {LocalizeMobName(code)}: {have}/{need}";
                }

                if (slots > 0)
                {
                    var lines = new List<string>();
                    for (int slot = 0; slot < slots; slot++)
                    {
                        lines.Add(BuildLine(slot));
                    }
                    progressText = string.Join("\n", lines);
                    return true;
                }

                // Legacy single target
                string legacyCode = wa.GetString($"vsquest:randkill:{questId}:code", "?");
                int legacyHave = wa.GetInt($"vsquest:randkill:{questId}:have", 0);
                int legacyNeed = wa.GetInt($"vsquest:randkill:{questId}:need", 0);
                if (legacyNeed < 0) legacyNeed = 0;
                if (legacyHave < 0) legacyHave = 0;
                if (legacyNeed > 0 && legacyHave > legacyNeed) legacyHave = legacyNeed;

                progressText = $"- {LocalizeMobName(legacyCode)}: {legacyHave}/{legacyNeed}";
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
