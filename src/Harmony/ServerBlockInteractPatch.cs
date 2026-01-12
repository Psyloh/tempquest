using HarmonyLib;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Block), "OnBlockInteractStart")]
    public class ServerBlockInteractPatch
    {
        public static void Postfix(Block __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, bool __result)
        {
            if (world.Api.Side != EnumAppSide.Server || !__result || blockSel == null) return;

            var sapi = world.Api as ICoreServerAPI;
            var player = byPlayer as IServerPlayer;
            if (sapi == null || player == null) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            int[] position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
            var playerQuests = questSystem.GetPlayerQuests(player.PlayerUID);
            foreach (var quest in playerQuests.ToArray())
            {
                quest.OnBlockUsed(__instance.Code.ToString(), position, player, sapi);
            }
        }
    }
}
