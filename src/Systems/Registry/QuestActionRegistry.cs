using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestActionRegistry : IRegistry
    {
        private readonly Dictionary<string, IQuestAction> actionRegistry;
        private readonly ICoreAPI api;
        private readonly ICoreServerAPI sapi;
        private readonly Action<IServerPlayer, QuestAcceptedMessage, ICoreServerAPI> onQuestAcceptedCallback;

        public QuestActionRegistry(Dictionary<string, IQuestAction> actionRegistry, ICoreAPI api, ICoreServerAPI sapi, Action<IServerPlayer, QuestAcceptedMessage, ICoreServerAPI> onQuestAcceptedCallback)
        {
            this.actionRegistry = actionRegistry;
            this.api = api;
            this.sapi = sapi;
            this.onQuestAcceptedCallback = onQuestAcceptedCallback;
        }

        public void Register()
        {
            actionRegistry.Add("despawnquestgiver", new DespawnQuestGiverAction());
            actionRegistry.Add("openquests", new OpenQuestsAction());

            actionRegistry.Add("playsound", new PlaySoundQuestAction());

            actionRegistry.Add("spawnentities", new SpawnEntitiesAction());
            actionRegistry.Add("spawnany", new SpawnAnyOfEntitiesAction());
            actionRegistry.Add("spawnsmoke", new SpawnSmokeAction());
            actionRegistry.Add("recruitentity", new RecruitEntityAction());

            actionRegistry.Add("healplayer", new HealPlayerAction());

            actionRegistry.Add("addplayerattribute", new AddPlayerAttributeAction());

            actionRegistry.Add("addplayerint", new AddPlayerIntAction());

            actionRegistry.Add("removeplayerattribute", new RemovePlayerAttributeAction());

            actionRegistry.Add("completequest", new CompleteQuestAction());

            actionRegistry.Add("acceptquest", new AcceptQuestAction(sapi, onQuestAcceptedCallback));

            actionRegistry.Add("addjournalentry", new AddJournalEntryQuestAction());

            actionRegistry.Add("giveitem", new GiveItemAction());
            actionRegistry.Add("addtraits", new AddTraitsAction());
            actionRegistry.Add("removetraits", new RemoveTraitsAction());
            actionRegistry.Add("servercommand", new ServerCommandAction());
            actionRegistry.Add("playercommand", new PlayerCommandAction());
            actionRegistry.Add("questitem", new GiveActionItemAction());
            actionRegistry.Add("giveactionitem", new GiveActionItemAction());

            actionRegistry.Add("allowcharselonce", new AllowCharSelOnceAction());

            actionRegistry.Add("randomkill", new RollKillObjectivesAction());

            actionRegistry.Add("resetwalkdistance", new ResetWalkDistanceQuestAction());
            actionRegistry.Add("checkobjective", new CheckObjectiveAction());
            actionRegistry.Add("markinteraction", new MarkInteractionAction());

            actionRegistry.Add("cooldownblock", new CooldownBlockAction());

            actionRegistry.Add("setquestgiverattribute", new SetQuestGiverAttributeQuestAction());
            actionRegistry.Add("notify", new NotifyQuestAction());
            actionRegistry.Add("showquestfinaldialog", new ShowQuestFinalDialogQuestAction());
        }
    }
}
