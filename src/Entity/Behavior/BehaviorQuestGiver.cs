using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class EntityBehaviorQuestGiver : EntityBehavior
    {
        private string[] quests;
        private bool selectRandom;
        private int selectRandomCount;

        public EntityBehaviorQuestGiver(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            selectRandom = attributes["selectrandom"].AsBool();
            selectRandomCount = attributes["selectrandomcount"].AsInt(1);
            quests = attributes["quests"].AsArray<string>();

            if (selectRandom)
            {
                int seed = unchecked((int)entity.EntityId);
                var questList = new List<string>(quests);
                var resultList = new List<string>();
                for (int i = 0; i < Math.Min(selectRandomCount, quests.Length); i++)
                {
                    seed = (seed * 5 + 7) % questList.Count;
                    resultList.Add(questList[seed]);
                    questList.RemoveAt(seed);
                }
                quests = resultList.ToArray();
            }
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);
            var bh = entity.GetBehavior<EntityBehaviorConversable>();
            if (bh != null)
            {
                bh.OnControllerCreated += (controller) =>
                {
                    controller.DialogTriggers += Dialog_DialogTriggers;
                };
            }
        }

        private int Dialog_DialogTriggers(EntityAgent triggeringEntity, string value, JsonObject data)
        {
            var behaviorConversable = entity.GetBehavior<EntityBehaviorConversable>();
            behaviorConversable.Dialog?.TryClose();
            if (value == "openquests" && triggeringEntity.Api is ICoreServerAPI sapi)
            {
                SendQuestInfoMessageToClient(sapi, triggeringEntity as EntityPlayer);
                return 0;
            }

            return -1;
        }


        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (entity.Alive
                && entity.Api is ICoreServerAPI sapi
                && byEntity is EntityPlayer player
                && mode == EnumInteractMode.Interact
                && player.Controls.Sneak
                && !entity.HasBehavior<EntityBehaviorConversable>())
            {
                SendQuestInfoMessageToClient(sapi, player);
            }
        }

        public void SendQuestInfoMessageToClient(ICoreServerAPI sapi, EntityPlayer player)
        {
            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            var allActiveQuests = questSystem.GetPlayerQuests(player.PlayerUID);
            var activeQuests = allActiveQuests
                .Where(activeQuest => quests.Contains(activeQuest.questId))
                .ToList();

            var serverPlayer = player.Player as IServerPlayer;

            var availableQuestIds = new List<string>();
            foreach (var questId in quests)
            {
                var quest = questSystem.QuestRegistry[questId];

                var key = String.Format("vsquest:lastaccepted-{0}", questId);
                if (player.WatchedAttributes.GetDouble(key, -quest.cooldown) + quest.cooldown < sapi.World.Calendar.TotalDays
                        && allActiveQuests.Find(activeQuest => activeQuest.questId == questId) == null
                        && predecessorsCompleted(quest, player.PlayerUID))
                {
                    availableQuestIds.Add(questId);
                }
            }
            var message = new QuestInfoMessage()
            {
                questGiverId = entity.EntityId,
                availableQestIds = availableQuestIds,
                activeQuests = activeQuests
            };

            sapi.Network.GetChannel("vsquest").SendPacket<QuestInfoMessage>(message, player.Player as IServerPlayer);
        }
        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            if (entity.Alive && !entity.HasBehavior<EntityBehaviorConversable>())
            {
                return new WorldInteraction[] {
                    new WorldInteraction(){
                        ActionLangCode = "vsquest:access-quests",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak"
                    }
                };
            }
            else { return base.GetInteractionHelp(world, es, player, ref handled); }
        }

        private bool predecessorsCompleted(Quest quest, string playerUID)
        {
            var completedQuests = new List<string>(entity.World.PlayerByUid(playerUID)?.Entity?.WatchedAttributes.GetStringArray("vsquest:playercompleted", new string[0]) ?? new string[0]);
            return String.IsNullOrEmpty(quest.predecessor)
                || completedQuests.Contains(quest.predecessor);
        }

        public override string PropertyName() => "questgiver";
    }
}