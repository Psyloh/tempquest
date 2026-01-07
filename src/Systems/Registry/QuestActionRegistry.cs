using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using vsquest.src.Systems.Actions;

namespace VsQuest
{
    public class QuestActionRegistry
    {
        private readonly Dictionary<string, QuestAction> actionRegistry;
        private readonly ICoreAPI api;

        public QuestActionRegistry(Dictionary<string, QuestAction> actionRegistry, ICoreAPI api)
        {
            this.actionRegistry = actionRegistry;
            this.api = api;
        }

        public void RegisterActions(ICoreServerAPI sapi, Action<IServerPlayer, QuestAcceptedMessage, ICoreServerAPI> onQuestAcceptedCallback)
        {
            actionRegistry.Add("despawnquestgiver", (api, message, byPlayer, args) => 
                api.World.RegisterCallback(dt => api.World.GetEntityById(message.questGiverId).Die(EnumDespawnReason.Removed), int.Parse(args[0])));
            
            actionRegistry.Add("playsound", PlaySoundAction.Execute);
            
            actionRegistry.Add("spawnentities", ActionUtil.SpawnEntities);
            actionRegistry.Add("spawnany", ActionUtil.SpawnAnyOfEntities);
            actionRegistry.Add("spawnsmoke", ActionUtil.SpawnSmoke);
            actionRegistry.Add("recruitentity", ActionUtil.RecruitEntity);
            
            actionRegistry.Add("healplayer", (api, message, byPlayer, args) => 
                byPlayer.Entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, 100));
            
            actionRegistry.Add("addplayerattribute", (api, message, byPlayer, args) => 
                byPlayer.Entity.WatchedAttributes.SetString(args[0], args[1]));
            
            actionRegistry.Add("removeplayerattribute", (api, message, byPlayer, args) => 
                byPlayer.Entity.WatchedAttributes.RemoveAttribute(args[0]));
            
            actionRegistry.Add("completequest", ActionUtil.CompleteQuest);
            
            actionRegistry.Add("acceptquest", (api, message, byPlayer, args) => 
                onQuestAcceptedCallback(byPlayer, new QuestAcceptedMessage() { questGiverId = long.Parse(args[0]), questId = args[1] }, sapi));
            
            actionRegistry.Add("giveitem", ActionUtil.GiveItem);
            actionRegistry.Add("addtraits", ActionUtil.AddTraits);
            actionRegistry.Add("removetraits", ActionUtil.RemoveTraits);
            actionRegistry.Add("servercommand", ActionUtil.ServerCommand);
            actionRegistry.Add("playercommand", ActionUtil.PlayerCommand);
            actionRegistry.Add("giveactionitem", ActionUtil.GiveActionItem);

            actionRegistry.Add("setquestgiverattribute", SetQuestGiverAttributeAction.Execute);
            actionRegistry.Add("notify", NotifyAction.Execute);
            actionRegistry.Add("showquestfinaldialog", ShowQuestFinalDialogAction.Execute);
        }
    }
}
