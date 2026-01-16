using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class CloseDialogueAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || message == null) return;

            var entity = sapi.World.GetEntityById(message.questGiverId);
            var conversable = entity?.GetBehavior<EntityBehaviorConversable>();
            conversable?.Dialog?.TryClose();
        }
    }
}
