using Newtonsoft.Json;
using ProtoBuf;
using Vintagestory.API.Datastructures;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class ItemSystem : ModSystem
    {
        private ICoreAPI api;
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private QuestSystem questSystem;
        private IClientNetworkChannel clientChannel;
        private IServerNetworkChannel serverChannel;

        private const string ActionItemsCreativeTabCode = "vsquest-actionitems";

        public Dictionary<string, ActionItem> ActionItemRegistry { get; private set; } = new Dictionary<string, ActionItem>();

        public override void StartPre(ICoreAPI api)
        {
            this.api = api;
            questSystem = api.ModLoader.GetModSystem<QuestSystem>();
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            foreach (var mod in api.ModLoader.Mods)
            {
                var assets = api.Assets.GetMany<ItemConfig>(api.Logger, "config/itemconfig", mod.Info.ModID);
                foreach (var asset in assets)
                {
                    if (asset.Value != null)
                    {
                        foreach (var actionItem in asset.Value.actionItems)
                        {
                            ActionItemRegistry[actionItem.id] = actionItem;
                        }
                    }
                }
            }
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api?.Side != EnumAppSide.Client) return;
            InjectActionItemsCreativeTab(api);
        }

        private void InjectActionItemsCreativeTab(ICoreAPI api)
        {
            if (ActionItemRegistry == null || ActionItemRegistry.Count == 0) return;

            var debugTool = api.World.GetItem(new AssetLocation("alegacyvsquest:debugtool"));
            if (debugTool == null || debugTool.IsMissing) return;

            var stacks = new List<JsonItemStack>();
            foreach (var kvp in ActionItemRegistry)
            {
                var actionItem = kvp.Value;
                if (actionItem == null || string.IsNullOrWhiteSpace(actionItem.itemCode)) continue;

                CollectibleObject collectible = api.World.GetItem(new AssetLocation(actionItem.itemCode));
                if (collectible == null)
                {
                    collectible = api.World.GetBlock(new AssetLocation(actionItem.itemCode));
                }
                if (collectible == null || collectible.IsMissing) continue;

                var stack = new ItemStack(collectible);
                ItemAttributeUtils.ApplyActionItemAttributes(stack, actionItem);

                var jis = new JsonItemStack
                {
                    Type = collectible is Block ? EnumItemClass.Block : EnumItemClass.Item,
                    Code = collectible.Code,
                    StackSize = stack.StackSize,
                    ResolvedItemstack = stack
                };

                stacks.Add(jis);
            }

            if (stacks.Count == 0) return;

            var tabStackList = new CreativeTabAndStackList
            {
                Tabs = new[] { ActionItemsCreativeTabCode },
                Stacks = stacks.ToArray()
            };

            var merged = new List<CreativeTabAndStackList>();
            if (debugTool.CreativeInventoryStacks != null)
            {
                foreach (var existing in debugTool.CreativeInventoryStacks)
                {
                    if (existing?.Tabs == null) { merged.Add(existing); continue; }

                    bool hasOurTab = false;
                    for (int i = 0; i < existing.Tabs.Length; i++)
                    {
                        if (existing.Tabs[i] == ActionItemsCreativeTabCode)
                        {
                            hasOurTab = true;
                            break;
                        }
                    }

                    if (!hasOurTab)
                    {
                        merged.Add(existing);
                    }
                }
            }

            merged.Add(tabStackList);
            debugTool.CreativeInventoryStacks = merged.ToArray();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            serverChannel = api.Network.RegisterChannel("vsquest-itemaction")
                .RegisterMessageType<ExecuteActionItemPacket>()
                .SetMessageHandler<ExecuteActionItemPacket>(OnActionPacket);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.RegisterChannel("vsquest-itemaction")
                .RegisterMessageType<ExecuteActionItemPacket>();

            api.Event.MouseDown += OnMouseDown;

            api.Event.BlockTexturesLoaded += () =>
            {
                InjectActionItemsCreativeTab(api);
            };
        }

        private void OnMouseDown(MouseEvent args)
        {
            if (args.Button != EnumMouseButton.Right) return;

            var slot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null) return;

            var attributes = slot.Itemstack.Attributes;
            var actions = attributes.GetString("alegacyvsquest:actions");

            if (actions != null)
            {
                args.Handled = true;
                clientChannel.SendPacket(new ExecuteActionItemPacket());
            }
        }

        private void OnActionPacket(IServerPlayer fromPlayer, ExecuteActionItemPacket packet)
        {
            var slot = fromPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null) return;

            var attributes = slot.Itemstack.Attributes;
            var actionsJson = attributes.GetString("alegacyvsquest:actions");
            if (actionsJson == null) return;

            var actions = JsonConvert.DeserializeObject<List<ItemAction>>(actionsJson);

            foreach (var action in actions)
            {
                if (questSystem.ActionRegistry.TryGetValue(action.id, out var registeredAction))
                {
                    var message = new QuestAcceptedMessage { questGiverId = fromPlayer.Entity.EntityId, questId = "item-action" };
                    registeredAction.Execute(sapi, message, fromPlayer, action.args);
                }
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ExecuteActionItemPacket
    {
    }
}