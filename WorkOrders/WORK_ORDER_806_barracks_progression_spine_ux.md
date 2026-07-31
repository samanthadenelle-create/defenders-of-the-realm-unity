# WO-806 — Barracks progression spine UX (unlock → train → troop L → barracks L)

**Status:** READY TO IMPLEMENT (Claude designs first; CLI after owner image-pair sign-off)  
**Minted:** 2026-07-30  
**Program:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2A (army ladder)  
**Lane:** Barracks / Army UI (single lane — owns BarracksPanel + Train tab presentation)  
**Roles:** Claude = READ-ONLY flow + mockups; CLI = implement after sign-off  

## Why
Code already implements a CoC-like ladder (unlock by barracks level, train on Train channel, troop L on Research, barracks L on Builder). Players still experience it as **disconnected buttons**, not one spine. WC3/CoC both teach “this building is your military growth.”

## Code baseline (do not reinvent)

| Step | Authority |
|------|-----------|
| Unlock | `BarracksProgression.IsTroopUnlocked` + `TroopUnlock.IsTrainable` (MAX BarracksLevel / legacy tier) |
| Train | `TroopTrainingVM` / `BarracksService.EnqueueTraining` |
| Troop L | `BarracksService.UpgradeTroop` → Research `TroopUpgrade` job |
| Barracks L | `BarracksService.UpgradeBarracks` → Builder `BarracksUpgrade` job |
| UI host | `BarracksPanel` + `BarracksPanelVM` + `TroopTrainingPanel` |

## Scope

### Claude (read-only)
1. **Player journey map** (one page): first open Barracks → see locked vs unlocked → train first unit → see queue → upgrade troop → raise barracks → new unit unlocks.  
2. **Single Barracks frame mock** with clear regions:
   - **Roster ladder** (locked/unlock tier badge “Barracks L3”)  
   - **Train** (selected troop, cost, time, CTA → queue)  
   - **Troop power** (current L, next L teaser — detail may land in 807)  
   - **Barracks level** strip (current L, next unlock list, CTA)  
3. Copy deck: never “Obsidian”; use Train / Upgrade troop / Raise barracks.  
4. Image pair: current dense panel vs proposed spine.  

### CLI (after sign-off)
1. Restructure presentation to match signed regions (prefer restyle existing VMs; no new progression math).  
2. Surface **next unlock** on barracks CTA (“Unlocks Spearman”).  
3. Locked troops show unlock requirement only (no fake train).  
4. Active Train + Research jobs for this domain visible (strip from queue — align WO-778/801).  
5. Document dual unlock authority in code comment only if still dual; do not invent a third gate.  

## Acceptance
- [ ] Owner signed spine mock  
- [ ] New player can answer: how do I unlock, train, and power a troop?  
- [ ] Locked vs unlocked vs in-queue states distinct (not color-only)  
- [ ] No second train/upgrade economy  

## Do NOT
- Rewrite `BarracksProgression` cost curves (balance = later / 771.14)  
- Hero gear (WO-808)  
- Raid loadout (774)  
- UXML  

## Files
- `BarracksPanel*.cs`, `TroopTrainingPanel*.cs`, `BarracksService`, catalogs read-only  
