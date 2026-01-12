using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class BlockEntityCooldownPlaceholder : BlockEntity
    {
        private const string AttrOriginalBlockCode = "vsquest:origBlockCode";
        private const string AttrOriginalBlockBeData = "vsquest:origBeData";
        private const string AttrRestoreAtHours = "vsquest:restoreAtHours";

        private string originalBlockCode;
        private byte[] originalBlockBeData;
        private double restoreAtHours;
        private bool ticking;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Api?.Side != EnumAppSide.Server) return;

            if (!ticking)
            {
                ticking = true;
                RegisterGameTickListener(OnServerTick, 1000);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            originalBlockCode = tree.GetString(AttrOriginalBlockCode, null);
            originalBlockBeData = tree.GetBytes(AttrOriginalBlockBeData, null);
            restoreAtHours = tree.GetDouble(AttrRestoreAtHours, 0);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (!string.IsNullOrWhiteSpace(originalBlockCode))
            {
                tree.SetString(AttrOriginalBlockCode, originalBlockCode);
            }
            if (originalBlockBeData != null)
            {
                tree.SetBytes(AttrOriginalBlockBeData, originalBlockBeData);
            }
            tree.SetDouble(AttrRestoreAtHours, restoreAtHours);
        }

        public void Arm(string originalBlockCode, byte[] originalBeData, double restoreAtHours)
        {
            if (Api == null) return;

            this.originalBlockCode = originalBlockCode;
            this.originalBlockBeData = originalBeData;
            this.restoreAtHours = restoreAtHours;
            MarkDirty(true);
        }

        private void OnServerTick(float dt)
        {
            if (Api?.Side != EnumAppSide.Server) return;
            if (Pos == null) return;

            double now = Api.World.Calendar.TotalHours;
            if (restoreAtHours <= 0 || now < restoreAtHours) return;

            var blockAccessor = Api.World.BlockAccessor;
            if (blockAccessor == null) return;

            var myBlock = blockAccessor.GetBlock(Pos);
            if (myBlock?.EntityClass == null || myBlock.EntityClass != Api.World.ClassRegistry.GetBlockEntityClass(typeof(BlockEntityCooldownPlaceholder)))
            {
                // Not our placeholder anymore.
                return;
            }

            if (string.IsNullOrWhiteSpace(originalBlockCode))
            {
                // Nothing to restore.
                return;
            }

            Block origBlock = Api.World.GetBlock(new AssetLocation(originalBlockCode));
            if (origBlock == null || origBlock.IsMissing)
            {
                // Original block no longer exists.
                return;
            }

            // Replace block, then restore BE if any
            blockAccessor.SetBlock(origBlock.BlockId, Pos);

            if (origBlock.EntityClass != null)
            {
                blockAccessor.SpawnBlockEntity(origBlock.EntityClass, Pos);

                if (originalBlockBeData != null && originalBlockBeData.Length > 0)
                {
                    try
                    {
                        var restoredBe = blockAccessor.GetBlockEntity(Pos);
                        if (restoredBe != null)
                        {
                            var restoredTree = TreeAttribute.CreateFromBytes(originalBlockBeData);
                            restoredBe.FromTreeAttributes(restoredTree, Api.World);
                            restoredBe.MarkDirty(true);
                        }
                    }
                    catch
                    {
                        // ignore restore issues
                    }
                }
            }
        }
    }
}
