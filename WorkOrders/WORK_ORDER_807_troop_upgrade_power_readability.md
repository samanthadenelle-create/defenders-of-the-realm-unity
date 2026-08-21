<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-30
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-30) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-807 — Troop upgrade power readability (deltas, L badges, combat fidelity)

**Status:** DONE — owner-confirmed 2026-08-21.
**Minted:** 2026-07-30  
**Program:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2A  
**Lane:** Barracks / troop combat presentation + light wiring  
**Prefer after / with:** WO-806 layout (can ship deltas first if panel regions already exist)  

## Why
`TroopStatResolver` + `troop-upgrades.json` already define L1–L7 reach/strength curves and ability unlocks at 3/5/7. `TroopDeployer` applies level on spawn. Players often **cannot see** what L means before or after paying Research time — so upgrades feel free-or-pointless, not CoC Lab power.

## Code baseline

| Piece | Path |
|-------|------|
| Curves / abilities | `troop-upgrades.json`, `TroopStatResolver.Effective` |
| Persist level | `GameState.TroopLevels`, `BarracksProgression.ApplyTroopLevel` |
| Enqueue | `BarracksService.UpgradeTroop` → Research channel |
| Apply combat | `TroopDeployer` → `TroopController.ApplyUpgradeStats` |

## Scope
1. **Detail card deltas** (Barracks / Train detail):
   - Show **Lcurrent → Lnext**  
   - HP / damage / range **before → after** from `TroopStatResolver.Effective` at both levels  
   - Next ability unlock line (“L3: Sweeping Cut”) from upgrade row  
2. **Army / raid tray badges:** each stack shows `Lv N` (from `BarracksService.TroopLevel`) so powered troops are obvious.  
3. **Verify all spawn paths** use `TroopStatResolver` (train complete grant, deploy, any dev spawn) — fix any path that uses raw `TroopDef` only.  
4. **Ability fidelity check:** unlocked ability ids resolve in `AbilityCatalog`; if missing, FlowTrace.Warn once + hide claim in UI.  
5. EditMode: Effective(L1) vs Effective(L5) strength monotonic; ability list grows at thresholds.  
6. Optional: Research glance shows “Footman → L3” (ties WO-801 M2).  

## Acceptance
- [ ] Player sees numeric power gain before confirming troop upgrade  
- [ ] Deployed L5 Footman stats match resolver (instrumented or unit test)  
- [ ] Tray/Army shows level badge  
- [ ] No silent “upgrade complete” with zero visible change  

## Do NOT
- Retune curve numbers without owner balance pass  
- Per-instance troop XP (type-level only, CoC Lab model)  
- Hero gear (808)  

## Files
- BarracksPanel/VM detail, TroopTraining detail, RaidDeploy tray tiles, TroopDeployer/Factory, tests  

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `BarracksPanelVM.cs:356-380; no TroopLevel` — stat deltas + Lv badges remain. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner: troop upgrade power readability is done. ⚠ The 2026-08-21 audit had read this as OPEN - STILL VALID (evidence above). Owner review supersedes it; the audit line is kept so the evidence survives if this is ever reopened.
