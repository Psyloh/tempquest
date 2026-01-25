using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class ActionItemCreativeTabInjector
    {
        private readonly Dictionary<string, ActionItem> actionItemRegistry;
        private readonly QuestSystem questSystem;
        private const string ActionItemsCreativeTabCode = "alegacyvsquest-actionitems";

        public ActionItemCreativeTabInjector(Dictionary<string, ActionItem> actionItemRegistry, QuestSystem questSystem)
        {
            this.actionItemRegistry = actionItemRegistry;
            this.questSystem = questSystem;
        }

        public void Inject(ICoreAPI api)
        {
            if (api == null) return;

            var debugTool = api.World.GetItem(new AssetLocation("alegacyvsquest:debugtool"));
            if (debugTool == null || debugTool.IsMissing) return;

            var entitySpawner = api.World.GetItem(new AssetLocation("alegacyvsquest:entityspawner"));

            var stacks = new List<JsonItemStack>();

            if (actionItemRegistry != null && actionItemRegistry.Count > 0)
            {
                foreach (var kvp in actionItemRegistry)
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
    }
}
