using System;
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

            var active = questSystem.GetPlayerQuests(target.PlayerUID) ?? new System.Collections.Generic.List<ActiveQuest>();
            var completed = target.Entity?.WatchedAttributes?.GetStringArray("vsquest:playercompleted", new string[0]) ?? new string[0];

            var lines = new System.Collections.Generic.List<string>();
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
                    string titleKey = aq.questId + "-title";
                    string title;
                    try
                    {
                        title = Lang.HasTranslation(titleKey) ? Lang.Get(titleKey) : titleKey;
                    }
                    catch
                    {
                        title = titleKey;
                    }

                    string progress = "";
                    try
                    {
                        var progArgs = aq.progress(target)
                            .ConvertAll<string>(x => x.ToString())
                            .ToArray();
                        progress = Lang.Get(aq.questId + "-obj", progArgs);
                    }
                    catch
                    {
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
                    string titleKey = questId + "-title";
                    string title;
                    try
                    {
                        title = Lang.HasTranslation(titleKey) ? Lang.Get(titleKey) : titleKey;
                    }
                    catch
                    {
                        title = titleKey;
                    }

                    lines.Add($"- {questId} - {title}");
                }
            }

            return TextCommandResult.Success(string.Join("\n", lines));
        }
    }
}
