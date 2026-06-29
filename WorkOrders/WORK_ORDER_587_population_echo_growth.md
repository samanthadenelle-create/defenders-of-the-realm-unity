# WORK_ORDER_587 — Population & Echo Growth System (V1)

**Status:** READY TO IMPLEMENT (owner spec, drafted w/ Grok, 2026-06-29) · Economy/Workforce lane · data-driven
**Origin:** F8 felt-test flag_07 (MainCastle_Hall, 2026-06-29): *"tower count never increases, what determines
population growth, waves cleared EXP gained? Need to determine unlock cadence of echoes."*
**Supersedes:** `WORK_ORDER_514` **Item B** (the "Population → Saved Echoes X/10 → SP" rebrand). WO-514 **Item A**
(tower cap/enforce — perf + anti-turtle) and **Item C** (town enemies siege structures, V2) REMAIN OPEN under 514.
**Canon:** memories `echo-workforce-drag-drop`, `combat-pivot-single-hero-northstar` (echo workforce = V1;
base-building = V2 behind `ff.basebuilding`), `owner-thinks-in-data-structures`.

---

## Goal
A meaningful, **quest/milestone-driven Population** counter that unlocks additional **Echo workforce slots**.
Replace any raw percentage logic with **milestone-driven** growth that feels *earned* and ties into the systems
we already have (quests, outpost reclamation, wave victories, village/housing upgrades). Echoes auto-gather after
assignment — **no micro-management**.

## Requirements
- Population has **Current XP** and a **Cap** (cap raised by housing / village level).
- Echo slots start at **1 (Wood)**, unlock up to **max 5** (3 organic + 2 flex) — matches memory `echo-workforce-drag-drop`.
- Growth is **earned** — primarily quests, outpost reclamation, and wave victories (village upgrades raise the cap).
- Lightweight, **data-driven**, **MVVM-friendly**.
- **No micro-management** of echoes (auto-gather after a single drag-drop assignment).
- Both `ff.blinkchrome` states must look correct.
- Add a **DataRegression** case for milestone validation.

> **REUSE, do not reinvent (BINDING):** drive the EXISTING echo workforce / life-force system — do not build a
> second echo concept. Hook the EXISTING Quest / Outpost / Wave / Village-upgrade events. The recon pass
> (file:line map of those systems) precedes implementation; PopulationService is the new *coordinator*, not a new economy.

---

## Design

### PopulationService (Core service — `DeNelle.Core`, resolved like other `CoreServices.*`)
- State: `currentXP`, `populationCap`, `echoSlotsUnlocked` (1–5).
- `AddPopulationXP(int amount, string source)` — **logs `source` via `FlowTrace`** (§12 self-reporting), adds XP,
  then checks thresholds and unlocks the next echo slot when a milestone is hit. Fires an event on unlock.
- Internal milestone check is **data-driven** (reads `population-milestones.json`); never hardcoded branches
  (owner thinks in data structures — table over control flow).
- Persists `currentXP` / `echoSlotsUnlocked` through the existing save system (GameState).

### population-milestones.json (data file — same loader/path as other canonical catalogs)
Milestone array; each entry = a population step that grants a population increase + an echo-slot unlock, gated by an
**OR/AND of earned conditions**. Example shape (final numbers owner-tunable):
```json
{
  "milestones": [
    { "echoSlot": 2, "any": { "xp": 800,  "questsCompleted": 8,  "outpostsCleared": 2 } },
    { "echoSlot": 3, "any": { "xp": 1800, "questsCompleted": 18, "outpostsCleared": 5 } },
    { "echoSlot": 4, "all": { "villageLevel": 4, "questsCompleted": 35 } },
    { "echoSlot": 5, "all": { "villageLevel": 6 }, "any": { "questsCompleted": 55, "outpostsCleared": 12 } }
  ]
}
```
(`any` = any one condition satisfies; `all` = all required; an entry may carry both.)

### Integration points — call `PopulationService.AddPopulationXP(amount, source)` from EXISTING hooks
- **Quest completion** (main story / daily) — quest-complete event.
- **Outpost reclamation / clear** — outpost-cleared event.
- **Wave victory** (especially defense waves) — wave-cleared event.
- **Village / housing upgrade** — raises `populationCap` (not XP) on upgrade.
> Implementation note: wire these as **subscriptions** in PopulationService (or thin one-line hooks at each event
> site) — pseudo-only in this WO; exact hook lines come from the recon file:line map.

### Echo slot logic
- Slot 1 = always active (**Wood**).
- New slots unlock → player drag-drops a flex echo onto any resource (**Wood / Iron / Grain**), then it's autonomous.
- Echo count **multiplies harvest rate** (or adds parallel workers) — drive the existing workforce's gather rate.

### UI / feedback (presentation = dumb View; logic in Service + VM — §5/MVVM)
- **Population counter** in the Village HUD (Obsidian frame chrome — memory `ui-blink-template-master-frame-formula`).
- **Unlock notification**: *"The village grows stronger — a new echo has awakened!"*
- **World-tree visual feedback**: brighter glow / more spirits tied to population / echo count (reuse the existing
  life-force → tree-growth visual loop; do not author a new VFX system).

---

## Deliverables (in order)
1. `PopulationService.cs` — core logic + data-driven milestone checking + unlock event + save persistence.
2. `population-milestones.json` — the data file (same loader/path convention as existing canonical catalogs).
3. Integration hooks in Quest / Outpost / Wave / Village-upgrade systems (pseudo in spec; real lines from recon).
4. Population counter UI in the Village HUD (Obsidian frame) + unlock toast + world-tree glow tie-in.
5. Update `CANON_GROUND_TRUTH_<date>.md` + relevant docs; add DataRegression milestone-validation case.

## Acceptance criteria
- AddPopulationXP logs source + advances XP; hitting a milestone unlocks the next echo slot (≤5) exactly once.
- Milestones are read from JSON (changing the JSON changes cadence with **no code change**); DataRegression validates
  the file (monotonic slots 2→5, no gaps, parseable).
- Quest / outpost / wave / village-upgrade events each move the counter (FlowTrace shows the source).
- Echo slot unlock → assignable flex echo → harvest rate increases; no per-echo micro-management.
- Population counter renders correctly in BOTH `ff.blinkchrome` on/off; unlock toast fires; tree glow responds.
- Save/reload preserves currentXP + echoSlotsUnlocked.

## What NOT to touch / out of scope
- Do **not** build a second echo/workforce economy — drive the existing one (memory `echo-workforce-drag-drop`).
- Do **not** implement WO-514 Item A (tower cap) or Item C (siege) here — they stay under WO-514.
- Do **not** greenfield quest/outpost/wave systems — only add the `AddPopulationXP` call at their existing events.
- Base-building proper stays V2 behind `ff.basebuilding`; this WO is the V1 echo-workforce growth only.

## OPEN (owner confirm before/at implementation)
- **SP linkage:** WO-514 Item B tied saved echoes to **skill points** (3 echoes → 1 SP). This WO's design ties echo
  growth to **workforce slots / harvest**, not SP. Decouple SP from this (keep SP on its own path), or also grant SP
  on milestone? **Owner call.** (Default if unanswered: workforce-only, no SP — SP stays on the Wisdom path.)
- Final milestone numbers (XP / quests / outposts / village level) are placeholders → owner-tune in the JSON.
