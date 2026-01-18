using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestNetworkChannelRegistry
    {
        private readonly QuestSystem questSystem;

        public QuestNetworkChannelRegistry(QuestSystem questSystem)
        {
            this.questSystem = questSystem;
        }

        public void RegisterClient(ICoreClientAPI capi)
        {
            capi.Network.RegisterChannel("alegacyvsquest")
                .RegisterMessageType<QuestAcceptedMessage>()
                .RegisterMessageType<QuestCompletedMessage>()
                .RegisterMessageType<QuestInfoMessage>().SetMessageHandler<QuestInfoMessage>(message => questSystem.OnQuestInfoMessage(message, capi))
                .RegisterMessageType<ExecutePlayerCommandMessage>().SetMessageHandler<ExecutePlayerCommandMessage>(message => questSystem.OnExecutePlayerCommand(message, capi))
                .RegisterMessageType<VanillaBlockInteractMessage>()
                .RegisterMessageType<ShowNotificationMessage>().SetMessageHandler<ShowNotificationMessage>(message => questSystem.OnShowNotificationMessage(message, capi))
                .RegisterMessageType<ShowDiscoveryMessage>().SetMessageHandler<ShowDiscoveryMessage>(message => questSystem.OnShowDiscoveryMessage(message, capi))
                .RegisterMessageType<ShowQuestDialogMessage>().SetMessageHandler<ShowQuestDialogMessage>(message => questSystem.OnShowQuestDialogMessage(message, capi))
                .RegisterMessageType<PreloadBossMusicMessage>().SetMessageHandler<PreloadBossMusicMessage>(message => questSystem.OnPreloadBossMusicMessage(message, capi));
        }

        public void RegisterServer(ICoreServerAPI sapi)
        {
            sapi.Network.RegisterChannel("alegacyvsquest")
                .RegisterMessageType<QuestAcceptedMessage>().SetMessageHandler<QuestAcceptedMessage>((player, message) => questSystem.OnQuestAccepted(player, message, sapi))
                .RegisterMessageType<QuestCompletedMessage>().SetMessageHandler<QuestCompletedMessage>((player, message) => questSystem.OnQuestCompleted(player, message, sapi))
                .RegisterMessageType<QuestInfoMessage>()
                .RegisterMessageType<ExecutePlayerCommandMessage>()
                .RegisterMessageType<VanillaBlockInteractMessage>().SetMessageHandler<VanillaBlockInteractMessage>((player, message) => questSystem.OnVanillaBlockInteract(player, message, sapi))
                .RegisterMessageType<ShowNotificationMessage>()
                .RegisterMessageType<ShowDiscoveryMessage>()
                .RegisterMessageType<ShowQuestDialogMessage>()
                .RegisterMessageType<PreloadBossMusicMessage>();
        }
    }
}
