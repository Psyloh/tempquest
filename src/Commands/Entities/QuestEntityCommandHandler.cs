using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestEntityCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;

        public QuestEntityCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.questSystem = questSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            try
            {
                string domain = args.ArgCount > 0 ? (string)args[0] : null;

                HashSet<string> allowedDomains;
                if (!string.IsNullOrWhiteSpace(domain))
                {
                    allowedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { domain };
                }
                else
                {
                    allowedDomains = GetQuestDomains();
                    if (allowedDomains.Count == 0)
                    {
                        return TextCommandResult.Error("No quest domains found. Usage: .quest entities all <domain>");
                    }
                }

                var lines = new List<string>();
                var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entityType in sapi.World.EntityTypes)
                {
                    try
                    {
                        if (entityType?.Code == null || !allowedDomains.Contains(entityType.Code.Domain))
                        {
                            continue;
                        }

                        var code = entityType.Code.ToShortString();
                        if (string.IsNullOrWhiteSpace(code) || !seenCodes.Add(code))
                        {
                            continue;
                        }

                        string name = GetEntityTypeDisplayName(entityType) ?? entityType.Code.Path;
                        lines.Add($"{code} - {name}");
                    }
                    catch (Exception e)
                    {
                        sapi.Logger.Error($"[vsquest] Error processing entity type '{entityType?.Code}' in QuestEntityCommandHandler: {e}");
                    }
                }

                if (lines.Count == 0)
                {
                    return TextCommandResult.Success(!string.IsNullOrWhiteSpace(domain)
                        ? $"No entity types found for domain '{domain}'."
                        : "No entity types found for quest domains.");
                }

                lines.Sort(StringComparer.OrdinalIgnoreCase);
                return TextCommandResult.Success(string.Join("\n", lines));
            }
            catch (Exception e)
            {
                sapi.Logger.Error($"[vsquest] Quest entity command failed: {e}");
                return TextCommandResult.Error("Quest entity command failed: " + e.Message);
            }
        }

        private HashSet<string> GetQuestDomains()
        {
            var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var registry = questSystem?.QuestRegistry;
            if (registry == null) return domains;

            foreach (var quest in registry.Values)
            {
                var id = quest?.id;
                if (string.IsNullOrWhiteSpace(id)) continue;

                int idx = id.IndexOf(':');
                if (idx <= 0) continue;

                domains.Add(id.Substring(0, idx));
            }

            return domains;
        }

        private static string GetEntityTypeDisplayName(EntityProperties entityType)
        {
            var code = entityType?.Code;
            if (code == null) return null;

            string key = "entity-" + code.ToShortString();
            string name = LocalizationUtils.GetSafe(key);
            if (name != key) return name;

            key = "entity-" + code.Path;
            name = LocalizationUtils.GetSafe(key);
            if (name != key) return name;

            return null;
        }
    }
}
