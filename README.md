

<div align="center">

# VS Quest

**A powerful and extensible quest system mod for Vintage Story**

[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Game Version](https://img.shields.io/badge/Vintage%20Story-1.21.6+-green.svg)](https://www.vintagestory.at/)
[![Version](https://img.shields.io/badge/Mod%20Version-3.0.0-orange.svg)](resources/modinfo.json)

</div>

---

## 📖 Overview

**VS Quest** is a fork of the original quest mod, enhanced with additional functionality for the [alegacy.online](https://alegacy.online) server. This mod adds a comprehensive quest system to Vintage Story, allowing players to accept, track, and complete quests from custom NPCs (Quest Givers).

---

## �️ Quest System Architecture

### How It Works

The quest system integrates with Vintage Story's entity and dialogue systems:

---

## 🧑‍🤝‍🧑 Quest Givers

Quest Givers are entities with the `questgiver` behavior. They use Vintage Story's `conversable` behavior for dialogue.

### Entity Configuration

Add the `questgiver` behavior to your entity's server behaviors:

```json
{
  "code": "questgiver",
  "behaviors": [
    {
      "code": "conversable",
      "dialogue": "config/dialogue/myquestgiver"
    },
    {
      "code": "questgiver",
      "quests": [
        "mymod:quest1",
        "mymod:quest2"
      ],
      "selectrandom": false,
      "selectrandomcount": 1
    }
  ]
}
```

### Quest Giver Behavior Properties

| Property | Type | Description |
|----------|------|-------------|
| `quests` | `string[]` | List of quest IDs this NPC can offer |
| `selectrandom` | `bool` | If `true`, randomly selects quests from the list per entity |
| `selectrandomcount` | `int` | How many quests to randomly select (default: 1) |

### Accessing Quests

There are two ways for players to access quests:

| Method | Condition | Description |
|--------|-----------|-------------|
| **Dialogue Trigger** | Entity has `conversable` behavior | Use `"trigger": "openquests"` in dialogue component |
| **Sneak + Right-click** | Entity has NO `conversable` behavior | Direct interaction opens quest GUI |

---

## 💬 Dialogue System

The dialogue system uses Vintage Story's native `conversable` behavior with a special trigger for opening the quest GUI.

### Dialogue File Structure

Location: `assets/{modid}/config/dialogue/{name}.json`

```json
{
  "components": [
    {
      "code": "intro",
      "owner": "npc",
      "type": "talk",
      "text": [
        { "value": "mymod:dialogue-intro" }
      ],
      "jumpTo": "main"
    },
    {
      "code": "main",
      "owner": "player",
      "type": "talk",
      "text": [
        { "value": "mymod:dialogue-open-quests", "jumpTo": "openquests" },
        { "value": "mymod:dialogue-goodbye", "jumpTo": "close" }
      ]
    },
    {
      "code": "openquests",
      "owner": "npc",
      "type": "talk",
      "trigger": "openquests",
      "text": [
        { "value": "mymod:dialogue-quest-confirm" }
      ]
    }
  ]
}
```

### Dialogue Component Types

| Type | Owner | Description |
|------|-------|-------------|
| `talk` | `npc` | NPC speaks text, can have `jumpTo` for next component |
| `talk` | `player` | Player chooses from multiple text options, each with own `jumpTo` |
| `condition` | `npc` | Checks a variable and jumps to different components |

### Special Trigger: `openquests`

When a dialogue component has `"trigger": "openquests"`, clicking it:
1. Closes the dialogue window
2. Opens the Quest Selection GUI
3. Shows available quests from this quest giver

### Condition Components

Check entity or player variables to branch dialogue:

```json
{
  "code": "checkquest",
  "owner": "npc",
  "type": "condition",
  "variable": "entity.questdone",
  "isValue": "true",
  "thenJumpTo": "postquest",
  "elseJumpTo": "prequest"
}
```

---

## 🖥️ Quest GUI

The quest GUI has two tabs:

| Tab | Content |
|-----|---------|
| **Available Quests** | Quests player can accept (dropdown selector + Accept button) |
| **Active Quests** | Quests in progress (progress display + Complete button) |

### Quest Availability Conditions

A quest appears in the "Available" tab only if:

1. **Cooldown passed** — Time since last acceptance ≥ `cooldown` (in game days)
2. **Not currently active** — Player doesn't have this quest in progress
3. **Predecessor completed** — If `predecessor` is set, that quest must be completed

---

## 🌐 Localization

All quest text uses Vintage Story's language system.

### Required Lang Keys

For each quest ID `{questId}`, define these in your `lang/en.json`:

| Lang Key | Usage |
|----------|-------|
| `{questId}-title` | Quest name in dropdown and GUI title |
| `{questId}-desc` | Quest description shown in GUI |
| `{questId}-obj` | Progress text with placeholders (e.g., `"Found: {0}/{1}"`) |
| `{questId}-final` | *(optional)* Message shown when quest is completable |

### Progress Placeholders

The `-obj` key receives progress values as indexed placeholders:

```json
"mymod:quest1-obj": "Flowers planted: {0}/10\nItems collected: {1}/{2}"
```

---

## 📁 File Structure

```
assets/{modid}/
├── config/
│   ├── quests.json          # Quest definitions
│   ├── itemconfig.json      # Action Items registry
│   └── dialogue/
│       └── {npcname}.json   # Dialogue trees
├── entities/
│   └── questgiver.json      # Quest giver entity
└── lang/
    └── en.json              # Localization
```

---

## � Action Item System

Action Items are special items that execute quest actions when used (Right-Click) by the player. 

### Configuration

Action Items are defined in `assets/{modid}/config/itemconfig.json`.

```json
{
  "actionItems": [
    {
      "id": "memorial-scarf-2026",
      "itemCode": "game:clothes-shoulder-artisans-scarf",
      "name": "<font color=\"#8B5CF6\">Memorial Scarf</font>",
      "description": "A scarf with embroidery...",
      "actions": [
        {
          "id": "playsound",
          "args": ["game:sounds/effect/cloth"]
        },
        {
          "id": "notify",
          "args": ["You feel warmth..."]
        }
      ]
    }
  ]
}
```

### Properties

| Property | Description |
|----------|-------------|
| `id` | Unique ID for the action item (used in `questitem`) |
| `itemCode` | The base game item/block code to use as a template |
| `name` | Custom display name (supports HTML formatting) |
| `description` | Custom tooltip description |
| `actions` | List of `ActionWithArgs` to execute on right-click |

### Obtaining Action Items

Players receive these items via the `questitem` action (in quests or commands):

```json
{
  "id": "questitem",
  "args": ["memorial-scarf-2026"]
}
```

When given, the item retains the visual appearance of the base `itemCode` but gains the custom name, description, and action triggers.

---

## 🎯 Actions Reference

Actions are executable commands that can be triggered at different points during a quest lifecycle. They are defined as objects with an `id` and an `args` array.

### Action Format

Actions are defined in JSON using the `ActionWithArgs` structure:

```json
{
  "id": "actionname",
  "args": ["arg1", "arg2", "arg3"]
}
```

### Where Actions Can Be Used

| Property | Trigger | Description |
|----------|---------|-------------|
| `onAcceptedActions` | Quest Accepted | Executes when a player accepts the quest |
| `actionRewards` | Quest Completed | Executes when a player completes the quest |
| Action Objectives (`args` field) | Objective Event | Inline action strings triggered during objective events (e.g., interacting at coordinates) |
| Action Items (`actions` field) | Item Used | Executes when player right-clicks an Action Item |

### Inline Action Strings

Some systems (like `interactat` objectives) support inline action strings. Multiple actions are separated by `;` and arguments with spaces use single quotes:

```
actionname arg1 arg2; anotheraction 'arg with spaces'
```

---

## 📋 Available Actions

### Entity Actions

| Action ID | Arguments | Description |
|-----------|-----------|-------------|
| `despawnquestgiver` | `delay_ms` | Removes the quest giver entity after a delay (in milliseconds) |
| `spawnentities` | `entityCode1`, `entityCode2`, ... | Spawns one or more entities at the quest giver's position |
| `spawnany` | `entityCode1`, `entityCode2`, ... | Spawns a **random** entity from the provided list |
| `recruitentity` | *(none)* | Makes the quest giver follow the player as a companion |
| `spawnsmoke` | *(none)* | Creates a smoke particle effect at the quest giver's position |

---

### Item Actions

| Action ID | Arguments | Description |
|-----------|-----------|-------------|
| `giveitem` | `itemCode`, `amount`, `[name]`, `[description]` | Gives the player an item. Optional Itemizer integration for custom name/description |
| `giveactionitem` | `actionItemId` | Gives a pre-configured Action Item from `itemconfig.json` |

---

### Player State Actions

| Action ID | Arguments | Description |
|-----------|-----------|-------------|
| `healplayer` | *(none)* | Fully heals the player (100 HP) |
| `addplayerattribute` | `key`, `value` | Sets a string attribute on the player entity |
| `removeplayerattribute` | `key` | Removes an attribute from the player entity |
| `addtraits` | `trait1`, `trait2`, ... | Adds traits to the player's `extraTraits` list |
| `removetraits` | `trait1`, `trait2`, ... | Removes traits from the player's `extraTraits` list |

---

### Quest Flow Actions

| Action ID | Arguments | Description |
|-----------|-----------|-------------|
| `acceptquest` | `questGiverId`, `questId` | Triggers acceptance of another quest |
| `completequest` | `[questId]`, `[questGiverId]` | Marks a quest as completed. Uses current quest if no args |

---

### UI & Notification Actions

| Action ID | Arguments | Description |
|-----------|-----------|-------------|
| `playsound` | `soundAsset` | Plays a sound (e.g., `game:sounds/effect/writing`) |
| `notify` | `message` | Displays a chat notification to the player (supports lang keys) |
| `showquestfinaldialog` | `titleKey`, `textKey`, `[option1Key]`, `[option2Key]` | Shows a dialog box with localized text and optional buttons |

---

### Command Actions

| Action ID | Arguments | Description |
|-----------|-----------|-------------|
| `servercommand` | `command`, `args...` | Executes a server console command |
| `playercommand` | `command`, `args...` | Sends a command to be executed on the player's client |

---

### Journal Actions

| Action ID | Arguments | Description |
|-----------|-----------|-------------|
| `addjournalentry` | `loreCode`, `title`, `chapter1`, `[chapter2]`, ... | Adds or updates a journal entry with multiple chapters |

---

## 🎯 Action Objectives Reference

Action Objectives are special quest completion conditions that track player progress. Unlike regular objectives (gather, kill, etc.), these use custom logic and can trigger inline actions.

### Action Objective Format

Action Objectives are defined in the `actionObjectives` array using the same `ActionWithArgs` structure:

```json
{
  "id": "objectivename",
  "args": ["arg1", "arg2", "..."]
}
```

---

## 📋 Available Action Objectives

### `interactat` — Interact at Coordinate

Completes when the player right-clicks a block at a specific coordinate. Can trigger inline actions on interaction.

| Argument | Description |
|----------|-------------|
| `x,y,z` | Target block coordinates (comma-separated) |
| `actionString` | *(optional)* Inline actions to execute on interaction |

---

### `interactcount` — Count Multiple Interactions

Tracks how many of the specified coordinates have been interacted with. Provides progress tracking (e.g., "3/8 found").

| Argument | Description |
|----------|-------------|
| `x,y,z` (repeated) | Multiple coordinate strings |
| *(last arg)* | Empty string or inline action string |


> **Note:** Use `interactat` for each coordinate to trigger per-location actions, and `interactcount` to track overall progress.

---

### `checkvariable` — Check Player Variable

Checks a player's integer attribute against a value using comparison operators. Can trigger actions when condition is met.

| Argument | Description |
|----------|-------------|
| `varName` | Player attribute name to check |
| `operator` | Comparison: `=`, `==`, `>`, `>=`, `<`, `<=`, `!=` |
| `value` | Integer value to compare against |

---

### `hasattribute` — Player Has Attribute

Completes when the player has a specific string attribute set to a specific value.

| Argument | Description |
|----------|-------------|
| `key` | Player attribute name |
| `value` | Expected string value |

---

### `plantflowers` — Nearby Flowers

Completes when there are enough flowers within a 31x11x31 block area around the player.

| Argument | Description |
|----------|-------------|
| `count` | Minimum number of flowers required nearby |


---

## 💡 Example: Complete Quest Definition

You can check newyear2026 for a complete example of a quest.

---

<div align="center">

**Happy Questing! 🎮**

</div>
