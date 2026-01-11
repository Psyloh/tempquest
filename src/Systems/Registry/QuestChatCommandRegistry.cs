using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestChatCommandRegistry
    {
        private readonly ICoreServerAPI sapi;
        private readonly ICoreAPI api;
        private readonly QuestSystem questSystem;

        public QuestChatCommandRegistry(ICoreServerAPI sapi, ICoreAPI api, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.api = api;
            this.questSystem = questSystem;
        }

        public void Register()
        {
            var itemSystem = api.ModLoader.GetModSystem<ItemSystem>();
            var getActionItemHandler = new GetActionItemCommandHandler(api, itemSystem);

            var questNpcListHandler = new QuestNpcListCommandHandler(sapi);

            var questListHandler = new QuestListCommandHandler(sapi, questSystem);
            var questCheckHandler = new QuestCheckCommandHandler(sapi, questSystem);
            var forgiveQuestHandler = new QuestForgiveCommandHandler(sapi, questSystem);
            var forgiveAllQuestHandler = new QuestForgiveAllCommandHandler(sapi, questSystem);
            var questCompleteHandler = new QuestCompleteCommandHandler(sapi, questSystem);
            var questCompleteActiveHandler = new QuestCompleteActiveCommandHandler(sapi, questSystem);
            var questActionItemsHandler = new QuestActionItemsCommandHandler(itemSystem);
            var questAttrSetHandler = new QuestAttrSetCommandHandler(sapi);
            var questAttrRemoveHandler = new QuestAttrRemoveCommandHandler(sapi);
            var questAttrListHandler = new QuestAttrListCommandHandler(sapi);

            var questStartHandler = new QuestStartCommandHandler(sapi, questSystem);

            var questEntityHandler = new QuestEntityCommandHandler(sapi, questSystem);

            sapi.ChatCommands.GetOrCreate("quest")
                .WithDescription("Quest administration commands")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("actionitems")
                    .WithDescription("Lists all registered action items.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(questActionItemsHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("getactionitem")
                    .WithDescription("Gives a player an action item defined in itemconfig.json.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("itemId"), sapi.ChatCommands.Parsers.OptionalInt("amount", 1))
                    .HandleWith(getActionItemHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("entities")
                    .WithDescription("Quest entity tools")
                    .RequiresPrivilege(Privilege.give)
                    .BeginSubCommand("spawned")
                        .WithDescription("Lists loaded questgiver NPCs (entity id, code, position).")
                        .RequiresPrivilege(Privilege.give)
                        .HandleWith(questNpcListHandler.Handle)
                    .EndSubCommand()
                    .BeginSubCommand("all")
                        .WithDescription("Lists entity types from a quest pack domain (assets/<domain>/entities).")
                        .RequiresPrivilege(Privilege.give)
                        .HandleWith(questEntityHandler.Handle)
                    .EndSubCommand()
                .EndSubCommand()
                .BeginSubCommand("complete")
                    .WithDescription("Force-completes an active quest for a player.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("questId"), sapi.ChatCommands.Parsers.Word("playerName"))
                    .HandleWith(questCompleteHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("completeactive")
                    .WithDescription("Force-completes the player's currently active quest.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                    .HandleWith(questCompleteActiveHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("start")
                    .WithDescription("Starts a quest for a player.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("questId"), sapi.ChatCommands.Parsers.Word("playerName"))
                    .HandleWith(questStartHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("attr")
                    .WithDescription("Admin player attributes.")
                    .RequiresPrivilege(Privilege.give)
                    .BeginSubCommand("set")
                        .WithDescription("Sets a string attribute on an online player.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(
                            sapi.ChatCommands.Parsers.Word("playerName"),
                            sapi.ChatCommands.Parsers.Word("key"),
                            sapi.ChatCommands.Parsers.Word("value")
                        )
                        .HandleWith(questAttrSetHandler.Handle)
                    .EndSubCommand()
                    .BeginSubCommand("list")
                        .WithDescription("Lists watched attributes for an online player.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(
                            sapi.ChatCommands.Parsers.Word("playerName")
                        )
                        .HandleWith(questAttrListHandler.Handle)
                    .EndSubCommand()
                    .BeginSubCommand("remove")
                        .WithDescription("Removes an attribute from an online player.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(
                            sapi.ChatCommands.Parsers.Word("playerName"),
                            sapi.ChatCommands.Parsers.Word("key")
                        )
                        .HandleWith(questAttrRemoveHandler.Handle)
                    .EndSubCommand()
                .EndSubCommand()
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
                .EndSubCommand()
                .BeginSubCommand("forgiveall")
                    .WithDescription("Resets all quests for a player: clears active quests, completed flags, and cooldowns.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                    .HandleWith(forgiveAllQuestHandler.Handle)
                .EndSubCommand();
        }
    }
}
