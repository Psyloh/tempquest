using Vintagestory.API.Server;

namespace VsQuest
{
    public class SetQuestGiverAttributeQuestAction : IQuestAction
    {
        public void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 3) throw new QuestException("The 'setquestgiverattribute' action requires 3 arguments: key, type, value.");

            var entity = api.World.GetEntityById(message.questGiverId);
            if (entity == null) return;

            string key = args[0];
            string type = args[1].ToLowerInvariant();
            string value = args[2];

            if (type == "bool")
            {
                entity.WatchedAttributes.SetBool(key, value == "true" || value == "1");
            }
            else if (type == "int")
            {
                entity.WatchedAttributes.SetInt(key, int.Parse(value));
            }
            else if (type == "string")
            {
                entity.WatchedAttributes.SetString(key, value);
            }
            else
            {
                throw new QuestException("The 'setquestgiverattribute' action type must be one of: bool, int, string.");
            }

            entity.WatchedAttributes.MarkPathDirty(key);
        }
    }
}
