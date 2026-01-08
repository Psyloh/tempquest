using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

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

            var dict = TryGetAttributesDictionary(tree);
            if (dict == null || dict.Count == 0)
            {
                return TextCommandResult.Success("No watched attributes.");
            }

            var sb = new StringBuilder();
            foreach (var kvp in dict.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append(kvp.Key);
                sb.Append(" = ");
                sb.Append(kvp.Value?.ToString() ?? "<null>");
                sb.Append('\n');
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd('\n'));
        }

        private static Dictionary<string, object> TryGetAttributesDictionary(object tree)
        {
            var type = tree.GetType();

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var ft = field.FieldType;
                if (typeof(IDictionary).IsAssignableFrom(ft))
                {
                    var obj = field.GetValue(tree) as IDictionary;
                    if (obj == null) continue;

                    var result = new Dictionary<string, object>();
                    foreach (DictionaryEntry entry in obj)
                    {
                        if (entry.Key is string sKey)
                        {
                            result[sKey] = entry.Value;
                        }
                    }
                    return result;
                }
            }

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var pt = prop.PropertyType;
                if (!prop.CanRead) continue;
                if (typeof(IDictionary).IsAssignableFrom(pt))
                {
                    var obj = prop.GetValue(tree, null) as IDictionary;
                    if (obj == null) continue;

                    var result = new Dictionary<string, object>();
                    foreach (DictionaryEntry entry in obj)
                    {
                        if (entry.Key is string sKey)
                        {
                            result[sKey] = entry.Value;
                        }
                    }
                    return result;
                }
            }

            return null;
        }
    }
}
