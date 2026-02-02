using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BossHuntArenaConfigData
    {
        public float yOffset;
        public bool keepInventory;
    }

    public class BossHuntArenaConfigGui : GuiDialogGeneric
    {
        private const string KeyYOffset = "yOffset";
        private const string KeyKeepInventory = "keepInventory";

        private readonly BlockPos bePos;
        private bool updating;

        public BossHuntArenaConfigData Data = new BossHuntArenaConfigData();

        public BossHuntArenaConfigGui(BlockPos bePos, ICoreClientAPI capi) : base("Boss hunt arena", capi)
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

            SingleComposer = capi.Gui.CreateCompo("alegacyvsquest-bosshuntarena-config", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds);

            SingleComposer
                .AddStaticText("Y offset", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddNumberInput(cur = fullRow.BelowCopy(0, -10).WithFixedSize(120, 29), null, CairoFont.WhiteDetailText(), KeyYOffset);

            var boolCodes = new[] { "false", "true" };
            var boolNames = new[] { "No", "Yes" };

            SingleComposer
                .AddStaticText("Keep inventory", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddDropDown(boolCodes, boolNames, 0, (_, __) => { }, cur = fullRow.BelowCopy(0, -10).WithFixedSize(160, 29), KeyKeepInventory);

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

            var data = new BossHuntArenaConfigData();
            data.yOffset = (float)SingleComposer.GetNumberInput(KeyYOffset).GetValue();

            string selected = SingleComposer.GetDropDown(KeyKeepInventory).SelectedValue;
            data.keepInventory = selected == "true";

            capi.Network.SendBlockEntityPacket(bePos, 3001, SerializerUtil.Serialize(data));
            return true;
        }

        public void UpdateFromServer(BossHuntArenaConfigData data)
        {
            if (data == null || SingleComposer == null) return;

            updating = true;
            Data = data;
            SingleComposer.GetNumberInput(KeyYOffset).SetValue(data.yOffset);
            SingleComposer.GetDropDown(KeyKeepInventory).SetSelectedValue(data.keepInventory ? "true" : "false");

            updating = false;
        }
    }
}
