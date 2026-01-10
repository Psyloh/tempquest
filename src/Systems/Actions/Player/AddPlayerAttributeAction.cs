using Vintagestory.API.Server;

namespace VsQuest
{
    public class AddPlayerAttributeAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 2)
            {
                sapi.Logger.Error($"[vsquest] 'addplayerattribute' action requires 2 arguments (key, value) but got {args.Length} in quest '{message?.questId}'.");
                return;
            }
            byPlayer.Entity.WatchedAttributes.SetString(args[0], args[1]);
        }
    }
}
