using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class OpenQuestsAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var questGiver = sapi.World.GetEntityById(message.questGiverId);
            if (questGiver == null) return;

            var questGiverBehavior = questGiver.GetBehavior<EntityBehaviorQuestGiver>();
            if (questGiverBehavior == null) return;

            if (byPlayer.Entity is EntityPlayer entityPlayer)
            {
                questGiverBehavior.SendQuestInfoMessageToClient(sapi, entityPlayer);
            }
        }
    }
}
