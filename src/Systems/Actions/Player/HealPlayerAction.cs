using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class HealPlayerAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            float amount = 1000f;
            if (args.Length > 0 && !float.TryParse(args[0], out amount))
            {
                sapi.Logger.Error($"[vsquest] 'healplayer' action has an invalid amount '{args[0]}' in quest '{message?.questId}'. Defaulting to full heal.");
                amount = 1000f;
            }
            byPlayer.Entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, amount);
        }
    }
}
