<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-30
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-30) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-802 — Raid CoC stakes: casualties + loot readability (PAIN F1)

**Status:** DONE — owner-confirmed 2026-08-21.
**Minted:** 2026-07-30  
**Program:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2 · `docs/PAIN_POINTS_2026-07-26.md` F1  
**Lane:** Raid V1 stakes (single lane)  
**Prefer after:** WO-774 (loadout/ring) so “who died” matches who was brought  

## Why
CoC hooks players because **troops are scarce** and **stars pay**. Spine exists (scoring, loot helpers, wounded recovery, victory reconcile WO-783) but stakes are still soft / opaque: defeat and low-star outcomes must **hurt the army** and **show loot by star** clearly.

## Depends on
- `RaidScoring` / `ComputeStars` / `ComputeLoot`  
- `ArmyStorage.ReconcileAfterRaid` / `TickRecovery` (TroopRecoveryService)  
- `RaidDeployController` deploy list + end reconcile path  

## Scope
1. **Casualties formula (V1 simple, owner-tunable constants):**
   - Defeat: lose/wound a clear % of **deployed** (not whole army)  
   - 1★ / 2★: partial wounded even on “win”  
   - 3★: optional light or zero wounded (owner lean: light or none)  
2. Wire formula through the **same** end-of-raid reconcile path as victory/retreat (no second army mutator).  
3. **Loot presentation:** summary panel lists resources **by star** + destruction/defenders %; no silent grant.  
4. **Army UI:** wounded count visible post-raid (existing wounded flags — surface them).  
5. EditMode oracle: pure casualty math + loot scaling tests.  
6. FlowTrace step in/out of settle.  

## Acceptance
- [ ] Defeat leaves wounded deployable set smaller / recovering  
- [ ] 1★ is not “free”  
- [ ] Loot numbers match star tier and are readable on summary  
- [ ] Oracle green; felt “I care about this army”  

## Do NOT
- Permadeath deletion of troops  
- Async PvP / shields  
- Structure-% stars (that is **WO-804**)  
- Change deploy ring (774)  

## Owner input (if missing, CLI uses provisional table + documents in RESULT)
| Outcome | Wounded % of deployed (provisional) |
|---------|-------------------------------------|
| Defeat / 0★ | 50% |
| 1★ | 25% |
| 2★ | 10% |
| 3★ | 0% |

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `RaidDeployController.cs:630-668` — star-tiered casualty table remains. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner: raid stakes: casualties + loot is done. ⚠ The 2026-08-21 audit had read this as OPEN - STILL VALID (evidence above). Owner review supersedes it; the audit line is kept so the evidence survives if this is ever reopened.
