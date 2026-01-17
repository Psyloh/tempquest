using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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
        private QuestJournalGui questJournalGui;
        private const string JournalHotkeyCode = "alegacyvsquest-journal";

        public override void StartPre(ICoreAPI api)
        {
            this.api = api;
            base.StartPre(api);

            api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));
            api.RegisterEntityBehaviorClass("questtarget", typeof(EntityBehaviorQuestTarget));
            api.RegisterEntityBehaviorClass("bossnametag", typeof(EntityBehaviorBossNameTag));
            api.RegisterEntityBehaviorClass("bossrespawn", typeof(EntityBehaviorBossRespawn));
            api.RegisterEntityBehaviorClass("bossdespair", typeof(EntityBehaviorBossDespair));
            api.RegisterEntityBehaviorClass("bosshuntcombatmarker", typeof(EntityBehaviorBossHuntCombatMarker));
            api.RegisterEntityBehaviorClass("bosssummonritual", typeof(EntityBehaviorBossSummonRitual));
            api.RegisterEntityBehaviorClass("bossgrowthritual", typeof(EntityBehaviorBossGrowthRitual));
            api.RegisterEntityBehaviorClass("bossrebirth", typeof(EntityBehaviorBossRebirth));
            api.RegisterEntityBehaviorClass("shiverdebug", typeof(EntityBehaviorShiverDebug));

            api.RegisterItemClass("ItemDebugTool", typeof(ItemDebugTool));
            api.RegisterItemClass("ItemEntitySpawner", typeof(ItemEntitySpawner));

            api.RegisterBlockClass("BlockCooldownPlaceholder", typeof(BlockCooldownPlaceholder));
            api.RegisterBlockEntityClass("CooldownPlaceholder", typeof(BlockEntityCooldownPlaceholder));

            api.RegisterBlockClass("BlockQuestSpawner", typeof(BlockQuestSpawner));
            api.RegisterBlockEntityClass("QuestSpawner", typeof(BlockEntityQuestSpawner));

            api.RegisterBlockClass("BlockBossHuntAnchor", typeof(BlockBossHuntAnchor));
            api.RegisterBlockEntityClass("BossHuntAnchor", typeof(BlockEntityBossHuntAnchor));
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            MobLocalizationUtils.LoadFromAssets(api);

            var harmony = new HarmonyLib.Harmony("alegacyvsquest");
            harmony.PatchAll();

            LocalizationUtils.LoadFromAssets(api);

            VsQuest.Harmony.EntityInteractPatch.TryPatch(harmony);

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

            if (networkChannelRegistry == null)
            {
                capi.Logger.Error("[alegacyvsquest] networkChannelRegistry was null in StartClientSide(). Recreating it (mod may have had an earlier startup error).");
                networkChannelRegistry = new QuestNetworkChannelRegistry(this);
            }

            networkChannelRegistry.RegisterClient(capi);

            capi.Input.RegisterHotKey(JournalHotkeyCode, Lang.Get("alegacyvsquest:hotkey-journal"), GlKeys.N, HotkeyType.GUIOrOtherControls);

            capi.Input.SetHotKeyHandler(JournalHotkeyCode, _ =>
            {
                ToggleQuestJournalGui(capi);
                return true;
            });

            try
            {
                discoveryHud = new VsQuestDiscoveryHud(capi);
            }
            catch
            {
                discoveryHud = null;
            }
        }

        private void ToggleQuestJournalGui(ICoreClientAPI capi)
        {
            if (questJournalGui == null)
            {
                questJournalGui = new QuestJournalGui(capi);
                questJournalGui.OnClosed += () =>
                {
                    if (questJournalGui != null && !questJournalGui.IsOpened())
                    {
                        questJournalGui = null;
                    }
                };
                questJournalGui.TryOpen();
                return;
            }

            if (questJournalGui.IsOpened())
            {
                questJournalGui.TryClose();
                return;
            }

            questJournalGui.Refresh();
            questJournalGui.TryOpen();
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

        internal void OnShowDiscoveryMessage(ShowDiscoveryMessage message, ICoreClientAPI capi)
        {
            if (message == null)
            {
                return;
            }

            string text = NotificationTextUtil.Build(new ShowNotificationMessage
            {
                Notification = message.Notification,
                Template = message.Template,
                Need = message.Need,
                MobCode = message.MobCode
            }, capi.Logger);

            if (discoveryHud != null)
            {
                discoveryHud.Show(text);
                return;
            }

            capi.TriggerIngameDiscovery(this, "alegacyvsquest", text);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            sapi.Logger.VerboseDebug($"[alegacyvsquest] QuestSystem.StartServerSide loaded ({DateTime.UtcNow:O})");

            // Initialize managers
            persistenceManager = new QuestPersistenceManager(sapi);
            lifecycleManager = new QuestLifecycleManager(QuestRegistry, ActionRegistry, api);
            eventHandler = new QuestEventHandler(QuestRegistry, persistenceManager, sapi);

            if (networkChannelRegistry == null)
            {
                sapi.Logger.Error("[alegacyvsquest] networkChannelRegistry was null in StartServerSide(). Recreating it (mod may have had an earlier startup error).");
                networkChannelRegistry = new QuestNetworkChannelRegistry(this);
            }

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

                string normalized = NormalizeQuestId(quest.questId);
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
            if (string.IsNullOrWhiteSpace(questId)) return questId;
            if (QuestRegistry == null) return questId;
            if (QuestRegistry.ContainsKey(questId)) return questId;

            const string legacyPrefix = "vsquest:";
            const string currentPrefix = "alegacyvsquest:";

            if (questId.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string mapped = currentPrefix + questId.Substring(legacyPrefix.Length);
                if (QuestRegistry.ContainsKey(mapped)) return mapped;
            }

            return questId;
        }

        internal string[] GetNormalizedCompletedQuestIds(IPlayer player)
        {
            var wa = player?.Entity?.WatchedAttributes;
            if (wa == null) return new string[0];

            var current = wa.GetStringArray("alegacyvsquest:playercompleted", new string[0]) ?? new string[0];
            var legacy = wa.GetStringArray("vsquest:playercompleted", null);

            var combined = new List<string>(current.Length + (legacy?.Length ?? 0));
            combined.AddRange(current);
            if (legacy != null) combined.AddRange(legacy);

            var normalizedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var questId in combined)
            {
                if (string.IsNullOrWhiteSpace(questId)) continue;
                normalizedSet.Add(NormalizeQuestId(questId));
            }

            var normalized = normalizedSet.ToArray();

            bool changed = legacy != null;
            if (!changed)
            {
                if (current.Length != normalized.Length)
                {
                    changed = true;
                }
                else
                {
                    var currentSet = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
                    foreach (var id in normalized)
                    {
                        if (!currentSet.Contains(id))
                        {
                            changed = true;
                            break;
                        }
                    }
                }
            }

            if (changed)
            {
                wa.SetStringArray("alegacyvsquest:playercompleted", normalized);
                wa.MarkPathDirty("alegacyvsquest:playercompleted");

                if (legacy != null)
                {
                    wa.RemoveAttribute("vsquest:playercompleted");
                    wa.MarkPathDirty("vsquest:playercompleted");
                }
            }

            return normalized;
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
            if (player == null || message == null)
            {
                return;
            }

            if (message?.BlockCode == "alegacyvsquest:cooldownplaceholder")
            {
                return;
            }

            int[] position = new int[] { message.Position.X, message.Position.Y, message.Position.Z };
            var playerQuests = GetPlayerQuests(player.PlayerUID);
            foreach (var quest in playerQuests.ToArray())
            {
                quest.OnBlockUsed(message.BlockCode, position, player, sapi);
            }
        }

        internal void OnShowQuestDialogMessage(ShowQuestDialogMessage message, ICoreClientAPI capi)
        {
            new QuestFinalDialogGui(capi, message.TitleLangKey, message.TextLangKey, message.Option1LangKey, message.Option2LangKey).TryOpen();
        }
    }

    public class QuestConfig
    {
        public bool CloseGuiAfterAcceptingAndCompleting { get; set; } = true;
        public string defaultObjectiveCompletionSound { get; set; } = "sounds/tutorialstepsuccess";
        public bool ShowCustomBossDeathMessage { get; set; } = false;
    }
}