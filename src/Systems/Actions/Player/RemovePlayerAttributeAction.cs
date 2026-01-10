using Vintagestory.API.Server;

namespace VsQuest
{
    public class RemovePlayerAttributeAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 1)
            {
                sapi.Logger.Error($"[vsquest] 'removeplayerattribute' action requires 1 argument (key) but got {args.Length} in quest '{message?.questId}'.");
                return;
            }
            byPlayer.Entity.WatchedAttributes.RemoveAttribute(args[0]);
        }
    }
}
