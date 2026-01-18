using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public static class QuestSystemAdminUtils
    {
        private static void ClearQuestGiverChainCooldowns(IServerPlayer player, ICoreServerAPI sapi)
        {
            if (sapi == null || player?.Entity?.WatchedAttributes == null) return;

            try
            {
                var entities = sapi.World.LoadedEntities?.Values;
                if (entities == null) return;

                foreach (var e in entities)
                {
                    if (e == null) continue;
                    if (!e.HasBehavior<EntityBehaviorQuestGiver>()) continue;

                    string chainKey = EntityBehaviorQuestGiver.ChainCooldownLastCompletedKey(e.EntityId);
                    player.Entity.WatchedAttributes.SetDouble(chainKey, -9999999);
                    player.Entity.WatchedAttributes.MarkPathDirty(chainKey);
                }
            }
            catch
            {
            }
        }

        private static void RemoveQuestJournalEntries(ICoreServerAPI sapi, QuestSystem questSystem, IServerPlayer player, string questId)
        {
            if (sapi == null || player == null || string.IsNullOrWhiteSpace(questId)) return;
            if (player.Entity?.WatchedAttributes == null) return;

            string loreCodesKey = $"alegacyvsquest:journal:{questId}:lorecodes";
            string[] loreCodes = player.Entity.WatchedAttributes.GetStringArray(loreCodesKey, null);
            var groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Backfill from quest definition if no per-player tracking is present (e.g. journal entries created before tracking existed)
            if ((loreCodes == null || loreCodes.Length == 0) && questSystem?.QuestRegistry != null && questSystem.QuestRegistry.TryGetValue(questId, out var quest) && quest != null)
            {
                var fromQuest = new List<string>();
                void AddFromActions(System.Collections.Generic.List<ActionWithArgs> actions)
                {
                    if (actions == null) return;
                    foreach (var a in actions)
                    {
                        if (a == null) continue;
                        if (!string.Equals(a.id, "addjournalentry", StringComparison.OrdinalIgnoreCase)) continue;
                        if (a.args == null || a.args.Length < 1) continue;

                        // New format: [groupId, loreCode, title, ...]
                        // Legacy format: [loreCode, title, ...]
                        if (a.args.Length >= 2)
                        {
                            if (!string.IsNullOrWhiteSpace(a.args[0])) groupIds.Add(a.args[0]);
                            if (!string.IsNullOrWhiteSpace(a.args[1])) fromQuest.Add(a.args[1]);
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(a.args[0])) fromQuest.Add(a.args[0]);
                        }
                    }
                }

                void AddFromActionString(string actionString)
                {
                    if (string.IsNullOrWhiteSpace(actionString)) return;

                    // Match ActionStringExecutor tokenization: allow single-quoted strings for multi-word args.
                    var actionStrings = actionString.Split(';').Select(s => s.Trim());
                    foreach (var singleAction in actionStrings)
                    {
                        if (string.IsNullOrWhiteSpace(singleAction)) continue;

                        var matches = Regex.Matches(singleAction, "(?:'([^']*)')|([^\\s]+)");
                        if (matches.Count < 2) continue;

                        var actionId = matches[0].Value;
                        if (!string.Equals(actionId, "addjournalentry", StringComparison.OrdinalIgnoreCase)) continue;

                        // Legacy format in action strings:
                        // addjournalentry <loreCode> <title> <chapter...>
                        // New format:
                        // addjournalentry <groupId> <loreCode> <title> <chapter...>
                        string arg1 = matches[1].Groups[1].Success ? matches[1].Groups[1].Value : matches[1].Groups[2].Value;
                        string arg2 = matches.Count >= 3
                            ? (matches[2].Groups[1].Success ? matches[2].Groups[1].Value : matches[2].Groups[2].Value)
                            : null;

                        if (!string.IsNullOrWhiteSpace(arg2))
                        {
                            // new format
                            groupIds.Add(arg1);
                            fromQuest.Add(arg2);
                        }
                        else
                        {
                            // legacy format
                            if (!string.IsNullOrWhiteSpace(arg1)) fromQuest.Add(arg1);
                        }
                    }
                }

                AddFromActions(quest.onAcceptedActions);
                AddFromActions(quest.actionRewards);

                if (quest.actionObjectives != null)
                {
                    foreach (var ao in quest.actionObjectives)
                    {
                        if (ao == null) continue;
                        AddFromActionString(ao.onCompleteActions);
                    }
                }

                loreCodes = fromQuest.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            // Always consider the questId itself as a potential groupId (for older entries)
            if (!string.IsNullOrWhiteSpace(questId))
            {
                groupIds.Add(questId);
            }

            if (loreCodes == null || loreCodes.Length == 0)
            {
                try
                {
                    var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
                    if (itemSystem?.ActionItemRegistry != null && itemSystem.ActionItemRegistry.Count > 0)
                    {
                        var fromItems = new List<string>();

                        foreach (var kvp in itemSystem.ActionItemRegistry)
                        {
                            var ai = kvp.Value;
                            if (ai?.actions == null || ai.actions.Count == 0) continue;

                            bool matches;
                            if (string.Equals(questId, ItemAttributeUtils.ActionItemDefaultSourceQuestId, StringComparison.OrdinalIgnoreCase))
                            {
                                matches = string.IsNullOrWhiteSpace(ai.sourceQuestId);
                            }
                            else
                            {
                                matches = string.Equals(ai.sourceQuestId, questId, StringComparison.OrdinalIgnoreCase);
                            }
                            if (!matches) continue;

                            foreach (var act in ai.actions)
                            {
                                if (act == null) continue;
                                if (!string.Equals(act.id, "addjournalentry", StringComparison.OrdinalIgnoreCase)) continue;
                                if (act.args == null || act.args.Length < 1) continue;

                                // New format: [groupId, loreCode, title, ...]
                                // Legacy format: [loreCode, title, ...]
                                if (act.args.Length >= 2)
                                {
                                    if (!string.IsNullOrWhiteSpace(act.args[0])) groupIds.Add(act.args[0]);
                                    if (!string.IsNullOrWhiteSpace(act.args[1])) fromItems.Add(act.args[1]);
                                }
                                else
                                {
                                    if (!string.IsNullOrWhiteSpace(act.args[0])) fromItems.Add(act.args[0]);
                                }
                            }
                        }

                        loreCodes = fromItems.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    }
                }
                catch
                {
                }
            }

            if (loreCodes == null || loreCodes.Length == 0)
            {
                player.Entity.WatchedAttributes.RemoveAttribute(loreCodesKey);
                player.Entity.WatchedAttributes.MarkPathDirty(loreCodesKey);
                loreCodes = Array.Empty<string>();
            }

            // Also remove group-specific lore code lists for new-format groupIds
            foreach (var gid in groupIds)
            {
                if (string.IsNullOrWhiteSpace(gid)) continue;
                string gkey = $"alegacyvsquest:journal:{gid}:lorecodes";
                player.Entity.WatchedAttributes.RemoveAttribute(gkey);
                player.Entity.WatchedAttributes.MarkPathDirty(gkey);
            }

            bool removedCustom = false;
            try
            {
                var wa = player.Entity.WatchedAttributes;
                var entries = QuestJournalEntry.Load(wa);
                if (entries.Count > 0)
                {
                    var loreSet = new HashSet<string>(loreCodes.Where(c => !string.IsNullOrWhiteSpace(c)), StringComparer.OrdinalIgnoreCase);
                    int before = entries.Count;
                    entries.RemoveAll(e => e != null &&
                        (!string.IsNullOrWhiteSpace(e.QuestId) && (string.Equals(e.QuestId, questId, StringComparison.OrdinalIgnoreCase) || groupIds.Contains(e.QuestId))
                        || (!string.IsNullOrWhiteSpace(e.LoreCode) && loreSet.Count > 0 && loreSet.Contains(e.LoreCode))));

                    if (entries.Count != before)
                    {
                        QuestJournalEntry.Save(wa, entries);
                        wa.MarkPathDirty(QuestJournalEntry.JournalEntriesKey);
                        removedCustom = true;
                    }
                }
            }
            catch (Exception e)
            {
                sapi?.Logger.Warning($"[vsquest] Failed to remove custom journal entries for player {player.PlayerUID}: {e.Message}");
            }

            try
            {
                var modJournal = sapi.ModLoader.GetModSystem<ModJournal>();
                if (modJournal == null) return;

                var t = modJournal.GetType();
                var journalsField = t.GetField("journalsByPlayerUid", BindingFlags.Instance | BindingFlags.NonPublic);
                var channelField = t.GetField("serverChannel", BindingFlags.Instance | BindingFlags.NonPublic);

                Dictionary<string, Journal> journals = journalsField?.GetValue(modJournal) as Dictionary<string, Journal>;
                IServerNetworkChannel serverChannel = channelField?.GetValue(modJournal) as IServerNetworkChannel;

                if (journals == null || serverChannel == null)
                {
                    var fields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                    if (journals == null)
                    {
                        var jf = fields.FirstOrDefault(f => typeof(Dictionary<string, Journal>).IsAssignableFrom(f.FieldType));
                        journals = jf?.GetValue(modJournal) as Dictionary<string, Journal>;
                    }

                    if (serverChannel == null)
                    {
                        var cf = fields.FirstOrDefault(f => typeof(IServerNetworkChannel).IsAssignableFrom(f.FieldType));
                        serverChannel = cf?.GetValue(modJournal) as IServerNetworkChannel;
                    }
                }
                if (journals == null || serverChannel == null) return;

                if (!journals.TryGetValue(player.PlayerUID, out var journal) || journal?.Entries == null) return;

                var loreSetLegacy = new HashSet<string>(loreCodes.Where(c => !string.IsNullOrWhiteSpace(c)), StringComparer.OrdinalIgnoreCase);
                if (loreSetLegacy.Count == 0) return;

                int beforeLegacy = journal.Entries.Count;
                journal.Entries.RemoveAll(e => e != null && !string.IsNullOrWhiteSpace(e.LoreCode) && loreSetLegacy.Contains(e.LoreCode));
                if (journal.Entries.Count != beforeLegacy)
                {
                    for (int i = 0; i < journal.Entries.Count; i++)
                    {
                        var entry = journal.Entries[i];
                        if (entry == null) continue;
                        entry.EntryId = i;
                        if (entry.Chapters != null)
                        {
                            for (int j = 0; j < entry.Chapters.Count; j++)
                            {
                                if (entry.Chapters[j] != null) entry.Chapters[j].EntryId = i;
                            }
                        }
                    }

                    serverChannel.SendPacket(journal, player);
                }
            }
            catch (Exception e)
            {
                if (!removedCustom)
                {
                    sapi?.Logger.Warning($"[vsquest] Failed to remove journal entries for player {player.PlayerUID}: {e.Message}");
                }
            }
        }

        public static int RemoveNoteJournalEntries(IServerPlayer player)
        {
            if (player?.Entity?.WatchedAttributes == null) return 0;

            var wa = player.Entity.WatchedAttributes;
            var entries = QuestJournalEntry.Load(wa);
            if (entries == null || entries.Count == 0) return 0;

            int removed = entries.RemoveAll(e => e != null && e.IsNote);
            if (removed <= 0) return 0;

            QuestJournalEntry.Save(wa, entries);
            wa.MarkPathDirty(QuestJournalEntry.JournalEntriesKey);
            return removed;
        }

        private static void ClearPerQuestPlayerState(IServerPlayer player, string questId)
        {
            if (player?.Entity?.WatchedAttributes == null) return;
            if (string.IsNullOrWhiteSpace(questId)) return;

            string cooldownKey = string.Format("alegacyvsquest:lastaccepted-{0}", questId);
            // Do NOT remove cooldownKey here.
            // If it is missing (NaN), EntityBehaviorQuestGiver will migrate legacy cooldown data
            // from the questgiver entity back onto the player the next time quests are opened.
            // Setting it to a very old value avoids that migration and makes cooldown expire.
            player.Entity.WatchedAttributes.SetDouble(cooldownKey, -9999999);
            player.Entity.WatchedAttributes.MarkPathDirty(cooldownKey);

            // Clear randkill state
            string slotsKey = RandomKillQuestUtils.SlotsKey(questId);
            int slots = player.Entity.WatchedAttributes.GetInt(slotsKey, 16); // Fallback to 16 for safety if slots key is missing

            player.Entity.WatchedAttributes.RemoveAttribute(slotsKey);
            player.Entity.WatchedAttributes.RemoveAttribute(RandomKillQuestUtils.OnProgressKey(questId));
            player.Entity.WatchedAttributes.RemoveAttribute(RandomKillQuestUtils.OnCompleteKey(questId));

            for (int slot = 0; slot < slots; slot++)
            {
                player.Entity.WatchedAttributes.RemoveAttribute(RandomKillQuestUtils.SlotCodeKey(questId, slot));
                player.Entity.WatchedAttributes.RemoveAttribute(RandomKillQuestUtils.SlotNeedKey(questId, slot));
                player.Entity.WatchedAttributes.RemoveAttribute(RandomKillQuestUtils.SlotHaveKey(questId, slot));
            }
        }

        private static void ClearActionObjectiveCompletionFlagsForQuest(QuestSystem questSystem, IServerPlayer player, string questId)
        {
            if (questSystem == null || player?.Entity?.WatchedAttributes == null) return;
            if (string.IsNullOrWhiteSpace(questId)) return;

            var wa = player.Entity.WatchedAttributes;

            static string CompletedKey(string qid, string objectiveKey) => $"alegacyvsquest:ao:completed:{qid}:{objectiveKey}";

            // Clear any per-objective completion markers so onCompleteActions can fire again.
            // Stored as flat keys on the player's WatchedAttributes.
            if (questSystem.QuestRegistry != null && questSystem.QuestRegistry.TryGetValue(questId, out var quest) && quest?.actionObjectives != null)
            {
                foreach (var ao in quest.actionObjectives)
                {
                    if (ao == null || string.IsNullOrWhiteSpace(ao.id)) continue;

                    string objectiveKey;

                    if (!string.IsNullOrWhiteSpace(ao.objectiveId))
                    {
                        objectiveKey = ao.objectiveId;
                    }
                    else if (ao.id == "interactat" && ao.args != null && ao.args.Length >= 1 && QuestInteractAtUtil.TryParsePos(ao.args[0], out int x, out int y, out int z))
                    {
                        // When interactat has no objectiveId, completion util falls back to coordinate key.
                        objectiveKey = QuestInteractAtUtil.InteractionKey(x, y, z);
                    }
                    else
                    {
                        objectiveKey = ao.id;
                    }

                    string key = CompletedKey(questId, objectiveKey);
                    wa.RemoveAttribute(key);
                    wa.MarkPathDirty(key);
                }
            }

            // Also clear interact-at tracking for interactat objectives in this quest
            if (questSystem.QuestRegistry != null && questSystem.QuestRegistry.TryGetValue(questId, out var quest2) && quest2?.actionObjectives != null)
            {
                string completedInteractions = wa.GetString("completedInteractions", "");
                if (!string.IsNullOrWhiteSpace(completedInteractions))
                {
                    var list = completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    bool changed = false;

                    foreach (var ao in quest2.actionObjectives)
                    {
                        if (ao?.id != "interactat" || ao.args == null || ao.args.Length < 1) continue;

                        var coordString = ao.args[0];
                        if (string.IsNullOrWhiteSpace(coordString)) continue;
                        if (!QuestInteractAtUtil.TryParsePos(coordString, out int x, out int y, out int z)) continue;

                        string interactionKey = QuestInteractAtUtil.InteractionKey(x, y, z);
                        if (list.Remove(interactionKey)) changed = true;
                    }

                    if (changed)
                    {
                        wa.SetString("completedInteractions", string.Join(",", list));
                        wa.MarkPathDirty("completedInteractions");
                    }
                }
            }
        }

        private static void RemoveQuestFromCompletedList(IServerPlayer player, string questId)
        {
            if (player?.Entity?.WatchedAttributes == null) return;

            var questSystem = player.Entity.Api?.ModLoader?.GetModSystem<QuestSystem>();
            var completed = questSystem != null
                ? questSystem.GetNormalizedCompletedQuestIds(player)
                : player.Entity.WatchedAttributes.GetStringArray("alegacyvsquest:playercompleted", new string[0]);
            if (completed == null || completed.Length == 0) return;

            string normalized = questSystem?.NormalizeQuestId(questId) ?? questId;
            var filtered = completed.Where(id => !string.Equals(id, normalized, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (filtered.Length == completed.Length) return;

            player.Entity.WatchedAttributes.SetStringArray("alegacyvsquest:playercompleted", filtered);
            player.Entity.WatchedAttributes.MarkAllDirty();
        }

        public static bool ForceCompleteQuestForPlayer(QuestSystem questSystem, IServerPlayer player, string questId, ICoreServerAPI sapi)
        {
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            var activeQuest = quests.Find(q => q.questId == questId);
            if (activeQuest == null)
            {
                return false;
            }

            var message = new QuestCompletedMessage { questGiverId = activeQuest.questGiverId, questId = questId };
            bool completed = questSystem.ForceCompleteQuestInternal(player, message, sapi);
            if (completed)
            {
                questSystem.SavePlayerQuests(player.PlayerUID, quests);
            }
            return completed;
        }

        public static bool ForceCompleteActiveQuestForPlayer(QuestSystem questSystem, IServerPlayer player, ICoreServerAPI sapi, out string questId)
        {
            questId = null;
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            if (quests == null || quests.Count == 0)
            {
                return false;
            }

            var activeQuest = quests[0];
            if (activeQuest == null || string.IsNullOrEmpty(activeQuest.questId))
            {
                return false;
            }

            questId = activeQuest.questId;
            var message = new QuestCompletedMessage { questGiverId = activeQuest.questGiverId, questId = questId };
            bool completed = questSystem.ForceCompleteQuestInternal(player, message, sapi);
            if (completed)
            {
                questSystem.SavePlayerQuests(player.PlayerUID, quests);
            }
            return completed;
        }

        public static bool ResetQuestForPlayer(QuestSystem questSystem, IServerPlayer player, string questId, ICoreServerAPI sapi = null)
        {
            return ResetQuestForPlayer(questSystem, player, questId, sapi, true);
        }

        public static bool ResetQuestForPlayer(QuestSystem questSystem, IServerPlayer player, string questId, ICoreServerAPI sapi, bool removeJournalEntries)
        {
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            var activeQuest = quests.Find(q => q.questId == questId);
            bool removed = false;

            if (activeQuest != null)
            {
                quests.Remove(activeQuest);
                removed = true;
            }

            if (sapi != null)
            {
                if (removeJournalEntries)
                {
                    RemoveQuestJournalEntries(sapi, questSystem, player, questId);
                    // Action items that don't specify sourceQuestId default to the "item-action" bucket.
                    // Clear it as well so journal entries added via items are removed when forgiving.
                    RemoveQuestJournalEntries(sapi, questSystem, player, ItemAttributeUtils.ActionItemDefaultSourceQuestId);
                }
                ClearQuestGiverChainCooldowns(player, sapi);
            }
            ClearPerQuestPlayerState(player, questId);
            ClearKillActionTargetProgressForQuest(questSystem, player, questId);
            ClearActionObjectiveCompletionFlagsForQuest(questSystem, player, questId);
            RemoveQuestFromCompletedList(player, questId);

            questSystem.SavePlayerQuests(player.PlayerUID, quests);
            return removed;
        }

        public static bool ForgetQuestForPlayer(QuestSystem questSystem, IServerPlayer player, string questId)
        {
            if (questSystem == null || player == null || string.IsNullOrWhiteSpace(questId)) return false;

            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            var activeQuest = quests.Find(q => q.questId == questId);
            if (activeQuest == null) return false;

            quests.Remove(activeQuest);
            questSystem.SavePlayerQuests(player.PlayerUID, quests);
            return true;
        }

        public static bool ForgetActiveQuestForPlayer(QuestSystem questSystem, IServerPlayer player, out string questId)
        {
            questId = null;
            if (questSystem == null || player == null) return false;

            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            if (quests == null || quests.Count == 0) return false;

            var activeQuest = quests[0];
            if (activeQuest == null || string.IsNullOrWhiteSpace(activeQuest.questId)) return false;

            questId = activeQuest.questId;
            quests.RemoveAt(0);
            questSystem.SavePlayerQuests(player.PlayerUID, quests);
            return true;
        }

        public static int ForgetOutdatedQuestsForPlayer(QuestSystem questSystem, IServerPlayer player, ICoreServerAPI sapi)
        {
            if (questSystem == null || player == null || sapi == null) return 0;

            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            if (quests == null || quests.Count == 0) return 0;

            int removedCount = 0;

            for (int i = quests.Count - 1; i >= 0; i--)
            {
                var activeQuest = quests[i];
                if (activeQuest == null || string.IsNullOrWhiteSpace(activeQuest.questId)) continue;

                var questGiver = sapi.World.GetEntityById(activeQuest.questGiverId);
                var questGiverBehavior = questGiver?.GetBehavior<EntityBehaviorQuestGiver>();
                if (questGiverBehavior == null) continue;

                if (!questGiverBehavior.IsQuestCurrentlyRelevant(sapi, activeQuest.questId))
                {
                    if (ResetQuestForPlayer(questSystem, player, activeQuest.questId, sapi, false))
                    {
                        removedCount++;
                    }
                }
            }

            return removedCount;
        }

        public static int ResetAllQuestsForPlayer(QuestSystem questSystem, IServerPlayer player, ICoreServerAPI sapi = null)
        {
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            int removedCount = quests?.Count ?? 0;

            if (quests != null)
            {
                quests.Clear();
            }

            if (sapi != null)
            {
                foreach (var questId in questSystem.QuestRegistry.Keys.ToList())
                {
                    RemoveQuestJournalEntries(sapi, questSystem, player, questId);
                }

                // Also clear journal entries added via action items not tied to any quest.
                RemoveQuestJournalEntries(sapi, questSystem, player, ItemAttributeUtils.ActionItemDefaultSourceQuestId);
                ClearQuestGiverChainCooldowns(player, sapi);
            }

            if (player.Entity?.WatchedAttributes != null)
            {
                // Clear completed list
                player.Entity.WatchedAttributes.RemoveAttribute("alegacyvsquest:playercompleted");
                player.Entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:playercompleted");

                player.Entity.WatchedAttributes.RemoveAttribute("vsquest:playercompleted");
                player.Entity.WatchedAttributes.MarkPathDirty("vsquest:playercompleted");

                // Clear cooldowns and any per-quest state we store on the player
                foreach (var questId in questSystem.QuestRegistry.Keys.ToList())
                {
                    ClearPerQuestPlayerState(player, questId);
                    ClearKillActionTargetProgressForQuest(questSystem, player, questId);
                    ClearActionObjectiveCompletionFlagsForQuest(questSystem, player, questId);
                }

                player.Entity.WatchedAttributes.MarkAllDirty();
            }

            questSystem.SavePlayerQuests(player.PlayerUID, quests ?? new System.Collections.Generic.List<ActiveQuest>());
            return removedCount;
        }

        private static void ClearKillActionTargetProgressForQuest(QuestSystem questSystem, IServerPlayer player, string questId)
        {
            if (questSystem?.QuestRegistry == null) return;
            if (player?.Entity?.WatchedAttributes == null) return;
            if (string.IsNullOrWhiteSpace(questId)) return;

            if (!questSystem.QuestRegistry.TryGetValue(questId, out var questDef) || questDef?.actionObjectives == null) return;

            var wa = player.Entity.WatchedAttributes;

            try
            {
                foreach (var ao in questDef.actionObjectives)
                {
                    if (ao == null) continue;
                    if (!string.Equals(ao.id, "killactiontarget", StringComparison.OrdinalIgnoreCase)) continue;
                    if (ao.args == null || ao.args.Length < 2) continue;

                    // Expected args: <questId> <objectiveId> <targetId> <need>
                    string qid = ao.args[0];
                    string objectiveId = ao.args[1];
                    if (string.IsNullOrWhiteSpace(qid) || string.IsNullOrWhiteSpace(objectiveId)) continue;

                    // Use the same key format as KillActionTargetObjective.CountKey
                    string key = $"alegacyvsquest:killactiontarget:{qid}:{objectiveId}:count";
                    wa.RemoveAttribute(key);
                    wa.MarkPathDirty(key);
                }
            }
            catch
            {
            }
        }
    }
}
