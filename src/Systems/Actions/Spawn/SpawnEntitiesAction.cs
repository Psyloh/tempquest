using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class SpawnEntitiesAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            foreach (var code in args)
            {
                var type = sapi.World.GetEntityType(new AssetLocation(code));
                if (type == null)
                {
                    throw new QuestException(string.Format("Tried to spawn {0} for quest {1} but could not find the entity type!", code, message.questId));
                }
                var entity = sapi.World.ClassRegistry.CreateEntity(type);
                entity.ServerPos = sapi.World.GetEntityById(message.questGiverId).ServerPos.Copy();
                sapi.World.SpawnEntity(entity);
            }
        }
    }
}
