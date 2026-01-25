using Newtonsoft.Json;
using ProtoBuf;
using Vintagestory.API.Datastructures;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using System;

namespace VsQuest
{
    public class ItemSystem : ModSystem
    {
        private const float ActionItemCastDurationSec = 3f;
        private const float ActionItemCastSlowdown = -0.5f;
        private const string ActionItemCastSpeedStatKey = "alegacyvsquest:actionitemcast";
        private const string BloodmeterActionItemId = "albase:bosshunt-tracker";

        private ICoreAPI api;
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private QuestSystem questSystem;

        private IClientNetworkChannel clientChannel;
        private IServerNetworkChannel serverChannel;

        private long inventoryScanListenerId = 0;
        private long hotbarEnforceListenerId = 0;

        private ActionItemAttributeResolver attributeResolver;
        private ActionItemCastController castController;
        private ActionItemInputHandler inputHandler;

        private ActionItemCreativeTabInjector creativeTabInjector;
        private ActionItemHotbarEnforcer hotbarEnforcer;
        private ActionItemInventoryScanner inventoryScanner;
        private ActionItemActionExecutor actionExecutor;
        private ActionItemPacketHandler packetHandler;
        private ActionItemSoundConfig soundConfig;

        private readonly Dictionary<string, (string invKey, int slot)> inventoryScanCursorByPlayerUid = new Dictionary<string, (string invKey, int slot)>(StringComparer.Ordinal);

        public Dictionary<string, ActionItem> ActionItemRegistry { get; private set; } = new Dictionary<string, ActionItem>();

        public override void StartPre(ICoreAPI api)
        {
            this.api = api;
            questSystem = api.ModLoader.GetModSystem<QuestSystem>();
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            if (api == null) return;

            foreach (var mod in api.ModLoader.Mods)
            {
                var assets = api.Assets.GetMany<ItemConfig>(api.Logger, "config/itemconfig", mod.Info.ModID);
                foreach (var asset in assets)
                {
                    if (asset.Value != null)
                    {
                        foreach (var actionItem in asset.Value.actionItems)
                        {
                            if (actionItem?.id != null && actionItem.id.StartsWith("albase:", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            ActionItemRegistry[actionItem.id] = actionItem;
                        }
                    }
                }
            }
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            // Must run on both sides: creative tab list is synced/validated server-side when moving items.
            if (api == null) return;
            if (ActionItemRegistry == null || ActionItemRegistry.Count == 0) return;
            if (creativeTabInjector == null)
            {
                creativeTabInjector = new ActionItemCreativeTabInjector(ActionItemRegistry, questSystem);
            }

            creativeTabInjector.Inject(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            if (ActionItemRegistry == null || ActionItemRegistry.Count == 0) return;

            attributeResolver = new ActionItemAttributeResolver(ActionItemRegistry);
            actionExecutor = new ActionItemActionExecutor(questSystem, sapi);
            packetHandler = new ActionItemPacketHandler(questSystem, attributeResolver, actionExecutor);

            serverChannel = api.Network.RegisterChannel("alegacyvsquest-itemaction")
                .RegisterMessageType<ExecuteActionItemPacket>()
                .SetMessageHandler<ExecuteActionItemPacket>(packetHandler.HandlePacket);
            hotbarEnforcer = new ActionItemHotbarEnforcer(api);
            inventoryScanner = new ActionItemInventoryScanner(
                api,
                questSystem,
                attributeResolver,
                inventoryScanCursorByPlayerUid,
                actionExecutor.Execute);

            // Periodically scan inventories for action items that should trigger when added.
            // This avoids relying on right click and allows one-time processing.
            inventoryScanListenerId = api.Event.RegisterGameTickListener(OnInventoryScanTick, 1000);

            // Periodically enforce hotbar-only placement for special quest items (blockEquip action items).
            hotbarEnforceListenerId = api.Event.RegisterGameTickListener(OnHotbarEnforceTick, 500);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            if (ActionItemRegistry == null || ActionItemRegistry.Count == 0) return;
            clientChannel = api.Network.RegisterChannel("alegacyvsquest-itemaction")
                .RegisterMessageType<ExecuteActionItemPacket>();

            attributeResolver = new ActionItemAttributeResolver(ActionItemRegistry);
            soundConfig = new ActionItemSoundConfig();
            castController = new ActionItemCastController(
                capi,
                clientChannel,
                attributeResolver,
                BloodmeterActionItemId,
                ActionItemCastDurationSec,
                ActionItemCastSlowdown,
                ActionItemCastSpeedStatKey,
                soundConfig.CastLoopSound,
                soundConfig.CastCompleteSound,
                soundConfig.CastSoundVolume,
                soundConfig.CastSoundRange,
                soundConfig.CastCompleteSoundRange,
                soundConfig.CastCompleteSoundVolume);

            inputHandler = new ActionItemInputHandler(capi, clientChannel, attributeResolver, castController);

            api.Event.MouseDown += inputHandler.OnMouseDown;
            api.Event.MouseUp += inputHandler.OnMouseUp;
            api.Event.RegisterGameTickListener(inputHandler.OnClientTick, 20);

            api.Event.BlockTexturesLoaded += () =>
            {
                if (creativeTabInjector == null)
                {
                    creativeTabInjector = new ActionItemCreativeTabInjector(ActionItemRegistry, questSystem);
                }

                creativeTabInjector.Inject(api);
            };
        }

        private void OnHotbarEnforceTick(float dt)
        {
            hotbarEnforcer?.Tick(dt);
        }

        private void OnInventoryScanTick(float dt)
        {
            inventoryScanner?.Tick(dt);
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ExecuteActionItemPacket
    {
    }
}