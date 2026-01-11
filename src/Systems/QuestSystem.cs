using HarmonyLib;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public class QuestSystem : ModSystem
    {
        public Dictionary<string, Quest> QuestRegistry { get; private set; } = new Dictionary<string, Quest>();
        public Dictionary<string, IQuestAction> ActionRegistry { get; private set; } = new Dictionary<string, IQuestAction>();
        public Dictionary<string, ActionObjectiveBase> ActionObjectiveRegistry { get; private set; } = new Dictionary<string, ActionObjectiveBase>();

        private QuestPersistenceManager persistenceManager;
        private QuestLifecycleManager lifecycleManager;
        private QuestEventHandler eventHandler;
        private QuestActionRegistry actionRegistry;
        private QuestObjectiveRegistry objectiveRegistry;
        private QuestNetworkChannelRegistry networkChannelRegistry;
        private QuestChatCommandRegistry chatCommandRegistry;

        public QuestConfig Config { get; set; }
        private ICoreAPI api;
        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);

            var harmony = new HarmonyLib.Harmony("vsquest");
            harmony.PatchAll();

            VsQuest.Harmony.EntityInteractPatch.TryPatch(harmony);

            api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));
            api.RegisterItemClass("ItemDebugTool", typeof(ItemDebugTool));
            api.RegisterItemClass("ItemEntitySpawner", typeof(ItemEntitySpawner));

            // Register objectives
            objectiveRegistry = new QuestObjectiveRegistry(ActionObjectiveRegistry, api);
            objectiveRegistry.Register();

            networkChannelRegistry = new QuestNetworkChannelRegistry(this);

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

            networkChannelRegistry.RegisterClient(capi);
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
            text = NotificationTextUtil.Build(message, capi.Logger);

            capi.ShowChatMessage(text);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            sapi.Logger.VerboseDebug($"[vsquest] QuestSystem.StartServerSide loaded ({DateTime.UtcNow:O})");

            // Initialize managers
            persistenceManager = new QuestPersistenceManager(sapi);
            lifecycleManager = new QuestLifecycleManager(QuestRegistry, ActionRegistry, api);
            eventHandler = new QuestEventHandler(QuestRegistry, persistenceManager, sapi);

            networkChannelRegistry.RegisterServer(sapi);

            // Register actions
            actionRegistry = new QuestActionRegistry(ActionRegistry, api, sapi, OnQuestAccepted);
            actionRegistry.Register();

            eventHandler.RegisterEventHandlers();

            chatCommandRegistry = new QuestChatCommandRegistry(sapi, api, this);
            chatCommandRegistry.Register();
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            LocalizationUtils.LoadFromAssets(api);
            foreach (var mod in api.ModLoader.Mods)
            {
                var questAssets = api.Assets.GetMany<Quest>(api.Logger, "config/quests", mod.Info.ModID);
                foreach (var questAsset in questAssets)
                {
                    try
                    {
                        if (questAsset.Value != null && !QuestRegistry.ContainsKey(questAsset.Value.id))
                        {
                            QuestRegistry.Add(questAsset.Value.id, questAsset.Value);
                        }
                    }
                    catch (Exception e)
                    {
                        api.Logger.Error($"Failed to load quest from {questAsset.Key}: {e.Message}");
                    }
                }
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

        private QuestSelectGui questSelectGui;

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
            if (questSelectGui == null)
            {
                questSelectGui = CreateQuestSelectGui(message, capi);
                questSelectGui.TryOpen();
                return;
            }

            if (questSelectGui.IsOpened())
            {
                questSelectGui.UpdateFromMessage(message);
                return;
            }

            questSelectGui = CreateQuestSelectGui(message, capi);
            questSelectGui.TryOpen();
        }

        private QuestSelectGui CreateQuestSelectGui(QuestInfoMessage message, ICoreClientAPI capi)
        {
            var gui = new QuestSelectGui(capi, message.questGiverId, message.availableQestIds, message.activeQuests, Config, message.noAvailableQuestDescLangKey, message.noAvailableQuestCooldownDescLangKey, message.noAvailableQuestCooldownDaysLeft);
            gui.OnClosed += () =>
            {
                if (questSelectGui != null && !questSelectGui.IsOpened())
                {
                    questSelectGui = null;
                }
            };
            return gui;
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
        public bool CloseGuiAfterAcceptingAndCompleting { get; set; } = true;
        public string defaultObjectiveCompletionSound { get; set; } = "survival:sounds/tutorialstepsuccess";
    }
}