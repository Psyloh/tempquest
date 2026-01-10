using System;
using System.Collections.Generic;
using System.Linq;
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
            sapi.Event.DidUseBlock += OnBlockUsed;
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
                var quests = persistenceManager.GetPlayerQuests(player.PlayerUID);
                QuestDeathUtil.HandleEntityDeath(sapi, quests, player, entity);
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

        private void OnBlockUsed(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return;

            var blockCode = sapi.World.BlockAccessor.GetBlock(blockSel.Position)?.Code?.Path;
            var position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };

            var quests = persistenceManager.GetPlayerQuests(byPlayer?.PlayerUID);
            if (blockCode != null && blockCode.IndexOf("present", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sapi.Logger.VerboseDebug($"[vsquest] DidUseBlock player='{byPlayer?.PlayerUID}' block='{blockCode}' pos={position[0]},{position[1]},{position[2]} quests={quests?.Count ?? 0}");
            }

            quests?.ForEach(quest => quest.OnBlockUsed(blockCode, position, byPlayer, sapi));
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
