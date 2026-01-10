using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class GetActionItemCommandHandler
    {
        private readonly ICoreAPI api;
        private readonly ItemSystem itemSystem;

        public GetActionItemCommandHandler(ICoreAPI api, ItemSystem itemSystem)
        {
            this.api = api;
            this.itemSystem = itemSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            if (player == null) return TextCommandResult.Error("This command can only be run by a player.");

            string itemId = (string)args[0];
            int amount = (int)args[1];

            if (!itemSystem.ActionItemRegistry.TryGetValue(itemId, out var actionItem))
            {
                return TextCommandResult.Error($"Action item with ID '{itemId}' not found in itemconfig.json.");
            }

            CollectibleObject collectible = api.World.GetItem(new AssetLocation(actionItem.itemCode));
            if (collectible == null)
            {
                collectible = api.World.GetBlock(new AssetLocation(actionItem.itemCode));
            }

            if (collectible == null)
            {
                return TextCommandResult.Error($"Could not find base item/block with code '{actionItem.itemCode}'.");
            }

            var stack = new ItemStack(collectible, amount);
            ItemAttributeUtils.ApplyActionItemAttributes(stack, actionItem);

            if (!player.InventoryManager.TryGiveItemstack(stack))
            {
                api.World.SpawnItemEntity(stack, player.Entity.ServerPos.XYZ);
            }

            return TextCommandResult.Success($"Successfully gave {amount}x {actionItem.name}.");
        }
    }
}
