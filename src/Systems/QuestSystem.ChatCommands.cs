using Vintagestory.API.Server;

namespace VsQuest
{
    public partial class QuestSystem
    {
        private void RegisterChatCommands(ICoreServerAPI sapi)
        {
            var itemSystem = api.ModLoader.GetModSystem<ItemSystem>();
            var giveActionItemHandler = new GiveActionItemCommandHandler(api, itemSystem);

            var forgiveQuestHandler = new QuestForgiveCommandHandler(sapi, this);
            var questListHandler = new QuestListCommandHandler(sapi, this);
            var questCheckHandler = new QuestCheckCommandHandler(sapi, this);

            sapi.ChatCommands.GetOrCreate("giveactionitem")
                .WithDescription("Gives a player an action item defined in itemconfig.json.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.Word("itemId"), sapi.ChatCommands.Parsers.OptionalInt("amount", 1))
                .HandleWith(giveActionItemHandler.Handle);

            sapi.ChatCommands.GetOrCreate("quest")
                .WithDescription("Quest administration commands")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("list")
                    .WithDescription("Lists all registered quest IDs and their titles.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(questListHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("check")
                    .WithDescription("Shows active/completed quests and progress for a player.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("playerName"))
                    .HandleWith(questCheckHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("forgive")
                    .WithDescription("Resets a quest for a player: removes it from active quests and clears cooldown/completed flags.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("questId"), sapi.ChatCommands.Parsers.Word("playerName"))
                    .HandleWith(forgiveQuestHandler.Handle)
                .EndSubCommand();
        }
    }
}
