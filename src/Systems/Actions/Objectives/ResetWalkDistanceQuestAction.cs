using Vintagestory.API.Server;

namespace VsQuest
{
    public class ResetWalkDistanceQuestAction : IQuestAction
    {
        public void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;

            string questId = null;
            if (args != null && args.Length >= 1 && !string.IsNullOrWhiteSpace(args[0])) questId = args[0];
            if (string.IsNullOrWhiteSpace(questId)) questId = message?.questId;
            if (string.IsNullOrWhiteSpace(questId)) return;

            int slots = 1;
            if (args != null && args.Length >= 2 && int.TryParse(args[1], out int parsedSlots)) slots = parsedSlots;
            if (slots < 1) slots = 1;
            if (slots > 32) slots = 32;

            var wa = byPlayer.Entity.WatchedAttributes;

            for (int slot = 0; slot < slots; slot++)
            {
                string haveKey = WalkDistanceObjective.HaveKey(questId, slot);
                string hasLastKey = WalkDistanceObjective.HasLastKey(questId, slot);

                wa.SetFloat(haveKey, 0f);
                wa.RemoveAttribute(hasLastKey);

                wa.MarkPathDirty(haveKey);
                wa.MarkPathDirty(hasLastKey);
            }
        }
    }
}
