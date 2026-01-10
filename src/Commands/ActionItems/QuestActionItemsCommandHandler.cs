using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestActionItemsCommandHandler
    {
        private readonly ItemSystem itemSystem;

        public QuestActionItemsCommandHandler(ItemSystem itemSystem)
        {
            this.itemSystem = itemSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            if (itemSystem?.ActionItemRegistry == null || itemSystem.ActionItemRegistry.Count == 0)
            {
                return TextCommandResult.Success("No action items registered.");
            }

            var lines = itemSystem.ActionItemRegistry
                .OrderBy(kvp => kvp.Key)
                .Select(kvp =>
                {
                    var item = kvp.Value;
                    string name = string.IsNullOrEmpty(item?.name) ? "" : $" - {item.name}";
                    return $"{kvp.Key}{name}";
                })
                .ToArray();

            return TextCommandResult.Success(string.Join("\n", lines));
        }
    }
}
