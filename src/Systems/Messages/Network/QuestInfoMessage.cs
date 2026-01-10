using ProtoBuf;
using System.Collections.Generic;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuestInfoMessage
    {
        public long questGiverId { get; set; }
        public List<string> availableQestIds { get; set; }
        public List<ActiveQuest> activeQuests { get; set; }
        public string noAvailableQuestDescLangKey { get; set; }
        public string noAvailableQuestCooldownDescLangKey { get; set; }
        public int noAvailableQuestCooldownDaysLeft { get; set; }
    }
}
