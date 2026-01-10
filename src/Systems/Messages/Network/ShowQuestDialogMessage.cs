using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class ShowQuestDialogMessage
    {
        [ProtoMember(1)]
        public string TitleLangKey { get; set; }

        [ProtoMember(2)]
        public string TextLangKey { get; set; }

        [ProtoMember(3)]
        public string Option1LangKey { get; set; }

        [ProtoMember(4)]
        public string Option2LangKey { get; set; }
    }
}
