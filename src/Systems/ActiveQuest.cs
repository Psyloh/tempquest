using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32.SafeHandles;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ActiveQuest
    {
        public long questGiverId { get; set; }
        public string questId { get; set; }
        public List<EventTracker> killTrackers { get; set; } = new List<EventTracker>();
        public List<EventTracker> blockPlaceTrackers { get; set; } = new List<EventTracker>();
        public List<EventTracker> blockBreakTrackers { get; set; } = new List<EventTracker>();
        public List<EventTracker> interactTrackers { get; set; } = new List<EventTracker>();
        public bool IsCompletableOnClient { get; set; }
        public string ProgressText { get; set; }

        public void OnEntityKilled(string entityCode, IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, questSystem?.ActionObjectiveRegistry, "kill")) return;

            checkEventTrackers(killTrackers, entityCode, null, quest.killObjectives);
        }

        public void OnBlockPlaced(string blockCode, int[] position, IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, questSystem?.ActionObjectiveRegistry, "blockplace")) return;

            checkEventTrackers(blockPlaceTrackers, blockCode, position, quest.blockPlaceObjectives);
        }

        public void OnBlockBroken(string blockCode, int[] position, IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, questSystem?.ActionObjectiveRegistry, "blockbreak")) return;

            checkEventTrackers(blockBreakTrackers, blockCode, position, quest.blockBreakObjectives);
        }

        public void OnBlockUsed(string blockCode, int[] position, IPlayer byPlayer, ICoreServerAPI sapi)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];

            if (byPlayer?.Entity?.WatchedAttributes != null && position != null && position.Length == 3)
            {
                byPlayer.Entity.WatchedAttributes.SetInt("alegacyvsquest:lastinteract:x", position[0]);
                byPlayer.Entity.WatchedAttributes.SetInt("alegacyvsquest:lastinteract:y", position[1]);
                byPlayer.Entity.WatchedAttributes.SetInt("alegacyvsquest:lastinteract:z", position[2]);
                byPlayer.Entity.WatchedAttributes.SetInt("alegacyvsquest:lastinteract:dim", byPlayer.Entity?.Pos?.Dimension ?? 0);
                byPlayer.Entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:lastinteract:x");
                byPlayer.Entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:lastinteract:y");
                byPlayer.Entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:lastinteract:z");
                byPlayer.Entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:lastinteract:dim");

                // Backfill legacy keys for compatibility with older code paths.
                byPlayer.Entity.WatchedAttributes.SetInt("vsquest:lastinteract:x", position[0]);
                byPlayer.Entity.WatchedAttributes.SetInt("vsquest:lastinteract:y", position[1]);
                byPlayer.Entity.WatchedAttributes.SetInt("vsquest:lastinteract:z", position[2]);
                byPlayer.Entity.WatchedAttributes.SetInt("vsquest:lastinteract:dim", byPlayer.Entity?.Pos?.Dimension ?? 0);
                byPlayer.Entity.WatchedAttributes.MarkPathDirty("vsquest:lastinteract:x");
                byPlayer.Entity.WatchedAttributes.MarkPathDirty("vsquest:lastinteract:y");
                byPlayer.Entity.WatchedAttributes.MarkPathDirty("vsquest:lastinteract:z");
                byPlayer.Entity.WatchedAttributes.MarkPathDirty("vsquest:lastinteract:dim");
            }

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, questSystem?.ActionObjectiveRegistry, "interact")) return;

            var serverPlayer = byPlayer as IServerPlayer;
            if (serverPlayer != null)
            {
                QuestInteractAtUtil.TryHandleInteractAtObjectives(quest, this, serverPlayer, position, sapi);
            }

            checkEventTrackers(interactTrackers, blockCode, position, quest.interactObjectives);
            for (int i = 0; i < quest.interactObjectives.Count; i++)
            {
                var objective = quest.interactObjectives[i];

                bool matches = QuestObjectiveMatchUtil.InteractObjectiveMatches(objective, blockCode, position);
                if (!matches) continue;

                if (serverPlayer != null)
                {
                    var message = new QuestAcceptedMessage { questGiverId = questGiverId, questId = questId };
                    foreach (var actionReward in objective.actionRewards)
                    {
                        if (questSystem.ActionRegistry.TryGetValue(actionReward.id, out var action))
                        {
                            action.Execute(sapi, message, serverPlayer, actionReward.args);
                        }
                    }
                }
            }
        }

        private void checkEventTrackers(List<EventTracker> trackers, string code, int[] position, List<Objective> objectives)
        {
            foreach (var tracker in trackers)
            {
                if (position == null)
                {
                    if (trackerMatches(tracker, code))
                    {
                        tracker.count++;
                    }
                }
                else
                {
                    var index = trackers.IndexOf(tracker);
                    if (index != -1 && trackerMatches(objectives[index], tracker, code, position))
                    {
                        tracker.count++;
                    }
                }
            }
        }

        private static bool trackerMatches(EventTracker tracker, string code)
        {
            foreach (var candidate in tracker.relevantCodes)
            {
                if (LocalizationUtils.MobCodeMatches(candidate, code))
                {
                    return true;
                }

                if (candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool trackerMatches(Objective objective, EventTracker tracker, string code, int[] position)
        {
            if (objective.positions != null && objective.positions.Count > 0)
            {
                foreach (var candidate in objective.positions)
                {
                    var pos = candidate.Split(',').Select(int.Parse).ToArray();
                    if (pos.Length == 3 && pos[0] == position[0] && pos[1] == position[1] && pos[2] == position[2])
                    {
                        // If validCodes is missing, treat position match as sufficient.
                        if (objective.validCodes == null || objective.validCodes.Count == 0)
                        {
                            if (tracker.placedPositions.Contains(candidate)) return false;
                            tracker.placedPositions.Add(candidate);
                            return true;
                        }

                        foreach (var codeCandidate in objective.validCodes)
                        {
                            if (LocalizationUtils.MobCodeMatches(codeCandidate, code))
                            {
                                if (tracker.placedPositions.Contains(candidate)) return false;
                                tracker.placedPositions.Add(candidate);
                                return true;
                            }

                            if (codeCandidate.EndsWith("*") && code.StartsWith(codeCandidate.Remove(codeCandidate.Length - 1)))
                            {
                                if (tracker.placedPositions.Contains(candidate)) return false;
                                tracker.placedPositions.Add(candidate);
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
            else
            {
                foreach (var candidate in tracker.relevantCodes)
                {
                    if (LocalizationUtils.MobCodeMatches(candidate, code))
                    {
                        return true;
                    }

                    if (candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool IsCompletable(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];
            var activeActionObjectives = quest.actionObjectives.ConvertAll<ActionObjectiveBase>(objective => questSystem.ActionObjectiveRegistry[objective.id]);
            bool completable = true;

            while (blockPlaceTrackers.Count < quest.blockPlaceObjectives.Count)
            {
                blockPlaceTrackers.Add(new EventTracker());
            }
            while (blockBreakTrackers.Count < quest.blockBreakObjectives.Count)
            {
                blockBreakTrackers.Add(new EventTracker());
            }
            while (killTrackers.Count < quest.killObjectives.Count)
            {
                killTrackers.Add(new EventTracker());
            }
            while (interactTrackers.Count < quest.interactObjectives.Count)
            {
                interactTrackers.Add(new EventTracker());
            }

            for (int i = 0; i < quest.blockPlaceObjectives.Count; i++)
            {
                if (quest.blockPlaceObjectives[i].positions != null && quest.blockPlaceObjectives[i].positions.Count > 0)
                {
                    completable &= quest.blockPlaceObjectives[i].positions.Count <= blockPlaceTrackers[i].placedPositions.Count;
                }
                else
                {
                    completable &= quest.blockPlaceObjectives[i].demand <= blockPlaceTrackers[i].count;
                }
            }
            for (int i = 0; i < quest.blockBreakObjectives.Count; i++)
            {
                completable &= quest.blockBreakObjectives[i].demand <= blockBreakTrackers[i].count;
            }
            for (int i = 0; i < quest.interactObjectives.Count; i++)
            {
                if (quest.interactObjectives[i].positions != null && quest.interactObjectives[i].positions.Count > 0)
                {
                    int demand = quest.interactObjectives[i].demand > 0 ? quest.interactObjectives[i].demand : quest.interactObjectives[i].positions.Count;
                    completable &= demand <= interactTrackers[i].count;
                }
                else
                {
                    completable &= quest.interactObjectives[i].demand <= interactTrackers[i].count;
                }
            }
            for (int i = 0; i < quest.killObjectives.Count; i++)
            {
                completable &= quest.killObjectives[i].demand <= killTrackers[i].count;
            }
            foreach (var gatherObjective in quest.gatherObjectives)
            {
                int itemsFound = itemsGathered(byPlayer, gatherObjective);
                completable &= itemsFound >= gatherObjective.demand;
            }
            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                completable &= activeActionObjectives[i].IsCompletable(byPlayer, quest.actionObjectives[i].args);
            }
            return completable;
        }

        public void completeQuest(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];
            foreach (var gatherObjective in quest.gatherObjectives)
            {
                handOverItems(byPlayer, gatherObjective);
            }
            for (int i = 0; i < quest.blockPlaceObjectives.Count; i++)
            {
                if (quest.blockPlaceObjectives[i].removeAfterFinished && i < blockPlaceTrackers.Count)
                {
                    foreach (var posStr in blockPlaceTrackers[i].placedPositions)
                    {
                        var pos = posStr.Split(',').Select(int.Parse).ToArray();
                        byPlayer.Entity.World.BlockAccessor.SetBlock(0, new Vintagestory.API.MathTools.BlockPos(pos[0], pos[1], pos[2]));
                    }
                }
            }
        }

        public List<int> trackerProgress()
        {
            var result = new List<int>();
            foreach (var trackerList in new List<EventTracker>[] { killTrackers, blockPlaceTrackers, blockBreakTrackers, interactTrackers })
            {
                if (trackerList != null)
                {
                    result.AddRange(trackerList.ConvertAll<int>(tracker => tracker.count));
                }
            }
            return result;
        }

        public List<int> gatherProgress(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];
            return quest.gatherObjectives.ConvertAll<int>(gatherObjective => itemsGathered(byPlayer, gatherObjective));
        }

        public List<int> GetProgress(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];
            var activeActionObjectives = quest.actionObjectives.ConvertAll<ActionObjectiveBase>(objective => questSystem.ActionObjectiveRegistry[objective.id]);

            var result = gatherProgress(byPlayer);
            result.AddRange(trackerProgress());

            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                result.AddRange(activeActionObjectives[i].GetProgress(byPlayer, quest.actionObjectives[i].args));
            }

            return result;
        }

        public int itemsGathered(IPlayer byPlayer, Objective gatherObjective)
        {
            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                {
                    continue;
                }
                foreach (var slot in inventory)
                {
                    if (gatherObjectiveMatches(slot, gatherObjective))
                    {
                        itemsFound += slot.Itemstack.StackSize;
                    }
                }
            }
            ;

            return itemsFound;
        }

        private bool gatherObjectiveMatches(ItemSlot slot, Objective gatherObjective)
        {
            if (slot.Empty) return false;

            var code = slot.Itemstack.Collectible.Code.Path;
            foreach (var candidate in gatherObjective.validCodes)
            {
                if (candidate == code || candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                {
                    return true;
                }
            }
            return false;
        }

        public void handOverItems(IPlayer byPlayer, Objective gatherObjective)
        {
            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                {
                    continue;
                }
                foreach (var slot in inventory)
                {
                    if (gatherObjectiveMatches(slot, gatherObjective))
                    {
                        var stack = slot.TakeOut(Math.Min(slot.Itemstack.StackSize, gatherObjective.demand - itemsFound));
                        slot.MarkDirty();
                        itemsFound += stack.StackSize;
                    }
                    if (itemsFound > gatherObjective.demand) { return; }
                }
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class EventTracker
    {
        public List<string> relevantCodes { get; set; } = new List<string>();
        public int count { get; set; }
        public List<string> placedPositions { get; set; } = new List<string>();
    }
}