using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest.Gui.Journal
{
    public class QuestEntryPage : JournalPage
    {
        private string questId;
        private string entryTitle;
        private List<string> chapters;
        private RichTextComponentBase[] components;

        public override string PageCode => "entry-" + questId;
        public override string CategoryCode => "quests";

        public QuestEntryPage(ICoreClientAPI capi, string questId, string entryTitle, List<string> chapters) : base(capi)
        {
            this.questId = questId;
            this.entryTitle = entryTitle;
            this.chapters = chapters ?? new List<string>();
            this.titleCached = entryTitle?.ToLowerInvariant() ?? "";
        }

        public string QuestId => questId;
        public string EntryTitle => entryTitle;

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            if (Texture == null)
            {
                Texture = new TextTextureUtil(capi).GenTextTexture(entryTitle, CairoFont.WhiteSmallText());
            }
            RenderTextureIfExists(x, y);
        }

        public override float GetTextMatchWeight(string searchText)
        {
            if (string.IsNullOrEmpty(searchText)) return 1f;
            if (titleCached.Equals(searchText, StringComparison.OrdinalIgnoreCase)) return 4f;
            if (titleCached.StartsWith(searchText, StringComparison.OrdinalIgnoreCase)) return 3f;
            if (titleCached.Contains(searchText, StringComparison.OrdinalIgnoreCase)) return 2.5f;

            foreach (var chapter in chapters)
            {
                if (chapter?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return 1.5f;
                }
            }
            return 0f;
        }

        public override void ComposePage(GuiComposer composer, ElementBounds textBounds, ActionConsumable<string> openDetailPageFor)
        {
            if (components == null)
            {
                string text = GetFormattedText();
                components = VtmlUtil.Richtextify(capi, text, CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2));
            }
            ElementBounds contentBounds = textBounds.FlatCopy().WithFixedOffset(4.0, 0.0);
            contentBounds.fixedWidth = Math.Max(0.0, contentBounds.fixedWidth - 4.0);
            composer.AddRichtext(components, contentBounds, "richtext");
        }

        private string GetFormattedText()
        {
            if (chapters == null || chapters.Count == 0)
            {
                return Lang.Get("alegacyvsquest:entry-no-content");
            }

            return string.Join("\n\n", chapters
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Replace("\\r\\n", "\n").Replace("\\n", "\n")));
        }

        public override void Dispose()
        {
            base.Dispose();
            components = null;
        }
    }
}
