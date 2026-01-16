using System;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BossHuntAnchorConfigData
    {
        public string bossKey;
        public string anchorId;
        public int pointOrder;
        public string[] knownBossKeys;
    }

    public class BossHuntAnchorConfigGui : GuiDialogGeneric
    {
        private const string KeyBossKey = "bossKey";
        private const string KeyAnchorId = "anchorId";
        private const string KeyPointOrder = "pointOrder";

        private readonly BlockPos bePos;
        private bool updating;

        public BossHuntAnchorConfigData Data = new BossHuntAnchorConfigData();

        public BossHuntAnchorConfigGui(BlockPos bePos, ICoreClientAPI capi) : base("Boss hunt anchor", capi)
        {
            this.bePos = bePos;
        }

        public override void OnGuiOpened()
        {
            Compose();
        }

        private void Compose()
        {
            ClearComposers();

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-20, 0);

            ElementBounds cur = ElementBounds.Fixed(0, 30, 420, 30);
            ElementBounds fullRow = cur.FlatCopy();

            ElementBounds closeButtonBounds = ElementBounds.FixedSize(0, 0).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(20, 4);
            ElementBounds saveButtonBounds = ElementBounds.FixedSize(0, 0).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(20, 4);

            bgBounds.WithChildren(cur, closeButtonBounds, saveButtonBounds);

            SingleComposer = capi.Gui.CreateCompo("alegacyvsquest-bosshuntanchor-config", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds);

            var bossKeys = Data?.knownBossKeys;
            if (bossKeys == null || bossKeys.Length == 0)
            {
                bossKeys = new[] { "" };
            }

            SingleComposer
                .AddStaticText("Boss key", CairoFont.WhiteDetailText(), cur)
                .AddDropDown(bossKeys, bossKeys, 0, (_, __) => { }, cur = cur.BelowCopy(0, -10).WithFixedSize(420, 29), KeyBossKey);

            SingleComposer
                .AddStaticText("Anchor id", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddTextInput(cur = fullRow.BelowCopy(0, -10).WithFixedSize(420, 29), null, CairoFont.WhiteDetailText(), KeyAnchorId);

            SingleComposer
                .AddStaticText("Point order", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddNumberInput(cur = fullRow.BelowCopy(0, -10).WithFixedSize(120, 29), null, CairoFont.WhiteDetailText(), KeyPointOrder);

            SingleComposer
                .AddSmallButton("Close", OnButtonClose, closeButtonBounds.FixedUnder(cur, 10))
                .AddSmallButton("Save", OnButtonSave, saveButtonBounds.FixedUnder(cur, 10))
                .EndChildElements()
                .Compose();

            UpdateFromServer(Data);
        }

        private void OnTitleBarClose()
        {
            OnButtonClose();
        }

        private bool OnButtonClose()
        {
            TryClose();
            return true;
        }

        private bool OnButtonSave()
        {
            if (updating) return true;

            var data = new BossHuntAnchorConfigData();

            data.bossKey = SingleComposer.GetDropDown(KeyBossKey).SelectedValue;
            data.knownBossKeys = Data?.knownBossKeys;
            data.anchorId = SingleComposer.GetTextInput(KeyAnchorId).GetText();
            data.pointOrder = (int)SingleComposer.GetNumberInput(KeyPointOrder).GetValue();

            capi.Network.SendBlockEntityPacket(bePos, 2001, SerializerUtil.Serialize(data));
            return true;
        }

        public void UpdateFromServer(BossHuntAnchorConfigData data)
        {
            if (data == null || SingleComposer == null) return;

            updating = true;
            Data = data;

            try
            {
                if (!string.IsNullOrWhiteSpace(data.bossKey))
                {
                    SingleComposer.GetDropDown(KeyBossKey).SetSelectedValue(data.bossKey);
                }
            }
            catch
            {
            }

            SingleComposer.GetTextInput(KeyAnchorId).SetValue(data.anchorId ?? "");
            SingleComposer.GetNumberInput(KeyPointOrder).SetValue(data.pointOrder);

            updating = false;
        }
    }
}
