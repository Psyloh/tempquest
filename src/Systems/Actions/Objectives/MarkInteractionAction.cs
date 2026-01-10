using Vintagestory.API.Server;

namespace VsQuest
{
    public class MarkInteractionAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 1) return;

            var wa = byPlayer.Entity.WatchedAttributes;

            // New storage: one bool per coordinate string
            var key = $"alegacyvsquest:interactat:{args[0]}";
            wa.SetBool(key, true);
            wa.MarkPathDirty(key);
        }
    }
}
