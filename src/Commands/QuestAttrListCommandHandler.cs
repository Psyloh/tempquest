using System;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using VsQuest.Util;

namespace VsQuest
{
    public class QuestAttrListCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public QuestAttrListCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];

            var target = sapi.World.AllOnlinePlayers
                .FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;

            if (target == null)
            {
                return TextCommandResult.Error($"Player '{playerName}' not found online.");
            }

            var tree = target.Entity?.WatchedAttributes as ITreeAttribute;
            if (tree == null)
            {
                return TextCommandResult.Success("No watched attributes.");
            }

            string[] statKeys = new[]
            {
                "walkspeed",
                "hungerrate",
                "healingeffectivness",
                "rangedWeaponsAcc",
                "rangedWeaponsSpeed"
            };

            string[] attrKeys = new[]
            {
                ItemAttributeUtils.AttrAttackPower,
                ItemAttributeUtils.AttrWarmth,
                ItemAttributeUtils.AttrProtection,
                ItemAttributeUtils.AttrProtectionPerc
            };

            var lines = statKeys
                .Select(statKey =>
                {
                    string storeKey = $"vsquestadmin:stat:{statKey}";
                    if (!tree.HasAttribute(storeKey)) return null;
                    float val = tree.GetFloat(storeKey, 0f);
                    return $"{statKey} = {val.ToString(CultureInfo.InvariantCulture)}";
                })
                .Where(l => l != null)
                .Concat(
                    attrKeys.Select(attrKey =>
                    {
                        string storeKey = $"vsquestadmin:attr:{attrKey}";
                        if (!tree.HasAttribute(storeKey)) return null;
                        float val = tree.GetFloat(storeKey, 0f);
                        return $"{attrKey} = {val.ToString(CultureInfo.InvariantCulture)}";
                    }).Where(l => l != null)
                )
                .ToArray();

            if (lines.Length == 0)
            {
                return TextCommandResult.Success("No vsquestadmin player stats set.");
            }

            return TextCommandResult.Success(string.Join("\n", lines));
        }
    }
}
