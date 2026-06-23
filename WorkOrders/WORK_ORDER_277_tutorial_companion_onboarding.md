# WO-277: Tutorial + Companion Onboarding Flow
**Linear:** [DEF-222](https://linear.app/defenders-of-the-realm/issue/DEF-222/tutorial-companion-onboarding-flow-forced-intro-dialogue-guided-tower)
**Lane:** Combat/AI
**Status:** READY TO IMPLEMENT
**Priority:** High

## Overview

Scripted first-time onboarding that teaches the player everything through narrative — no generic UI prompts. Companion meets hero, tours the village, guides tower building, fights alongside in first combat, introduces pets, then sets the player free.

## Full Flow (7 Scenes)

### Scene 1: Arrival + Meeting
- Hero spawns at Heartwood tree
- Companion (DIFFERENT character from hero — see mapping below) runs up
- Forced dialogue (unskippable first time):
  - "You made it. I wasn't sure anyone else would come."
  - "The enemy's been pushing closer every night. They've breached the outer walls twice this week."
  - "Elarion needs defenders. I'm [Name] — I'll show you what we're working with."

**Companion mapping:**
- Thrain (mage) → Grom or scholar NPC
- Grom (knight) → Sylas or veteran NPC
- Sylas (archer) → Elara or scout NPC
- Elara (cleric) → Thrain or acolyte NPC

### Scene 2: Village Tour + First Tower
- Auto-walk: companion leads, hero follows (player input disabled)
- Companion narrates buildings as they pass: Forge, Arcane Tower, Pet House
- At nearest gate: "See that gate? Last attack came through there. We need a watchtower."
- Tutorial prompt: "Tap BUILD to place your first tower"
- Player places tower — FREE (no resource cost)
- "Good. That'll slow them down. Let's check the other entrances."

### Scene 3: Second Gate — Resource Wall + First Combat
- Auto-walk to second gate
- "We should fortify this one too... Not enough resources."
- HORN BLAST — enemies spawn AT THIS GATE (nearest to hero, not random)
- "They're here! Defend this gate — NOW!"
- Small wave (2-3 enemies) — companion fights alongside hero
- Companion uses abilities, demonstrates combat by example

### Scene 4: Post-Battle — Supplies
- "Thanks for the help pushing them back."
- "We better hurry to get some supplies. Here — take these to get started."
- **Grant exactly enough resources for 3 more Level 1 towers**
- "That should be enough to put up a watchtower at each entrance. Get them built before the next attack."

### Scene 5: Daily Quests Callout
- Companion gestures at quest panel (panel pulses/glows)
- "See those tasks on the side? Complete them for extra rewards. Resources, experience, sometimes rarer things."
- "Keep an eye on them — they change, and they're worth doing."

### Scene 6: Pet Introduction
- Starting pet runs up to hero — playful bounce animation
- "Well, looks like you've got a friend already. One of the Echoes — it's chosen you."
- **Name prompt:** text input with default suggestion (e.g. "Scrap" for Crow, "Fang" for Grimhound)
- "What should [PetName] do? They can stay and defend... or go gather supplies."
- **Choice: Defend or Gather**
- If DEFEND: "[PetName] will hold the line. But we need more resources — you should explore beyond the gates."
- If GATHER: "Smart. [PetName] will bring back what they find. Keep your eyes open out there."
- → Either path nudges player toward exploration outside the village to reach nodes

### Scene 7: Freedom
- Camera shows all 4 gates in quick sequence
- "North, South, East, West — they can come from any direction. Fortify all four."
- Tutorial complete — full player control
- Companion follows as party member
- HUD objective: "Build towers at the remaining 3 gates (0/3)"

## Files to Create

| File | Purpose |
|---|---|
| `Assets/_Modules/Village/Tutorial/TutorialDirector.cs` | Master sequencer — drives all 7 scenes via coroutine chain |
| `Assets/_Modules/Village/Tutorial/CompanionSpawner.cs` | Spawns correct companion based on hero class |
| `Assets/_Modules/Village/Tutorial/TutorialAutoWalk.cs` | Waypoint follower — disables player input, moves hero along path |
| `Assets/_Modules/Village/Tutorial/TutorialDialogue.cs` | Dialogue queue — feeds lines to TownsfolkBubble, waits for tap-to-advance |
| `Assets/_Modules/Village/Tutorial/PetIntroduction.cs` | Pet spawn, name prompt UI, defend/gather choice |
| `Assets/_Modules/Village/Tutorial/TutorialWaveSpawner.cs` | Spawns tutorial wave at specific gate (not random) |

## Files to Modify

| File | Change |
|---|---|
| `Assets/_Modules/Village/Hero/HeroLocomotion.cs` | Add `SetAutoWalk(Transform target)` / `ClearAutoWalk()` for tutorial control |
| `Assets/_Modules/Village/WaveManager.cs` | Add `SpawnAtGate(int gateIndex, int enemyCount)` for tutorial-specific spawn |
| `Assets/_Modules/HUD/DailyQuestPanel.cs` | Add `Pulse()` method for tutorial highlight |
| `Assets/_Modules/Pets/PetHarvester.cs` | Add `SetMode(PetMode.Defend \| PetMode.Gather)` if not already present |

## Tutorial Economy

| Moment | Resources | Towers |
|---|---|---|
| Gate 1 | FREE | 1st placed |
| Post-battle grant | 3× Level 1 cost | — |
| Gates 2-4 | Player spends grant | 2nd, 3rd, 4th |
| **End** | **0 remaining** | **4 total** |

## Do NOT Touch
- Village.unity (never hand-edit)
- VillageSceneBuilder.cs
- Any existing wave balance or economy tuning
- Existing combat systems — companion uses them, doesn't modify them

## Dependencies
- `TowerPlacementSystem` (done — DEF-73)
- `TownsfolkBubble` / `WandererDialogue` (done — dialogue delivery)
- `PetHarvester` (done — DEF-122)
- `EconomyService` (done — DEF-78)
- `WaveManager` (done — DEF-58)
- Hero prefabs for all 4 classes must be wired (DEF-219 — Elara fix needed first)

## Acceptance Criteria
- [ ] Companion spawns as DIFFERENT character from hero
- [ ] Forced dialogue on first start — introduces companion + threat
- [ ] Auto-walk village tour with building narration
- [ ] First tower placed free at nearest gate
- [ ] Second gate: "not enough resources" callout
- [ ] First wave spawns at nearest gate — NOT random
- [ ] Companion fights alongside hero in first wave
- [ ] Post-battle: companion grants exactly 3× Level 1 tower cost
- [ ] Tutorial ends with 4 towers (one per gate), zero surplus
- [ ] Quest panel callout with pulse/glow
- [ ] Pet runs up, player names it, chooses Defend or Gather
- [ ] Defend choice → companion suggests exploring outside for resources
- [ ] Gather choice → pet runs to nearest node
- [ ] Pet name persisted in GameState
- [ ] Full player control after Scene 7
- [ ] Companion follows as party member
- [ ] HUD objective: "Build towers at remaining gates (0/3)"
- [ ] Skippable on subsequent playthroughs
- [ ] Brace balance check on all .cs files
