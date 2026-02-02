using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class BlockEntityBossHuntArena : BlockEntity
    {
        private const string AttrYOffset = "alegacyvsquest:bosshuntarena:yOffset";
        private const string AttrKeepInventory = "alegacyvsquest:bosshuntarena:keepInventory";

        private const int PacketOpenGui = 3000;
        private const int PacketSave = 3001;

        private float yOffset;
        private bool keepInventory;

        private BossHuntArenaConfigGui dlg;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Api?.Side == EnumAppSide.Server)
            {
                TryRegister();
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api?.Side != EnumAppSide.Server) return;

            var attrs = Block?.Attributes;
            yOffset = attrs?["yOffset"].AsFloat(yOffset) ?? yOffset;
            keepInventory = attrs?["keepInventory"].AsBool(keepInventory) ?? keepInventory;

            TryRegister();
            MarkDirty(true);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            yOffset = tree.GetFloat(AttrYOffset, yOffset);
            keepInventory = tree.GetBool(AttrKeepInventory, keepInventory);

            if (Api?.Side == EnumAppSide.Server)
            {
                TryRegister();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat(AttrYOffset, yOffset);
            tree.SetBool(AttrKeepInventory, keepInventory);
        }

        internal void OnInteract(IPlayer byPlayer)
        {
            if (byPlayer == null) return;

            if (Api.Side == EnumAppSide.Server)
            {
                var sp = byPlayer as IServerPlayer;
                if (sp == null) return;

                var data = BuildConfigData();
                (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(data));
                return;
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] bytes)
        {
            if (packetid != PacketOpenGui) return;

            var data = SerializerUtil.Deserialize<BossHuntArenaConfigData>(bytes);

            if (dlg == null || !dlg.IsOpened())
            {
                dlg = new BossHuntArenaConfigGui(Pos, Api as Vintagestory.API.Client.ICoreClientAPI);
                dlg.Data = data;
                dlg.TryOpen();
                dlg.OnClosed += () =>
                {
                    dlg?.Dispose();
                    dlg = null;
                };
            }
            else
            {
                dlg.UpdateFromServer(data);
            }

            ApplyConfigData(data, markDirty: false);
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] bytes)
        {
            var sp = fromPlayer as IServerPlayer;
            if (sp == null || !sp.HasPrivilege(Privilege.controlserver)) return;

            if (packetid != PacketSave) return;

            var data = SerializerUtil.Deserialize<BossHuntArenaConfigData>(bytes);
            ApplyConfigData(data, markDirty: true);

            var refreshed = BuildConfigData();
            (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(refreshed));
        }

        internal void OnRemovedServerSide()
        {
            if (Api?.Side != EnumAppSide.Server) return;

            try
            {
                var system = Api.ModLoader.GetModSystem<BossHuntArenaSystem>();
                system?.UnregisterArena(new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension));
            }
            catch
            {
            }
        }

        private void TryRegister()
        {
            if (Api?.Side != EnumAppSide.Server) return;

            try
            {
                var system = Api.ModLoader.GetModSystem<BossHuntArenaSystem>();
                system?.RegisterArena(new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension), yOffset, keepInventory);
            }
            catch
            {
            }
        }

        private BossHuntArenaConfigData BuildConfigData()
        {
            return new BossHuntArenaConfigData
            {
                yOffset = yOffset,
                keepInventory = keepInventory
            };
        }

        private void ApplyConfigData(BossHuntArenaConfigData data, bool markDirty)
        {
            if (data == null) return;

            yOffset = data.yOffset;
            keepInventory = data.keepInventory;

            TryRegister();

            if (markDirty)
            {
                MarkDirty(true);
            }
        }
    }
}
