using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class QuestSelectGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private const string DropDownKey = "questdropdown";

        private bool recomposeQueued;

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
            RequestRecompose();
        }

        private void RequestRecompose()
        {
            if (recomposeQueued) return;
            recomposeQueued = true;

            capi.Event.EnqueueMainThreadTask(() =>
            {
                recomposeQueued = false;
                recompose();
            }, "vsquest-recompose");
        }

        private void CloseOpenedDropDownDeferred()
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                CloseOpenedDropDown();
            }, "vsquest-closedropdown");
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

            // Preserve the currently selected tab when updating data.
            // Only switch tabs if the current tab has no content.
            bool hasAvailable = availableQuestIds != null && availableQuestIds.Count > 0;
            bool hasActive = activeQuests != null && activeQuests.Count > 0;

            if (curTab == 0 && !hasAvailable && hasActive)
            {
                curTab = 1;
            }
            else if (curTab == 1 && !hasActive && hasAvailable)
            {
                curTab = 0;
            }
            else if (curTab != 0 && curTab != 1)
            {
                // Initial state fallback
                curTab = hasAvailable ? 0 : (hasActive ? 1 : 0);
            }
        }

        private void recompose()
        {
            CloseOpenedDropDown();
            var prevComposer = SingleComposer;
            if (prevComposer != null)
            {
                prevComposer.Dispose();
                Composers.Remove("single");
            }

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

            // GuiElementVerticalTabs constructor forces tabs[0].Active = true.
            // Force the correct active tab for visual highlight.
            SingleComposer.GetVerticalTab("tabs").SetValue(curTab, false);

            if (curTab == 0)
            {
                if (availableQuestIds != null && availableQuestIds.Count > 0)
                {
                    if (string.IsNullOrEmpty(selectedAvailableQuestId) || !availableQuestIds.Contains(selectedAvailableQuestId))
                    {
                        selectedAvailableQuestId = availableQuestIds[0];
                    }

                    int selectedIndex = Math.Max(0, availableQuestIds.IndexOf(selectedAvailableQuestId));

                    SingleComposer.AddDropDown(availableQuestIds.ToArray(), availableQuestIds.ConvertAll<string>(id => Lang.Get(id + "-title")).ToArray(), selectedIndex, onAvailableQuestSelectionChanged, ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, 400, 30), DropDownKey)
                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, bottomLeftButtonBounds)
                        .AddButton(Lang.Get("alegacyvsquest:button-accept"), acceptQuest, bottomRightButtonBounds)
                        .BeginClip(clippingBounds)
                            .AddRichtext(questText(selectedAvailableQuestId), CairoFont.WhiteSmallishText(), questTextBounds, "questtext")
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
                    if (selectedActiveQuest == null || activeQuests.FindIndex(match => match.questId == selectedActiveQuest.questId) < 0)
                    {
                        selectedActiveQuest = activeQuests[0];
                    }

                    int selected = Math.Max(0, activeQuests.FindIndex(match => match.questId == selectedActiveQuest.questId));

                    SingleComposer.AddDropDown(activeQuests.ConvertAll<string>(quest => quest.questId).ToArray(), activeQuests.ConvertAll<string>(quest => Lang.Get(quest.questId + "-title")).ToArray(), selected, onActiveQuestSelectionChanged, ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, 400, 30), DropDownKey)
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
            CloseOpenedDropDown();
            curTab = id;
            RequestRecompose();
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
                RequestRecompose();
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
                RequestRecompose();
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

                SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
                OnNewScrollbarvalue(0);

                capi.Event.EnqueueMainThreadTask(() =>
                {
                    CloseOpenedDropDown();
                    RequestRecompose();
                }, "vsquest-availablequestchanged");
            }
        }

        private void onActiveQuestSelectionChanged(string questId, bool selected)
        {
            if (selected)
            {
                selectedActiveQuest = activeQuests.Find(quest => quest.questId == questId);

                if (selectedActiveQuest == null)
                {
                    return;
                }

                SingleComposer.GetRichtext("questtext").SetNewText(activeQuestText(selectedActiveQuest), CairoFont.WhiteSmallishText());
                SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("questtext").TotalHeight);

                capi.Event.EnqueueMainThreadTask(() =>
                {
                    CloseOpenedDropDown();
                    RequestRecompose();
                }, "vsquest-activequestchanged");
            }
        }

        public void UpdateFromMessage(QuestInfoMessage message)
        {
            if (message == null) return;

            CloseOpenedDropDown();
            ApplyData(message.questGiverId, message.availableQestIds, message.activeQuests, message.noAvailableQuestDescLangKey, message.noAvailableQuestCooldownDescLangKey, message.noAvailableQuestCooldownDaysLeft);

            RequestRecompose();
        }

        private void CloseOpenedDropDown()
        {
            var dropdown = SingleComposer?.GetDropDown(DropDownKey);
            if (dropdown?.listMenu?.IsOpened == true)
            {
                try
                {
                    // GuiElementListMenu.Close() is internal in some VS API builds; invoke it via reflection.
                    MethodInfo closeMethod = dropdown.listMenu.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    closeMethod?.Invoke(dropdown.listMenu, null);
                }
                catch
                {
                }

                // Fallback: still attempt to close via focus loss
                dropdown.listMenu.OnFocusLost();
                dropdown.OnFocusLost();
                SingleComposer?.UnfocusOwnElements();
            }
        }

        public override void OnMouseDown(MouseEvent args)
        {
            var dropdown = SingleComposer?.GetDropDown(DropDownKey);
            if (dropdown?.listMenu?.IsOpened == true && !dropdown.IsPositionInside(args.X, args.Y))
            {
                CloseOpenedDropDown();
            }

            base.OnMouseDown(args);
        }

        public override void OnGuiClosed()
        {
            CloseOpenedDropDown();
            base.OnGuiClosed();
        }
    }
}