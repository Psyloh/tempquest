using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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

            QuestNetworkChannelRegistry.RegisterClient(capi, this);
        }

        internal void OnShowNotificationMessage(ShowNotificationMessage message, ICoreClientAPI capi)
        {
            if (message == null)
            {
                capi.ShowChatMessage(null);
                return;
            }

            string text = null;

            // Preferred path: server sends template + mob code, client localizes mob name in its own language.
            text = NotificationTextUtil.Build(message);

            capi.ShowChatMessage(text);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            // Initialize managers
            persistenceManager = new QuestPersistenceManager(sapi);
            lifecycleManager = new QuestLifecycleManager(QuestRegistry, ActionRegistry, api);
            eventHandler = new QuestEventHandler(QuestRegistry, persistenceManager, sapi);

            QuestNetworkChannelRegistry.RegisterServer(sapi, this);

            // Register actions
            actionRegistry = new QuestActionRegistry(ActionRegistry, api);
            actionRegistry.RegisterActions(sapi, OnQuestAccepted);
            
            eventHandler.RegisterEventHandlers();

            QuestChatCommandRegistry.Register(sapi, api, this);
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            MobLocalizationUtils.LoadFromAssets(api);
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

        public void SavePlayerQuests(string playerUID, List<ActiveQuest> activeQuests)
        {
            persistenceManager.SavePlayerQuests(playerUID, activeQuests);
        }

        internal bool ForceCompleteQuestInternal(IServerPlayer player, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            return lifecycleManager.ForceCompleteQuest(player, message, sapi, GetPlayerQuests);
        }

        internal void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi)
        {
            lifecycleManager.OnQuestAccepted(fromPlayer, message, sapi, GetPlayerQuests);
        }

        internal void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            lifecycleManager.OnQuestCompleted(fromPlayer, message, sapi, GetPlayerQuests);
        }

        internal void OnQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            new QuestSelectGui(capi, message.questGiverId, message.availableQestIds, message.activeQuests, Config, message.noAvailableQuestDescLangKey, message.noAvailableQuestCooldownDescLangKey, message.noAvailableQuestCooldownDaysLeft).TryOpen();
        }

        internal void OnExecutePlayerCommand(ExecutePlayerCommandMessage message, ICoreClientAPI capi)
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

        internal void OnVanillaBlockInteract(IServerPlayer player, VanillaBlockInteractMessage message, ICoreServerAPI sapi)
        {
            int[] position = new int[] { message.Position.X, message.Position.Y, message.Position.Z };
            GetPlayerQuests(player?.PlayerUID).ForEach(quest => quest.OnBlockUsed(message.BlockCode, position, player, sapi));
        }

        internal void OnShowQuestDialogMessage(ShowQuestDialogMessage message, ICoreClientAPI capi)
        {
            new QuestFinalDialogGui(capi, message.TitleLangKey, message.TextLangKey, message.Option1LangKey, message.Option2LangKey).TryOpen();
        }
    }

    public class QuestConfig
    {
        public bool CloseGuiAfterAcceptingAndCompleting = true;
    }
}