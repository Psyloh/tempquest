using Vintagestory.API.Server;

namespace VsQuest
{
    public class RecruitEntityAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var recruit = sapi.World.GetEntityById(message.questGiverId);
            if (recruit == null)
            {
                throw new QuestException($"Could not find quest giver with id {message.questGiverId} to recruit for quest {message.questId}");
            }
            recruit.WatchedAttributes.SetDouble("employedSince", sapi.World.Calendar.TotalHours);
            recruit.WatchedAttributes.SetString("guardedPlayerUid", byPlayer.PlayerUID);
            recruit.WatchedAttributes.SetBool("commandSit", false);
            recruit.WatchedAttributes.MarkPathDirty("guardedPlayerUid");
        }
    }
}
