using Vintagestory.API.Server;

namespace VsQuest
{
    public class PlayerCommandAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length > 0)
            {
                string command = string.Join(" ", args);
                sapi.Network.GetChannel("vsquest").SendPacket(new ExecutePlayerCommandMessage() { Command = command }, byPlayer);
            }
        }
    }
}
