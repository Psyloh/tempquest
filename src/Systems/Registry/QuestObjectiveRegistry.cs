using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class QuestObjectiveRegistry : IRegistry
    {
        private readonly Dictionary<string, IActionObjective> objectiveRegistry;
        private readonly ICoreAPI api;

        public QuestObjectiveRegistry(Dictionary<string, IActionObjective> objectiveRegistry, ICoreAPI api)
        {
            this.objectiveRegistry = objectiveRegistry;
            this.api = api;
        }

        public void Register()
        {
            objectiveRegistry.Add("plantflowers", new NearbyFlowersActionObjective());
            objectiveRegistry.Add("hasattribute", new PlayerHasAttributeActionObjective());
            objectiveRegistry.Add("interactat", new InteractAtCoordinateObjective());
            objectiveRegistry.Add("interactcount", new InteractCountObjective());
            objectiveRegistry.Add("checkvariable", new CheckVariableObjective());
            objectiveRegistry.Add("randomkill", new RandomKillObjective());
            objectiveRegistry.Add("walkdistance", new WalkDistanceObjective());
            objectiveRegistry.Add("timeofday", new TimeOfDayObjective());
        }
    }
}
