using HarmonyLib;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest.Harmony
{
    public static class EntityInteractPatch
    {
        private static bool patched;

        public static void TryPatch(HarmonyLib.Harmony harmony)
        {
            if (patched) return;
            if (harmony == null) return;

            // We patch EntityBehavior.OnInteract (base virtual) using reflection-safe postfix args.
            MethodInfo target = AccessTools.Method(typeof(EntityBehavior), "OnInteract");
            if (target == null) return;

            var postfix = new HarmonyMethod(typeof(EntityInteractPatch).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.Public));
            harmony.Patch(target, postfix: postfix);
            patched = true;
        }

        public static void Postfix(EntityBehavior __instance, object[] __args)
        {
            var entity = __instance?.entity;
            if (entity == null) return;

            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            if (__args == null || __args.Length < 4) return;

            if (__args[0] is not EntityPlayer byPlayerEntity) return;
            if (__args[3] is not EnumInteractMode mode) return;
            if (mode != EnumInteractMode.Interact) return;

            var serverPlayer = byPlayerEntity.Player as IServerPlayer;
            if (serverPlayer == null) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            var activeQuests = questSystem.GetPlayerQuests(serverPlayer.PlayerUID);
            if (activeQuests == null || activeQuests.Count == 0) return;

            long targetEntityId = entity.EntityId;
            string targetEntityCode = entity?.Code?.ToString()?.Trim()?.ToLowerInvariant();

            foreach (var activeQuest in activeQuests.ToArray())
            {
                if (activeQuest == null || string.IsNullOrWhiteSpace(activeQuest.questId)) continue;
                if (!questSystem.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef) || questDef?.actionObjectives == null) continue;

                foreach (var ao in questDef.actionObjectives)
                {
                    if (ao == null) continue;
                    if (ao.id != "interactwithentity") continue;
                    if (ao.args == null || ao.args.Length < 3) continue;

                    // Dialog-driven interactwithentity: only count when explicitly triggered from dialogue.
                    if (ao.args.Length >= 4 && string.Equals(ao.args[3], "dialog", System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string questIdArg = ao.args[0];
                    if (!string.Equals(questIdArg, activeQuest.questId, System.StringComparison.OrdinalIgnoreCase)) continue;

                    string desired = ao.args[1];
                    if (string.IsNullOrWhiteSpace(desired)) continue;

                    desired = desired.Trim().ToLowerInvariant();

                    bool matched = false;
                    if (long.TryParse(desired, out long desiredEntityId))
                    {
                        matched = desiredEntityId == targetEntityId;
                    }
                    else
                    {
                        matched = !string.IsNullOrWhiteSpace(targetEntityCode) && desired == targetEntityCode;
                    }

                    if (!matched) continue;

                    var wa = serverPlayer.Entity?.WatchedAttributes;
                    if (wa == null) continue;

                    string key = InteractWithEntityObjective.CountKey(activeQuest.questId, desired);
                    int cur = wa.GetInt(key, 0);
                    wa.SetInt(key, cur + 1);
                    wa.MarkPathDirty(key);

                    if (questSystem.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue("interactwithentity", out var impl) && impl != null)
                    {
                        if (impl.IsCompletable(serverPlayer, ao.args))
                        {
                            QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, serverPlayer, activeQuest, ao, ao.objectiveId, true);
                        }
                    }
                }
            }
        }
    }
}
