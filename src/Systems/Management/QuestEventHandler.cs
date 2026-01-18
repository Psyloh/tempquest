using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class QuestEventHandler
    {
        private const double BossKillCreditMinShare = 0.25;
        private const float BossKillHealFraction = 0.10f;

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

                    try
                    {
                        var epl = byPlayer.Entity;
                        if (epl?.Stats != null)
                        {
                            epl.Stats.Remove("walkspeed", "alegacyvsquest");
                            epl.Stats.Remove("walkspeed", "alegacyvsquest:bossgrab");
                            epl.Stats.Remove("walkspeed", "alegacyvsquest:bosshook");
                            epl.walkSpeed = epl.Stats.GetBlended("walkspeed");
                        }
                    }
                    catch
                    {
                    }

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

            try
            {
                var killer = damageSource?.SourceEntity ?? damageSource?.CauseEntity;
                if (killer != null && killer.Alive && killer != entity && IsBossEntity(killer))
                {
                    TryHealBossOnKill(killer);
                }
            }
            catch
            {
            }

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
                        try
                        {
                            float maxHp = 0;
                            var healthBh = entity.GetBehavior<EntityBehaviorHealth>();
                            if (healthBh != null)
                            {
                                maxHp = healthBh.MaxHealth;
                            }

                            var dmgTree = wa.GetTreeAttribute(EntityBehaviorBossCombatMarker.BossCombatDamageByPlayerKey);
                            if (dmgTree != null && maxHp > 0)
                            {
                                var attackers = wa.GetStringArray(EntityBehaviorBossCombatMarker.BossCombatAttackersKey, new string[0]) ?? new string[0];
                                for (int i = 0; i < attackers.Length; i++)
                                {
                                    var uid = attackers[i];
                                    if (string.IsNullOrWhiteSpace(uid)) continue;

                                    double dmg = dmgTree.GetDouble(uid, 0);
                                    if (dmg / maxHp >= BossKillCreditMinShare)
                                    {
                                        credited.Add(uid);
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }

            var creditedPlayers = new List<IServerPlayer>();

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

                var serverPlayer = iPlayer as IServerPlayer;
                if (serverPlayer != null)
                {
                    creditedPlayers.Add(serverPlayer);
                }

                var quests = persistenceManager.GetPlayerQuests(uid);
                QuestDeathUtil.HandleEntityDeath(sapi, quests, epl, entity);
            }

            try
            {
                if (IsBossEntity(entity) && IsFinalBossStage(entity))
                {
                    if (creditedPlayers.Count > 0)
                    {
                        BossKillAnnouncementUtil.AnnounceBossDefeated(sapi, creditedPlayers, entity);
                    }
                    else if (damageSource?.SourceEntity is EntityPlayer announcePlayer)
                    {
                        var serverPlayer = announcePlayer.Player as IServerPlayer;
                        if (serverPlayer != null)
                        {
                            BossKillAnnouncementUtil.AnnounceBossDefeated(sapi, serverPlayer, entity);
                        }
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

        private void TryHealBossOnKill(Entity boss)
        {
            if (boss == null) return;

            try
            {
                if (!BossBehaviorUtils.TryGetHealth(boss, out ITreeAttribute healthTree, out float currentHealth, out float maxHealth)) return;
                if (maxHealth <= 0f) return;

                float add = maxHealth * BossKillHealFraction;
                if (add <= 0f) return;

                float next = currentHealth + add;
                if (next > maxHealth) next = maxHealth;

                healthTree.SetFloat("currenthealth", next);
                boss.WatchedAttributes.MarkPathDirty("health");
            }
            catch
            {
            }
        }

        private static bool IsBossEntity(Entity entity)
        {
            if (entity == null) return false;

            return entity.GetBehavior<EntityBehaviorBossHuntCombatMarker>() != null
                || entity.GetBehavior<EntityBehaviorBossCombatMarker>() != null
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
