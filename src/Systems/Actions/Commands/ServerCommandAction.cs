using Vintagestory.API.Server;

namespace VsQuest
{
    public class ServerCommandAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length > 0)
            {
                string command = string.Join(" ", args);
                if (!command.StartsWith("/"))
                {
                    command = "/" + command;
                }
                sapi.InjectConsole(command);
            }
        }
    }
}
