using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Block), "OnBlockInteractStart")]
    public class ServerBlockInteractPatch
    {
        public static void Postfix(Block __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, bool __result)
        {
            if (world.Api.Side != EnumAppSide.Server || blockSel == null) return;

            var sapi = world.Api as ICoreServerAPI;
            var player = byPlayer as IServerPlayer;
            if (sapi == null || player == null) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            var blockCode = __instance?.Code?.Path;
            if (blockCode != null && blockCode.IndexOf("present", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var quests = questSystem.GetPlayerQuests(player.PlayerUID);
                sapi.Logger.VerboseDebug($"[vsquest] InteractStart block='{blockCode}' pos={blockSel.Position.X},{blockSel.Position.Y},{blockSel.Position.Z} result={__result} quests={quests?.Count ?? 0}");
            }

            int[] position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
            questSystem.GetPlayerQuests(player.PlayerUID).ForEach(quest => quest.OnBlockUsed(blockCode, position, player, sapi));
        }
    }
}
