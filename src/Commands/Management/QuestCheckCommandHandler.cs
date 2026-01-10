using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestCheckCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;

        public QuestCheckCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.questSystem = questSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];

            var target = sapi.World.AllOnlinePlayers
                .FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;

            if (target == null)
            {
                return TextCommandResult.Error($"Player '{playerName}' not found online.");
            }

            var active = questSystem.GetPlayerQuests(target.PlayerUID) ?? new List<ActiveQuest>();
            var completed = target.Entity?.WatchedAttributes?.GetStringArray("alegacyvsquest:playercompleted", new string[0]) ?? new string[0];

            var lines = new List<string>();
            lines.Add($"Player: {target.PlayerName}");

            lines.Add("Active quests:");
            if (active.Count == 0)
            {
                lines.Add("- (none)");
            }
            else
            {
                foreach (var aq in active.OrderBy(q => q.questId))
                {
                    string title = LocalizationUtils.GetSafe(aq.questId + "-title");

                    string progress = "";
                    try
                    {
                        var progArgs = aq.GetProgress(target).Cast<object>().ToArray();
                        string progressKey = aq.questId + "-obj";
                        if (Lang.HasTranslation(progressKey)) {
                            progress = LocalizationUtils.GetSafe(progressKey, progArgs);
                        }
                    }
                    catch (Exception e)
                    {
                        sapi.Logger.Warning($"[vsquest] Could not get progress for quest '{aq.questId}': {e.Message}");
                    }

                    if (!string.IsNullOrEmpty(progress))
                    {
                        progress = progress.Replace("\r\n", " ").Replace("\n", " ");
                        lines.Add($"- {aq.questId} - {title} | {progress}");
                    }
                    else
                    {
                        lines.Add($"- {aq.questId} - {title}");
                    }
                }
            }

            lines.Add("Completed quests:");
            if (completed.Length == 0)
            {
                lines.Add("- (none)");
            }
            else
            {
                foreach (var questId in completed.OrderBy(q => q))
                {
                    string title = LocalizationUtils.GetSafe(questId + "-title");

                    lines.Add($"- {questId} - {title}");
                }
            }

            return TextCommandResult.Success(string.Join("\n", lines));
        }
    }
}
