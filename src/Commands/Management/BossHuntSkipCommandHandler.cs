using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BossHuntSkipCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public BossHuntSkipCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            var bossSystem = sapi?.ModLoader?.GetModSystem<BossHuntSystem>();
            if (bossSystem == null)
            {
                return TextCommandResult.Error("BossHuntSystem not available.");
            }

            if (!bossSystem.ForceRotateToNext(out string bossKey, out string questId))
            {
                return TextCommandResult.Error("Failed to rotate bosshunt target.");
            }

            return TextCommandResult.Success($"Bosshunt rotated to '{bossKey}' (quest '{questId}').");
        }
    }
}
