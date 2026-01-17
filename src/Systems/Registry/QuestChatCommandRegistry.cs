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

            var questFxHandler = new QuestFxCommandHandler(sapi);

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

            var bossHuntSkipHandler = new BossHuntSkipCommandHandler(sapi);
            var bossHuntStatusHandler = new BossHuntStatusCommandHandler(sapi);

            var questWAttrHandler = new QuestWAttrCommandHandler(sapi);

            var questStartHandler = new QuestStartCommandHandler(sapi, questSystem);

            var questExecActionStringHandler = new QuestExecActionStringCommandHandler(sapi);

            var questEntityHandler = new QuestEntityCommandHandler(sapi, questSystem);

            sapi.ChatCommands.GetOrCreate("quest")
                .WithDescription("Quest administration commands")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("fx")
                    .WithDescription("Particle FX debug tools")
                    .RequiresPrivilege(Privilege.give)
                    .BeginSubCommand("list")
                        .WithDescription("Lists loaded particle FX preset IDs.")
                        .RequiresPrivilege(Privilege.give)
                        .HandleWith(questFxHandler.List)
                    .EndSubCommand()
                    .BeginSubCommand("select")
                        .WithDescription("Selects a preset for use with action-items or spawnselected.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("presetId"), sapi.ChatCommands.Parsers.OptionalWord("scope"))
                        .HandleWith(questFxHandler.Select)
                    .EndSubCommand()
                    .BeginSubCommand("spawn")
                        .WithDescription("Spawns a preset at the caller's position. Optional: count, radiusTimes10.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(
                            sapi.ChatCommands.Parsers.Word("presetId"),
                            sapi.ChatCommands.Parsers.OptionalInt("count", 0),
                            sapi.ChatCommands.Parsers.OptionalInt("radiusTimes10", 0)
                        )
                        .HandleWith(questFxHandler.Spawn)
                    .EndSubCommand()
                    .BeginSubCommand("spawnselected")
                        .WithDescription("Spawns the selected preset at the caller's position. Optional: count, radiusTimes10.")
                        .RequiresPrivilege(Privilege.give)
                        .WithArgs(
                            sapi.ChatCommands.Parsers.OptionalWord("scope"),
                            sapi.ChatCommands.Parsers.OptionalInt("count", 0),
                            sapi.ChatCommands.Parsers.OptionalInt("radiusTimes10", 0)
                        )
                        .HandleWith(questFxHandler.SpawnSelected)
                    .EndSubCommand()
                .EndSubCommand()
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
                ;
        }
    }
}
