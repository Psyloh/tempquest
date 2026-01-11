using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class WalkDistanceObjective : ActionObjectiveBase
    {
        public static string HaveKey(string questId, int slot) => $"alegacyvsquest:walkdist:{questId}:slot{slot}:have";
        private static string LastXKey(string questId, int slot) => $"alegacyvsquest:walkdist:{questId}:slot{slot}:lastx";
        private static string LastZKey(string questId, int slot) => $"alegacyvsquest:walkdist:{questId}:slot{slot}:lastz";
        public static string HasLastKey(string questId, int slot) => $"alegacyvsquest:walkdist:{questId}:slot{slot}:haslast";

        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return false;
            if (!TryParseArgs(args, out string questId, out int slot, out int needMeters)) return false;

            float have = byPlayer.Entity.WatchedAttributes.GetFloat(HaveKey(questId, slot), 0f);
            if (have < 0f) have = 0f;

            return needMeters > 0 && have >= needMeters;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return new List<int>(new int[] { 0, 0 });
            if (!TryParseArgs(args, out string questId, out int slot, out int needMeters)) return new List<int>(new int[] { 0, 0 });

            float have = byPlayer.Entity.WatchedAttributes.GetFloat(HaveKey(questId, slot), 0f);
            if (have < 0f) have = 0f;

            int haveInt = (int)Math.Floor(have);
            if (haveInt > needMeters && needMeters > 0) haveInt = needMeters;
            if (needMeters < 0) needMeters = 0;

            return new List<int>(new int[] { haveInt, needMeters });
        }

        public void OnTick(IServerPlayer player, ActiveQuest activeQuest, int objectiveIndex, string[] args, ICoreServerAPI sapi, float dt)
        {
            if (player?.Entity == null) return;

            var wa = player.Entity.WatchedAttributes;
            if (wa == null) return;

            if (!TryParseArgs(args, out string questId, out int slot, out int needMeters)) return;
            if (needMeters <= 0) return;

            var entity = player.Entity;
            var controls = entity.Controls;
            if (controls == null) return;

            if (!controls.TriesToMove) { EnsureLastPosInitialized(entity, wa, questId, slot); return; }
            if (!entity.OnGround) { EnsureLastPosInitialized(entity, wa, questId, slot); return; }
            if (entity.Swimming) { EnsureLastPosInitialized(entity, wa, questId, slot); return; }
            if (controls.IsFlying || controls.Gliding || controls.DetachedMode) { EnsureLastPosInitialized(entity, wa, questId, slot); return; }

            double curX = entity.ServerPos.X;
            double curZ = entity.ServerPos.Z;

            bool hasLast = wa.GetBool(HasLastKey(questId, slot), false);
            if (!hasLast)
            {
                wa.SetDouble(LastXKey(questId, slot), curX);
                wa.SetDouble(LastZKey(questId, slot), curZ);
                wa.SetBool(HasLastKey(questId, slot), true);
                wa.MarkPathDirty(LastXKey(questId, slot));
                wa.MarkPathDirty(LastZKey(questId, slot));
                wa.MarkPathDirty(HasLastKey(questId, slot));
                return;
            }

            double lastX = wa.GetDouble(LastXKey(questId, slot), curX);
            double lastZ = wa.GetDouble(LastZKey(questId, slot), curZ);

            double dx = curX - lastX;
            double dz = curZ - lastZ;
            double dist = Math.Sqrt(dx * dx + dz * dz);

            if (dist > 20)
            {
                wa.SetDouble(LastXKey(questId, slot), curX);
                wa.SetDouble(LastZKey(questId, slot), curZ);
                wa.MarkPathDirty(LastXKey(questId, slot));
                wa.MarkPathDirty(LastZKey(questId, slot));
                return;
            }

            if (dist < 0.05)
            {
                wa.SetDouble(LastXKey(questId, slot), curX);
                wa.SetDouble(LastZKey(questId, slot), curZ);
                wa.MarkPathDirty(LastXKey(questId, slot));
                wa.MarkPathDirty(LastZKey(questId, slot));
                return;
            }

            float have = wa.GetFloat(HaveKey(questId, slot), 0f);
            if (have < 0f) have = 0f;
            if (have >= needMeters)
            {
                wa.SetDouble(LastXKey(questId, slot), curX);
                wa.SetDouble(LastZKey(questId, slot), curZ);
                wa.MarkPathDirty(LastXKey(questId, slot));
                wa.MarkPathDirty(LastZKey(questId, slot));
                return;
            }

            have += (float)dist;
            if (have > needMeters) have = needMeters;

            wa.SetFloat(HaveKey(questId, slot), have);
            wa.MarkPathDirty(HaveKey(questId, slot));

            if (have >= needMeters)
            {
                try
                {
                    var questSystem = sapi?.ModLoader?.GetModSystem<QuestSystem>();
                    if (questSystem?.QuestRegistry != null && questSystem.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef))
                    {
                        var objectiveDef = questDef?.actionObjectives != null && objectiveIndex >= 0 && objectiveIndex < questDef.actionObjectives.Count
                            ? questDef.actionObjectives[objectiveIndex]
                            : null;

                        if (objectiveDef != null)
                        {
                            QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, activeQuest, objectiveDef, objectiveDef.objectiveId, true);
                        }
                    }
                }
                catch
                {
                }
            }

            wa.SetDouble(LastXKey(questId, slot), curX);
            wa.SetDouble(LastZKey(questId, slot), curZ);
            wa.MarkPathDirty(LastXKey(questId, slot));
            wa.MarkPathDirty(LastZKey(questId, slot));
        }

        private static void EnsureLastPosInitialized(Entity entity, SyncedTreeAttribute wa, string questId, int slot)
        {
            if (entity == null || wa == null) return;
            if (wa.GetBool(HasLastKey(questId, slot), false)) return;

            wa.SetDouble(LastXKey(questId, slot), entity.ServerPos.X);
            wa.SetDouble(LastZKey(questId, slot), entity.ServerPos.Z);
            wa.SetBool(HasLastKey(questId, slot), true);
            wa.MarkPathDirty(LastXKey(questId, slot));
            wa.MarkPathDirty(LastZKey(questId, slot));
            wa.MarkPathDirty(HasLastKey(questId, slot));
        }

        public static bool TryParseArgs(string[] args, out string questId, out int slot, out int needMeters)
        {
            questId = null;
            slot = 0;
            needMeters = 0;

            if (args == null || args.Length < 2) return false;

            questId = args[0];
            if (string.IsNullOrWhiteSpace(questId)) return false;

            if (args.Length >= 3 && int.TryParse(args[1], out int parsedSlot))
            {
                slot = parsedSlot;
                if (!int.TryParse(args[2], out needMeters)) needMeters = 0;
            }
            else
            {
                slot = 0;
                if (!int.TryParse(args[1], out needMeters)) needMeters = 0;
            }

            if (slot < 0) slot = 0;
            if (needMeters < 0) needMeters = 0;

            return true;
        }
    }
}
