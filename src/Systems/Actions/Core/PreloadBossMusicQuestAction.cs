using System;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class PreloadBossMusicQuestAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer == null) return;
            if (args == null || args.Length < 1) return;

            for (int i = 0; i < args.Length; i++)
            {
                var url = args[i];
                if (string.IsNullOrWhiteSpace(url)) continue;

                try
                {
                    sapi.Network.GetChannel("alegacyvsquest").SendPacket(new PreloadBossMusicMessage { Url = url }, byPlayer);
                }
                catch
                {
                }
            }
        }
    }
}
