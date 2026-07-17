# Claude Handoff — Barracks / CoC / Troop Roster (2026-07-16)

**Pass this file to Claude as the boot packet.** All WOs are on disk under `WorkOrders/`.  
**Numbering authority:** `CLI_LANES_WO_NUMBERS.md` — **next free after this block = 738**.

---

## What you are building (player fantasy)

1. **Barracks** trains an army (default troops day-one).  
2. **Upgrade Barracks** unlocks more troop types (CoC ladder).  
3. **Train UI** is proper **Obsidian FrameCrafting** master-detail (locked rows visible).  
4. Later (CoC spine): deploy that army against **AI camps**; architecture ready for async PvP snapshots.

---

## Read first (BINDING)

| Priority | Doc |
|----------|-----|
| 1 | `CLAUDE.md` / project rules + `SESSION_CANON_LOADER.md` if starting a session |
| 2 | `docs/UI_BLINK_TEMPLATE_CANON.md` + `docs/UI/Grok-02-Obsidian-UI-guidance.md` (any UI WO) |
| 3 | Program indexes below |

**Do not invent chrome.** Factory = `ElarionUiKit.BuildObsidianPanel`. No UXML. No hand-edit `.unity`. Dual-copy any `Data/Canonical/*.json` to StreamingAssets **and** Resources.

---

## Program A — Troop roster + Train UI (DO THIS FIRST)

**Index:** `WorkOrders/WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md`  
**Layout WO:** `WorkOrders/WORK_ORDER_737_barracks_train_obsidian_layout.md`

| Order | WO | File | Intent |
|------:|----|------|--------|
| 1 | **732** | `WORK_ORDER_732_troop_roster_data_schema.md` | 7 troops in `troops.json` + `unlockBarracksTier`; dual-copy |
| 2 | **733** | `WORK_ORDER_733_troop_unlock_train_ui_gate.md` | Unlock helper + train refuse; locked still listed |
| 2b | **737** | `WORK_ORDER_737_barracks_train_obsidian_layout.md` | **Obsidian layout contract** (zones, lock/select/CTA) — implement **with** 733 |
| 3 | **734** | `WORK_ORDER_734_barracks_tier_unlock_copy.md` | Barracks tier **effect** text announces unit unlocks |
| 3 | **735** | `WORK_ORDER_735_troop_visual_placeholders.md` | Models/icons placeholders (∥ 734) |
| 4 | **736** | `WORK_ORDER_736_troop_roster_verify_canon.md` | Regression + dual-copy + canon one-liner |

### Locked roster (do not freestyle)

| Barracks tier | Unlocks |
|---------------|---------|
| **T1 default** | `troop-footman`, `troop-archer` |
| T2 | `troop-spearman` |
| T3 | `troop-shieldguard` |
| T4 | `troop-outrider` |
| T5 | `troop-battlemage` |
| T6 | `troop-echo-legionnaire` |

Full stats/costs: program index table.

### Suggested Claude batch for first PR

**Implement 732 + 733 + 737 together** (data + gate + layout). Then 734/735. Close with 736.

---

## Program B — CoC Arena / AI camps (AFTER roster Train is usable)

**Index:** `WorkOrders/WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`

### WO-723 = DONE — do not re-open

Charter is closed: `WorkOrders/WORK_ORDER_723_coc_offense_path_charter.RESULT.md`

| Pin | Value |
|-----|--------|
| **Path** | **A** — Barracks → `ArmyStorage` → `RaidDeployController` (Path B parked) |
| **Entry** | Arena Herald → **camp select → Path A raid** (NOT ArenaAttack budget squad) |
| **Landmark** | Colosseum model (`ff.colosseum` at close) |
| **Flags end-state** | barracks/arena/colosseum ON at program close; raid/raidwalk stay ON |

**Start Program B at WO-724.** Treat 723 RESULT as binding law only.

| Order | WO | File | Intent |
|------:|----|------|--------|
| **START** | **724** | `WORK_ORDER_724_barracks_live_train_army.md` | Surface barracks + train path (uses roster from Program A) |
| ∥ | **725** | `WORK_ORDER_725_settlement_arena_entry_live.md` | Herald entry live; **retarget panel to Path A camp select** (723 §2) |
| 2 | **726** | `WORK_ORDER_726_ai_camp_attack_loop.md` | Deploy army → clear AI camp → return |
| 3 | **727** | `WORK_ORDER_727_recipe_ai_settlements.md` | Tiered AI `BaseLayout` recipes |
| 4 | **728** | `WORK_ORDER_728_repeatable_raid_economy.md` | Cooldown / stars / loot |
| 5 | **729** | `WORK_ORDER_729_defend_and_watch.md` | AI attacks your base (optional) |
| 5 | **730** | `WORK_ORDER_730_async_pvp_foundation.md` | Snapshot I/O for future PvP (optional) |
| 6 | **731** | `WORK_ORDER_731_coc_felt_complete_close.md` | PO felt + flag defaults ON + canon |
---

## Definition of done (every WO)

- [ ] Acceptance checkboxes in that WO marked with proof  
- [ ] `WorkOrders/WORK_ORDER_NNN_….RESULT.md` written  
- [ ] Every touched `.cs`: brace balance + no NULs  
- [ ] Canonical JSON dual-copied when edited  
- [ ] CompileGate when code changes  
- [ ] No production flag default ON without PO (731 / 724 close)

---

## Paste starter for Claude

```
You are implementing Defenders / Echoes of Elarion barracks + CoC prep.

BOOT:
1. Read WorkOrders/CLAUDE_HANDOFF_2026-07-16_barracks_coc_roster.md
2. WO-723 is DONE — read only WorkOrders/WORK_ORDER_723_coc_offense_path_charter.RESULT.md
   (Path A + Herald entry). Do NOT re-implement or re-charter 723.
3. Read WorkOrders/WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md
4. Read WorkOrders/WORK_ORDER_737_barracks_train_obsidian_layout.md
5. Read docs/UI_BLINK_TEMPLATE_CANON.md + docs/UI/Grok-02-Obsidian-UI-guidance.md

FIRST BATCH (roster / Train UI — do before CoC loop):
- WO-732 troop roster data schema
- WO-733 unlock train UI gate
- WO-737 Obsidian layout for Train panel

THEN CoC spine STARTING AT WO-724 (skip 723):
- WO-724 barracks live
- WO-725 arena/herald entry (Path A camp select, not Path B recruit)
- WO-726+ per program order

Rules: code-built uGUI only; BuildObsidianPanel FrameCrafting; dual-copy JSON;
no scene hand-edits; FlowTrace on refuse; write RESULT files.
```
---

## File checklist (all should exist)

```
WorkOrders/CLAUDE_HANDOFF_2026-07-16_barracks_coc_roster.md   ← this file
WorkOrders/WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md
WorkOrders/WORK_ORDER_732_troop_roster_data_schema.md
WorkOrders/WORK_ORDER_733_troop_unlock_train_ui_gate.md
WorkOrders/WORK_ORDER_734_barracks_tier_unlock_copy.md
WorkOrders/WORK_ORDER_735_troop_visual_placeholders.md
WorkOrders/WORK_ORDER_736_troop_roster_verify_canon.md
WorkOrders/WORK_ORDER_737_barracks_train_obsidian_layout.md
WorkOrders/WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md
WorkOrders/WORK_ORDER_723_coc_offense_path_charter.md          ← DONE (RESULT only)
WorkOrders/WORK_ORDER_723_coc_offense_path_charter.RESULT.md   ← Path A law
WorkOrders/WORK_ORDER_724_barracks_live_train_army.md          ← CoC START
WorkOrders/WORK_ORDER_725_settlement_arena_entry_live.md
WorkOrders/WORK_ORDER_726_ai_camp_attack_loop.md
WorkOrders/WORK_ORDER_727_recipe_ai_settlements.md
WorkOrders/WORK_ORDER_728_repeatable_raid_economy.md
WorkOrders/WORK_ORDER_729_defend_and_watch.md
WorkOrders/WORK_ORDER_730_async_pvp_foundation.md
WorkOrders/WORK_ORDER_731_coc_felt_complete_close.md
```

---

*Minted for owner → Claude handoff. CLI implements; PO felt-closes flag flips.*
