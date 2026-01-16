using System.Globalization;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class CooldownBlockAction : IQuestAction
    {
        private const string PlaceholderBlockCode = "alegacyvsquest:cooldownplaceholder";

        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (sapi == null || player == null) return;

            double delayHours = 1;
            if (args != null && args.Length >= 1 && double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDelay))
            {
                delayHours = parsedDelay;
            }
            if (delayHours <= 0) return;

            BlockPos pos = null;

            if (args != null && args.Length >= 2 && QuestInteractAtUtil.TryParsePos(args[1], out int ax, out int ay, out int az))
            {
                pos = new BlockPos(ax, ay, az, player.Entity?.Pos?.Dimension ?? 0);
            }
            else
            {
                var wa = player.Entity?.WatchedAttributes;
                if (wa == null) return;

                int x = wa.GetInt("alegacyvsquest:lastinteract:x", int.MinValue);
                int y = wa.GetInt("alegacyvsquest:lastinteract:y", int.MinValue);
                int z = wa.GetInt("alegacyvsquest:lastinteract:z", int.MinValue);
                int dim = wa.GetInt("alegacyvsquest:lastinteract:dim", 0);

                if (x == int.MinValue || y == int.MinValue || z == int.MinValue) return;

                pos = new BlockPos(x, y, z, dim);
            }

            var blockAccessor = sapi.World.BlockAccessor;
            if (blockAccessor == null) return;

            Block originalBlock = blockAccessor.GetBlock(pos);
            if (originalBlock == null || originalBlock.IsMissing || originalBlock.Code == null) return;

            if (originalBlock.Code.ToShortString() == PlaceholderBlockCode) return;

            Block placeholder = sapi.World.GetBlock(new AssetLocation(PlaceholderBlockCode));
            if (placeholder == null || placeholder.IsMissing)
            {
                sapi.Logger.Warning($"[vsquest] CooldownBlockAction: placeholder block '{PlaceholderBlockCode}' is missing. Ensure the client/server have the 'alegacyvsquest' mod assets installed.");
                return;
            }

            byte[] originalBeBytes = null;
            if (originalBlock.EntityClass != null)
            {
                var originalBe = blockAccessor.GetBlockEntity(pos);
                if (originalBe != null)
                {
                    var tree = new TreeAttribute();
                    originalBe.ToTreeAttributes(tree);
                    originalBeBytes = tree.ToBytes();
                }

                blockAccessor.RemoveBlockEntity(pos);
            }

            blockAccessor.SetBlock(placeholder.BlockId, pos);

            if (placeholder.EntityClass != null)
            {
                blockAccessor.SpawnBlockEntity(placeholder.EntityClass, pos);
                var cooldownBe = blockAccessor.GetBlockEntity(pos) as BlockEntityCooldownPlaceholder;

                double restoreAtHours = sapi.World.Calendar.TotalHours + delayHours;
                cooldownBe?.Arm(originalBlock.Code.ToShortString(), originalBeBytes, restoreAtHours);
            }
        }
    }
}
