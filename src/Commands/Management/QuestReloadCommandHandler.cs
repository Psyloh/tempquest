using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestReloadCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;

        public QuestReloadCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.questSystem = questSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            if (questSystem == null)
            {
                return TextCommandResult.Error("QuestSystem not available.");
            }

            try
            {
                if (!questSystem.TryReloadConfigs(out string resultMessage))
                {
                    return TextCommandResult.Error(resultMessage ?? "Reload failed.");
                }

                return TextCommandResult.Success(resultMessage ?? "Reloaded.");
            }
            catch (Exception e)
            {
                try
                {
                    sapi?.Logger?.Error("[alegacyvsquest] /avq reload failed: {0}", e);
                }
                catch
                {
                }

                return TextCommandResult.Error($"Reload failed: {e.Message}");
            }
        }
    }
}
