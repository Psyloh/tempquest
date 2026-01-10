using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class QuestSelectGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private long questGiverId;
        private string selectedAvailableQuestId;
        private ActiveQuest selectedActiveQuest;

        private List<string> availableQuestIds;
        private List<ActiveQuest> activeQuests;
        private IClientPlayer player;
        private string noAvailableQuestDescLangKey;
        private string noAvailableQuestCooldownDescLangKey;
        private int noAvailableQuestCooldownDaysLeft;

        private int curTab = 0;
        private bool closeGuiAfterAcceptingAndCompleting;
        public QuestSelectGui(ICoreClientAPI capi, long questGiverId, List<string> availableQuestIds, List<ActiveQuest> activeQuests, QuestConfig questConfig, string noAvailableQuestDescLangKey = null, string noAvailableQuestCooldownDescLangKey = null, int noAvailableQuestCooldownDaysLeft = 0) : base(capi)
        {
            player = capi.World.Player;
            closeGuiAfterAcceptingAndCompleting = questConfig.CloseGuiAfterAcceptingAndCompleting;
            ApplyData(questGiverId, availableQuestIds, activeQuests, noAvailableQuestDescLangKey, noAvailableQuestCooldownDescLangKey, noAvailableQuestCooldownDaysLeft);
            recompose();
        }

        private void ApplyData(long questGiverId, List<string> availableQuestIds, List<ActiveQuest> activeQuests, string noAvailableQuestDescLangKey, string noAvailableQuestCooldownDescLangKey, int noAvailableQuestCooldownDaysLeft)
        {
            this.questGiverId = questGiverId;
            this.availableQuestIds = availableQuestIds;
            this.activeQuests = activeQuests;
            this.noAvailableQuestDescLangKey = noAvailableQuestDescLangKey;
            this.noAvailableQuestCooldownDescLangKey = noAvailableQuestCooldownDescLangKey;
            this.noAvailableQuestCooldownDaysLeft = noAvailableQuestCooldownDaysLeft;

            selectedActiveQuest = activeQuests?.Find(quest => true);
            curTab = (activeQuests != null && activeQuests.Count > 0) ? 1 : 0;
        }

        private void recompose()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            ElementBounds questTextBounds = ElementBounds.Fixed(0, 60, 400, 500);
            ElementBounds scrollbarBounds = questTextBounds.CopyOffsetedSibling(questTextBounds.fixedWidth + 10).WithFixedWidth(20).WithFixedHeight(questTextBounds.fixedHeight);
            ElementBounds clippingBounds = questTextBounds.ForkBoundingParent();
            ElementBounds bottomLeftButtonBounds = ElementBounds.Fixed(10, 570, 200, 20);
            ElementBounds bottomRightButtonBounds = ElementBounds.Fixed(220, 570, 200, 20);

            GuiTab[] tabs = new GuiTab[] {
                new GuiTab() { Name = Lang.Get("alegacyvsquest:tab-available-quests"), DataInt = 0 },
                new GuiTab() { Name = Lang.Get("alegacyvsquest:tab-active-quests"), DataInt = 1 }
            };

            bgBounds.BothSizing = ElementSizing.FitToChildren;
            SingleComposer = capi.Gui.CreateCompo("QuestSelectDialog-", dialogBounds)
                            .AddShadedDialogBG(bgBounds)
                            .AddDialogTitleBar(Lang.Get("alegacyvsquest:quest-select-title"), () => TryClose())
                            .AddVerticalTabs(tabs, ElementBounds.Fixed(-200, 35, 200, 200), OnTabClicked, "tabs")
                            .BeginChildElements(bgBounds);
            SingleComposer.GetVerticalTab("tabs").ActiveElement = curTab;
            if (curTab == 0)
            {
                if (availableQuestIds != null && availableQuestIds.Count > 0)
                {
                    selectedAvailableQuestId = availableQuestIds[0];
                    SingleComposer.AddDropDown(availableQuestIds.ToArray(), availableQuestIds.ConvertAll<string>(id => Lang.Get(id + "-title")).ToArray(), 0, onAvailableQuestSelectionChanged, ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, 400, 30))
                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, bottomLeftButtonBounds)
                        .AddButton(Lang.Get("alegacyvsquest:button-accept"), acceptQuest, bottomRightButtonBounds)
                        .BeginClip(clippingBounds)
                            .AddRichtext(questText(availableQuestIds[0]), CairoFont.WhiteSmallishText(), questTextBounds, "questtext")
                        .EndClip()
                        .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar");
                }
                else
                {
                    string noQuestText = (noAvailableQuestCooldownDaysLeft > 0 && !string.IsNullOrEmpty(noAvailableQuestCooldownDescLangKey))
                        ? LocalizationUtils.GetSafe(noAvailableQuestCooldownDescLangKey, noAvailableQuestCooldownDaysLeft)
                        : LocalizationUtils.GetFallback(noAvailableQuestDescLangKey, "alegacyvsquest:no-quest-available-desc");

                    SingleComposer.AddStaticText(noQuestText, CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 400, 500))
                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20));
                }
            }
            else
            {
                if (activeQuests != null && activeQuests.Count > 0)
                {
                    int selected = selectedActiveQuest == null ? 0 : activeQuests.FindIndex(match => match.questId == selectedActiveQuest.questId);
                    SingleComposer.AddDropDown(activeQuests.ConvertAll<string>(quest => quest.questId).ToArray(), activeQuests.ConvertAll<string>(quest => Lang.Get(quest.questId + "-title")).ToArray(), selected, onActiveQuestSelectionChanged, ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, 400, 30))
                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, bottomLeftButtonBounds)
                        .AddIf(selectedActiveQuest.IsCompletableOnClient)
                            .AddButton(Lang.Get("alegacyvsquest:button-complete"), completeQuest, bottomRightButtonBounds)
                        .EndIf()

                        .BeginClip(clippingBounds)
                            .AddRichtext(activeQuestText(selectedActiveQuest), CairoFont.WhiteSmallishText(), questTextBounds, "questtext")
                        .EndClip()
                        .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar");
                }
                else
                {
                    SingleComposer.AddStaticText(Lang.Get("alegacyvsquest:no-quest-active-desc"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 400, 500))
                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20));
                }
            }
            ;
            SingleComposer.GetScrollbar("scrollbar")?.SetHeights((float)questTextBounds.fixedHeight, (float)questTextBounds.fixedHeight);
            SingleComposer.EndChildElements()
                    .Compose();
            SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("questtext").TotalHeight);
            SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
        }

        private void OnNewScrollbarvalue(float value)
        {
            var textArea = SingleComposer.GetRichtext("questtext");

            textArea.Bounds.fixedY = -value;
            textArea.Bounds.CalcWorldBounds();
        }

        private void OnTabClicked(int id, GuiTab tab)
        {
            curTab = id;
            recompose();
        }

        private string questText(string questId)
        {
            return Lang.Get(questId + "-desc");
        }

        private string activeQuestText(ActiveQuest quest)
        {
            return quest.ProgressText;
        }

        private bool acceptQuest()
        {
            var message = new QuestAcceptedMessage()
            {
                questGiverId = questGiverId,
                questId = selectedAvailableQuestId
            };
            capi.Network.GetChannel("vsquest").SendPacket(message);
            if (closeGuiAfterAcceptingAndCompleting)
            {
                TryClose();
            }
            else
            {
                availableQuestIds.Remove(selectedAvailableQuestId);
                recompose();
            }
            return true;
        }

        private bool completeQuest()
        {
            var message = new QuestCompletedMessage()
            {
                questGiverId = questGiverId,
                questId = selectedActiveQuest.questId
            };
            capi.Network.GetChannel("vsquest").SendPacket(message);
            if (closeGuiAfterAcceptingAndCompleting)
            {
                TryClose();
            }
            else
            {
                activeQuests.RemoveAll(quest => selectedActiveQuest.questId == quest.questId);
                recompose();
            }
            return true;
        }
        private void onAvailableQuestSelectionChanged(string questId, bool selected)
        {
            if (selected)
            {
                selectedAvailableQuestId = questId;
                SingleComposer.GetRichtext("questtext").SetNewText(questText(questId), CairoFont.WhiteSmallishText());
                SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("questtext").TotalHeight);
            }
        }

        private void onActiveQuestSelectionChanged(string questId, bool selected)
        {
            if (selected)
            {
                selectedActiveQuest = activeQuests.Find(quest => quest.questId == questId);
                SingleComposer.GetRichtext("questtext").SetNewText(questText(questId), CairoFont.WhiteSmallishText());
                recompose();
            }
        }

        public void UpdateFromMessage(QuestInfoMessage message)
        {
            if (message == null) return;

            ApplyData(message.questGiverId, message.availableQestIds, message.activeQuests, message.noAvailableQuestDescLangKey, message.noAvailableQuestCooldownDescLangKey, message.noAvailableQuestCooldownDaysLeft);

            recompose();
        }
    }
}