using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public class AddTraitsAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var traits = byPlayer.Entity.WatchedAttributes
                .GetStringArray("extraTraits", new string[0])
                .ToHashSet();
            traits.AddRange(args);
            byPlayer.Entity.WatchedAttributes
                .SetStringArray("extraTraits", traits.ToArray());
        }
    }
}
