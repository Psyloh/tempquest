using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class LandClaimAllowanceAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;

            var key = "landclaimallowance";

            int value;
            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                value = byPlayer.Entity.WatchedAttributes.GetInt(key, 0);

                // If the extra allowance was set by an admin command (ServerData only), do not let this quest action lower it.
                if (byPlayer.ServerData != null)
                {
                    value = System.Math.Max(value, byPlayer.ServerData.ExtraLandClaimAllowance);
                }
            }
            else if (!int.TryParse(args[0], out value))
            {
                sapi.Logger.Error($"[vsquest] 'landclaimallowance' action argument 'value' must be an int, but got '{args[0]}' in quest '{message?.questId}'.");
                return;
            }

            if (byPlayer.ServerData != null)
            {
                byPlayer.ServerData.ExtraLandClaimAllowance = value;
            }

            byPlayer.Entity.WatchedAttributes.SetInt(key, value);
            byPlayer.Entity.WatchedAttributes.MarkPathDirty(key);
        }
    }
}
