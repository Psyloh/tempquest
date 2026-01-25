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
            var questCompleteHandler = new QuestCompleteCommandHandler(sapi, questSystem);
            var questCompleteActiveHandler = new QuestCompleteActiveCommandHandler(sapi, questSystem);
            var questActionItemsHandler = new QuestActionItemsCommandHandler(itemSystem);
            var questAttrSetHandler = new QuestAttrSetCommandHandler(sapi);
            var questAttrRemoveHandler = new QuestAttrRemoveCommandHandler(sapi);
            var questAttrListHandler = new QuestAttrListCommandHandler(sapi);

            var bossHuntSkipHandler = new BossHuntSkipCommandHandler(sapi);
            var bossHuntStatusHandler = new BossHuntStatusCommandHandler(sapi);

            var questWAttrHandler = new QuestWAttrCommandHandler(sapi);

            var questStartHandler = new QuestStartCommandHandler(sapi, questSystem);

            var questExecActionStringHandler = new QuestExecActionStringCommandHandler(sapi);

            var questEntityHandler = new QuestEntityCommandHandler(sapi, questSystem);
            var questNoteForgiveHandler = new QuestNoteForgiveCommandHandler(sapi);
            var questForgiveActiveAliasHandler = new QuestForgiveAliasCommandHandler(sapi, questSystem, "active");
            var questForgiveAllAliasHandler = new QuestForgiveAliasCommandHandler(sapi, questSystem, "all");

            var actionItemDurabilityHandler = new ActionItemDurabilityCommandHandler();

            sapi.ChatCommands.GetOrCreate("vsq")
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
                .BeginSubCommand("wattr")
                    .WithDescription("Admin WatchedAttributes on an online player. If no player is given, uses the caller.")
                    .RequiresPrivilege(Privilege.give)
                    .BeginSubCommand("setint")
                        .WithDescription("Sets an int WatchedAttribute.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(
                            sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                            sapi.ChatCommands.Parsers.Word("key"),
                            sapi.ChatCommands.Parsers.Int("value")
                        )
                        .HandleWith(questWAttrHandler.SetInt)
                    .EndSubCommand()
                    .BeginSubCommand("addint")
                        .WithDescription("Adds delta to an int WatchedAttribute.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(
                            sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                            sapi.ChatCommands.Parsers.Word("key"),
                            sapi.ChatCommands.Parsers.Int("delta")
                        )
                        .HandleWith(questWAttrHandler.AddInt)
                    .EndSubCommand()
                    .BeginSubCommand("setbool")
                        .WithDescription("Sets a bool WatchedAttribute.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(
                            sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                            sapi.ChatCommands.Parsers.Word("key"),
                            sapi.ChatCommands.Parsers.Bool("value")
                        )
                        .HandleWith(questWAttrHandler.SetBool)
                    .EndSubCommand()
                    .BeginSubCommand("setstring")
                        .WithDescription("Sets a string WatchedAttribute.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(
                            sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                            sapi.ChatCommands.Parsers.Word("key"),
                            sapi.ChatCommands.Parsers.All("value")
                        )
                        .HandleWith(questWAttrHandler.SetString)
                    .EndSubCommand()
                    .BeginSubCommand("remove")
                        .WithDescription("Removes a WatchedAttribute key.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(
                            sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                            sapi.ChatCommands.Parsers.Word("key")
                        )
                        .HandleWith(questWAttrHandler.Remove)
                    .EndSubCommand()
                .EndSubCommand()
                .BeginSubCommand("qlist")
                    .WithDescription("Lists all registered quest IDs and their titles.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(questListHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("qcheck")
                    .WithDescription("Shows active/completed quests and progress for a player.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("playerName"))
                    .HandleWith(questCheckHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("qstart")
                    .WithDescription("Starts a quest for a player.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("questId"), sapi.ChatCommands.Parsers.Word("playerName"))
                    .HandleWith(questStartHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("qcomplete")
                    .WithDescription("Force-completes an active quest for a player.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("questId"), sapi.ChatCommands.Parsers.Word("playerName"))
                    .HandleWith(questCompleteHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("qcompleteactive")
                    .WithDescription("Force-completes the player's currently active quest.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                    .HandleWith(questCompleteActiveHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("qca")
                    .WithDescription("Alias for qcompleteactive.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                    .HandleWith(questCompleteActiveHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("qforgive")
                    .WithDescription("Resets a quest for a player: removes it from active quests and clears cooldown/completed flags.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.Word("modeOrQuestId"),
                        sapi.ChatCommands.Parsers.OptionalWord("playerName")
                    )
                    .HandleWith(forgiveQuestHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("qfa")
                    .WithDescription("Alias for qforgive active.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                    .HandleWith(questForgiveActiveAliasHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("qfall")
                    .WithDescription("Alias for qforgive all.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                    .HandleWith(questForgiveAllAliasHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("nforgive")
                    .WithDescription("Removes all note entries from the journal for a player.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                    .HandleWith(questNoteForgiveHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("exec")
                    .WithDescription("Executes an action string (ActionStringExecutor) on a player. If no player is given, uses the caller.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                        sapi.ChatCommands.Parsers.All("actionString")
                    )
                    .HandleWith(questExecActionStringHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("bosshunt")
                    .WithDescription("Bosshunt admin commands")
                    .RequiresPrivilege(Privilege.give)
                    .BeginSubCommand("skip")
                        .WithDescription("Force-rotates the active bosshunt target to the next entry.")
                        .RequiresPrivilege(Privilege.give)
                        .HandleWith(bossHuntSkipHandler.Handle)
                    .EndSubCommand()
                    .BeginSubCommand("status")
                        .WithDescription("Shows the current bosshunt target and time until rotation.")
                        .RequiresPrivilege(Privilege.give)
                        .HandleWith(bossHuntStatusHandler.Handle)
                    .EndSubCommand()
                .EndSubCommand()
                .BeginSubCommand("ai")
                    .WithDescription("Action item durability tools")
                    .RequiresPrivilege(Privilege.give)
                    .BeginSubCommand("repair")
                        .WithDescription("Repair held item to max durability.")
                        .RequiresPrivilege(Privilege.give)
                        .HandleWith(actionItemDurabilityHandler.Repair)
                    .EndSubCommand()
                    .BeginSubCommand("destruct")
                        .WithDescription("Damage held item by a value.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(sapi.ChatCommands.Parsers.Int("amount"))
                        .HandleWith(actionItemDurabilityHandler.Destruct)
                    .EndSubCommand()
                .EndSubCommand()
                ;
        }
    }
}
