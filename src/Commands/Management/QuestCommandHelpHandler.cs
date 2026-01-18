using Vintagestory.API.Common;

namespace VsQuest
{
    public class QuestCommandHelpHandler
    {
        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            return TextCommandResult.Success(
                "Quest commands:\n" +
                "/vsq quest list - list all quest ids\n" +
                "/vsq quest check <playerName> - show active/completed quests\n" +
                "/vsq quest start <questId> <playerName> - start quest for player\n" +
                "/vsq quest complete <questId> <playerName> - force-complete quest\n" +
                "/vsq quest completeactive [playerName] - force-complete active quest\n" +
                "/vsq quest forgive <all|notes|active|questId> [playerName] - reset quest(s)\n" +
                "\nExamples:\n" +
                "/vsq quest start albase:bosshunt-ossuarywarden PlayerName\n" +
                "/vsq quest forgive notes PlayerName"
            );
        }
    }
}
