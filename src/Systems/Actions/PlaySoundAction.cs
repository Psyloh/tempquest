using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VsQuest;

namespace vsquest.src.Systems.Actions
{
    public static class PlaySoundAction
    {
        public static void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 1)
            {
                throw new QuestException("The 'playsound' action requires at least 1 argument: soundLocation.");
            }

            var sound = new AssetLocation(args[0]);
            if (args.Length < 2)
            {
                api.World.PlaySoundFor(sound, byPlayer);
                return;
            }

            if (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float volume))
            {
                volume = 1f;
            }

            var world = api.World;

            var methods = world.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "PlaySoundFor")
                .ToArray();

            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                if (ps.Length < 2) continue;
                if (ps[0].ParameterType != typeof(AssetLocation)) continue;
                if (!ps[1].ParameterType.IsInstanceOfType(byPlayer)) continue;

                int volumeIndex = -1;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (ps[i].ParameterType == typeof(float) && string.Equals(ps[i].Name, "volume", StringComparison.OrdinalIgnoreCase))
                    {
                        volumeIndex = i;
                        break;
                    }
                }
                if (volumeIndex == -1) continue;

                var invokeArgs = new object[ps.Length];
                invokeArgs[0] = sound;
                invokeArgs[1] = byPlayer;

                for (int i = 2; i < ps.Length; i++)
                {
                    if (i == volumeIndex)
                    {
                        invokeArgs[i] = volume;
                        continue;
                    }

                    invokeArgs[i] = ps[i].HasDefaultValue
                        ? ps[i].DefaultValue
                        : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
                }

                m.Invoke(world, invokeArgs);
                return;
            }

            world.PlaySoundFor(sound, byPlayer);
        }
    }
}
