using Vintagestory.API.Server;

namespace VsQuest
{
    public class AllowCharSelOnceAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            byPlayer?.Entity?.WatchedAttributes?.SetBool("allowcharselonce", true);
            byPlayer?.Entity?.WatchedAttributes?.MarkPathDirty("allowcharselonce");
        }
    }
}
