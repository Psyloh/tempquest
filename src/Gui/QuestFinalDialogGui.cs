using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class QuestFinalDialogGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private readonly string titleLangKey;
        private readonly string textLangKey;
        private readonly string option1LangKey;
        private readonly string option2LangKey;

        public QuestFinalDialogGui(ICoreClientAPI capi, string titleLangKey, string textLangKey, string option1LangKey = null, string option2LangKey = null) : base(capi)
        {
            this.titleLangKey = titleLangKey;
            this.textLangKey = textLangKey;
            this.option1LangKey = option1LangKey;
            this.option2LangKey = option2LangKey;
            recompose();
        }

        private void recompose()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            ElementBounds textBounds = ElementBounds.Fixed(0, 40, 520, 380);
            ElementBounds clippingBounds = textBounds.ForkBoundingParent();
            ElementBounds scrollbarBounds = textBounds.CopyOffsetedSibling(textBounds.fixedWidth + 10).WithFixedWidth(20).WithFixedHeight(textBounds.fixedHeight);
            ElementBounds leftButtonBounds = ElementBounds.Fixed(10, 440, 250, 20);
            ElementBounds rightButtonBounds = ElementBounds.Fixed(270, 440, 250, 20);
            ElementBounds closeButtonBounds = ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20);

            bgBounds.BothSizing = ElementSizing.FitToChildren;

            string titleText = LangUtil.GetSafe(titleLangKey);
            string bodyText = LangUtil.GetSafe(textLangKey);

            SingleComposer = capi.Gui.CreateCompo("QuestFinalDialog-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(titleText, () => TryClose())
                .BeginChildElements(bgBounds);

            SingleComposer
                .BeginClip(clippingBounds)
                    .AddRichtext(bodyText, CairoFont.WhiteSmallishText(), textBounds, "finaltext")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar")
                .AddIf(!string.IsNullOrEmpty(option1LangKey) && !string.IsNullOrEmpty(option2LangKey))
                    .AddButton(LangUtil.GetSafe(option1LangKey), TryClose, leftButtonBounds)
                    .AddButton(LangUtil.GetSafe(option2LangKey), TryClose, rightButtonBounds)
                .EndIf()
                .AddIf(!string.IsNullOrEmpty(option1LangKey) && string.IsNullOrEmpty(option2LangKey))
                    .AddButton(LangUtil.GetSafe(option1LangKey), TryClose, closeButtonBounds)
                .EndIf()
                .AddIf(string.IsNullOrEmpty(option1LangKey))
                    .AddButton(Lang.Get("vsquest:button-cancel"), TryClose, closeButtonBounds)
                .EndIf()
                .EndChildElements()
                .Compose();

            SingleComposer.GetScrollbar("scrollbar")?.SetHeights((float)textBounds.fixedHeight, (float)textBounds.fixedHeight);
            SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("finaltext").TotalHeight);
            SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
        }

        private void OnNewScrollbarvalue(float value)
        {
            var textArea = SingleComposer.GetRichtext("finaltext");
            textArea.Bounds.fixedY = -value;
            textArea.Bounds.CalcWorldBounds();
        }
    }
}
