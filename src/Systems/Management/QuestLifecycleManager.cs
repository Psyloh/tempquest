using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestLifecycleManager
    {
        private readonly Dictionary<string, Quest> questRegistry;
        private readonly Dictionary<string, QuestAction> actionRegistry;
        private readonly ICoreAPI api;

        public QuestLifecycleManager(Dictionary<string, Quest> questRegistry, Dictionary<string, QuestAction> actionRegistry, ICoreAPI api)
        {
            this.questRegistry = questRegistry;
            this.actionRegistry = actionRegistry;
            this.api = api;
        }

        private List<EventTracker> CreateTrackers(List<Objective> objectives)
        {
            var trackers = new List<EventTracker>();
            foreach (var objective in objectives)
            {
                var tracker = new EventTracker()
                {
                    count = 0,
                    relevantCodes = new List<string>(objective.validCodes)
                };
                trackers.Add(tracker);
            }
            return trackers;
        }

        public void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi, System.Func<string, List<ActiveQuest>> getPlayerQuests)
        {
            var quest = questRegistry[message.questId];
            var playerQuests = getPlayerQuests(fromPlayer.PlayerUID);

            if (playerQuests.Exists(q => q.questId == message.questId))
            {
                return;
            }

            var killTrackers = CreateTrackers(quest.killObjectives);
            var blockPlaceTrackers = CreateTrackers(quest.blockPlaceObjectives);
            var blockBreakTrackers = CreateTrackers(quest.blockBreakObjectives);

            var activeQuest = new ActiveQuest()
            {
                questGiverId = message.questGiverId,
                questId = message.questId,
                killTrackers = killTrackers,
                blockPlaceTrackers = blockPlaceTrackers,
                blockBreakTrackers = blockBreakTrackers
            };
            playerQuests.Add(activeQuest);
            var questgiver = sapi.World.GetEntityById(message.questGiverId);
            var key = String.Format("vsquest:lastaccepted-{0}", quest.id);
            fromPlayer.Entity.WatchedAttributes.SetDouble(key, sapi.World.Calendar.TotalDays);
            fromPlayer.Entity.WatchedAttributes.MarkPathDirty(key);

            if (questgiver != null)
            {
                var legacyKey = quest.perPlayer ? String.Format("lastaccepted-{0}-{1}", quest.id, fromPlayer.PlayerUID) : String.Format("lastaccepted-{0}", quest.id);
                questgiver.WatchedAttributes.SetDouble(legacyKey, sapi.World.Calendar.TotalDays);
                questgiver.WatchedAttributes.MarkPathDirty(legacyKey);
            }
            foreach (var action in quest.onAcceptedActions)
            {
                try
                {
                    actionRegistry[action.id].Invoke(sapi, message, fromPlayer, action.args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error(string.Format("Action {0} caused an Error in Quest {1}. The Error had the following message: {2}\n Stacktrace:", action.id, quest.id, ex.Message, ex.StackTrace));
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, string.Format("An error occurred during quest {0}, please check the server logs for more details.", quest.id), EnumChatType.Notification);
                }
            }
        }

        public void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, System.Func<string, List<ActiveQuest>> getPlayerQuests)
        {
            var playerQuests = getPlayerQuests(fromPlayer.PlayerUID);
            var activeQuest = playerQuests.Find(item => item.questId == message.questId);
            if (activeQuest.isCompletable(fromPlayer))
            {
                activeQuest.completeQuest(fromPlayer);
                playerQuests.Remove(activeQuest);
                var questgiver = sapi.World.GetEntityById(message.questGiverId);
                RewardPlayer(fromPlayer, message, sapi, questgiver);
                MarkQuestCompleted(fromPlayer, message, questgiver);
            }
            else
            {
                sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, "Something went wrong, the quest could not be completed", EnumChatType.Notification);
            }
        }

        private void RewardPlayer(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, Entity questgiver)
        {
            var quest = questRegistry[message.questId];
            foreach (var reward in quest.itemRewards)
            {
                CollectibleObject item = sapi.World.GetItem(new AssetLocation(reward.itemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(reward.itemCode));
                }
                var stack = new ItemStack(item, reward.amount);
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, (questgiver ?? fromPlayer.Entity).ServerPos.XYZ);
                }
            }
            List<RandomItem> randomItems = quest.randomItemRewards.items;
            for (int i = 0; i < quest.randomItemRewards.selectAmount; i++)
            {
                if (randomItems.Count <= 0) break;
                var randomItem = randomItems[sapi.World.Rand.Next(0, randomItems.Count)];
                randomItems.Remove(randomItem);
                CollectibleObject item = sapi.World.GetItem(new AssetLocation(randomItem.itemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(randomItem.itemCode));
                }
                var stack = new ItemStack(item, sapi.World.Rand.Next(randomItem.minAmount, randomItem.maxAmount + 1));
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, (questgiver ?? fromPlayer.Entity).ServerPos.XYZ);
                }
            }
            foreach (var action in quest.actionRewards)
            {
                try
                {
                    actionRegistry[action.id].Invoke(sapi, message, fromPlayer, action.args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error(string.Format("Action {0} caused an Error in Quest {1}. The Error had the following message: {2}\n Stacktrace:", action.id, quest.id, ex.Message, ex.StackTrace));
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, string.Format("An error occurred during quest {0}, please check the server logs for more details.", quest.id), EnumChatType.Notification);
                }
            }
        }

        private static void MarkQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, Entity questgiver)
        {
            var completedQuests = new HashSet<string>(fromPlayer.Entity.WatchedAttributes.GetStringArray("vsquest:playercompleted", new string[0]));
            completedQuests.Add(message.questId);
            var completedQuestsArray = new string[completedQuests.Count];
            completedQuests.CopyTo(completedQuestsArray);
            fromPlayer.Entity.WatchedAttributes.SetStringArray("vsquest:playercompleted", completedQuestsArray);
            fromPlayer.Entity.WatchedAttributes.MarkAllDirty();
        }
    }
}
