# VSQuest Actions

> **Documentation Version:** v1.1.0

---

## What are Actions?

**Actions** are executable commands that trigger when specific quest events occur. They perform operations like giving items, spawning entities, playing sounds, running commands, etc.

> [!IMPORTANT]
> **Actions** are different from **ActionObjectives**:
> - **Actions** = Things that *happen* (give item, play sound, spawn entity)
> - **ActionObjectives** = Conditions the player must *complete* (kill X enemies, walk Y distance)

---

## When Actions Trigger

### In Quest JSON Files

Actions can be defined in these places within a quest JSON:

| Property | When it triggers |
|----------|------------------|
| `onAcceptedActions` | When the player accepts the quest |
| `actionRewards` | When the player completes the quest |
| `onFailedActions` | When the player fails the quest |

### In NPC Dialogues (Action Strings)

Actions can be triggered from **NPC dialogue triggers** using a compact string format. This is processed by `ActionStringExecutor`.

**Where Action Strings Are Used:**

1. **Dialogue triggers** — When an NPC dialogue option has a trigger value, it's intercepted by `ConversablePatch` (a Harmony patch on `EntityBehaviorConversable.Controller_DialogTriggers`) and passed to `ActionStringExecutor.Execute()`.

2. **RandomKill objective callbacks** — The `randomkill` action stores progress and completion action strings in player attributes. When kill progress is made, `RandomKillQuestUtils.FireActions()` executes the stored action strings.

**Parsing Rules (from `ActionStringExecutor`):**

1. The input string is split by `;` (semicolon) into individual action commands
2. Each command is parsed using regex: `(?:'([^']*)')|([^\s]+)`
3. The first token is the action ID
4. Subsequent tokens are arguments
5. Arguments wrapped in single quotes `'...'` preserve spaces inside them
6. The action ID is looked up in `QuestSystem.ActionRegistry` and executed

**Syntax:**
```
actionId arg1 arg2 'argument with spaces'; anotherActionId arg1
```

---

## Action Format

```json
{
  "id": "actionId",
  "args": ["arg1", "arg2", "arg3"]
}
```

- `id` — The action identifier (see list below)
- `args` — Array of string arguments passed to the action

---

## Commands Category

### `servercommand`

Executes a command as the server console. Useful for admin operations, giving permissions, or triggering other mod commands.

**Arguments:**
- All arguments are joined with spaces to form the command
- Leading `/` is added automatically if missing

**Example:**
```json
{
  "id": "servercommand",
  "args": ["give", "PlayerName", "game:sword-iron", "1"]
}
```
This executes: `/give PlayerName game:sword-iron 1`

---

### `playercommand`

Sends a command to be executed on the player's client. The command is transmitted via network and executed locally.

**Arguments:**
- All arguments are joined with spaces to form the command

**Example:**
```json
{
  "id": "playercommand", 
  "args": [".waypoint", "add", "Quest Location"]
}
```
This makes the player run: `.waypoint add Quest Location`

---

## Core Quest Category

### `acceptquest`

Makes the player accept a quest programmatically. Triggers the full quest acceptance flow including `onAcceptedActions`.

**Arguments:**
- `<questId>` — The quest ID to accept (required)
- `[questGiverId]` — Optional entity ID of the quest giver (defaults to current quest giver from context)

---

### `completequest`

Force-completes a quest for the player. Triggers the quest completion flow including `actionRewards`.

**Arguments:**
- `[questId]` — The quest ID to complete (defaults to current quest if not specified)
- `[questGiverId]` — Optional entity ID of the quest giver (defaults to current quest giver from context)

---

### `despawnquestgiver`

Removes the quest giver NPC from the world after a delay.

**Arguments:**
- `<delayMs>` — Delay in milliseconds before despawning (required)

---

### `openquests`

Opens the quest selection GUI for the player, showing available quests from the current quest giver.

**Arguments:** None

---

### `playsound`

Plays a sound effect for the player.

**Arguments:**
- `<soundLocation>` — Asset location of the sound (required)
- `[volume]` — Volume level as float, default 1.0 (optional)

---

## Items Category

### `giveitem`

Gives a vanilla or mod item/block to the player.

**Arguments:**
- `<itemCode>` — Full item/block code including domain (required)
- `<amount>` — Quantity to give (required)
- `[itemizerName]` — Optional custom display name (stored as `itemizerName` attribute)
- `[itemizerDesc...]` — Optional custom description, all remaining args joined with spaces

If the player's inventory is full, the item spawns as an entity at the player's position.

---

### `questitem`

Gives an action item defined in `itemconfig.json` to the player.

**Arguments:**
- `<actionItemId>` — The ID of the action item from `itemconfig.json` (required)

---

## Journal Category

### `addjournalentry`

Adds or updates a journal entry for the player.

**Arguments:**
- `<loreCode>` — Unique identifier for this journal entry category (required)
- `<title>` — Journal entry title, supports localization keys (required)
- `<chapter1>` — First chapter text, supports localization keys (required)
- `[chapter2...]` — Additional chapters, each remaining arg becomes a chapter

---

## Player Category

### `addplayerattribute`

Sets a persistent string attribute on the player's watched attributes. These attributes are synced to the client and can be used for quest conditions, dialogue checks, etc.

**Arguments:**
- `<key>` — Attribute key/name (required)
- `<value>` — Attribute value (required)

---

### `removeplayerattribute`

Removes a persistent attribute from the player's watched attributes.

**Arguments:**
- `<key>` — Attribute key/name to remove (required)

---

### `healplayer`

Heals the player by applying healing damage. If no amount is specified, defaults to 1000 HP (essentially full heal).

**Arguments:**
- `[amount]` — Amount of health to restore, default 1000 (optional)

---

### `allowcharselonce`

Sets the `allowcharselonce` boolean attribute to `true` on the player, allowing them to change their character class/selection one time. This is useful for quests that grant the player a second chance at character creation.

**Arguments:** None

---

### `addtraits`

Adds one or more extra character traits to the player's `extraTraits` attribute. Traits are stored in a hash set, so duplicates are ignored.

**Arguments:**
- `<trait1>` — First trait code (required)
- `[trait2...]` — Additional traits to add


---

### `removetraits`

Removes one or more character traits from the player's `extraTraits` attribute.

**Arguments:**
- `<trait1>` — First trait code to remove (required)
- `[trait2...]` — Additional traits to remove

---

## Entity Spawning Category

### `spawnentities`

Spawns one or more entities at the quest giver's position. Each argument is treated as an entity code and spawned.

**Arguments:**
- `<entityCode1>` — First entity code to spawn (required)
- `[entityCode2...]` — Additional entity codes, each spawned separately

---

### `spawnany`

Spawns one random entity from the provided list at the quest giver's position.

**Arguments:**
- `<entityCode1>` — First entity code option (required)
- `<entityCode2>` — Second entity code option (required for randomness)
- `[entityCode3...]` — Additional entity code options

---

### `spawnsmoke`

Spawns a smoke particle effect at the quest giver's position. Useful for dramatic appearances or disappearances.

**Arguments:** None

---

### `recruitentity`

Recruits the current quest giver entity as a companion/guard for the player. Sets the `guardedPlayerUid` and `employedSince` attributes on the entity.

**Arguments:** None

---

## UI & Feedback Category

### `notify`

Shows a notification message to the player. The message is sent via network and displayed on the client.

**Arguments:**
- `<message>` — Notification text to display (required)

---

### `showquestfinaldialog`

Opens the quest completion dialog with custom title, text, and optional buttons.

**Arguments:**
- `<titleLangKey>` — Dialog title, supports localization keys (required)
- `<textLangKey>` — Dialog body text, supports localization keys (required)
- `[option1LangKey]` — First button text (optional)
- `[option2LangKey]` — Second button text (optional)

---

## World Category

### `cooldownblock`

Replaces a target block with an invisible placeholder block for a duration (in **in-game hours**), then restores the original block (including its block entity data, if any).

**Arguments:**
- `<delayHours>` — Duration in in-game hours (float, invariant culture, required)
- `[x,y,z]` — Optional coordinate string. If omitted, uses the last interacted block coordinates stored in player watched attributes (`vsquest:lastinteract:x|y|z|dim`).

**Notes:**
- Requires the placeholder block asset `alegacyvsquest:cooldownplaceholder` to exist on server/client.
- If the target block is already the placeholder, the action does nothing.

**Example (action string):**
```
cooldownblock 1 512000,3,512000
```
---

### `setquestgiverattribute`

Sets a typed attribute on the current quest giver entity's watched attributes.

**Arguments:**
- `<key>` — Attribute key/name (required)
- `<type>` — Value type: `bool`, `int`, or `string` (required)
- `<value>` — Attribute value (required)

For bool type, accepts `true`/`1` for true, anything else for false.
