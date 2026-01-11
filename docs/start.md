# VSQuest Mod Structure Documentation

> **Documentation Version:** v1.1.0

---

## � Documentation Index

| Document | Description |
|----------|-------------|
| [start.md](start.md) | Mod structure overview (this file) |
| [example.md](example.md) | Step-by-step quest creation guide |
| [actions.md](actions.md) | All available quest actions |
| [objectives.md](objectives.md) | All available quest objectives |
| [actionitems.md](actionitems.md) | Action Items system |
| [dialogue.md](dialogue.md) | NPC dialogue system |
| [commands.md](commands.md) | Chat commands |

---

## �📁 Root Level (`vsquest/`)

```
├── vsquest.csproj              # C# project file
├── README.md                   # Documentation
├── LICENSE                     # MIT License
├── Thumbnail.png               # Mod thumbnail
├── prepare-debugging.ps1       # Debug setup script
├── release.sh                  # Release script
├── update-lang.ps1             # Localization update script
├── .git/                       # Git repository
├── .gitignore
├── .vscode/                    # VS Code settings
├── src/                        # Source code (97 files)
├── resources/                  # Mod assets (17 items)
└── quests/                     # Quest packs (46 items)
```

---

## 📂 `src/` — C# Source Code

### `src/Commands/` — Chat Command Handlers

```
Commands/
├── ActionItems/
│   ├── GetActionItemCommandHandler.cs        # /getactionitem command
│   └── QuestActionItemsCommandHandler.cs     # List action items
├── Attributes/
│   ├── QuestAttrListCommandHandler.cs        # /questattrlist - list player attrs
│   ├── QuestAttrRemoveCommandHandler.cs      # /questattrremove - remove attr
│   └── QuestAttrSetCommandHandler.cs         # /questattrset - set attr
├── Entities/
│   ├── QuestEntityCommandHandler.cs          # Entity management
│   └── QuestNpcListCommandHandler.cs         # List NPCs
└── Management/
    ├── QuestCheckCommandHandler.cs           # Check quest status
    ├── QuestCompleteActiveCommandHandler.cs  # Complete active quest
    ├── QuestCompleteCommandHandler.cs        # Complete specific quest
    ├── QuestForgiveAllCommandHandler.cs      # Reset all failed quests
    ├── QuestForgiveCommandHandler.cs         # Reset specific failed quest
    └── QuestListCommandHandler.cs            # List quests
```

### `src/Entity/` — Entity Behaviors

```
Entity/
└── Behavior/
    └── EntityBehaviorQuestGiver.cs    # Quest Giver NPC behavior (10KB)
```

### `src/Gui/` — User Interface

```
Gui/
├── QuestFinalDialogGui.cs    # Quest completion dialog
└── QuestSelectGui.cs         # Quest selection dialog (11KB)
```

### `src/Harmony/` — Harmony Patches

```
Harmony/
├── BlockInteractPatch.cs           # Block interaction hooks
├── ConversablePatch.cs             # NPC conversation hooks
├── ItemAttributePatches.cs         # Item attribute display
├── ItemTooltipPatch.cs             # Custom tooltip rendering (9KB)
├── PlayerAttributePatches.cs       # Player attribute hooks
└── ServerBlockInteractPatch.cs     # Server-side block hooks
```

### `src/Item/` — Custom Items

```
Item/
└── ItemDebugTool.cs    # Debug tool item
```

### `src/Systems/` — Core Quest Logic (74 files)

**Root Files:**

```
Systems/
├── Quest.cs                  # Quest data model
├── ActiveQuest.cs            # Active quest state (15KB)
├── QuestSystem.cs            # Main mod system (8KB)
└── QuestException.cs         # Custom exceptions
```

#### `Systems/Actions/` — Quest Actions (28 files)

```
Actions/
├── ActionStringExecutor.cs           # Execute action strings
├── Commands/
│   ├── PlayerCommandAction.cs        # Run command as player
│   └── ServerCommandAction.cs        # Run server command
├── Core/
│   ├── AcceptQuestAction.cs          # Accept a quest
│   ├── CompleteQuestAction.cs        # Complete a quest
│   ├── DespawnQuestGiverAction.cs    # Remove quest giver
│   ├── OpenQuestsAction.cs           # Open quest menu
│   └── PlaySoundQuestAction.cs       # Play sound
├── Items/
│   ├── GiveActionItemAction.cs       # Give action item
│   └── GiveItemAction.cs             # Give regular item
├── Journal/
│   └── AddJournalEntryQuestAction.cs # Add journal entry
├── Objectives/
│   ├── CheckObjectiveAction.cs       # Check objective status
│   ├── MarkInteractionAction.cs      # Mark block interaction
│   ├── ResetWalkDistanceQuestAction.cs
│   └── RollKillObjectivesAction.cs   # Generate random kill objectives
├── Player/
│   ├── AddPlayerAttributeAction.cs   # Add player attr
│   ├── AllowCharSelOnceAction.cs     # Allow character reselect
│   ├── HealPlayerAction.cs           # Heal player
│   └── RemovePlayerAttributeAction.cs
├── Spawn/
│   ├── RecruitEntityAction.cs        # Recruit NPC
│   ├── SpawnAnyOfEntitiesAction.cs   # Spawn random entity
│   ├── SpawnEntitiesAction.cs        # Spawn specific entities
│   └── SpawnSmokeAction.cs           # Spawn smoke particles
├── Traits/
│   ├── AddTraitsAction.cs            # Add character traits
│   └── RemoveTraitsAction.cs         # Remove traits
├── UI/
│   ├── NotifyQuestAction.cs          # Show notification
│   └── ShowQuestFinalDialogQuestAction.cs
└── World/
    └── SetQuestGiverAttributeQuestAction.cs
```

#### `Systems/ActionObjectives/` — Quest Objectives (10 files)

```
ActionObjectives/
├── Combat/
│   ├── KillNearObjective.cs
│   └── RandomKillObjective.cs
├── Gates/
│   ├── LandGateObjective.cs
│   └── TimeOfDayObjective.cs
├── Interaction/
│   ├── InteractAtCoordinateObjective.cs
│   ├── InteractCountObjective.cs
│   ├── InteractWithEntityObjective.cs
│   └── NearbyFlowersActionObjective.cs
├── Inventory/
│   ├── HasItemObjective.cs
│   └── WearingObjective.cs
├── Logic/
│   ├── CheckVariableObjective.cs
│   ├── EventCountObjectiveBase.cs
│   ├── PlayerHasAttributeActionObjective.cs
│   └── SequenceObjective.cs
└── World/
    ├── InLandObjective.cs
    ├── ReachWaypointObjective.cs
    ├── TemporalStormObjective.cs
    └── WalkDistanceObjective.cs
```

#### `Systems/ActionItems/` — Special Item Behaviors

```
ActionItems/
├── ItemConfig.cs     # Item configuration model
└── ItemSystem.cs     # Action item system
```

#### `Systems/Registry/` — Registration

```
Registry/
├── QuestActionRegistry.cs        # Register actions
├── QuestChatCommandRegistry.cs   # Register commands (7KB)
├── QuestNetworkChannelRegistry.cs
└── QuestObjectiveRegistry.cs     # Register objectives
```

#### `Systems/Messages/Network/` — Network Messages

```
Messages/Network/
├── ExecutePlayerCommandMessage.cs
├── QuestInfoMessage.cs
├── QuestMessage.cs
├── ShowNotificationMessage.cs
├── ShowQuestDialogMessage.cs
└── VanillaBlockInteractMessage.cs
```

#### `Systems/Utils/` — Utilities (14 files)

```
Utils/
├── Admin/
│   ├── PlayerAttributeAdminUtils.cs
│   └── QuestSystemAdminUtils.cs      # Admin utilities (11KB)
├── Items/
│   └── ItemAttributeUtils.cs
├── Localization/
│   ├── LocalizationUtils.cs          # Translation helpers
│   └── MobLocalizationUtils.cs       # Mob name localization
├── Quests/
│   ├── QuestDeathUtil.cs
│   ├── QuestObjectiveAnnounceUtil.cs
│   ├── QuestObjectiveMatchUtil.cs
│   ├── QuestProgressTextUtil.cs      # Progress text (7KB)
│   ├── QuestTickUtil.cs
│   ├── QuestTimeGateUtil.cs
│   └── RandomKillQuestUtils.cs       # Random kill logic (8KB)
├── UI/
│   └── NotificationTextUtil.cs
└── World/
    └── BlockEntitySearchUtils.cs
```

#### `Systems/Management/` — Quest Lifecycle

```
Management/
├── QuestEventHandler.cs          # Handle quest events
├── QuestLifecycleManager.cs      # Quest state machine (10KB)
└── QuestPersistenceManager.cs    # Save/load quests
```

#### `Systems/Interfaces/` — Interfaces

```
Interfaces/
├── IActionObjective.cs
├── IQuestAction.cs
└── IRegistry.cs
```

---

## 📂 `resources/` — Mod Assets

```
resources/
├── modinfo.json              # Mod metadata
├── modicon.png               # Mod icon
└── assets/
    └── alegacyvsquest/
        ├── config/
        │   ├── mobdisplaynames.json  # Mob name overrides
        │   └── quests/               # (empty, for builtin quests)
        ├── itemtypes/
        │   └── debugtool.json        # Debug tool item definition
        └── lang/                     # 13 language files
            ├── en.json
            ├── ru.json
            ├── de.json
            ├── pl.json
            ├── fr.json
            ├── uk.json
            ├── ja.json
            ├── cs.json
            ├── pt-br.json
            └── ... (+ 4 more)
```

---

## 📂 `quests/` — Quest Content Packs

Each pack is a standalone mod that depends on `alegacyvsquest`.

### `quests/example/` — Example Quest Pack

```
example/
├── modinfo.json
└── assets/
    └── vsquestexample/
        ├── config/
        │   ├── itemconfig.json       # Action item definitions
        │   ├── dialogue/
        │   │   └── waystone-humanoid.json  # NPC dialogue tree
        │   └── quests/               # 10 example quests
        │       ├── quest1.json
        │       ├── quest2.json
        │       ├── kill1drifter.json
        │       ├── kill1deepdrifter.json
        │       ├── talktome.json
        │       ├── talktootherguy.json
        │       ├── placeandbreak.json
        │       ├── testcommands.json
        │       ├── vanish.json
        │       └── NewYear2026.json
        ├── entities/
        │   ├── questgiver.json       # Quest Giver entity def
        │   └── humanoid/             # Humanoid NPCs
        ├── itemtypes/
        │   └── creatures.json        # Custom items
        └── lang/
            └── [translations]
```

### `quests/albase/` — Alegacy Base Quests

```
albase/
├── modinfo.json
└── assets/
    └── albase/
        ├── config/
        │   ├── dialogue/
        │   │   ├── innkeeper.json    # Innkeeper dialogue
        │   │   └── priest.json       # Priest dialogue
        │   └── quests/               # 10 quests
        │       ├── innkeeper-badcustomers.json
        │       ├── innkeeper-cellarwatch.json
        │       ├── innkeeper-doorposter.json
        │       ├── innkeeper-firstimpression.json
        │       ├── innkeeper-foxthief.json
        │       ├── innkeeper-missingmugs.json
        │       ├── innkeeper-quietpatrol.json
        │       ├── innkeeper-silentdeliveries.json
        │       ├── innkeeper-talkingbarrel.json
        │       └── priest-allowcharselonce.json
        ├── entities/
        │   ├── innkeeper.json        # Innkeeper entity
        │   └── priest.json           # Priest entity
        ├── lang/                     # Translations
        └── sounds/                   # Custom sounds
```

### `quests/newyear2026/` — New Year 2026 Event

```
newyear2026/
├── modinfo.json
└── assets/
    └── newyear2026/
        ├── config/
        │   ├── itemconfig.json       # Action items
        │   ├── dialogue/             # Event dialogues
        │   └── quests/               # Event quests
        ├── entities/
        │   └── questgiver.json       # Event NPC
        ├── lang/                     # Translations
        └── sounds/                   # Event sounds
```

---

## 🔑 Key Concepts Summary

| Component | Purpose |
|-----------|---------|
| **Quest** | Definition of objectives, rewards, actions |
| **Quest Giver** | NPC entity with `EntityBehaviorQuestGiver` |
| **Dialogue** | Conversation trees for NPCs |
| **Action** | Something that happens (give item, spawn entity, etc.) |
| **Objective** | Condition to complete (kill, walk, interact, etc.) |
| **Action Item** | Special items with custom behaviors when used |
| **Player Attribute** | Persistent player flags for quest progress |

---

## Dependencies

From `modinfo.json`:
- **Game**: Vintage Story 1.21.6+
- **Mod**: `itemizer` 1.1.1
