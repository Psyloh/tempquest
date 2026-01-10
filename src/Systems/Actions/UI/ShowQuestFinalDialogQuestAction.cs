using Vintagestory.API.Server;

namespace VsQuest
{
    public class ShowQuestFinalDialogQuestAction : IQuestAction
    {
        public void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 2) throw new QuestException("The 'showquestfinaldialog' action requires at least 2 arguments: titleLangKey, textLangKey.");

            api.Network.GetChannel("vsquest").SendPacket(new ShowQuestDialogMessage()
            {
                TitleLangKey = args[0],
                TextLangKey = args[1],
                Option1LangKey = args.Length >= 3 ? args[2] : null,
                Option2LangKey = args.Length >= 4 ? args[3] : null
            }, byPlayer);
        }
    }
}
