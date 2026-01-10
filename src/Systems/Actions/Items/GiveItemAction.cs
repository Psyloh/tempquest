using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class GiveItemAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 2)
            {
                throw new QuestException("The 'giveitem' action requires at least 2 arguments: itemCode and amount.");
            }

            string code = args[0];
            if (!int.TryParse(args[1], out int amount))
            {
                throw new QuestException($"Invalid amount '{args[1]}' for 'giveitem' action in quest {message.questId}.");
            }

            CollectibleObject item = sapi.World.GetItem(new AssetLocation(code));
            if (item == null)
            {
                item = sapi.World.GetBlock(new AssetLocation(code));
            }
            if (item == null)
            {
                throw new QuestException(string.Format("Could not find item {0} for quest {1}!", code, message.questId));
            }

            var stack = new ItemStack(item, amount);

            if (args.Length > 2)
            {
                stack.Attributes.SetString("itemizerName", args[2]);
            }
            if (args.Length > 3)
            {
                string desc = string.Join(" ", args, 3, args.Length - 3);
                stack.Attributes.SetString("itemizerDesc", desc);
            }

            if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
            {
                sapi.World.SpawnItemEntity(stack, byPlayer.Entity.ServerPos.XYZ);
            }
        }
    }
}
