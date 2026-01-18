using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Datastructures;

namespace VsQuest
{
    public class QuestJournalEntry
    {
        public const string JournalEntriesKey = "alegacyvsquest:journal:entries";

        public string QuestId { get; set; }
        public string LoreCode { get; set; }
        public string Title { get; set; }
        public List<string> Chapters { get; set; } = new List<string>();
        public bool IsNote { get; set; }

        public static List<QuestJournalEntry> Load(ITreeAttribute attributes)
        {
            if (attributes == null) return new List<QuestJournalEntry>();

            string json = attributes.GetString(JournalEntriesKey, null);
            if (string.IsNullOrWhiteSpace(json)) return new List<QuestJournalEntry>();

            try
            {
                return JsonConvert.DeserializeObject<List<QuestJournalEntry>>(json) ?? new List<QuestJournalEntry>();
            }
            catch
            {
                return new List<QuestJournalEntry>();
            }
        }

        public static void Save(ITreeAttribute attributes, List<QuestJournalEntry> entries)
        {
            if (attributes == null) return;

            string json = JsonConvert.SerializeObject(entries ?? new List<QuestJournalEntry>());
            attributes.SetString(JournalEntriesKey, json);
        }
    }
}
