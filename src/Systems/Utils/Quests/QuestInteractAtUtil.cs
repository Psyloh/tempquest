using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestInteractAtUtil
    {
        public static string InteractionKey(int x, int y, int z) => $"interactat_{x}_{y}_{z}";

        public static bool TryParsePos(string coordString, out int x, out int y, out int z)
        {
            x = y = z = 0;
            if (string.IsNullOrWhiteSpace(coordString)) return false;

            var coords = coordString.Split(',');
            if (coords.Length != 3) return false;

            return int.TryParse(coords[0], out x)
                && int.TryParse(coords[1], out y)
                && int.TryParse(coords[2], out z);
        }

        public static string[] GetCompletedInteractions(IPlayer player)
        {
            var wa = player?.Entity?.WatchedAttributes;
            if (wa == null) return Array.Empty<string>();

            string completedInteractions = wa.GetString("completedInteractions", "");
            return completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool HasInteraction(IPlayer player, int x, int y, int z)
        {
            var completed = GetCompletedInteractions(player);
            return completed.Contains(InteractionKey(x, y, z));
        }

        public static int CountCompleted(IPlayer player, string[] coordArgs)
        {
            if (player?.Entity?.WatchedAttributes == null) return 0;
            if (coordArgs == null || coordArgs.Length == 0) return 0;

            var completed = GetCompletedInteractions(player);
            int count = 0;

            foreach (var coordString in coordArgs)
            {
                if (!TryParsePos(coordString, out int x, out int y, out int z)) continue;
                if (completed.Contains(InteractionKey(x, y, z))) count++;
            }

            return count;
        }

        public static string[] GetCompletedInteractions(IServerPlayer serverPlayer)
        {
            var wa = serverPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return Array.Empty<string>();

            string completedInteractions = wa.GetString("completedInteractions", "");
            return completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static int CountCompleted(IServerPlayer serverPlayer, string[] coordArgs)
        {
            if (serverPlayer?.Entity?.WatchedAttributes == null) return 0;
            if (coordArgs == null || coordArgs.Length == 0) return 0;

            var completed = GetCompletedInteractions(serverPlayer);
            int count = 0;

            foreach (var coordString in coordArgs)
            {
                if (!TryParsePos(coordString, out int x, out int y, out int z)) continue;
                if (completed.Contains(InteractionKey(x, y, z))) count++;
            }

            return count;
        }

        public static bool HasInteraction(IServerPlayer serverPlayer, int x, int y, int z)
        {
            var completed = GetCompletedInteractions(serverPlayer);
            return completed.Contains(InteractionKey(x, y, z));
        }

        public static bool TryMarkInteraction(IServerPlayer serverPlayer, int x, int y, int z)
        {
            var wa = serverPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            string interactionKey = InteractionKey(x, y, z);
            string completedInteractions = wa.GetString("completedInteractions", "");
            var completed = completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (completed.Contains(interactionKey)) return false;

            completed.Add(interactionKey);
            wa.SetString("completedInteractions", string.Join(",", completed));
            wa.MarkPathDirty("completedInteractions");
            return true;
        }

        public static void ResetCompletedInteractAtObjectives(Quest quest, IServerPlayer serverPlayer)
        {
            if (quest?.actionObjectives == null || quest.actionObjectives.Count == 0) return;
            if (serverPlayer?.Entity?.WatchedAttributes == null) return;

            var wa = serverPlayer.Entity.WatchedAttributes;

            try
            {
                string completedInteractions = wa.GetString("completedInteractions", "");
                if (string.IsNullOrWhiteSpace(completedInteractions)) return;

                var completed = completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                bool changed = false;

                foreach (var ao in quest.actionObjectives)
                {
                    if (ao?.id != "interactat" || ao.args == null || ao.args.Length < 1) continue;
                    var coordString = ao.args[0];
                    if (string.IsNullOrWhiteSpace(coordString)) continue;

                    if (!TryParsePos(coordString, out int x, out int y, out int z)) continue;

                    string interactionKey = InteractionKey(x, y, z);
                    if (completed.Remove(interactionKey)) changed = true;
                }

                if (changed)
                {
                    wa.SetString("completedInteractions", string.Join(",", completed));
                    wa.MarkPathDirty("completedInteractions");
                }
            }
            catch
            {
            }
        }

        public static void TryHandleInteractAtObjectives(Quest quest, ActiveQuest activeQuest, IServerPlayer serverPlayer, int[] position, ICoreServerAPI sapi)
        {
            if (quest?.actionObjectives == null || quest.actionObjectives.Count == 0) return;
            if (serverPlayer?.Entity?.WatchedAttributes == null) return;
            if (position == null || position.Length != 3) return;

            var wa = serverPlayer.Entity.WatchedAttributes;
            bool anyChanged = false;

            for (int i = 0; i < quest.actionObjectives.Count; i++)
            {
                var ao = quest.actionObjectives[i];
                if (ao?.id != "interactat" || ao.args == null || ao.args.Length < 1) continue;

                var coordString = ao.args[0];
                if (string.IsNullOrWhiteSpace(coordString)) continue;

                if (!TryParsePos(coordString, out int targetX, out int targetY, out int targetZ)) continue;

                if (position[0] != targetX || position[1] != targetY || position[2] != targetZ) continue;

                bool changed = TryMarkInteraction(serverPlayer, targetX, targetY, targetZ);
                if (!changed) continue;

                anyChanged = true;
                bool completableNow = true;
                try
                {
                    var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                    if (questSystem?.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue("interactat", out var impl) && impl != null)
                    {
                        completableNow = impl.IsCompletable(serverPlayer, ao.args);
                    }
                }
                catch
                {
                    completableNow = true;
                }

                string completionKey = !string.IsNullOrWhiteSpace(ao.objectiveId)
                    ? ao.objectiveId
                    : InteractionKey(targetX, targetY, targetZ);

                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, serverPlayer, activeQuest, ao, completionKey, completableNow);
            }

            if (!anyChanged) return;

            for (int i = 0; i < quest.actionObjectives.Count; i++)
            {
                var ao = quest.actionObjectives[i];
                if (ao?.id != "interactcount") continue;

                bool completableNow;
                try
                {
                    var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                    if (questSystem?.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue("interactcount", out var impl) && impl != null)
                    {
                        completableNow = impl.IsCompletable(serverPlayer, ao.args);
                    }
                    else
                    {
                        completableNow = false;
                    }
                }
                catch
                {
                    completableNow = false;
                }

                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, serverPlayer, activeQuest, ao, ao.objectiveId, completableNow);
            }
        }
    }
}
