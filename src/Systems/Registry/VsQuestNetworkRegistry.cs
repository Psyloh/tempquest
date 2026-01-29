using System;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class VsQuestNetworkRegistry
    {
        public const string QuestChannelName = "alegacyvsquest";
        public const string ItemActionChannelName = "alegacyvsquest-itemaction";
        public const string BossMusicChannelName = "alegacyvsquestmusic";

        public static void RegisterQuestClient(ICoreClientAPI capi, QuestSystem questSystem)
        {
            capi.Network.RegisterChannel(QuestChannelName)
                .RegisterMessageType<QuestAcceptedMessage>()
                .RegisterMessageType<QuestCompletedMessage>()
                .RegisterMessageType<QuestInfoMessage>().SetMessageHandler<QuestInfoMessage>(message => questSystem.OnQuestInfoMessage(message, capi))
                .RegisterMessageType<ExecutePlayerCommandMessage>().SetMessageHandler<ExecutePlayerCommandMessage>(message => questSystem.OnExecutePlayerCommand(message, capi))
                .RegisterMessageType<VanillaBlockInteractMessage>()
                .RegisterMessageType<ShowNotificationMessage>().SetMessageHandler<ShowNotificationMessage>(message => questSystem.OnShowNotificationMessage(message, capi))
                .RegisterMessageType<ShowDiscoveryMessage>().SetMessageHandler<ShowDiscoveryMessage>(message => questSystem.OnShowDiscoveryMessage(message, capi))
                .RegisterMessageType<ShowQuestDialogMessage>().SetMessageHandler<ShowQuestDialogMessage>(message => questSystem.OnShowQuestDialogMessage(message, capi))
                .RegisterMessageType<ClaimReputationRewardsMessage>()
                .RegisterMessageType<ClaimQuestCompletionRewardMessage>()
                .RegisterMessageType<PreloadBossMusicMessage>().SetMessageHandler<PreloadBossMusicMessage>(message => questSystem.OnPreloadBossMusicMessage(message, capi));
        }

        public static void RegisterQuestServer(ICoreServerAPI sapi, QuestSystem questSystem)
        {
            sapi.Network.RegisterChannel(QuestChannelName)
                .RegisterMessageType<QuestAcceptedMessage>().SetMessageHandler<QuestAcceptedMessage>((player, message) => questSystem.OnQuestAccepted(player, message, sapi))
                .RegisterMessageType<QuestCompletedMessage>().SetMessageHandler<QuestCompletedMessage>((player, message) => questSystem.OnQuestCompleted(player, message, sapi))
                .RegisterMessageType<QuestInfoMessage>()
                .RegisterMessageType<ExecutePlayerCommandMessage>()
                .RegisterMessageType<VanillaBlockInteractMessage>().SetMessageHandler<VanillaBlockInteractMessage>((player, message) => questSystem.OnVanillaBlockInteract(player, message, sapi))
                .RegisterMessageType<ShowNotificationMessage>()
                .RegisterMessageType<ShowDiscoveryMessage>()
                .RegisterMessageType<ShowQuestDialogMessage>()
                .RegisterMessageType<ClaimReputationRewardsMessage>().SetMessageHandler<ClaimReputationRewardsMessage>((player, message) => questSystem.OnClaimReputationRewardsMessage(player, message, sapi))
                .RegisterMessageType<ClaimQuestCompletionRewardMessage>().SetMessageHandler<ClaimQuestCompletionRewardMessage>((player, message) => questSystem.OnClaimQuestCompletionRewardMessage(player, message, sapi))
                .RegisterMessageType<PreloadBossMusicMessage>();
        }

        public static IServerNetworkChannel RegisterItemActionServer(ICoreServerAPI sapi, ActionItemPacketHandler packetHandler)
        {
            return sapi.Network.RegisterChannel(ItemActionChannelName)
                .RegisterMessageType<ExecuteActionItemPacket>()
                .SetMessageHandler<ExecuteActionItemPacket>(packetHandler.HandlePacket);
        }

        public static IClientNetworkChannel RegisterItemActionClient(ICoreClientAPI capi)
        {
            return capi.Network.RegisterChannel(ItemActionChannelName)
                .RegisterMessageType<ExecuteActionItemPacket>();
        }

        public static IClientNetworkChannel RegisterBossMusicClient(ICoreClientAPI capi, NetworkServerMessageHandler<BossMusicUrlMapMessage> handler)
        {
            return capi.Network.RegisterChannel(BossMusicChannelName)
                .RegisterMessageType<BossMusicUrlMapMessage>()
                .SetMessageHandler<BossMusicUrlMapMessage>(handler);
        }
    }
}
