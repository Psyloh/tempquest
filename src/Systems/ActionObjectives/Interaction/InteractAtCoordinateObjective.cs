using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractAtCoordinateObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (args.Length < 1) return false;
            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            string coordString = args[0];
            if (string.IsNullOrWhiteSpace(coordString)) return false;

            // Legacy storage (comma-separated list)
            string[] coords = coordString.Split(',');
            if (coords.Length != 3) return false;

            if (!int.TryParse(coords[0], out int targetX) ||
                !int.TryParse(coords[1], out int targetY) ||
                !int.TryParse(coords[2], out int targetZ))
            {
                return false;
            }

            string interactionKey = $"interactat_{targetX}_{targetY}_{targetZ}";
            string completedInteractions = wa.GetString("completedInteractions", "");
            string[] completed = completedInteractions.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            return completed.Contains(interactionKey);
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            bool completed = IsCompletable(byPlayer, args);
            return new List<int>(new int[] { completed ? 1 : 0 });
        }
    }
}
