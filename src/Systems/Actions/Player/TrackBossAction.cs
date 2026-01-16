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
        private const double CooldownHours = 5.0 / 60.0; // 5 minutes
        private const float HpCost = 2f;

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
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, "Недостаточно здоровья, чтобы использовать трекер.", EnumChatType.Notification);
                return;
            }

            var bossSystem = sapi.ModLoader.GetModSystem<BossHuntSystem>();
            if (bossSystem == null)
            {
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, "Система охоты недоступна.", EnumChatType.Notification);
                return;
            }

            if (string.IsNullOrWhiteSpace(bossKey))
            {
                bossKey = bossSystem.GetActiveBossKey();
            }

            if (string.IsNullOrWhiteSpace(bossKey))
            {
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, "Нет активной цели для отслеживания.", EnumChatType.Notification);
                return;
            }

            string cooldownKey = $"alegacyvsquest:trackboss:cooldownUntil:{bossKey}";
            double cooldownUntil = playerEntity.WatchedAttributes.GetDouble(cooldownKey, 0);
            if (cooldownUntil > nowHours)
            {
                double remainingMinutes = Math.Max(0, (cooldownUntil - nowHours) * 60.0);
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, $"Трекер ещё не восстановился. Подождите {remainingMinutes:0} мин.", EnumChatType.Notification);
                return;
            }

            var activeQuestId = bossSystem.GetActiveBossQuestId();
            if (!string.IsNullOrWhiteSpace(activeQuestId))
            {
                var active = sapi.ModLoader.GetModSystem<QuestSystem>()?.GetPlayerQuests(byPlayer.PlayerUID);
                bool isActive = active != null && active.Exists(q => q != null && string.Equals(q.questId, activeQuestId, StringComparison.OrdinalIgnoreCase));
                if (!isActive)
                {
                    sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, "Отслеживание доступно только во время активной охоты.", EnumChatType.Notification);
                    return;
                }
            }

            if (!bossSystem.TryGetBossPosition(bossKey, out Vec3d bossPos, out int bossDim, out bool isLiveEntity))
            {
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, "След босса не найден.", EnumChatType.Notification);
                return;
            }

            int playerDim = playerEntity.ServerPos?.Dimension ?? 0;
            if (playerDim != bossDim)
            {
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, "След босса ведёт в другой мир.", EnumChatType.Notification);
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

            playerEntity.WatchedAttributes.SetDouble(cooldownKey, nowHours + CooldownHours);
            playerEntity.WatchedAttributes.MarkPathDirty(cooldownKey);

            string liveSuffix = isLiveEntity ? "" : " (след)";
            sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, $"Босс{liveSuffix}: {dist:0} блоков.", EnumChatType.Notification);
        }
    }
}
