using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class SpawnEntitiesAtPlayerAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer?.Entity == null || args == null || args.Length == 0) return;

            var spawnPos = byPlayer.Entity.ServerPos.Copy();
            foreach (var code in args)
            {
                var type = sapi.World.GetEntityType(new AssetLocation(code));
                if (type == null)
                {
                    throw new QuestException(string.Format("Tried to spawn {0} for quest {1} but could not find the entity type!", code, message.questId));
                }

                var entity = sapi.World.ClassRegistry.CreateEntity(type);
                if (entity == null) continue;

                entity.ServerPos = spawnPos.Copy();
                sapi.World.SpawnEntity(entity);
            }
        }
    }
}
