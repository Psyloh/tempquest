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
            if (damageSource?.SourceEntity is EntityPlayer player)
            {
                var quests = persistenceManager.GetPlayerQuests(player.PlayerUID);
                QuestDeathUtil.HandleEntityDeath(sapi, quests, player, entity);

                try
                {
                    if (IsBossEntity(entity) && IsFinalBossStage(entity))
                    {
                        var serverPlayer = player.Player as IServerPlayer;
                        if (serverPlayer != null)
                        {
                            BossKillAnnouncementUtil.AnnounceBossDefeated(sapi, serverPlayer, entity);
                        }
                    }
                }
                catch
                {
                }
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

            var serverPlayers = players.OfType<IServerPlayer>().ToArray();
            if (serverPlayers.Length == 0) return;

            QuestTickUtil.HandleQuestTick(dt, questRegistry, questSystem.ActionObjectiveRegistry, serverPlayers, persistenceManager.GetPlayerQuests, sapi);
        }
    }
}
