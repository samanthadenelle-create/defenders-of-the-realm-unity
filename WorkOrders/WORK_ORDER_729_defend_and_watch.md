<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-16
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-16) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 729 — Defend & Watch (AI Attacks Player Base)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Priority:** P2 (CoC half-loop; not required for first attack ship)  
**Silo:** Combat / Defense  
**Depends on:** WO-727  
**Program:** `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`  
**Effort:** L  
**Parallel-safe with:** WO-730  

---

## Goal

**AI raids the player’s `BaseLayout`** while the player watches (and/or pre-sets defenders). Inverse of WO-726. Still **async/sim** — not live multiplayer.

---

## Built already

- `ArenaDefenseSetupController` + `ArenaDefenseCatalog` (50-pt defenders)
- `ArenaDefense` on GameState
- Friendly defender spawn path in `EnemyOutpost` for placed defense
- Towers auto-fire Hostile
- Flat-plate Realize + NavMesh bake

---

## Gaps to close

1. **`ArenaDefendController`** (or equivalent): spawn AI attacker wave against **player** `BaseLayout` on siege plate.
2. Win/lose = base destruction threshold / timer (not hero death alone).
3. Entry: player-initiated “Test Defense” or herald “Incoming raid” sim (V1).
4. Reuse enemy factory / garrison patterns — **no new combat stack**.
5. Flag-gate: `ff.arena` or new `ff.defendwatch` (default OFF until felt-pass).

---

## Tasks

1. Mirror attack flow controller structure from `ArenaMode` (do not rewrite combat).
2. Map AI attackers from existing enemy/troop defs.
3. Score destruction %; show result; restore BaseLayout if sim was non-destructive copy.
4. Ensure cancel does not corrupt saved layout.
5. FlowTrace defend enter/exit.

---

## Acceptance

- [ ] Place base + defenders → start defense sim → AI pathing works → result screen.
- [ ] Cancel does not corrupt `BaseLayout`.
- [ ] Flag-gated; OFF = no entry.
- [ ] CompileGate green.

---

## Not in scope

- Real “someone attacked you offline” server push (post–WO-730).
- Live netcode.
- Perfect CoC AI pathing polish pass (iterate after felt).

---

## Key files

- `Assets/_Modules/Village/Arena/ArenaDefenseSetupController.cs`
- `Assets/_Modules/Village/Arena/ArenaDefenseCatalog.cs`
- `Assets/_Modules/Village/Arena/ArenaMode.cs` (mirror patterns)
- `Assets/_Modules/Village/World/Camps/EnemyOutpost.cs`
- GameState `BaseLayout` / `ArenaDefense`

---

## RESULT

`WorkOrders/WORK_ORDER_729_defend_and_watch.RESULT.md`

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `no ArenaDefendController/DefenseSim` — defend-and-watch unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
