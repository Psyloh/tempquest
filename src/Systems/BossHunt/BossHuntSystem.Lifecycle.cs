using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public partial class BossHuntSystem
    {
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            LoadConfigs();
            LoadState();

            tickListenerId = sapi.Event.RegisterGameTickListener(OnTick, 1000);
            sapi.Event.GameWorldSave += OnWorldSave;
            sapi.Event.OnEntityDeath += OnEntityDeath;
        }

        public override void Dispose()
        {
            if (sapi != null)
            {
                if (tickListenerId != 0)
                {
                    sapi.Event.UnregisterGameTickListener(tickListenerId);
                    tickListenerId = 0;
                }

                sapi.Event.GameWorldSave -= OnWorldSave;
                sapi.Event.OnEntityDeath -= OnEntityDeath;
            }

            base.Dispose();
        }

        private void LoadConfigs()
        {
            configs.Clear();

            foreach (var mod in sapi.ModLoader.Mods)
            {
                try
                {
                    var assets = sapi.Assets.GetMany<BossHuntConfig>(sapi.Logger, "config/bosshunt", mod.Info.ModID);
                    foreach (var asset in assets)
                    {
                        if (asset.Value != null)
                        {
                            if (string.Equals(asset.Value.bossKey, "vsquestdebugging:bosshunt:breathbreaker", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            configs.Add(asset.Value);
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
