using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;

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

        private VsQuestDiscoveryHud discoveryHud;
        private QuestJournalHotkeyHandler journalHotkeyHandler;
        private QuestSelectGuiManager questSelectGuiManager;
        private QuestNotificationHandler notificationHandler;

        public override void StartPre(ICoreAPI api)
        {
            this.api = api;
            base.StartPre(api);
            ModClassRegistry.RegisterAll(api);
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            MobLocalizationUtils.LoadFromAssets(api);

            var harmony = new HarmonyLib.Harmony("alegacyvsquest");
            harmony.PatchAll();

            LocalizationUtils.LoadFromAssets(api);

            VsQuest.Harmony.EntityInteractPatch.TryPatch(harmony);

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

            if (networkChannelRegistry == null)
            {
                capi.Logger.Error("[alegacyvsquest] networkChannelRegistry was null in StartClientSide(). Recreating it (mod may have had an earlier startup error).");
                networkChannelRegistry = new QuestNetworkChannelRegistry(this);
            }

            networkChannelRegistry.RegisterClient(capi);

            journalHotkeyHandler = new QuestJournalHotkeyHandler(capi);
            journalHotkeyHandler.Register();

            capi.RegisterVtmlTagConverter("qhover", (clientApi, token, fontStack, onClick) =>
            {
                if (token == null) return null;

                string displayText = token.ContentText;
                string hoverText = null;
                if (token.Attributes != null && token.Attributes.TryGetValue("text", out var attrText))
                {
                    hoverText = attrText;
                }

                if (string.IsNullOrWhiteSpace(hoverText))
                {
                    return new RichTextComponent(clientApi, displayText, fontStack.Peek());
                }

                return new RichTextComponentQuestHover(clientApi, displayText, hoverText, fontStack.Peek());
            });

            try
            {
                discoveryHud = new VsQuestDiscoveryHud(capi);
            }
            catch
            {
                discoveryHud = null;
            }

            notificationHandler = new QuestNotificationHandler(discoveryHud);
            questSelectGuiManager = new QuestSelectGuiManager(Config);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            sapi.Logger.VerboseDebug($"[alegacyvsquest] QuestSystem.StartServerSide loaded ({DateTime.UtcNow:O})");

            persistenceManager = new QuestPersistenceManager(sapi);
            lifecycleManager = new QuestLifecycleManager(QuestRegistry, ActionRegistry, api);
            eventHandler = new QuestEventHandler(QuestRegistry, persistenceManager, sapi);

            if (networkChannelRegistry == null)
            {
                sapi.Logger.Error("[alegacyvsquest] networkChannelRegistry was null in StartServerSide(). Recreating it (mod may have had an earlier startup error).");
                networkChannelRegistry = new QuestNetworkChannelRegistry(this);
            }

            networkChannelRegistry.RegisterServer(sapi);

            actionRegistry = new QuestActionRegistry(ActionRegistry, api, sapi, OnQuestAccepted);
            actionRegistry.Register();

            eventHandler.RegisterEventHandlers();

            chatCommandRegistry = new QuestChatCommandRegistry(sapi, api, this);
            chatCommandRegistry.Register();
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            MobLocalizationUtils.LoadFromAssets(api);

            LocalizationUtils.LoadFromAssets(api);
            foreach (var mod in api.ModLoader.Mods)
            {
                var questAssets = api.Assets.GetMany<Quest>(api.Logger, "config/quests", mod.Info.ModID);
                foreach (var questAsset in questAssets)
                {
                    try
                    {
                        TryRegisterQuest(api, questAsset.Value, questAsset.Key);
                    }
                    catch (Exception e)
                    {
                        api.Logger.Error($"Failed to load quest from {questAsset.Key}: {e.Message}");
                    }
                }

                LoadQuestAssetsFromFile(api, mod.Info.ModID);
            }
        }

        private void LoadQuestAssetsFromFile(ICoreAPI api, string domain)
        {
            if (api == null || string.IsNullOrWhiteSpace(domain)) return;

            var assets = api.Assets;
            var asset = assets.TryGet(new AssetLocation(domain, "config/quests.json"))
                ?? assets.TryGet(new AssetLocation(domain, "config/quest.json"));

            if (asset == null) return;

            try
            {
                var root = asset.ToObject<JsonObject>();
                if (root == null) return;

                if (root.IsArray())
                {
                    var array = root.AsArray();
                    if (array == null) return;

                    foreach (var entry in array)
                    {
                        if (entry == null || !entry.Exists) continue;
                        var quest = entry.AsObject<Quest>();
                        TryRegisterQuest(api, quest, asset.Location?.ToString() ?? "config/quests.json");
                    }

                    return;
                }

                var singleQuest = root.AsObject<Quest>();
                TryRegisterQuest(api, singleQuest, asset.Location?.ToString() ?? "config/quests.json");
            }
            catch (Exception e)
            {
                api.Logger.Error($"Failed to load quests from {asset.Location}: {e.Message}");
            }
        }

        private void TryRegisterQuest(ICoreAPI api, Quest quest, string source)
        {
            if (quest == null) return;
            if (string.IsNullOrWhiteSpace(quest.id)) return;

            if (QuestRegistry.ContainsKey(quest.id)) return;

            QuestRegistry.Add(quest.id, quest);
        }

        public List<ActiveQuest> GetPlayerQuests(string playerUID)
        {
            var quests = persistenceManager.GetPlayerQuests(playerUID);
            if (quests == null || quests.Count == 0) return quests;

            bool changed = false;
            foreach (var quest in quests)
            {
                if (quest == null || string.IsNullOrWhiteSpace(quest.questId)) continue;

                string normalized = QuestJournalMigration.NormalizeQuestId(quest.questId, QuestRegistry);
                if (!string.Equals(normalized, quest.questId, StringComparison.OrdinalIgnoreCase))
                {
                    quest.questId = normalized;
                    changed = true;
                }
            }

            if (changed)
            {
                persistenceManager.SavePlayerQuests(playerUID, quests);
            }

            return quests;
        }

        public void SavePlayerQuests(string playerUID, List<ActiveQuest> activeQuests)
        {
            persistenceManager.SavePlayerQuests(playerUID, activeQuests);
        }

        internal string NormalizeQuestId(string questId)
        {
            return QuestJournalMigration.NormalizeQuestId(questId, QuestRegistry);
        }

        internal string[] GetNormalizedCompletedQuestIds(IPlayer player)
        {
            return QuestJournalMigration.GetNormalizedCompletedQuestIds(player, QuestRegistry);
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
            questSelectGuiManager.HandleQuestInfoMessage(message, capi);
        }

        internal void OnShowNotificationMessage(ShowNotificationMessage message, ICoreClientAPI capi)
        {
            notificationHandler.HandleNotificationMessage(message, capi);
        }

        internal void OnShowDiscoveryMessage(ShowDiscoveryMessage message, ICoreClientAPI capi)
        {
            notificationHandler.HandleDiscoveryMessage(message, capi);
        }

        internal void OnExecutePlayerCommand(ExecutePlayerCommandMessage message, ICoreClientAPI capi)
        {
            ClientCommandExecutor.Execute(message, capi);
        }

        internal void OnVanillaBlockInteract(IServerPlayer player, VanillaBlockInteractMessage message, ICoreServerAPI sapi)
        {
            eventHandler.HandleVanillaBlockInteract(player, message);
        }

        internal void OnShowQuestDialogMessage(ShowQuestDialogMessage message, ICoreClientAPI capi)
        {
            QuestFinalDialogGui.ShowFromMessage(message, capi);
        }

        internal void OnPreloadBossMusicMessage(PreloadBossMusicMessage message, ICoreClientAPI capi)
        {
            try
            {
                var sys = capi?.ModLoader?.GetModSystem<BossMusicUrlSystem>();
                sys?.Preload(message?.Url);
            }
            catch
            {
            }
        }
    }
}