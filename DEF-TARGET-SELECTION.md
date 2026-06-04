# ATB Target Selection — Design Spec

**Feature:** Player manually selects which enemy to attack in the ATB battle screen  
**Scope:** UI layer only — the engine already supports `explicitTargetId`  
**Testing platform:** Windows (keyboard + mouse)

---

## Background

The ATB engine (`Targeting.ResolveTargets`) already accepts an `explicitTargetId` string. When it's non-empty it uses that specific target; when empty it falls back to `RngPick` (random). `BattleAction.TargetId` also exists as a field on every action. The feature gap is entirely in the **UI layer** — nothing tells the player which enemy is targeted, and nothing lets them change it.

---

## Current Behavior

- Player clicks "Attack" → `BattleController.HandleAttackClicked` submits `BattleAction.MakeAttack(targetId: <first enemy id or null>)`
- Engine random-picks a living enemy if `targetId` is null/empty
- Player has no visibility into or control over which enemy is targeted

---

## Desired Behavior

### Trigger conditions

Target selection activates when:
- The active unit is the **hero or a controllable pet** (not an AI enemy)
- The player selects **Attack**, or an **ability whose `TargetMode == SingleEnemy`** or **`SingleAlly`**
- There are **two or more** living units on the target side

When only one valid target remains, **auto-select it immediately** — no selection UI shown.

For `AllEnemies`, `RandomEnemies`, `AllAllies`, `Self` — skip target selection entirely and submit the action directly (target selection is not meaningful for these modes).

---

## UI Flow

```
Player clicks "Attack"
    │
    ├─ 1 living enemy? → auto-submit BattleAction.MakeAttack(enemy.Id)
    │
    └─ 2+ living enemies?
            │
            └─ Enter TARGET SELECTION MODE
                    │
                    ├─ Enemy cards highlighted; a reticle appears on current selection
                    ├─ Default selection: first living enemy in the unit list
                    │
                    ├─ [Tab] or [→ / ↓]  → cycle to next living enemy
                    ├─ [Shift+Tab] or [← / ↑] → cycle to previous living enemy
                    ├─ [Click enemy card]       → jump to that enemy
                    ├─ [Enter] or [Space]       → confirm → submit action
                    └─ [Escape]                 → cancel → return to action buttons
```

---

## Visual Spec

### Enemy card highlight states
| State | Treatment |
|---|---|
| Default (no selection mode) | Normal card, no border |
| Selection mode — not selected | Cards dim slightly (opacity ~0.6), dim border |
| Selection mode — selected | Full brightness, **gold border** (2px), reticle icon top-right corner |
| Confirmed (action submitted) | Flash white briefly (~0.15s), then resolve as normal |

### Reticle
A small targeting diamond `◈` or crosshair rendered as a label or icon in the top-right corner of the selected enemy card. Colour: gold (`#F5C518`). Flashes once per second at a slow pulse (no animation required for v1 — static is fine).

### Action buttons during selection
- "Attack" button text changes to **"Confirm Target"** while in selection mode
- "Escape" label appears below the action cluster: `[Esc] Cancel`
- All other action buttons (Skills, Item, Flee) are **disabled** while selecting

---

## Input Bindings (Windows)

| Action | Primary | Alternate |
|---|---|---|
| Cycle next target | `Tab` | `D` / `RightArrow` / `DownArrow` |
| Cycle previous target | `Shift+Tab` | `A` / `LeftArrow` / `UpArrow` |
| Confirm selection | `Enter` | `Space` |
| Cancel | `Escape` | Right-click |
| Click to select | Left-click on enemy card | — |

---

## Engine Integration

No engine changes required. The target selection UI produces a `string selectedTargetId` which gets passed into the existing `BattleAction.MakeAttack(targetId)` call:

```csharp
// Current (random target):
_runtimeState.ChooseAction(BattleAction.MakeAttack(null));

// After this feature:
_runtimeState.ChooseAction(BattleAction.MakeAttack(_selectedTargetId));
```

For abilities, the existing `BattleAction.MakeAbility(slot, targetId)` already accepts a `targetId` parameter — pass the selected id there too.

---

## Components to Create / Modify

### New: `TargetSelector.cs` (in `BattleATB/`)
A MonoBehaviour (or inner class of BattleController) that:
- Holds a `List<BattleUnit> _validTargets` (living enemies, sorted by unit list order)
- Tracks `int _selectedIndex`
- Exposes `string SelectedId => _validTargets[_selectedIndex].Id`
- `Enter()` — activates, populates valid targets, sets default selection
- `Exit()` — deactivates, resets visual state
- `CycleNext()` / `CyclePrev()` — wraps around the list, skips dead units
- `Confirm()` → fires `OnConfirmed(string targetId)` event
- `Cancel()` → fires `OnCancelled` event
- `Update()` — reads keyboard input while active

### Modified: `BattleController.cs`
- `HandleAttackClicked()` — instead of directly submitting, call `_targetSelector.Enter()` when multiple targets exist
- `HandleActionSubmitted()` — hook `TargetSelector.OnConfirmed` to call `ChooseAction`
- `HandleBattleChanged()` — update enemy card visual state to reflect `_targetSelector.SelectedId`

### Modified: HUD enemy card(s)
- Enemy cards need to be **clickable** (register click callbacks)
- A `selected` USS class or inline style toggles the gold border
- Cards need to display the reticle icon when selected

---

## Edge Cases

| Scenario | Handling |
|---|---|
| Selected enemy dies before confirmation | Re-run `Enter()` — if only one remains, auto-confirm; if none remain, cancel and show "No targets" |
| Hero has `AllEnemies` ability — player clicks Attack | Skip selection, submit immediately |
| Only 1 enemy on field from the start | Never enter selection mode — auto-submit with that enemy's id |
| Player presses Escape with 0 other buttons available | Just exit selection mode; do not submit any action |
| Pet's turn (AI-controlled) | Target selection never triggers — engine resolves pet AI targets automatically |

---

## Out of Scope (this ticket)

- Ally targeting UI (for items / heals on party members) — same system, separate ticket
- Enemy intent preview (showing what the enemy *will* do while you're selecting)
- Target stat tooltip on hover (HP, element, status — future quality-of-life)
- Controller / gamepad support beyond keyboard
- Any change to the ATB engine, `Targeting.cs`, or `BattleState`

---

## V2 — Smart Default Targeting (Archetype Brain)

> Build on V1 without changing its inputs or outputs. The player still confirms or overrides freely — V2 just makes the **cursor land on the right enemy by default** and adds **visual threat cues** so tactical choices are obvious at a glance.

### Concept

The enemy AI already uses archetype logic (`Ai.PickEnemyAttackTarget` has Tank hit the lowest-defense foe; Caster/Boss use specials at HP thresholds). V2 mirrors that on the player side: when target selection opens, the system scores every living enemy and pre-selects the highest-priority one. A small icon on each card explains the reasoning.

### Threat Priority Score (engine-side, pure function)

Add `TargetPriority.ScoreTargets(BattleState, BattleUnit actor)` to `Targeting.cs` (or a new `TargetPriority.cs`). Returns an ordered list of `(BattleUnit unit, int score, ThreatTag tag)`. Rules, highest score wins:

| Condition | Score bonus | Tag shown |
|---|---|---|
| Unit has `StatusKind.Mark` (already in engine) | +60 | `◎ Marked` |
| Unit has a `Taunt` flag active (see below) | forces selection | `🛡 Taunt` |
| `EnemyArchetype.Caster` AND has a special on cooldown 0 | +40 | `⚡ Casting` |
| Lowest current HP among living enemies | +30 | `💀 Finish` |
| `EnemyArchetype.Tank` with `SelfHeal` AND HP < 40% | +25 | `❤ Healing` |
| `EnemyArchetype.Boss` AND HP < 60% (enrage threshold) | +20 | `⚠ Enraged` |
| Default / tie-break: original list order | 0 | *(no tag)* |

The `ThreatTag` is a string label rendered as a small pill badge on the enemy card. Only the top-scoring tag is shown per card — the highest bonus wins.

### Taunt Mechanic

Add `StatusKind.Taunt` to the `StatusKind` enum (between `Shield` and `Mark`).

Behaviour:
- When any living enemy has `Taunt` active, the target cursor **locks to that unit** — Tab/arrow cycling is disabled, all other cards dim to 30% opacity
- The locked card shows a `🛡 TAUNT` badge in red
- If the player has a "Cleanse" item, the item button **pulses** to hint that Taunt can be broken
- If the taunting enemy dies mid-turn, Taunt lock releases immediately
- Only one unit can hold Taunt at a time (applying it to a second unit clears the first)

Tank enemies can gain Taunt via their special move (add `ApplyStatus: Taunt` to `EnemySpecial.ApplyStatus`). The `EnemyDef` for tank archetypes in `Defs.cs` would set this.

### Visual Changes (V2 additions)

```
┌─────────────────────────────┐
│  SKELETON GUARD    🛡 TAUNT │   ← threat tag pill (red)
│  HP ████████░░░░░  142/200  │
│  ATB ██████░░░░░░           │
│  [Burn] [Slow]              │   ← status icons
└─────────────────────────────┘
       ↑ gold border + ◈ reticle when selected
```

Enemy cards gain:
1. **Threat tag pill** — top-right, colour-coded by tag type
2. **ATB bar** — shows how close the enemy is to acting (urgency cue: if ATB is near full, the player might want to kill it first)
3. **Status icons** — tiny icons for active statuses (burn, slow, freeze, mark, taunt)

### New Engine Items Required

| Item | Location | What it does |
|---|---|---|
| `StatusKind.Taunt` | `Types.cs` | New status enum value |
| `TargetPriority.cs` | `Engine/` | Pure scoring function, returns ordered target list + tags |
| `SpecialTargetMode.Taunt` or `Special.ApplyTaunt` | `Defs.cs` | Tank special that sets Taunt on the caster |
| Taunt enforcement in `Targeting.ResolveTargets` | `Targeting.cs` | When a living unit has Taunt, override `explicitTargetId` to that unit's id |

The last point is the key engine change: `ResolveTargets` would check for a living taunting unit first, before using `explicitTargetId`. This way Taunt is enforced even if the player somehow submits a different target id.

### V2 Out of Scope

- Healer enemies that cast `Heal` on allies (requires new `TargetMode.SingleAlly` for enemies — a separate mechanic)
- Multi-target Taunt (AOE taunt that locks all party members simultaneously)
- Threat meter (persistent aggro tracking across turns)
- Player-side Taunt / provoke abilities
