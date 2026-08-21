<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-16
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-16) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 731 — Felt-Complete Vertical + Flag Flip + Canon

**Status:** CLOSED — REMOVED (owner ruling 2026-08-21).
**Priority:** P0 (ship gate for the CoC block)  
**Silo:** QA / PO close / Canon  
**Depends on:** WO-724, 725, 726, 727; **728 recommended**; 729–730 optional for “full CoC”  
**Program:** `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`  
**Effort:** M  

---

## Goal

One continuous playtest script; flip flag **defaults** only after PO felt-pass; update load-bearing canon so the studio never re-debates the spine.

---

## PO playtest script

1. New game → founding → place/unlock Barracks → train army.  
2. Open Arena/Raid entry → pick easy AI camp.  
3. Deploy troops CoC-style → clear → loot → return.  
4. Spend loot on upgrade/train → harder camp.  
5. *(If 729)* one defense sim.  
6. *(If 730)* export/import self-raid smoke.  
7. 10-run soft-lock smoke (win + retreat mixed).

---

## Deliverables

1. Flag defaults (only after PO sign-off):

| Flag | Target default |
|------|----------------|
| `ff.barracks` | **ON** |
| `ff.arena` | **ON** |
| `ff.colosseum` | ON only if chosen landmark in 723/725 |
| `ff.raid` / `ff.raidwalk` | ON (proven) |

2. Canon updates same change-set:
   - `CANON_GROUND_TRUTH_<date>.md` (or supersede banner)
   - `PIPELINE_STATE.md` (distribution / combat spine line)
   - `SESSION_CANON_LOADER.md` current-state bullets if load-bearing
   - `CLI_LANES_WO_NUMBERS.md` already at next-free **732** after mint; note program closed
3. RESULT files present for 723–731 (or explicit defer notes for 729/730).
4. Fleet/regression markers listed in RESULT.
5. One-pager pointer: **PvP = async snapshot (730); live netcode = future block.**

---

## Acceptance

- [ ] PO signs: “CoC vs AI camps is live.”
- [ ] No soft-lock in 10-run smoke.
- [ ] Flag defaults match table above.
- [ ] Canon reflects Barracks ON, Arena entry ON, PvP = snapshot async later.
- [ ] CompileGate + DataRegression green on final tree.

---

## Not in scope

- New features beyond flag/canon close.
- Open Google Play ship.
- Ranked PvP launch.

---

## RESULT

`WorkOrders/WORK_ORDER_731_coc_felt_complete_close.RESULT.md`

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — NEEDS OWNER RULING. Evidence: `FeatureFlags.cs:33,872,863` — flag flip is PO sign-off. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** Colosseum stays OFF. The flag-flip vertical is withdrawn rather than deferred; FeatureFlags.cs:33 (Arena) and :872 (Colosseum) remain defaultOn:false and NOTHING here authorises flipping them.
