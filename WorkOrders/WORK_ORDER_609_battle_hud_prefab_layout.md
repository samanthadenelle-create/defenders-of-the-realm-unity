# WORK ORDER 609 — Battle HUD: Prefab-First Layout (data-focused combat chrome)

**Status:** READY TO IMPLEMENT (Phase 1 in flight)  
**Lane:** HUD/Presentation (HudKit + Core models + Village producers)  
**Minted:** 2026-07-05  
**Supersedes:** WO-507 9-zone grid (retired shim); Diablo-orbs mockup is **not** this spec — bars/plates only.  
**Architecture law:** `docs/HUD_OBSIDIAN_ARCHITECTURE_2026-07-03.md` A1 + A4 — prefab YAML → `widget-params.json` → factory binder → model.

---

## Goal

Rebuild the **hostile(activebattle)** HUD occupancy to show only the combat data the owner cares about, using **mirrored Blink Obsidian prefabs** (`Assets/Resources/RpgUi/prefabs/`) and named child bind paths from `Assets/Resources/Data/Canonical/widget-params.json`. Presentation is dumb; producers own all state.

---

## Layout (landscape, center empty for 3D fight)

```
┌─────────────────────────────────────────────────────────────────┐
│ [Player plate TL]      [Enemy plate TC]           [Flee][⚙]   │
│ [player buff row]      [enemy buff row]   (Phase 2)             │
│                                                                 │
│                    (battle view — no HUD)                       │
│                                                                 │
│ [lean D-pad BL]              [4 hotswap slots BC]  [static WER] │
│                              [HP pot][MP pot]      [attack BR]  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Prefab binding table (acceptance — every row must pass)

| Zone | Widget id | Prefab (`RpgUi/prefabs/`) | Bind child (`widget-params.json` path) | Model / command |
|---|---|---|---|---|
| vitals | `playerNameplate` | `PartyNameplate` | `PlayerName`, `StatBars/HealthBackground/HealthFill`, `StatBars/ManaBackground/ManaFill` | `HeroVitalsModel` |
| status | `targetFrame` | `TargetNameplate` | `TargetIcon`, `*Name*` TMP, `StatBars/HealthBackground/HealthFill` | `TargetModel` |
| status | `castBar` | `CastBar1` | `CastBar1Fill` (`fillAmount`) | `CastModel` |
| moveCluster | `moveCluster` | *(kit §1.11 — 4 round buttons, not one prefab)* | `BuildControllerCluster` | `HudMoveInput` |
| actionRail | `abilityRow` | `Action_Bar_Slot` ×4 | `Image` icon, frame sprite | `AbilityLoadoutModel` ← `HeroLoadout` W/E/R + Q |
| actionRail | `attackButton` | `Action_Bar_Slot` | `Image` | `HudCommands.Attack` |
| actionBar | `assignableSkillRow` | `Action_Bar_Slot` ×4 | `Image` | `AssignableLoadoutModel` ← `AssignableSkillBar` |
| actionBar | `hpPotionSlot` | `Action_Bar_Slot` | `Image`, count TMP | `ConsumableHotbarModel.HpCount` → `HudCommands.Potion` |
| actionBar | `manaPotionSlot` | `Action_Bar_Slot` | `Image`, count TMP | `ConsumableHotbarModel.ManaCount` → `HudCommands.ManaPotion` |

**Phase 2 (not blocking Phase 1 gate):**

| Zone | Widget | Prefab | Bind | Model |
|---|---|---|---|---|
| vitals | `playerBuffRow` | `Action_Bar_Slot` ×N small | `Image` | `StatusEffectsModel` (hero) — **new** |
| status | `enemyBuffRow` | `Action_Bar_Slot` ×N small | `Image` | `StatusEffectsModel` (locked target) — **new** |

---

## `hud-areas.json` — hostile postures

### `hostile(prebattle)`

```json
{ "area": "vitals",      "widgets": ["playerNameplate"] },
{ "area": "status",      "widgets": ["targetFrame"] },
{ "area": "actionBar",   "widgets": ["assignableSkillRow", "hpPotionSlot", "manaPotionSlot"] },
{ "area": "actionRail",  "widgets": ["abilityRow"] },
{ "area": "moveCluster", "widgets": ["moveCluster"] }
```

### `hostile(activebattle)`

```json
{ "area": "vitals",      "widgets": ["playerNameplate"] },
{ "area": "status",      "widgets": ["targetFrame", "castBar"] },
{ "area": "system",      "widgets": ["fleeButton", "settingsButton"] },
{ "area": "moveCluster", "widgets": ["moveCluster"] },
{ "area": "actionBar",   "widgets": ["assignableSkillRow", "hpPotionSlot", "manaPotionSlot"] },
{ "area": "actionRail",  "widgets": ["abilityRow", "attackButton"] }
```

Remove `targetCycle` from top-center unless owner re-requests the 4-enemy strip.

---

## Data producers (Village → Core, new in Phase 1)

| Producer | Writes | Reads |
|---|---|---|
| `AssignableLoadoutProducer` | `AssignableLoadoutModel` (4 slots) | `AssignableSkillBarAccess`, `AbilityCatalog`, `HeroAbilities.ExtraCooldownRemaining` |
| `ConsumableHotbarProducer` | `ConsumableHotbarModel` | `VillageInventory.Get("minor-heal-potion")`, `Get("cons_mana_draught")` |

## Commands (Village bridge registers)

| Command | Handler |
|---|---|
| `HudCommands.AssignableCast(int slot)` | `AssignableSkillBar.AbilityIdForSlot` → `HeroAbilities.TryCastExtra(id)` |
| `HudCommands.Potion()` | `ConsumableUseService.TryUse("minor-heal-potion", inFight: true)` |
| `HudCommands.ManaPotion()` | `ConsumableUseService.TryUse("cons_mana_draught", inFight: true)` |

---

## Acceptance criteria (Phase 1 — headless + owner felt)

- [ ] `hostile(activebattle)` shows player plate top-left, enemy plate top-center, D-pad bottom-left.
- [ ] Static W/E/R + attack on bottom-right (`actionRail`); hotswap 4 on bottom-center (`actionBar`).
- [ ] HP + mana potion slots visible in battle when inventory count > 0; `SetCount` shows stack.
- [ ] Tapping hotswap slot fires `TryCastExtra` for assigned ability (WO-574 path).
- [ ] Tapping potions consumes from larder via `ConsumableUseService`.
- [ ] All plates/slots instantiate from `RpgUi/prefabs/*` when `PrefabMode` on; fills use `fillAmount` only (§1.1).
- [ ] `COMPILE_GATE_OK`; brace balance on every edited `.cs`.

## Do NOT touch

- Diablo orbs mockup scene (design-only, uncommitted tooling).
- Buff/debuff rows until Phase 2 (`StatusEffectsModel`).
- `.unity` hand-edits. No Village ↔ HUD assembly edge.

---

## Files (Phase 1)

| File | Change |
|---|---|
| `Assets/Resources/Data/Canonical/hud-areas.json` | Battle posture rows |
| `Assets/StreamingAssets/Data/Canonical/hud-areas.json` | Mirror |
| `Assets/_Modules/Core/HudModel/HudModels.cs` | `AssignableLoadoutModel`, `ConsumableHotbarModel` |
| `Assets/_Modules/Village/HUD/HudModelProducers.cs` | Two producers |
| `Assets/_Modules/Village/HUD/HudModelHost.cs` | Register producers |
| `Assets/_Modules/Core/HUD/HudCommands.cs` | Assignable + mana potion |
| `Assets/_Modules/Village/HUD/HudKitCommandBridge.cs` | Register handlers |
| `Assets/_Modules/HUD/Kit/HudKitController.cs` | Widgets + bind |