using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class RandomKillObjective : ActiveActionObjective
    {
        public bool isCompletable(IPlayer byPlayer, params string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return false;
            if (args == null || args.Length < 1) return false;

            string questId = args[0];
            int slot = 0;
            bool useSlot = args.Length >= 2 && int.TryParse(args[1], out slot);

            string needKey = useSlot ? $"vsquest:randkill:{questId}:slot{slot}:need" : $"vsquest:randkill:{questId}:need";
            string haveKey = useSlot ? $"vsquest:randkill:{questId}:slot{slot}:have" : $"vsquest:randkill:{questId}:have";

            int need = byPlayer.Entity.WatchedAttributes.GetInt(needKey, 0);
            int have = byPlayer.Entity.WatchedAttributes.GetInt(haveKey, 0);

            return need > 0 && have >= need;
        }

        public List<int> progress(IPlayer byPlayer, params string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return new List<int>(new int[] { 0, 0 });
            if (args == null || args.Length < 1) return new List<int>(new int[] { 0, 0 });

            string questId = args[0];
            int slot = 0;
            bool useSlot = args.Length >= 2 && int.TryParse(args[1], out slot);

            string needKey = useSlot ? $"vsquest:randkill:{questId}:slot{slot}:need" : $"vsquest:randkill:{questId}:need";
            string haveKey = useSlot ? $"vsquest:randkill:{questId}:slot{slot}:have" : $"vsquest:randkill:{questId}:have";

            int need = byPlayer.Entity.WatchedAttributes.GetInt(needKey, 0);
            int have = byPlayer.Entity.WatchedAttributes.GetInt(haveKey, 0);

            if (need < 0) need = 0;
            if (have < 0) have = 0;
            if (have > need && need > 0) have = need;

            return new List<int>(new int[] { have, need });
        }
    }
}
