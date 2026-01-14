# VSQuest Chat Commands

> **Documentation Version:** v1.1.1

All commands require **`give` privilege** and are accessed via the `/quest` command.

---

## Command Reference

### Action Items

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/quest actionitems` | — | Lists all registered action items from `itemconfig.json` |
| `/quest getactionitem` | `<itemId> [amount]` | Gives an action item to yourself. Default amount is 1 |

---

### Entity Management

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/quest entities spawned` | — | Lists all currently loaded Quest Giver NPCs (entity ID, code, position) |
| `/quest entities all` | — | Lists all entity types from a quest pack domain (`assets/<domain>/entities`) |

---

### Quest Management

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/quest list` | — | Lists all registered quest IDs and their titles |
| `/quest check` | `<playerName>` | Shows active/completed quests and progress for a player |
| `/quest complete` | `<questId> <playerName>` | Force-completes a specific active quest for a player |
| `/quest completeactive` | `<playerName>` | Force-completes the player's currently active quest |
| `/quest forgive` | `<questId> <playerName>` | Resets a quest for a player: removes from active quests, clears cooldown and completed flags |
| `/quest forgiveall` | `[playerName]` | Resets ALL quests for a player: clears active quests, completed flags, and cooldowns |
| `/quest exec` | `[playerName] <actionString>` | Executes an action string on a player. If no player is given, uses the caller |

---

### Player Attributes

Player attributes are persistent flags used for quest progress tracking.

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/quest attr list` | `<playerName>` | Lists all watched quest attributes for an online player |
| `/quest attr set` | `<playerName> <key> <value>` | Sets a string attribute on an online player |
| `/quest attr remove` | `<playerName> <key>` | Removes an attribute from an online player |

### WatchedAttributes

| Command | Arguments | Description |
|---------|-----------|-------------|
| `/quest wattr setint` | `[playerName] <key> <value>` | Sets an int WatchedAttribute |
| `/quest wattr addint` | `[playerName] <key> <delta>` | Adds delta to an int WatchedAttribute |
| `/quest wattr setbool` | `[playerName] <key> <value>` | Sets a bool WatchedAttribute |
| `/quest wattr setstring` | `[playerName] <key> <value...>` | Sets a string WatchedAttribute |
| `/quest wattr remove` | `[playerName] <key>` | Removes a WatchedAttribute key |

---

## Examples

```
# List all available quests
/quest list

# Check player's quest progress
/quest check PlayerName

# Give action item
/quest getactionitem example_sword 1

# Reset a specific quest for a player
/quest forgive innkeeper-firstimpression PlayerName

# Reset all quest progress for a player
/quest forgiveall PlayerName

# Set a custom attribute
/quest attr set PlayerName talked_to_innkeeper true

# View player's attributes
/quest attr list PlayerName
```

---

## Notes

- All `<playerName>` arguments require the player to be **online**
- Quest IDs are the filename without `.json` extension (e.g., `innkeeper-firstimpression`)
- Action item IDs are defined in the quest pack's `itemconfig.json`
