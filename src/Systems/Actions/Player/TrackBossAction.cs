using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class TrackBossAction : IQuestAction
    {
        private const float HpCost = 3f;

        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer == null) return;

            string bossKey = args != null && args.Length >= 1 ? args[0] : null;

            var playerEntity = byPlayer.Entity;
            if (playerEntity?.WatchedAttributes == null) return;

            double nowHours = sapi.World.Calendar.TotalHours;

            var healthBh = playerEntity.GetBehavior<EntityBehaviorHealth>();
            if (healthBh != null && healthBh.Health <= HpCost)
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("alegacyvsquest:trackboss-not-enough-health")
                }, byPlayer);
                return;
            }

            var bossSystem = sapi.ModLoader.GetModSystem<BossHuntSystem>();
            if (bossSystem == null)
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("alegacyvsquest:trackboss-system-unavailable")
                }, byPlayer);
                return;
            }

            if (string.IsNullOrWhiteSpace(bossKey))
            {
                bossKey = bossSystem.GetActiveBossKey();
            }

            if (string.IsNullOrWhiteSpace(bossKey))
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("alegacyvsquest:trackboss-no-active-target")
                }, byPlayer);
                return;
            }

            var activeQuestId = bossSystem.GetActiveBossQuestId();
            if (!string.IsNullOrWhiteSpace(activeQuestId))
            {
                var active = sapi.ModLoader.GetModSystem<QuestSystem>()?.GetPlayerQuests(byPlayer.PlayerUID);
                bool isActive = active != null && active.Exists(q => q != null && string.Equals(q.questId, activeQuestId, StringComparison.OrdinalIgnoreCase));
                if (!isActive)
                {
                    sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                    {
                        Notification = Lang.Get("alegacyvsquest:trackboss-only-during-hunt")
                    }, byPlayer);
                    return;
                }
            }

            if (!bossSystem.TryGetBossPosition(bossKey, out Vec3d bossPos, out int bossDim, out bool isLiveEntity))
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("alegacyvsquest:trackboss-trail-not-found")
                }, byPlayer);
                return;
            }

            int playerDim = playerEntity.ServerPos?.Dimension ?? 0;
            if (playerDim != bossDim)
            {
                sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
                {
                    Notification = Lang.Get("alegacyvsquest:trackboss-different-dimension")
                }, byPlayer);
                return;
            }

            Vec3d playerPos = new Vec3d(playerEntity.ServerPos.X, playerEntity.ServerPos.Y, playerEntity.ServerPos.Z);
            double dx = playerPos.X - bossPos.X;
            double dy = playerPos.Y - bossPos.Y;
            double dz = playerPos.Z - bossPos.Z;
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            playerEntity.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Injury,
                DamageTier = 0,
                KnockbackStrength = 0f,
                IgnoreInvFrames = true
            }, HpCost);

            string liveSuffix = isLiveEntity ? "" : Lang.Get("alegacyvsquest:trackboss-trail-suffix");
            sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage
            {
                Notification = Lang.Get("alegacyvsquest:trackboss-distance", liveSuffix, dist)
            }, byPlayer);
        }
    }
}
