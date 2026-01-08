using System;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestListCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;

        public QuestListCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.questSystem = questSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            if (questSystem.QuestRegistry == null || questSystem.QuestRegistry.Count == 0)
            {
                return TextCommandResult.Success("No quests registered.");
            }

            var lines = questSystem.QuestRegistry
                .OrderBy(kvp => kvp.Key)
                .Select(kvp =>
                {
                    string questId = kvp.Key;
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

                    return $"{questId} - {title}";
                })
                .ToArray();

            // TextCommandResult supports multiline strings
            return TextCommandResult.Success(string.Join("\n", lines));
        }
    }
}
