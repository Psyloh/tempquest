using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

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
                string ApplyPrefixes(string text, string objectiveId)
                {
                    if (string.IsNullOrWhiteSpace(text)) return text;

                    string timeOfDayPrefix = null;
                    string landPrefix = null;

                    if (questDef.actionObjectives != null)
                    {
                        // timeofday prefix: only apply if there is a gate targeting this objectiveId,
                        // or if the gate has no objectiveId (legacy behavior).
                        foreach (var ao in questDef.actionObjectives)
                        {
                            if (ao?.id != "timeofday") continue;
                            if (ao.args == null || ao.args.Length == 0) continue;

                            bool applies = false;
                            if (ao.args.Length == 1)
                            {
                                // Legacy: show prefix globally
                                applies = true;
                            }
                            else if (ao.args.Length == 2)
                            {
                                applies = !string.IsNullOrWhiteSpace(objectiveId)
                                    && string.Equals(ao.args[1], objectiveId, StringComparison.OrdinalIgnoreCase);
                            }

                            if (!applies) continue;

                            if (TimeOfDayObjective.TryGetModeLabelKey(ao.args, out string labelKey))
                            {
                                timeOfDayPrefix = LocalizationUtils.GetSafe(labelKey);
                            }
                            break;
                        }

                        // landgate prefix
                        foreach (var ao in questDef.actionObjectives)
                        {
                            if (ao?.id != "landgate") continue;

                            if (!LandGateObjective.TryParseArgs(ao.args, out _, out string gateObjectiveId, out string prefix, out bool hidePrefix))
                            {
                                continue;
                            }

                            bool applies = string.IsNullOrWhiteSpace(gateObjectiveId)
                                || (!string.IsNullOrWhiteSpace(objectiveId) && string.Equals(gateObjectiveId, objectiveId, StringComparison.OrdinalIgnoreCase));

                            if (!applies) continue;

                            if (!hidePrefix)
                            {
                                landPrefix = prefix;
                            }
                            break;
                        }
                    }

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
                        lines.Add($"- {ApplyPrefixes($"{LocalizationUtils.GetMobDisplayName(code)}: {have}/{need}", null)}");
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
                        lines.Add($"- {ApplyPrefixes(custom, null)}");
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

                                        lines.Add($"- {ApplyPrefixes(template, null)}");
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
                            lines.Add($"- {ApplyPrefixes($"{MobLocalizationUtils.GetMobDisplayName(code)}: {have}/{need}", null)}");
                        }
                    }
                }

                AddProgressLines(questDef.gatherObjectives);
                AddProgressLines(questDef.killObjectives);
                AddProgressLines(questDef.blockPlaceObjectives);
                AddProgressLines(questDef.blockBreakObjectives);
                AddProgressLines(questDef.interactObjectives);

                // Action objectives
                if (questDef.actionObjectives != null)
                {
                    var questSystem = api.ModLoader.GetModSystem<QuestSystem>();

                    foreach (var actionObjective in questDef.actionObjectives)
                    {
                        if (actionObjective == null) continue;
                        if (string.IsNullOrWhiteSpace(actionObjective.id)) continue;

                        // Do not show gates as progress lines
                        if (actionObjective.id == "timeofday") continue;
                        if (actionObjective.id == "landgate") continue;

                        // Do not show technical wrapper objectives
                        if (actionObjective.id == "sequence") continue;

                        // Do not show interact-with-entity objectives in progress text
                        if (actionObjective.id == "interactwithentity") continue;

                        // randomkill already has its own slot lines
                        if (actionObjective.id == "randomkill") continue;

                        var impl = questSystem?.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue(actionObjective.id, out var objectiveImpl)
                            ? objectiveImpl
                            : null;

                        var prog = impl?.GetProgress(player, actionObjective.args);
                        if (prog == null || prog.Count == 0) continue;

                        // Try custom per-objective progress string first
                        string customKeyBase = activeQuest.questId + "-obj-" + (string.IsNullOrWhiteSpace(actionObjective.objectiveId) ? actionObjective.id : actionObjective.objectiveId);
                        string customProgress = LocalizationUtils.GetSafe(customKeyBase, prog.Cast<object>().ToArray());
                        if (!string.IsNullOrWhiteSpace(customProgress) && !string.Equals(customProgress, customKeyBase, StringComparison.OrdinalIgnoreCase))
                        {
                            lines.Add($"- {ApplyPrefixes(customProgress, actionObjective.objectiveId)}");
                            continue;
                        }

                        string objectiveLabel;
                        if (actionObjective.id == "walkdistance")
                        {
                            objectiveLabel = LocalizationUtils.GetSafe("alegacyvsquest:objective-walkdistance");
                        }
                        else
                        {
                            var candidate = LocalizationUtils.GetSafe($"alegacyvsquest:objective-{actionObjective.id}");
                            objectiveLabel = string.Equals(candidate, $"alegacyvsquest:objective-{actionObjective.id}", StringComparison.OrdinalIgnoreCase)
                                ? actionObjective.id
                                : candidate;
                        }

                        if (actionObjective.id == "killactiontarget" && actionObjective.args != null && actionObjective.args.Length >= 3 && prog.Count >= 2)
                        {
                            string targetId = actionObjective.args[2];
                            string targetCode = targetId?.Trim();

                            if (!string.IsNullOrWhiteSpace(targetCode))
                            {
                                int lastColon = targetCode.LastIndexOf(':');
                                if (lastColon >= 0 && lastColon < targetCode.Length - 1)
                                {
                                    targetCode = targetCode.Substring(lastColon + 1);
                                }

                                string targetName = MobLocalizationUtils.GetMobDisplayName(targetCode);
                                if (string.IsNullOrWhiteSpace(targetName))
                                {
                                    targetName = targetCode;
                                }

                                string killLine = Lang.Get("alegacyvsquest:progress-pair", targetName, prog[0], prog[1]);
                                lines.Add($"- {ApplyPrefixes(killLine, actionObjective.objectiveId)}");
                                continue;
                            }
                        }

                        string line;
                        if (actionObjective.id == "walkdistance" && prog.Count >= 2)
                        {
                            var meterUnit = LocalizationUtils.GetSafe("alegacyvsquest:unit-meter-short");
                            line = Lang.Get("alegacyvsquest:progress-walkdistance", objectiveLabel, prog[0], prog[1], meterUnit);
                        }
                        else if (prog.Count >= 2)
                        {
                            line = Lang.Get("alegacyvsquest:progress-pair", objectiveLabel, prog[0], prog[1]);
                        }
                        else
                        {
                            line = Lang.Get("alegacyvsquest:progress-single", objectiveLabel, prog[0]);
                        }

                        lines.Add($"- {ApplyPrefixes(line, actionObjective.objectiveId)}");
                    }
                }

                return string.Join("\n", lines);
            }
            catch (Exception e)
            {
                api.Logger.Error($"[alegacyvsquest] Error building progress text for quest '{activeQuest.questId}': {e}");
                return LocalizationUtils.GetSafe("alegacyvsquest:progress-load-error");
            }
        }
    }
}
