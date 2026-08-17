<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-30
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-30) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-803 — Raid session comfort: 2× · ghost deploy · Auto Recommend · scout stub

**Status:** READY TO IMPLEMENT — **sequence AFTER WO-774**  
**Minted:** 2026-07-30  
**Program:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2  
**Lane:** Raid V1 UX (single lane — same deploy path as 774; do not parallel 774)  

## Why
CoC raids feel good because deploy is **legible** (ghost), **fast** (2×), and **planned** (army recipe + scout). After WO-774’s loadout + ring, these make the session mobile-friendly without new combat systems.

## Depends on
- **WO-774** loadout bag + deploy ring + naming  
- `RaidDeployController`, `RaidDeployScreen` / VM, `RaidSelectionScreen`  

## Scope
1. **2× speed toggle** on raid HUD (timeScale or combat tick multiplier — prefer local raid-only, restore on exit).  
2. **Ghost unit** under finger while deploy armed (silhouette + ring ok/forbidden using 774 ring).  
3. **Drop VFX/SFX** on successful deploy (existing VFX catalog / SfxId if present; graceful missing).  
4. **Auto Recommend** on Army screen: fill loadout from a simple recipe table (e.g. 50% melee / 30% ranged / 20% siege by housing), not “select all.”  
5. **Scout stub** on Army or Selection: one-line per base (“Heavy walls · N towers · boss”) — static data on raid config OK for V1.  
6. FlowTrace for speed toggle + recommend fill.  

## Acceptance
- [ ] Player can 2× a full raid and exit restores normal speed  
- [ ] Ghost shows legal vs illegal drop on ring  
- [ ] Auto Recommend produces a legal loadout under housing cap  
- [ ] Scout line visible before March  
- [ ] Felt on device: “I know where and what I’m dropping”  

## Do NOT
- Spells bar / hero raid ability (later)  
- Breach-expand interior deploy (V1.5, separate)  
- Deterministic sim  
- Parallel edit with 774 incomplete  

## Files
- Raid HUD, RaidDeployController, RaidDeployScreen/VM, raid base config / selection data  
