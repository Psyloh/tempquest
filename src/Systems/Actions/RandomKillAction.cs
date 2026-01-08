using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using vsquest.src.Systems.Actions;

namespace VsQuest
{
    public static class RandomKillAction
    {
        public static void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;

            // Support legacy rollkillobjective args:
            // [minCount, maxCount, template, (opt) onprogress, (opt) oncomplete, mobs...]
            // by translating to multi-objective args:
            // [objectiveCount=1, minCount, maxCount, template, (opt) onprogress, (opt) oncomplete, mobs...]
            if (args != null && args.Length >= 3)
            {
                bool looksLikeLegacy = int.TryParse(args[0], out _)
                    && int.TryParse(args[1], out _)
                    && !int.TryParse(args[2], out _);

                if (looksLikeLegacy)
                {
                    var newArgs = new string[args.Length + 1];
                    newArgs[0] = "1";
                    for (int i = 0; i < args.Length; i++)
                    {
                        newArgs[i + 1] = args[i];
                    }
                    args = newArgs;
                }
            }

            RandomKillQuestUtils.ParseRollArgsMulti(args, out int objectiveCount, out int minCount, out int maxCount, out string template, out string progressActions, out string completeActions, out int mobListStartIndex);

            var mobCodes = RandomKillQuestUtils.ReadMobCodes(args, mobListStartIndex);
            if (mobCodes.Count == 0)
            {
                throw new QuestException("The 'rollkillobjectives' action requires at least 1 entityCode.");
            }
            if (objectiveCount > mobCodes.Count)
            {
                throw new QuestException($"The 'rollkillobjectives' action requires at least {objectiveCount} distinct entityCodes, but only {mobCodes.Count} were provided.");
            }

            string questId = message.questId;
            string slotsKey = $"vsquest:randkill:{questId}:slots";

            byPlayer.Entity.WatchedAttributes.SetInt(slotsKey, objectiveCount);
            byPlayer.Entity.WatchedAttributes.MarkPathDirty(slotsKey);

            RandomKillQuestUtils.StoreQuestActionStrings(byPlayer, questId, progressActions, completeActions);

            var remaining = new List<string>(mobCodes);

            for (int slot = 0; slot < objectiveCount; slot++)
            {
                int need = api.World.Rand.Next(minCount, maxCount + 1);
                int idx = api.World.Rand.Next(0, remaining.Count);
                string code = remaining[idx];
                remaining.RemoveAt(idx);

                string codeKey = $"vsquest:randkill:{questId}:slot{slot}:code";
                string needKey = $"vsquest:randkill:{questId}:slot{slot}:need";
                string haveKey = $"vsquest:randkill:{questId}:slot{slot}:have";

                byPlayer.Entity.WatchedAttributes.SetString(codeKey, code);
                byPlayer.Entity.WatchedAttributes.SetInt(needKey, need);
                byPlayer.Entity.WatchedAttributes.SetInt(haveKey, 0);
                byPlayer.Entity.WatchedAttributes.MarkPathDirty(codeKey);
                byPlayer.Entity.WatchedAttributes.MarkPathDirty(needKey);
                byPlayer.Entity.WatchedAttributes.MarkPathDirty(haveKey);

                RandomKillQuestUtils.SendRollNotification(api, byPlayer, template, need, code);
            }
        }
    }
}
