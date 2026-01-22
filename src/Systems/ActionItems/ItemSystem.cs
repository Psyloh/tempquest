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
        private ICoreAPI api;
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private QuestSystem questSystem;

        private IClientNetworkChannel clientChannel;
        private IServerNetworkChannel serverChannel;

        private long inventoryScanListenerId = 0;
        private long hotbarEnforceListenerId = 0;

        private readonly Dictionary<string, (string invKey, int slot)> inventoryScanCursorByPlayerUid = new Dictionary<string, (string invKey, int slot)>(StringComparer.Ordinal);

        private const string ActionItemsCreativeTabCode = "alegacyvsquest-actionitems";

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
            // Must run on both sides: creative tab list is synced/validated server-side when moving items.
            if (api == null) return;
            InjectActionItemsCreativeTab(api);
        }

        private void InjectActionItemsCreativeTab(ICoreAPI api)
        {
            var debugTool = api.World.GetItem(new AssetLocation("alegacyvsquest:debugtool"));
            if (debugTool == null || debugTool.IsMissing) return;

            var entitySpawner = api.World.GetItem(new AssetLocation("alegacyvsquest:entityspawner"));

            var stacks = new List<JsonItemStack>();

            if (ActionItemRegistry != null && ActionItemRegistry.Count > 0)
            {
                foreach (var kvp in ActionItemRegistry)
                {
                    var actionItem = kvp.Value;
                    if (actionItem == null || string.IsNullOrWhiteSpace(actionItem.itemCode)) continue;

                    if (!ItemAttributeUtils.TryResolveCollectible(api, actionItem.itemCode, out var collectible)) continue;

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
            }

            if (entitySpawner != null && !entitySpawner.IsMissing && api.World?.EntityTypes != null)
            {
                var allowedDomains = new HashSet<string>();
                if (questSystem?.QuestRegistry != null)
                {
                    foreach (var quest in questSystem.QuestRegistry.Values)
                    {
                        if (quest?.id == null) continue;
                        int idx = quest.id.IndexOf(':');
                        if (idx <= 0) continue;
                        allowedDomains.Add(quest.id.Substring(0, idx));
                    }
                }

                foreach (var et in api.World.EntityTypes)
                {
                    if (et?.Code == null) continue;
                    if (allowedDomains.Count > 0 && !allowedDomains.Contains(et.Code.Domain)) continue;

                    var stack = new ItemStack(entitySpawner);
                    stack.Attributes.SetString("type", et.Code.ToShortString());

                    var jis = new JsonItemStack
                    {
                        Type = EnumItemClass.Item,
                        Code = entitySpawner.Code,
                        StackSize = stack.StackSize,
                        ResolvedItemstack = stack
                    };

                    stacks.Add(jis);
                }
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
            serverChannel = api.Network.RegisterChannel("alegacyvsquest-itemaction")
                .RegisterMessageType<ExecuteActionItemPacket>()
                .SetMessageHandler<ExecuteActionItemPacket>(OnActionPacket);

            // Periodically scan inventories for action items that should trigger when added.
            // This avoids relying on right click and allows one-time processing.
            inventoryScanListenerId = api.Event.RegisterGameTickListener(OnInventoryScanTick, 1000);

            // Periodically enforce hotbar-only placement for special quest items (blockEquip action items).
            hotbarEnforceListenerId = api.Event.RegisterGameTickListener(OnHotbarEnforceTick, 500);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.RegisterChannel("alegacyvsquest-itemaction")
                .RegisterMessageType<ExecuteActionItemPacket>();

            api.Event.MouseDown += OnMouseDown;

            api.Event.BlockTexturesLoaded += () =>
            {
                InjectActionItemsCreativeTab(api);
            };
        }

        private void OnHotbarEnforceTick(float dt)
        {
            if (sapi == null) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            foreach (var p in players)
            {
                if (p is not IServerPlayer sp) continue;

                var invMgr = sp.InventoryManager;
                if (invMgr?.Inventories == null) continue;

                var hotbarInv = invMgr.GetHotbarInventory();
                if (hotbarInv == null) continue;

                bool foundOutside = false;

                // Find one blocked action item outside hotbar and move it into first free hotbar slot.
                foreach (var kvp in invMgr.Inventories)
                {
                    var inv = kvp.Value;

                    if (inv == null) continue;
                    if (inv == hotbarInv) continue;
                    if (inv.ClassName == GlobalConstants.creativeInvClassName) continue;

                    for (int i = 0; i < inv.Count; i++)
                    {
                        var sourceSlot = inv[i];
                        if (sourceSlot?.Empty != false) continue;

                        var stack = sourceSlot.Itemstack;
                        if (!ItemAttributeUtils.IsActionItemBlockedMove(stack)) continue;

                        foundOutside = true;

                        ItemSlot freeHotbarSlot = null;
                        for (int j = 0; j < hotbarInv.Count; j++)
                        {
                            var hs = hotbarInv[j];
                            if (hs?.Empty == true)
                            {
                                freeHotbarSlot = hs;
                                break;
                            }
                        }

                        // No free hotbar slot => do nothing (never delete items).
                        if (freeHotbarSlot == null) break;

                        var op = new ItemStackMoveOperation(sapi.World, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, sourceSlot.StackSize)
                        {
                            ActingPlayer = sp
                        };

                        sourceSlot.TryPutInto(freeHotbarSlot, ref op);
                        sourceSlot.MarkDirty();
                        freeHotbarSlot.MarkDirty();

                        // Only move one per tick per player to reduce churn
                        foundOutside = false;
                        break;
                    }

                    if (!foundOutside)
                    {
                        // either nothing found, or we moved one and want to stop scanning inventories for this player
                        break;
                    }
                }
            }
        }

        private bool TryGetActionItemActionsFromAttributes(ITreeAttribute attributes, out List<ItemAction> actions, out string sourceQuestId)
        {
            actions = null;
            sourceQuestId = null;

            if (attributes == null) return false;

            var actionsJson = attributes.GetString(ItemAttributeUtils.ActionItemActionsKey);
            if (string.IsNullOrWhiteSpace(actionsJson)) return false;

            try
            {
                actions = JsonConvert.DeserializeObject<List<ItemAction>>(actionsJson);
            }
            catch
            {
                actions = null;
                return false;
            }

            if (actions == null || actions.Count == 0) return false;

            sourceQuestId = attributes.GetString(ItemAttributeUtils.ActionItemSourceQuestKey);
            if (string.IsNullOrWhiteSpace(sourceQuestId)) sourceQuestId = ItemAttributeUtils.ActionItemDefaultSourceQuestId;

            return true;
        }

        private void OnMouseDown(MouseEvent args)
        {
            if (args.Button != EnumMouseButton.Right) return;

            var slot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null) return;

            var attributes = slot.Itemstack.Attributes;
            if (attributes == null) return;

            // If item is configured to trigger on inventory add, do not allow manual right-click triggering.
            if (attributes.GetBool(ItemAttributeUtils.ActionItemTriggerOnInvAddKey, false))
            {
                return;
            }

            if (!TryGetActionItemActionsFromAttributes(attributes, out var actions, out string sourceQuestId))
            {
                return;
            }

            args.Handled = true;
            clientChannel.SendPacket(new ExecuteActionItemPacket());
        }

        private void OnActionPacket(IServerPlayer fromPlayer, ExecuteActionItemPacket packet)
        {
            var slot = fromPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null) return;

            var attributes = slot.Itemstack.Attributes;
            if (!TryGetActionItemActionsFromAttributes(attributes, out var actions, out string sourceQuestId)) return;

            bool triggerOnInvAdd = attributes.GetBool(ItemAttributeUtils.ActionItemTriggerOnInvAddKey, false);

            string actionItemId = attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
            var wa = fromPlayer?.Entity?.WatchedAttributes;
            bool enforceOnce = triggerOnInvAdd
                && !string.IsNullOrWhiteSpace(actionItemId)
                && wa != null
                && (string.IsNullOrWhiteSpace(sourceQuestId)
                    || string.Equals(sourceQuestId, ItemAttributeUtils.ActionItemDefaultSourceQuestId, StringComparison.OrdinalIgnoreCase)
                    || questSystem?.QuestRegistry?.ContainsKey(sourceQuestId) != true);
            string onceKey = enforceOnce ? $"alegacyvsquest:itemaction:invadd:{actionItemId}" : null;
            if (enforceOnce && wa.GetBool(onceKey, false))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(sourceQuestId)
                && !string.Equals(sourceQuestId, ItemAttributeUtils.ActionItemDefaultSourceQuestId, StringComparison.OrdinalIgnoreCase)
                && questSystem?.QuestRegistry?.ContainsKey(sourceQuestId) == true)
            {
                var active = questSystem.GetPlayerQuests(fromPlayer.PlayerUID);
                bool isActive = active != null && active.Exists(q => q != null && string.Equals(q.questId, sourceQuestId, StringComparison.OrdinalIgnoreCase));
                if (!isActive)
                {
                    return;
                }
            }

            foreach (var action in actions)
            {
                if (questSystem.ActionRegistry.TryGetValue(action.id, out var registeredAction))
                {
                    var message = new QuestAcceptedMessage { questGiverId = fromPlayer.Entity.EntityId, questId = sourceQuestId };
                    registeredAction.Execute(sapi, message, fromPlayer, action.args);
                }
            }

            if (enforceOnce)
            {
                wa.SetBool(onceKey, true);
                wa.MarkPathDirty(onceKey);
            }
        }

        private void OnInventoryScanTick(float dt)
        {
            if (sapi == null || questSystem == null) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            foreach (var p in players)
            {
                if (!(p is IServerPlayer sp)) continue;
                var inv = sp?.InventoryManager;
                if (inv == null) continue;

                string uid = sp.PlayerUID;
                if (string.IsNullOrWhiteSpace(uid)) continue;

                const int maxSlotsPerTick = 64;
                int scanned = 0;

                inventoryScanCursorByPlayerUid.TryGetValue(uid, out var cursor);
                string resumeInvKey = cursor.invKey;
                int resumeSlot = cursor.slot;
                if (resumeSlot < 0) resumeSlot = 0;

                bool resuming = !string.IsNullOrWhiteSpace(resumeInvKey);
                bool resumeInvReached = !resuming;

                // Walk all inventories/slots; trigger only for stacks that request auto-processing.
                foreach (var iinv in inv.Inventories)
                {
                    // If we have a resume inventory key, skip inventories until we reach it.
                    if (!resumeInvReached)
                    {
                        if (!string.Equals(iinv.Key, resumeInvKey, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        resumeInvReached = true;
                    }

                    var inventory = iinv.Value;
                    if (inventory == null) continue;

                    if (inventory.ClassName == GlobalConstants.creativeInvClassName) continue;

                    int slotCount;
                    try
                    {
                        slotCount = inventory.Count;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (slotCount <= 0) continue;

                    int startSlot = (resuming && string.Equals(iinv.Key, resumeInvKey, StringComparison.Ordinal)) ? resumeSlot : 0;
                    if (startSlot < 0) startSlot = 0;
                    if (startSlot >= slotCount) startSlot = 0;

                    for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                    {
                        if (slotIndex < startSlot) continue;

                        var slot = inventory[slotIndex];
                        var stack = slot?.Itemstack;
                        if (stack?.Attributes == null) continue;

                        if (!stack.Attributes.GetBool(ItemAttributeUtils.ActionItemTriggerOnInvAddKey, false)) continue;

                        string actionItemId = stack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
                        if (string.IsNullOrWhiteSpace(actionItemId)) continue;

                        string onceKey = $"alegacyvsquest:itemaction:invadd:{actionItemId}";
                        var wa = sp?.Entity?.WatchedAttributes;
                        if (wa == null) break;
                        if (wa.GetBool(onceKey, false)) continue;

                        if (!TryGetActionItemActionsFromAttributes(stack.Attributes, out var actions, out string sourceQuestId))
                        {
                            continue;
                        }

                        // If this action item is tied to a quest, only auto-trigger it while that quest is active.
                        // This prevents pre-collecting quest items and consuming their one-time trigger too early.
                        if (!string.Equals(sourceQuestId, ItemAttributeUtils.ActionItemDefaultSourceQuestId, StringComparison.OrdinalIgnoreCase)
                            && questSystem?.QuestRegistry?.ContainsKey(sourceQuestId) == true)
                        {
                            var active = questSystem.GetPlayerQuests(sp.PlayerUID);
                            bool isActive = active != null && active.Exists(q => q != null && string.Equals(q.questId, sourceQuestId, StringComparison.OrdinalIgnoreCase));
                            if (!isActive)
                            {
                                continue;
                            }
                        }

                        foreach (var action in actions)
                        {
                            if (action == null) continue;
                            if (questSystem.ActionRegistry.TryGetValue(action.id, out var registeredAction))
                            {
                                var message = new QuestAcceptedMessage { questGiverId = sp.Entity.EntityId, questId = sourceQuestId };
                                registeredAction.Execute(sapi, message, sp, action.args);
                            }
                        }

                        wa.SetBool(onceKey, true);
                        wa.MarkPathDirty(onceKey);

                        scanned++;
                        if (scanned >= maxSlotsPerTick)
                        {
                            // Continue from next slot on next tick.
                            inventoryScanCursorByPlayerUid[uid] = (iinv.Key, slotIndex + 1);
                            goto nextPlayer;
                        }
                    }
                }

                // Finished full scan => reset cursor.
                inventoryScanCursorByPlayerUid.Remove(uid);

            nextPlayer:
                continue;
            }
        }

    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ExecuteActionItemPacket
    {
    }
}