using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BossHuntStatusCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public BossHuntStatusCommandHandler(ICoreServerAPI sapi)
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

            if (!bossSystem.TryGetBossHuntStatus(out string bossKey, out string questId, out double hoursLeft))
            {
                return TextCommandResult.Error("BossHunt status unavailable.");
            }

            double daysLeft = hoursLeft / 24.0;
            string msg = $"Bosshunt: '{bossKey}' (quest '{questId}'), rotation in {hoursLeft:0.0}h (~{daysLeft:0.0}d).";
            return TextCommandResult.Success(msg);
        }
    }
}
