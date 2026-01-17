# Alegacy VS Quest Chat Commands

> **Documentation Version:** v1.2.0

All commands require **`give` privilege** and are accessed via the `/vsq` command.

---

## Command Reference

### Action Items

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/vsq actionitems` | — | Lists all registered action items from `itemconfig.json` |
| `/vsq getactionitem` | `<itemId> [amount]` | Gives an action item to yourself. Default amount is 1 |

---

### Entity Management

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/vsq entities spawned` | — | Lists all currently loaded Quest Giver NPCs (entity ID, code, position) |
| `/vsq entities all` | — | Lists all entity types from a quest pack domain (`assets/<domain>/entities`) |

---

### Quest Management

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/vsq list` | — | Lists all registered quest IDs and their titles |
| `/vsq check` | `<playerName>` | Shows active/completed quests and progress for a player |
| `/vsq complete` | `<questId> <playerName>` | Force-completes a specific active quest for a player |
| `/vsq completeactive` | `[playerName]` | Force-completes the player's currently active quest |
| `/vsq start` | `<questId> <playerName>` | Starts a quest for a player |
| `/vsq forgive` | `<questId> <playerName>` | Resets a quest for a player: removes from active quests, clears cooldown and completed flags |
| `/vsq forgiveall` | `[playerName]` | Resets ALL quests for a player: clears active quests, completed flags, and cooldowns |
| `/vsq exec` | `[playerName] <actionString>` | Executes an action string on a player. If no player is given, uses the caller |

---

### Player Attributes

Player attributes are persistent flags used for quest progress tracking.

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/vsq attr list` | `<playerName>` | Lists all watched quest attributes for an online player |
| `/vsq attr set` | `<playerName> <key> <value>` | Sets a string attribute on an online player |
| `/vsq attr remove` | `<playerName> <key>` | Removes an attribute from an online player |

### WatchedAttributes

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/vsq wattr setint` | `[playerName] <key> <value>` | Sets an int WatchedAttribute |
| `/vsq wattr addint` | `[playerName] <key> <delta>` | Adds delta to an int WatchedAttribute |
| `/vsq wattr setbool` | `[playerName] <key> <value>` | Sets a bool WatchedAttribute |
| `/vsq wattr setstring` | `[playerName] <key> <value...>` | Sets a string WatchedAttribute |
| `/vsq wattr remove` | `[playerName] <key>` | Removes a WatchedAttribute key |

---

## Examples

```
# List all available quests
/vsq list

# Check player's quest progress
/vsq check PlayerName

# Give action item
/vsq getactionitem example_sword 1

# Reset a specific quest for a player
/vsq forgive innkeeper-firstimpression PlayerName

# Reset all quest progress for a player
/vsq forgiveall PlayerName

# Set a custom attribute
/vsq attr set PlayerName talked_to_innkeeper true

# View player's attributes
/vsq attr list PlayerName
```

---

## Notes

- All `<playerName>` arguments require the player to be **online**
- Quest IDs are the filename without `.json` extension (e.g., `innkeeper-firstimpression`)
- Action item IDs are defined in the quest pack's `itemconfig.json`
