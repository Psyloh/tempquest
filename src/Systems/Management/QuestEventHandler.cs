using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class QuestEventHandler
    {
        private readonly Dictionary<string, Quest> questRegistry;
        private readonly QuestPersistenceManager persistenceManager;
        private readonly ICoreServerAPI sapi;

        public QuestEventHandler(Dictionary<string, Quest> questRegistry, QuestPersistenceManager persistenceManager, ICoreServerAPI sapi)
        {
            this.questRegistry = questRegistry;
            this.persistenceManager = persistenceManager;
            this.sapi = sapi;
        }

        public void RegisterEventHandlers()
        {
            sapi.Event.GameWorldSave += OnGameWorldSave;
            sapi.Event.PlayerJoin += OnPlayerJoin;
            sapi.Event.PlayerDisconnect += OnPlayerDisconnect;
            sapi.Event.OnEntityDeath += OnEntityDeath;
            sapi.Event.DidBreakBlock += OnBlockBroken;
            sapi.Event.DidPlaceBlock += OnBlockPlaced;
            sapi.Event.RegisterGameTickListener(OnQuestTick, 1000);
        }

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            if (byPlayer == null) return;

            // Delay to give vanilla ModJournal time to load the player's journal.
            sapi.Event.RegisterCallback(_ =>
            {
                try
                {
                    QuestJournalMigration.MigrateFromVanilla(sapi, byPlayer);

                    var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                    QuestSystemAdminUtils.ForgetOutdatedQuestsForPlayer(questSystem, byPlayer, sapi);

                }
                catch (Exception e)
                {
                    sapi.Logger.Warning($"[alegacyvsquest] Journal migration failed for {byPlayer.PlayerUID}: {e.Message}");
                }
            }, 1000);
        }

        private void OnGameWorldSave()
        {
            persistenceManager.SaveAllPlayerQuests();
        }

        private void OnPlayerDisconnect(IServerPlayer byPlayer)
        {
            persistenceManager.UnloadPlayerQuests(byPlayer.PlayerUID);
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            var credited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (damageSource?.SourceEntity is EntityPlayer player && !string.IsNullOrWhiteSpace(player.PlayerUID))
            {
                credited.Add(player.PlayerUID);
            }

            if (IsBossEntity(entity) && IsFinalBossStage(entity))
            {
                try
                {
                    var wa = entity?.WatchedAttributes;
                    if (wa != null)
                    {
                        var attackers = wa.GetStringArray(EntityBehaviorBossHuntCombatMarker.BossHuntAttackersKey, new string[0]) ?? new string[0];
                        for (int i = 0; i < attackers.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(attackers[i])) credited.Add(attackers[i]);
                        }
                    }
                }
                catch
                {
                }
            }

            foreach (var uid in credited)
            {
                if (string.IsNullOrWhiteSpace(uid)) continue;

                IPlayer iPlayer = null;
                try
                {
                    iPlayer = sapi?.World?.PlayerByUid(uid);
                }
                catch
                {
                    iPlayer = null;
                }

                var epl = iPlayer?.Entity as EntityPlayer;
                if (epl == null) continue;

                var quests = persistenceManager.GetPlayerQuests(uid);
                QuestDeathUtil.HandleEntityDeath(sapi, quests, epl, entity);
            }

            try
            {
                if (damageSource?.SourceEntity is EntityPlayer announcePlayer && IsBossEntity(entity) && IsFinalBossStage(entity))
                {
                    var serverPlayer = announcePlayer.Player as IServerPlayer;
                    if (serverPlayer != null)
                    {
                        BossKillAnnouncementUtil.AnnounceBossDefeated(sapi, serverPlayer, entity);
                    }
                }
            }
            catch
            {
            }

            var victimPlayer = entity as EntityPlayer;
            if (victimPlayer != null)
            {
                var killer = damageSource?.SourceEntity ?? damageSource?.CauseEntity;
                if (killer != null && (killer.GetBehavior<EntityBehaviorQuestBoss>() != null || killer.GetBehavior<EntityBehaviorQuestTarget>() != null || killer.GetBehavior<EntityBehaviorBoss>() != null))
                {
                    var serverVictim = victimPlayer.Player as IServerPlayer;
                    if (serverVictim != null)
                    {
                        var qs = sapi.ModLoader.GetModSystem<QuestSystem>();
                        if (qs?.Config == null || qs.Config.ShowCustomBossDeathMessage)
                        {
                            BossKillAnnouncementUtil.AnnouncePlayerKilledByBoss(sapi, serverVictim, killer);
                        }
                    }
                }
            }
        }

        private static bool IsBossEntity(Entity entity)
        {
            if (entity == null) return false;

            return entity.GetBehavior<EntityBehaviorBossHuntCombatMarker>() != null
                || entity.GetBehavior<EntityBehaviorBossRespawn>() != null
                || entity.GetBehavior<EntityBehaviorBossDespair>() != null
                || entity.GetBehavior<EntityBehaviorQuestBoss>() != null;
        }

        private static bool IsFinalBossStage(Entity entity)
        {
            if (entity == null) return false;

            var rebirth = entity.GetBehavior<EntityBehaviorBossRebirth>();
            return rebirth == null || rebirth.IsFinalStage;
        }

        private void OnBlockBroken(IServerPlayer byPlayer, int blockId, BlockSelection blockSel)
        {
            if (byPlayer == null || blockSel == null)
            {
                return;
            }

            var blockCode = sapi.World.GetBlock(blockId)?.Code.ToString();
            var position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
            var playerQuests = persistenceManager.GetPlayerQuests(byPlayer.PlayerUID);
            foreach (var quest in playerQuests.ToArray())
            {
                quest.OnBlockBroken(blockCode, position, byPlayer);
            }
        }

        private void OnBlockPlaced(IServerPlayer byPlayer, int oldBlockId, BlockSelection blockSel, ItemStack itemstack)
        {
            if (byPlayer == null || blockSel == null)
            {
                return;
            }

            var blockCode = sapi.World.BlockAccessor.GetBlock(blockSel.Position)?.Code.ToString();
            var position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
            var playerQuests = persistenceManager.GetPlayerQuests(byPlayer.PlayerUID);
            foreach (var quest in playerQuests.ToArray())
            {
                quest.OnBlockPlaced(blockCode, position, byPlayer);
            }
        }

        private void OnQuestTick(float dt)
        {
            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            QuestTickUtil.HandleQuestTick(dt, questRegistry, questSystem.ActionObjectiveRegistry, players, persistenceManager.GetPlayerQuests, sapi);
        }

        public void HandleVanillaBlockInteract(IServerPlayer player, VanillaBlockInteractMessage message)
        {
            if (player == null || message == null)
            {
                return;
            }

            if (message?.BlockCode == "alegacyvsquest:cooldownplaceholder")
            {
                return;
            }

            int[] position = new int[] { message.Position.X, message.Position.Y, message.Position.Z };
            var playerQuests = persistenceManager.GetPlayerQuests(player.PlayerUID);
            foreach (var quest in playerQuests.ToArray())
            {
                quest.OnBlockUsed(message.BlockCode, position, player, sapi);
            }
        }
    }
}
