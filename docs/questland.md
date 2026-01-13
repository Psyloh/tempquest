# Alegacy VS Quest QuestLand (land-claim notifications)

> **Documentation Version:** v1.1.0

---

## Overview

VSQuest can optionally fire an action string when a player **enters/leaves a land claim** while they have a matching active quest.

This is controlled by an optional quest pack config file: `config/questland.json`.

---

## Configuration File

Create `assets/<yourDomain>/config/questland.json`:

```json
{
  "allowedQuestPrefixes": ["yourmod:"],
  "enterAction": "notify '{message}'",
  "exitAction": "notify '{message}'",
  "defaultExitMessage": "Outside",
  "enterMessages": {
    "Some Claim": "Entering Some Claim"
  }
}
```

---

## Config Keys

- **`allowedQuestPrefixes`**: array of quest id prefixes that enable QuestLand for the player (the system chooses the most specific/longest prefix match among active quests)
- **`enterAction`**: action string template executed when entering a claim (or changing claim)
- **`exitAction`**: action string template executed when leaving a claim
- **`defaultExitMessage`**: message used on exit when no specific message is available
- **`enterMessages`**: map `claimName -> message` for custom enter messages

---

## Placeholders

Placeholders are supported inside `enterAction` / `exitAction` templates:

- **`{message}`**: resolved message (from `enterMessages` for enter, or `defaultExitMessage` for exit)
- **`{claim}`**: current claim name (enter/change)
- **`{lastclaim}`**: previous claim name (exit/change)
