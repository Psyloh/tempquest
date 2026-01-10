using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestNpcListCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public QuestNpcListCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            var entities = sapi.World.LoadedEntities?.Values;
            if (entities == null) return TextCommandResult.Success("No loaded entities.");

            var lines = entities
                .Where(e => e != null && e.HasBehavior<EntityBehaviorQuestGiver>())
                .Select(e =>
                {
                    long id = e.EntityId;
                    string code = e.Code?.ToShortString() ?? "<null>";
                    int x = (int)e.ServerPos.X;
                    int y = (int)e.ServerPos.Y;
                    int z = (int)e.ServerPos.Z;
                    return $"{id} {code} @ {x},{y},{z}";
                })
                .OrderBy(s => s)
                .ToArray();

            if (lines.Length == 0) return TextCommandResult.Success("No questgivers found in loaded entities.");

            return TextCommandResult.Success(string.Join("\n", lines));
        }
    }
}
