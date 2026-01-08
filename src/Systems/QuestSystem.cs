using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using vsquest.src.Systems.Actions;

namespace VsQuest
{
    public delegate void QuestAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args);
    public class QuestSystem : ModSystem
    {
        public Dictionary<string, Quest> QuestRegistry { get; private set; } = new Dictionary<string, Quest>();
        public Dictionary<string, QuestAction> ActionRegistry { get; private set; } = new Dictionary<string, QuestAction>();
        public Dictionary<string, ActiveActionObjective> ActionObjectiveRegistry { get; private set; } = new Dictionary<string, ActiveActionObjective>();
        
        private QuestPersistenceManager persistenceManager;
        private QuestLifecycleManager lifecycleManager;
        private QuestEventHandler eventHandler;
        private QuestActionRegistry actionRegistry;
        private QuestObjectiveRegistry objectiveRegistry;
        
        public QuestConfig Config { get; set; }
        private ICoreAPI api;
        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);

            var harmony = new HarmonyLib.Harmony("vsquest");
            harmony.PatchAll();

            api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));
            api.RegisterItemClass("ItemDebugTool", typeof(ItemDebugTool));

            // Register objectives
            objectiveRegistry = new QuestObjectiveRegistry(ActionObjectiveRegistry, api);
            objectiveRegistry.RegisterObjectives();

            try
            {
                Config = api.LoadModConfig<QuestConfig>("questconfig.json");
                if (Config != null)
                {
                    api.Logger.Notification("Mod Config successfully loaded.");
                }
                else
                {
                    api.Logger.Notification("No Mod Config specified. Falling back to default settings");
                    Config = new QuestConfig();
                }
            }
            catch
            {
                Config = new QuestConfig();
                api.Logger.Error("Failed to load custom mod configuration. Falling back to default settings!");
            }
            finally
            {
                api.StoreModConfig(Config, "questconfig.json");
            }
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);

            capi.Network.RegisterChannel("vsquest")
                .RegisterMessageType<QuestAcceptedMessage>()
                .RegisterMessageType<QuestCompletedMessage>()
                .RegisterMessageType<QuestInfoMessage>().SetMessageHandler<QuestInfoMessage>(message => OnQuestInfoMessage(message, capi))
                .RegisterMessageType<ExecutePlayerCommandMessage>().SetMessageHandler<ExecutePlayerCommandMessage>(message => OnExecutePlayerCommand(message, capi))
                .RegisterMessageType<VanillaBlockInteractMessage>()
                .RegisterMessageType<ShowNotificationMessage>().SetMessageHandler<ShowNotificationMessage>(message => OnShowNotificationMessage(message, capi))
                .RegisterMessageType<ShowQuestDialogMessage>().SetMessageHandler<ShowQuestDialogMessage>(message => OnShowQuestDialogMessage(message, capi));
        }

        private void OnShowNotificationMessage(ShowNotificationMessage message, ICoreClientAPI capi)
        {
            string text = message?.Notification;
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    if (Lang.HasTranslation(text)) text = Lang.Get(text);
                }
                catch
                {
                }
            }

            capi.ShowChatMessage(text);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            // Initialize managers
            persistenceManager = new QuestPersistenceManager(sapi);
            lifecycleManager = new QuestLifecycleManager(QuestRegistry, ActionRegistry, api);
            eventHandler = new QuestEventHandler(QuestRegistry, persistenceManager, sapi);

            sapi.Network.RegisterChannel("vsquest")
                .RegisterMessageType<QuestAcceptedMessage>().SetMessageHandler<QuestAcceptedMessage>((player, message) => OnQuestAccepted(player, message, sapi))
                .RegisterMessageType<QuestCompletedMessage>().SetMessageHandler<QuestCompletedMessage>((player, message) => OnQuestCompleted(player, message, sapi))
                .RegisterMessageType<QuestInfoMessage>()
                .RegisterMessageType<ExecutePlayerCommandMessage>()
                .RegisterMessageType<VanillaBlockInteractMessage>().SetMessageHandler<VanillaBlockInteractMessage>((player, message) => OnVanillaBlockInteract(player, message, sapi))
                .RegisterMessageType<ShowNotificationMessage>()
                .RegisterMessageType<ShowQuestDialogMessage>();

            // Register actions
            actionRegistry = new QuestActionRegistry(ActionRegistry, api);
            actionRegistry.RegisterActions(sapi, OnQuestAccepted);
            
            eventHandler.RegisterEventHandlers();

            // Register chat commands
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

        public bool ForgiveQuest(IServerPlayer player, string questId)
        {
            var quests = persistenceManager.GetPlayerQuests(player.PlayerUID);
            var activeQuest = quests.Find(q => q.questId == questId);
            bool removed = false;

            if (activeQuest != null)
            {
                quests.Remove(activeQuest);
                removed = true;
            }

            // Clear per-player cooldown marker
            var key = string.Format("vsquest:lastaccepted-{0}", questId);
            if (player.Entity?.WatchedAttributes != null)
            {
                player.Entity.WatchedAttributes.RemoveAttribute(key);
                player.Entity.WatchedAttributes.MarkPathDirty(key);

                // Clear completion flag
                var completed = player.Entity.WatchedAttributes.GetStringArray("vsquest:playercompleted", new string[0]);
                if (completed != null && completed.Length > 0)
                {
                    var filtered = completed.Where(id => id != questId).ToArray();
                    if (filtered.Length != completed.Length)
                    {
                        player.Entity.WatchedAttributes.SetStringArray("vsquest:playercompleted", filtered);
                        player.Entity.WatchedAttributes.MarkAllDirty();
                    }
                }
            }

            persistenceManager.SavePlayerQuests(player.PlayerUID, quests);
            return removed;
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);
            foreach (var mod in api.ModLoader.Mods)
            {
                api.Assets
                    .GetMany<List<Quest>>(api.Logger, "config/quests", mod.Info.ModID)
                    .SelectMany(pair => pair.Value)
                    .Foreach(quest => QuestRegistry.Add(quest.id, quest));
            }
        }

        public List<ActiveQuest> GetPlayerQuests(string playerUID)
        {
            return persistenceManager.GetPlayerQuests(playerUID);
        }

        private void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi)
        {
            lifecycleManager.OnQuestAccepted(fromPlayer, message, sapi, GetPlayerQuests);
        }

        public void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            lifecycleManager.OnQuestCompleted(fromPlayer, message, sapi, GetPlayerQuests);
        }

        private void OnQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            new QuestSelectGui(capi, message.questGiverId, message.availableQestIds, message.activeQuests, Config).TryOpen();
        }

        private void OnExecutePlayerCommand(ExecutePlayerCommandMessage message, ICoreClientAPI capi)
        {
            string command = message.Command;

            if (command.StartsWith("."))
            {
                capi.TriggerChatMessage(command);
            }
            else
            {
                capi.SendChatMessage(command);
            }
        }

        private void OnVanillaBlockInteract(IServerPlayer player, VanillaBlockInteractMessage message, ICoreServerAPI sapi)
        {
            int[] position = new int[] { message.Position.X, message.Position.Y, message.Position.Z };
            GetPlayerQuests(player?.PlayerUID).ForEach(quest => quest.OnBlockUsed(message.BlockCode, position, player, sapi));
        }

        private void OnShowQuestDialogMessage(ShowQuestDialogMessage message, ICoreClientAPI capi)
        {
            new QuestFinalDialogGui(capi, message.TitleLangKey, message.TextLangKey, message.Option1LangKey, message.Option2LangKey).TryOpen();
        }
    }

    public class QuestConfig
    {
        public bool CloseGuiAfterAcceptingAndCompleting = true;
    }

    [ProtoContract]
    public class QuestAcceptedMessage : QuestMessage
    {
    }

    [ProtoContract]
    public class QuestCompletedMessage : QuestMessage
    {
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [ProtoInclude(10, typeof(QuestAcceptedMessage))]
    [ProtoInclude(11, typeof(QuestCompletedMessage))]
    public abstract class QuestMessage
    {
        public string questId { get; set; }

        public long questGiverId { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuestInfoMessage
    {
        public long questGiverId { get; set; }
        public List<string> availableQestIds { get; set; }
        public List<ActiveQuest> activeQuests { get; set; }
    }

    [ProtoContract]
    public class ExecutePlayerCommandMessage
    {
        [ProtoMember(1)]
        public string Command { get; set; }
    }

    [ProtoContract]
    public class ShowNotificationMessage
    {
        [ProtoMember(1)]
        public string Notification { get; set; }
    }

    [ProtoContract]
    public class ShowQuestDialogMessage
    {
        [ProtoMember(1)]
        public string TitleLangKey { get; set; }

        [ProtoMember(2)]
        public string TextLangKey { get; set; }

        [ProtoMember(3)]
        public string Option1LangKey { get; set; }

        [ProtoMember(4)]
        public string Option2LangKey { get; set; }
    }
}