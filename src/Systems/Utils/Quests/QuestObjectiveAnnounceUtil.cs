using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestObjectiveAnnounceUtil
    {

        public static void AnnounceOnAccept(IServerPlayer player, QuestAcceptedMessage message, ICoreServerAPI sapi, Quest quest)
        {
            if (player == null || sapi == null || quest == null) return;

            AnnounceRandomKillTargets(player, message, sapi, quest);

            // walkdistance
            if (quest.actionObjectives != null)
            {
                foreach (var obj in quest.actionObjectives)
                {
                    if (obj?.id != "walkdistance") continue;

                    if (WalkDistanceObjective.TryParseArgs(obj.args, out _, out _, out int needMeters) && needMeters > 0)
                    {
                        var walkLabel = LocalizationUtils.GetSafe("alegacyvsquest:objective-walkdistance");
                        var meterUnit = LocalizationUtils.GetSafe("alegacyvsquest:unit-meter-short");
                        sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, $"{walkLabel}: 0/{needMeters} {meterUnit}", EnumChatType.Notification);
                    }
                }
            }

            // killObjectives
            if (quest.killObjectives != null)
            {
                foreach (var ko in quest.killObjectives)
                {
                    if (ko == null) continue;
                    int need = ko.demand;
                    if (need <= 0) continue;

                    string code = null;
                    if (ko.validCodes != null && ko.validCodes.Count > 0) code = ko.validCodes[0];
                    if (string.IsNullOrWhiteSpace(code)) continue;

                    // Client-side localization (server language may be EN)
                    sapi.Network.GetChannel("vsquest").SendPacket(new ShowNotificationMessage()
                    {
                        Template = "kill-notify",
                        Need = need,
                        MobCode = code,
                        Notification = null
                    }, player);
                }
            }
        }

        private static void AnnounceTimeOfDayGate(IServerPlayer player, ICoreServerAPI sapi, Quest quest)
        {
            return;
        }

        private static void AnnounceRandomKillTargets(IServerPlayer player, QuestAcceptedMessage message, ICoreServerAPI sapi, Quest quest)
        {
            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return;

            string questId = message?.questId;
            if (string.IsNullOrWhiteSpace(questId)) return;

            int slots = wa.GetInt(RandomKillQuestUtils.SlotsKey(questId), 0);
            if (slots <= 0) return;

            string template = null;
            if (quest.onAcceptedActions != null)
            {
                foreach (var action in quest.onAcceptedActions)
                {
                    if (action?.id != "randomkill") continue;
                    if (action.args == null || action.args.Length < 4) continue;

                    var args = action.args;
                    RandomKillQuestUtils.ParseRollArgsMulti(args, out _, out _, out _, out template, out _, out _, out _);
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(template)) return;

            for (int slot = 0; slot < slots; slot++)
            {
                string code = wa.GetString(RandomKillQuestUtils.SlotCodeKey(questId, slot), null);
                int need = wa.GetInt(RandomKillQuestUtils.SlotNeedKey(questId, slot), 0);
                if (string.IsNullOrWhiteSpace(code) || need <= 0) continue;

                // Client-side localization for the template
                sapi.Network.GetChannel("vsquest").SendPacket(new ShowNotificationMessage()
                {
                    Template = template,
                    Need = need,
                    MobCode = code,
                    Notification = null
                }, player);
            }
        }
    }
}
