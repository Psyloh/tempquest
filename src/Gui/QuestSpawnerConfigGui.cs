using System;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuestSpawnerConfigData
    {
        public int maxAlive;
        public float spawnIntervalSeconds;
        public float spawnRadius;
        public float leashRange;
        public float yOffset;
        public string entries;
    }

    public class QuestSpawnerConfigGui : GuiDialogGeneric
    {
        private const string KeyMaxAlive = "maxAlive";
        private const string KeyInterval = "interval";
        private const string KeySpawnRadius = "spawnRadius";
        private const string KeyLeashRange = "leashRange";
        private const string KeyYOffset = "yOffset";
        private const string KeyEntries = "entries";

        private readonly BlockPos bePos;
        private bool updating;

        public QuestSpawnerConfigData Data = new QuestSpawnerConfigData();

        public QuestSpawnerConfigGui(BlockPos bePos, ICoreClientAPI capi) : base("Quest spawner config", capi)
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
            ElementBounds toggleButtonBounds = ElementBounds.FixedSize(0, 0).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPadding(20, 4);
            ElementBounds killButtonBounds = ElementBounds.FixedSize(0, 0).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPadding(20, 4);
            ElementBounds saveButtonBounds = ElementBounds.FixedSize(0, 0).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(20, 4);

            bgBounds.WithChildren(cur, closeButtonBounds, saveButtonBounds);

            SingleComposer = capi.Gui.CreateCompo("vsquest-questspawner-config", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds);

            SingleComposer
                .AddStaticText("Max alive", CairoFont.WhiteDetailText(), cur)
                .AddNumberInput(cur = cur.BelowCopy(0, -10).WithFixedSize(120, 29), null, CairoFont.WhiteDetailText(), KeyMaxAlive);

            SingleComposer
                .AddStaticText("Spawn interval (seconds)", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddNumberInput(cur = fullRow.BelowCopy(0, -10).WithFixedSize(120, 29), null, CairoFont.WhiteDetailText(), KeyInterval);

            SingleComposer
                .AddStaticText("Spawn radius", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddNumberInput(cur = fullRow.BelowCopy(0, -10).WithFixedSize(120, 29), null, CairoFont.WhiteDetailText(), KeySpawnRadius);

            SingleComposer
                .AddStaticText("Leash range", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddNumberInput(cur = fullRow.BelowCopy(0, -10).WithFixedSize(120, 29), null, CairoFont.WhiteDetailText(), KeyLeashRange);

            SingleComposer
                .AddStaticText("Y offset (surface + offset)", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddNumberInput(cur = fullRow.BelowCopy(0, -10).WithFixedSize(120, 29), null, CairoFont.WhiteDetailText(), KeyYOffset);

            SingleComposer
                .AddStaticText("Entries (one per line: entityCode|targetId|weight)", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddTextArea(cur = fullRow.BelowCopy(0, -10).WithFixedSize(420, 140), null, CairoFont.WhiteDetailText(), KeyEntries);

            SingleComposer
                .AddSmallButton("Close", OnButtonClose, closeButtonBounds.FixedUnder(cur, 10))
                .AddSmallButton("Enable/Disable", OnButtonToggle, toggleButtonBounds.FixedUnder(cur, 10))
                .AddSmallButton("Kill", OnButtonKill, killButtonBounds.FixedUnder(cur, 10).WithFixedOffset(0, 30))
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

            var data = new QuestSpawnerConfigData();

            data.maxAlive = (int)SingleComposer.GetNumberInput(KeyMaxAlive).GetValue();
            data.spawnIntervalSeconds = SingleComposer.GetNumberInput(KeyInterval).GetValue();
            data.spawnRadius = SingleComposer.GetNumberInput(KeySpawnRadius).GetValue();
            data.leashRange = SingleComposer.GetNumberInput(KeyLeashRange).GetValue();
            data.yOffset = SingleComposer.GetNumberInput(KeyYOffset).GetValue();
            data.entries = SingleComposer.GetTextArea(KeyEntries).GetText();

            capi.Network.SendBlockEntityPacket(bePos, 1001, SerializerUtil.Serialize(data));
            return true;
        }

        private bool OnButtonKill()
        {
            if (updating) return true;

            capi.Network.SendBlockEntityPacket(bePos, 1002, new byte[0]);
            return true;
        }

        private bool OnButtonToggle()
        {
            if (updating) return true;

            capi.Network.SendBlockEntityPacket(bePos, 1003, new byte[0]);
            return true;
        }

        public void UpdateFromServer(QuestSpawnerConfigData data)
        {
            if (data == null || SingleComposer == null) return;

            updating = true;
            Data = data;

            SingleComposer.GetNumberInput(KeyMaxAlive).SetValue(data.maxAlive);
            SingleComposer.GetNumberInput(KeyInterval).SetValue(data.spawnIntervalSeconds);
            SingleComposer.GetNumberInput(KeySpawnRadius).SetValue(data.spawnRadius);
            SingleComposer.GetNumberInput(KeyLeashRange).SetValue(data.leashRange);
            SingleComposer.GetNumberInput(KeyYOffset).SetValue(data.yOffset);
            SingleComposer.GetTextArea(KeyEntries).SetValue(data.entries ?? "");

            updating = false;
        }
    }
}
