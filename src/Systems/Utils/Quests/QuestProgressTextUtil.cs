using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace VsQuest
{
    public static class QuestProgressTextUtil
    {
        public static string GetActiveQuestText(ICoreAPI api, IPlayer player, ActiveQuest quest)
        {
            var questSystem = api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null || !questSystem.QuestRegistry.TryGetValue(quest.questId, out var questDef))
            {
                return LocalizationUtils.GetSafe(quest.questId + "-desc");
            }

            string progressText = BuildProgressText(api, player, quest, questDef);

            string desc = LocalizationUtils.GetSafe(quest.questId + "-desc");
            if (string.IsNullOrEmpty(progressText))
            {
                return desc;
            }
            else
            {
                return $"{desc}<br><br><strong>{LocalizationUtils.GetSafe("alegacyvsquest:progress-title")}</strong><br>{progressText}";
            }
        }

        private static string BuildProgressText(ICoreAPI api, IPlayer player, ActiveQuest activeQuest, Quest questDef)
        {
            var lines = new List<string>();
            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return "";

            try
            {
                string timeOfDayPrefix = null;
                string landPrefix = null;
                if (questDef.actionObjectives != null)
                {
                    foreach (var ao in questDef.actionObjectives)
                    {
                        if (ao?.id != "timeofday") continue;

                        if (TimeOfDayObjective.TryGetModeLabelKey(ao.args, out string labelKey))
                        {
                            timeOfDayPrefix = LocalizationUtils.GetSafe(labelKey);
                        }
                        break;
                    }

                    foreach (var ao in questDef.actionObjectives)
                    {
                        if (ao?.id != "landgate") continue;

                        if (LandGateObjective.TryParseArgs(ao.args, out _, out _, out string prefix, out bool hidePrefix) && !hidePrefix)
                        {
                            landPrefix = prefix;
                        }
                        break;
                    }
                }

                string ApplyPrefixes(string text)
                {
                    // Order matters: landgate wraps timeofday so that final text becomes "land: time: ..."
                    string[] prefixes = new[] { timeOfDayPrefix, landPrefix };
                    foreach (var p in prefixes)
                    {
                        if (string.IsNullOrWhiteSpace(p)) continue;
                        text = $"{p}: {text}";
                    }

                    return text;
                }

                // randomkill objectives
                int slots = wa.GetInt(RandomKillQuestUtils.SlotsKey(activeQuest.questId), 0);
                if (slots > 0)
                {
                    for (int slot = 0; slot < slots; slot++)
                    {
                        string code = wa.GetString(RandomKillQuestUtils.SlotCodeKey(activeQuest.questId, slot), "?");
                        int have = wa.GetInt(RandomKillQuestUtils.SlotHaveKey(activeQuest.questId, slot), 0);
                        int need = wa.GetInt(RandomKillQuestUtils.SlotNeedKey(activeQuest.questId, slot), 0);
                        lines.Add($"- {ApplyPrefixes($"{LocalizationUtils.GetMobDisplayName(code)}: {have}/{need}")}");
                    }
                }

                // Generic objectives from GetProgress
                var progress = activeQuest.GetProgress(player);
                int progressIndex = 0;

                // Quest-specific progress line (e.g. witness: "Gifts found: {8}/{9}")
                // The template may reference any index in the full progress array.
                try
                {
                    var customKey = activeQuest.questId + "-obj";
                    var custom = LocalizationUtils.GetSafe(customKey, progress.Cast<object>().ToArray());
                    if (!string.IsNullOrWhiteSpace(custom) && !string.Equals(custom, customKey, StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add($"- {ApplyPrefixes(custom)}");
                        return string.Join("\n", lines);
                    }
                }
                catch
                {
                    try
                    {
                        // Fallback for quests that use interactcount: compute have/need directly instead of relying on hardcoded indices.
                        if (questDef.actionObjectives != null)
                        {
                            foreach (var ao in questDef.actionObjectives)
                            {
                                if (ao?.id != "interactcount") continue;

                                var questSystem = api.ModLoader.GetModSystem<QuestSystem>();
                                if (questSystem?.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue("interactcount", out var impl) && impl != null)
                                {
                                    var prog = impl.GetProgress(player, ao.args);
                                    if (prog != null && prog.Count >= 2)
                                    {
                                        int have = prog[0];
                                        int need = prog[1];

                                        var labelKey = activeQuest.questId + "-obj";
                                        var template = LocalizationUtils.GetSafe(labelKey, have, need);
                                        if (string.IsNullOrWhiteSpace(template) || string.Equals(template, labelKey, StringComparison.OrdinalIgnoreCase))
                                        {
                                            template = $"{have}/{need}";
                                        }

                                        lines.Add($"- {ApplyPrefixes(template)}");
                                        return string.Join("\n", lines);
                                    }
                                }

                                break;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                void AddProgressLines(List<Objective> objectives)
                {
                    foreach (var objective in objectives)
                    {
                        if (progressIndex < progress.Count)
                        {
                            int have = progress[progressIndex++];
                            int need = objective.demand;
                            string code = objective.validCodes.FirstOrDefault() ?? "?";
                            lines.Add($"- {ApplyPrefixes($"{LocalizationUtils.GetMobDisplayName(code)}: {have}/{need}")}");
                        }
                    }
                }

                AddProgressLines(questDef.gatherObjectives);
                AddProgressLines(questDef.killObjectives);
                AddProgressLines(questDef.blockPlaceObjectives);
                AddProgressLines(questDef.blockBreakObjectives);
                AddProgressLines(questDef.interactObjectives);

                // Action objectives (e.g. walkdistance)
                if (questDef.actionObjectives != null)
                {
                    var questSystem = api.ModLoader.GetModSystem<QuestSystem>();

                    foreach (var actionObjective in questDef.actionObjectives)
                    {
                        if (actionObjective == null) continue;

                        if (actionObjective.id == "walkdistance")
                        {
                            // Do not rely on progressIndex here: other action objectives (e.g. randomkill)
                            // may contribute variable-length progress arrays and desync indices.
                            var impl = questSystem?.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue(actionObjective.id, out var objectiveImpl)
                                ? objectiveImpl
                                : null;

                            var prog = impl?.GetProgress(player, actionObjective.args);
                            if (prog != null && prog.Count >= 2)
                            {
                                int have = prog[0];
                                int need = prog[1];

                                var walkLabel = LocalizationUtils.GetSafe("alegacyvsquest:objective-walkdistance");
                                var meterUnit = LocalizationUtils.GetSafe("alegacyvsquest:unit-meter-short");
                                lines.Add($"- {ApplyPrefixes($"{walkLabel}: {have}/{need} {meterUnit}")}");
                            }
                            continue;
                        }
                    }
                }

                return string.Join("\n", lines);
            }
            catch (Exception e)
            {
                api.Logger.Error($"[vsquest] Error building progress text for quest '{activeQuest.questId}': {e}");
                return "Error loading progress.";
            }
        }
    }
}
