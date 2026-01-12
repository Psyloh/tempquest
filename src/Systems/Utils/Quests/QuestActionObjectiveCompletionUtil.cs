using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestActionObjectiveCompletionUtil
    {
        private static string CompletedKey(string questId, string objectiveKey) => $"vsquest:ao:completed:{questId}:{objectiveKey}";

        public static void ResetCompletionFlags(Quest quest, IServerPlayer player)
        {
            if (quest == null || player?.Entity?.WatchedAttributes == null) return;
            if (quest.actionObjectives == null || quest.actionObjectives.Count == 0) return;

            var wa = player.Entity.WatchedAttributes;

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
                    objectiveKey = QuestInteractAtUtil.InteractionKey(x, y, z);
                }
                else
                {
                    objectiveKey = ao.id;
                }

                string key = CompletedKey(quest.id, objectiveKey);
                wa.RemoveAttribute(key);
                wa.MarkPathDirty(key);
            }
        }

        public static void TryFireOnComplete(ICoreServerAPI sapi, IServerPlayer player, ActiveQuest activeQuest, ActionWithArgs objectiveDef, string objectiveKey, bool isNowCompletable)
        {
            if (sapi == null || player == null || activeQuest == null || objectiveDef == null) return;
            if (!isNowCompletable) return;
            if (string.IsNullOrWhiteSpace(activeQuest.questId)) return;

            objectiveKey = string.IsNullOrWhiteSpace(objectiveKey)
                ? (string.IsNullOrWhiteSpace(objectiveDef.objectiveId) ? objectiveDef.id : objectiveDef.objectiveId)
                : objectiveKey;

            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return;

            string key = CompletedKey(activeQuest.questId, objectiveKey);
            if (wa.GetBool(key, false)) return;

            string actionString = objectiveDef.onCompleteActions;
            if (string.IsNullOrWhiteSpace(actionString)
                && objectiveDef.id == "interactat"
                && objectiveDef.args != null
                && objectiveDef.args.Length >= 2
                && !string.IsNullOrWhiteSpace(objectiveDef.args[1]))
            {
                actionString = objectiveDef.args[1];
            }
            if (string.IsNullOrWhiteSpace(actionString)
                && objectiveDef.id == "checkvariable"
                && objectiveDef.args != null
                && objectiveDef.args.Length >= 4
                && !string.IsNullOrWhiteSpace(objectiveDef.args[3]))
            {
                actionString = objectiveDef.args[3];
            }

            wa.SetBool(key, true);
            wa.MarkPathDirty(key);

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            string defaultSound = questSystem?.Config?.defaultObjectiveCompletionSound;

            if (string.IsNullOrWhiteSpace(actionString))
            {
                if (!string.IsNullOrWhiteSpace(defaultSound))
                {
                    sapi.World.PlaySoundFor(new AssetLocation(defaultSound), player);
                }
                return;
            }

            if (string.Equals(actionString.Trim(), "nosound", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var msg = new QuestAcceptedMessage { questGiverId = activeQuest.questGiverId, questId = activeQuest.questId };
            ActionStringExecutor.Execute(sapi, msg, player, actionString);

            // If objective actions didn't explicitly include playsound, play the default completion sound.
            if (!string.IsNullOrWhiteSpace(defaultSound)
                && actionString.IndexOf("playsound", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                sapi.World.PlaySoundFor(new AssetLocation(defaultSound), player);
            }
        }
    }
}
