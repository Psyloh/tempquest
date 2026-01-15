using System;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class BossKillAnnouncementUtil
    {
        private static readonly string[] TemplateLangKeys = new[]
        {
            "alegacyvsquest:bosskill-template-1",
            "alegacyvsquest:bosskill-template-2",
            "alegacyvsquest:bosskill-template-3",
            "alegacyvsquest:bosskill-template-4",
            "alegacyvsquest:bosskill-template-5",
            "alegacyvsquest:bosskill-template-6",
            "alegacyvsquest:bosskill-template-7",
            "alegacyvsquest:bosskill-template-8",
            "alegacyvsquest:bosskill-template-9",
            "alegacyvsquest:bosskill-template-10"
        };

        public static void AnnouncePlayerKilledByBoss(ICoreServerAPI sapi, IServerPlayer victim, Entity killerBossEntity)
        {
            if (sapi == null || victim == null || killerBossEntity == null) return;

            string bossName = MobLocalizationUtils.GetMobDisplayName(killerBossEntity.Code?.ToShortString());
            if (string.IsNullOrWhiteSpace(bossName)) bossName = killerBossEntity.Code?.ToShortString() ?? "?";

            string victimName = ChatFormatUtil.Font(victim.PlayerName, "#ffd75e");
            string bossNameColored = ChatFormatUtil.Font(bossName, "#ff77ff");

            string template = "{victim} погиб от {boss}";
            if (TemplateLangKeys.Length > 0)
            {
                string langKey = TemplateLangKeys[sapi.World.Rand.Next(0, TemplateLangKeys.Length)];
                try
                {
                    string localized = Lang.Get(langKey);
                    if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, langKey, StringComparison.OrdinalIgnoreCase))
                    {
                        template = localized;
                    }
                }
                catch
                {
                }
            }

            string message = template
                .Replace("{victim}", victimName)
                .Replace("{boss}", bossNameColored);

            GlobalChatBroadcastUtil.BroadcastGeneralChat(sapi, ChatFormatUtil.PrefixAlert(message), Vintagestory.API.Common.EnumChatType.Notification);
        }
    }
}
