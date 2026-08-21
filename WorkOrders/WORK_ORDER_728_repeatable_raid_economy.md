<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-16
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-16) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 728 — Repeatable Raid Economy (Cooldown, Stars, Loot)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Priority:** P1  
**Silo:** Economy / Meta  
**Depends on:** WO-727  
**Blocks:** WO-731 (recommended)  
**Program:** `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`  
**Effort:** M  

---

## Goal

Raiding is a **repeatable beat**, not one-and-done: cooldowns, light star scoring, loot that funds train/upgrade costs.

---

## Built already

- Outpost loot / `GrantArenaLoot` patterns
- `ArenaProgressStore` W/L ledger (partial)
- Soft resources wood / iron / grain + crystals
- `BattleRewardSummary` pattern (itemized grants) on hero arena — mirror for raid

---

## Gaps to close

1. **`RaidCooldownService`** (per ARENA_SOLUTION): per-camp cooldown; optional threat+1 on respawn.
2. **Star / destruction scoring** (light): % buildings destroyed and/or time → loot multiplier.
3. **Optional grain sink** for army (pivot: grain feeds troops) — only if cheap.
4. Persist cooldowns + W/L in **SaveSchema** (not PlayerPrefs-only).
5. Victory UI lists exact grants.

---

## Tasks

1. Design cooldown table (easy camp short CD, hard longer).
2. Implement service + save field (additive schema bump if needed).
3. Wire clear → score → loot multiplier → UI.
4. Balance pass: train cost vs easy-camp loot (not free-infinite).
5. Instrument grants via FlowTrace.

---

## Acceptance

- [ ] Clear camp A → re-enter blocked or weaker until cooldown elapses.
- [ ] Save/load preserves cooldowns + W/L.
- [ ] Victory shows itemized loot.
- [ ] Loot meaningful vs train costs (PO can tune numbers).
- [ ] CompileGate green; schema migration safe for old saves.

---

## Not in scope

- Ranked seasons / battle pass.
- Live PvP rewards.
- Full economy redesign.

---

## Key files

- New: `RaidCooldownService` (Village or Core.State as appropriate)
- `ArenaProgressStore.cs`
- `RaidVictoryController.cs` / victory UI
- SaveSchema / GameState

---

## RESULT

`WorkOrders/WORK_ORDER_728_repeatable_raid_economy.RESULT.md`

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `no RaidCooldown/campCooldown hits` — per-camp cooldown unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
