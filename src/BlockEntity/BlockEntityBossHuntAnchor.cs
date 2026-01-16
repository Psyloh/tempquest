using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public class BlockEntityBossHuntAnchor : BlockEntity
    {
        private const string AttrBossKey = "alegacyvsquest:bosshuntanchor:bossKey";
        private const string AttrAnchorId = "alegacyvsquest:bosshuntanchor:anchorId";
        private const string AttrPointOrder = "alegacyvsquest:bosshuntanchor:pointOrder";

        private const int PacketOpenGui = 2000;
        private const int PacketSave = 2001;

        private string bossKey;
        private string anchorId;
        private int pointOrder;

        private BossHuntAnchorConfigGui dlg;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Api?.Side == EnumAppSide.Server)
            {
                TryRegisterAnchor();
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api?.Side != EnumAppSide.Server) return;

            var attrs = Block?.Attributes;
            bossKey = attrs?["bossKey"].AsString(bossKey);
            anchorId = attrs?["anchorId"].AsString(anchorId);
            pointOrder = attrs?["pointOrder"].AsInt(pointOrder) ?? pointOrder;

            TryRegisterAnchor();
            MarkDirty(true);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            bossKey = tree.GetString(AttrBossKey, bossKey);
            anchorId = tree.GetString(AttrAnchorId, anchorId);
            pointOrder = tree.GetInt(AttrPointOrder, pointOrder);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (!string.IsNullOrWhiteSpace(bossKey)) tree.SetString(AttrBossKey, bossKey);
            if (!string.IsNullOrWhiteSpace(anchorId)) tree.SetString(AttrAnchorId, anchorId);
            tree.SetInt(AttrPointOrder, pointOrder);
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

            var data = SerializerUtil.Deserialize<BossHuntAnchorConfigData>(bytes);

            if (dlg == null || !dlg.IsOpened())
            {
                dlg = new BossHuntAnchorConfigGui(Pos, Api as Vintagestory.API.Client.ICoreClientAPI);
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
            if (packetid != PacketSave) return;

            var data = SerializerUtil.Deserialize<BossHuntAnchorConfigData>(bytes);
            ApplyConfigData(data, markDirty: true);

            var sp = fromPlayer as IServerPlayer;
            if (sp != null)
            {
                var refreshed = BuildConfigData();
                (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(refreshed));
            }
        }

        internal void OnRemovedServerSide()
        {
            if (Api?.Side != EnumAppSide.Server) return;

            try
            {
                var system = Api.ModLoader.GetModSystem<BossHuntSystem>();
                system?.UnsetAnchorPoint(bossKey, anchorId, new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension));
            }
            catch
            {
            }
        }

        private BossHuntAnchorConfigData BuildConfigData()
        {
            string[] keys = null;
            try
            {
                var system = Api?.ModLoader?.GetModSystem<BossHuntSystem>();
                keys = system?.GetKnownBossKeys();
            }
            catch
            {
                keys = null;
            }

            return new BossHuntAnchorConfigData
            {
                bossKey = bossKey,
                anchorId = anchorId,
                pointOrder = pointOrder,
                knownBossKeys = keys
            };
        }

        private void ApplyConfigData(BossHuntAnchorConfigData data, bool markDirty)
        {
            if (data == null) return;

            bossKey = data.bossKey;
            anchorId = data.anchorId;
            pointOrder = data.pointOrder;

            TryRegisterAnchor();

            if (markDirty)
            {
                MarkDirty(true);
            }
        }

        private void TryRegisterAnchor()
        {
            if (Api?.Side != EnumAppSide.Server) return;
            if (string.IsNullOrWhiteSpace(bossKey)) return;

            if (string.IsNullOrWhiteSpace(anchorId))
            {
                anchorId = $"alegacyvsquest:bosshuntanchor:{Pos.dimension}:{Pos.X}:{Pos.Y}:{Pos.Z}";
                MarkDirty(true);
            }

            try
            {
                var system = Api.ModLoader.GetModSystem<BossHuntSystem>();
                system?.SetAnchorPoint(bossKey, anchorId, pointOrder, new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension));
            }
            catch
            {
            }
        }
    }
}
