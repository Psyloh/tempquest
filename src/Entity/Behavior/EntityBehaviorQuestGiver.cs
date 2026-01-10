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
        private string[] alwaysQuests;
        private string[] rotationPool;
        private bool selectRandom;
        private int selectRandomCount;
        private int rotationDays;
        private int rotationCount;
        private string noAvailableQuestDescLangKey;
        private string noAvailableQuestCooldownDescLangKey;

        public EntityBehaviorQuestGiver(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            selectRandom = attributes["selectrandom"].AsBool();
            selectRandomCount = attributes["selectrandomcount"].AsInt(1);
            rotationDays = attributes["rotationdays"].AsInt(0);
            rotationCount = attributes["rotationcount"].AsInt(1);

            quests = attributes["quests"].AsArray<string>() ?? Array.Empty<string>();
            alwaysQuests = attributes["alwaysquests"].AsArray<string>() ?? Array.Empty<string>();
            rotationPool = attributes["rotationpool"].AsArray<string>();
            noAvailableQuestDescLangKey = attributes["noAvailableQuestDescLangKey"].AsString(null);
            noAvailableQuestCooldownDescLangKey = attributes["noAvailableQuestCooldownDescLangKey"].AsString(null);

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

        private HashSet<string> BuildAllQuestIds()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (quests != null)
            {
                foreach (var q in quests) if (!string.IsNullOrWhiteSpace(q)) set.Add(q);
            }
            if (alwaysQuests != null)
            {
                foreach (var q in alwaysQuests) if (!string.IsNullOrWhiteSpace(q)) set.Add(q);
            }
            if (rotationPool != null)
            {
                foreach (var q in rotationPool) if (!string.IsNullOrWhiteSpace(q)) set.Add(q);
            }
            return set;
        }

        private List<string> GetCurrentQuestSelection(ICoreServerAPI sapi)
        {
            var result = new List<string>();

            if (alwaysQuests != null)
            {
                foreach (var q in alwaysQuests)
                {
                    if (!string.IsNullOrWhiteSpace(q)) result.Add(q);
                }
            }

            var pool = rotationPool ?? quests;
            if (pool == null || pool.Length == 0)
            {
                return result;
            }

            if (rotationDays <= 0 || sapi == null)
            {
                result.AddRange(pool);
                return result;
            }

            int count = rotationCount;
            if (count < 1) count = 1;
            if (count > pool.Length) count = pool.Length;

            int period = (int)Math.Floor(sapi.World.Calendar.TotalDays / rotationDays);
            int offset = Math.Abs(unchecked((int)entity.EntityId));
            offset = pool.Length == 0 ? 0 : offset % pool.Length;

            for (int i = 0; i < count; i++)
            {
                int idx = (offset + period + i) % pool.Length;
                string questId = pool[idx];
                if (!string.IsNullOrWhiteSpace(questId) && !result.Contains(questId))
                {
                    result.Add(questId);
                }
            }

            return result;
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

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (damageSource != null)
            {
                damageSource.KnockbackStrength = 0f;
            }

            damage = 0f;
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
            var allQuestIds = BuildAllQuestIds();

            var activeQuests = allActiveQuests
                .Where(activeQuest => allQuestIds.Contains(activeQuest.questId))
                .Select(aq =>
                {
                    aq.IsCompletableOnClient = aq.IsCompletable(player.Player);
                    aq.ProgressText = QuestProgressTextUtil.GetActiveQuestText(sapi, player.Player, aq);
                    return aq;
                })
                .ToList();

            var serverPlayer = player.Player as IServerPlayer;

            var availableQuestIds = new List<string>();
            int? minCooldownDaysLeft = null;

            var selection = GetCurrentQuestSelection(sapi);
            foreach (var questId in selection)
            {
                var quest = questSystem.QuestRegistry[questId];

                var key = String.Format("alegacyvsquest:lastaccepted-{0}", questId);
                double lastAccepted = player.WatchedAttributes.GetDouble(key, -quest.cooldown);
                bool onCooldown = lastAccepted + quest.cooldown >= sapi.World.Calendar.TotalDays;
                bool isActive = allActiveQuests.Find(activeQuest => activeQuest.questId == questId) != null;
                bool eligible = !isActive && predecessorsCompleted(quest, player.PlayerUID);

                if (eligible && !onCooldown)
                {
                    availableQuestIds.Add(questId);
                }
                else if (eligible && onCooldown)
                {
                    double daysLeft = (lastAccepted + quest.cooldown) - sapi.World.Calendar.TotalDays;
                    int left = (int)Math.Ceiling(daysLeft);
                    if (left < 0) left = 0;
                    if (!minCooldownDaysLeft.HasValue || left < minCooldownDaysLeft.Value)
                    {
                        minCooldownDaysLeft = left;
                    }
                }
            }

            int cooldownDaysLeft = (availableQuestIds.Count == 0 && minCooldownDaysLeft.HasValue) ? minCooldownDaysLeft.Value : 0;
            var message = new QuestInfoMessage()
            {
                questGiverId = entity.EntityId,
                availableQestIds = availableQuestIds,
                activeQuests = activeQuests,
                noAvailableQuestDescLangKey = noAvailableQuestDescLangKey,
                noAvailableQuestCooldownDescLangKey = noAvailableQuestCooldownDescLangKey,
                noAvailableQuestCooldownDaysLeft = cooldownDaysLeft
            };

            sapi.Network.GetChannel("vsquest").SendPacket<QuestInfoMessage>(message, player.Player as IServerPlayer);
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            if (entity.Alive && !entity.HasBehavior<EntityBehaviorConversable>())
            {
                return new WorldInteraction[] {
                    new WorldInteraction(){
                        ActionLangCode = "alegacyvsquest:access-quests",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak"
                    }
                };
            }
            else { return base.GetInteractionHelp(world, es, player, ref handled); }
        }

        private bool predecessorsCompleted(Quest quest, string playerUID)
        {
            var completedQuests = new List<string>(entity.World.PlayerByUid(playerUID)?.Entity?.WatchedAttributes.GetStringArray("alegacyvsquest:playercompleted", new string[0]) ?? new string[0]);
            return String.IsNullOrEmpty(quest.predecessor)
                || completedQuests.Contains(quest.predecessor);
        }

        public override string PropertyName() => "questgiver";
    }
}