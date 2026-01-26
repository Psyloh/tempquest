using Vintagestory.API.Client;

namespace VsQuest
{
    public class QuestSelectGuiManager
    {
        private QuestSelectGui questSelectGui;
        private readonly QuestConfig config;

        public QuestSelectGuiManager(QuestConfig config)
        {
            this.config = config;
        }

        public void HandleQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            if (questSelectGui == null)
            {
                questSelectGui = CreateQuestSelectGui(message, capi);
                questSelectGui.TryOpen();
                return;
            }

            if (questSelectGui.IsOpened())
            {
                questSelectGui.UpdateFromMessage(message);
                return;
            }

            questSelectGui = CreateQuestSelectGui(message, capi);
            questSelectGui.TryOpen();
        }

        private QuestSelectGui CreateQuestSelectGui(QuestInfoMessage message, ICoreClientAPI capi)
        {
            var gui = new QuestSelectGui(capi, message, config);
            gui.OnClosed += () =>
            {
                if (questSelectGui != null && !questSelectGui.IsOpened())
                {
                    questSelectGui = null;
                }
            };
            return gui;
        }
    }
}
