using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class DespawnQuestGiverAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            sapi.World.RegisterCallback(dt => sapi.World.GetEntityById(message.questGiverId).Die(EnumDespawnReason.Removed), int.Parse(args[0]));
        }
    }
}
