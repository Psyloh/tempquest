using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

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
            sapi.Event.PlayerDisconnect += OnPlayerDisconnect;
            sapi.Event.OnEntityDeath += OnEntityDeath;
            sapi.Event.DidBreakBlock += OnBlockBroken;
            sapi.Event.DidPlaceBlock += OnBlockPlaced;
            sapi.Event.RegisterGameTickListener(OnQuestTick, 1000);
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
                string killedCode = entity?.Code?.Path;
                var quests = persistenceManager.GetPlayerQuests(player.PlayerUID);
                var serverPlayer = player.Player as IServerPlayer;

                foreach (var quest in quests)
                {
                    quest.OnEntityKilled(killedCode, player.Player);

                    if (serverPlayer != null)
                    {
                        RandomKillQuestUtils.TryHandleKill(sapi, serverPlayer, quest, killedCode);
                    }
                }
            }
        }

        private void OnBlockBroken(IServerPlayer byPlayer, int blockId, BlockSelection blockSel)
        {
            var blockCode = sapi.World.GetBlock(blockId)?.Code.Path;
            var position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
            persistenceManager.GetPlayerQuests(byPlayer?.PlayerUID)
                .ForEach(quest => quest.OnBlockBroken(blockCode, position, byPlayer));
        }

        private void OnBlockPlaced(IServerPlayer byPlayer, int oldBlockId, BlockSelection blockSel, ItemStack itemstack)
        {
            var blockCode = sapi.World.BlockAccessor.GetBlock(blockSel.Position)?.Code.Path;
            var position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
            persistenceManager.GetPlayerQuests(byPlayer?.PlayerUID)
                .ForEach(quest => quest.OnBlockPlaced(blockCode, position, byPlayer));
        }

        private void OnQuestTick(float dt)
        {
            // This would need access to ActionObjectiveRegistry which is in QuestSystem
            // For now, leaving as placeholder - will be handled by QuestSystem calling this method
        }

        public void HandleQuestTick(float dt, Dictionary<string, ActiveActionObjective> actionObjectiveRegistry, IServerPlayer[] players, System.Func<string, List<ActiveQuest>> getPlayerQuests)
        {
            foreach (var serverPlayer in players)
            {
                var activeQuests = getPlayerQuests(serverPlayer.PlayerUID);
                foreach (var activeQuest in activeQuests)
                {
                    var quest = questRegistry[activeQuest.questId];
                    for (int i = 0; i < quest.actionObjectives.Count; i++)
                    {
                        var objective = quest.actionObjectives[i];
                        if (objective.id == "checkvariable")
                        {
                            var objectiveImplementation = actionObjectiveRegistry[objective.id] as CheckVariableObjective;
                            objectiveImplementation?.CheckAndFire(serverPlayer, quest, activeQuest, i, sapi);
                        }
                    }
                }
            }
        }
    }
}
