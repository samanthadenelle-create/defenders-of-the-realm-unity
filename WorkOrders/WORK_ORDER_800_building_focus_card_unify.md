# WO-800 — Building focus card: Level · Enhancements · active job (one door)

**Status:** READY TO IMPLEMENT (Claude designs first; CLI after owner sign-off)  
**Minted:** 2026-07-30  
**Program:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2  
**Lane:** UI / Build presentation (single lane)  
**Roles:** Claude = READ-ONLY mockups + copy matrix; CLI = implement after image-pair sign-off  

## Why
Players meet **four** upgrade-ish systems: structure **level** timers, **perk grid** (“Enhancement”), **VillageTier**, troop/barracks tracks. WC3 veterans expect “select building → one panel.” CoC veterans expect “upgrade building” as a clear level buy. Depth is fine; **fragmented doors** are not.

## Depends on
- Existing: `BuildingUpgradePanelMvvm` / `BuildingUpgradeVM` (perk grid)  
- Existing: `BuildTimerService.StartUpgrade` / `UnderConstructionVisual`  
- Existing: `VillageTierService`  
- Optional fold: active job for this structure from queue  

## Scope

### Claude (read-only)
1. Mock **one building focus card** (portrait 1080×1920) with tabs or segments:
   - **Level** — current Lv → next, cost, timer, one-line effect  
   - **Enhancements** — existing perk grid (keep “Unlock” language)  
   - **Queue / In progress** — if this structure has an active/pending job, show it (WC3 selected-building queue)  
2. Copy matrix (player-facing, ASCII only):
   - Level action label (recommend: “Raise to Lv N”)  
   - Perk action (keep “Unlock …”)  
   - Village tier (recommend: “Stronghold tier”)  
   - Forbidden: calling all three “Upgrade” interchangeably  
3. Entry points: Build mode select structure, interact, upgrade verb (WO-794) — one panel.  
4. Image pair: before (scattered) vs after (one card).  

### CLI (after sign-off)
1. Wire single open path → one host panel (restyle/compose existing VMs; prefer not greenfield logic).  
2. Level tab drives existing timer path; Enhancements tab binds existing VM; Queue tab reads job by structureId from `BuildTimerService` if any.  
3. Village tier affordance stays findable (top row or dedicated tile — match signed mock).  
4. No engine rewrite; no save schema unless a pure UI flag is required (avoid).  

## Acceptance
- [ ] Owner signed mock of one card  
- [ ] From a selected structure, player reaches Level + Enhancements without a second mystery panel  
- [ ] Active upgrade for that structure visible when in flight  
- [ ] Copy matrix used; no “Obsidian”  
- [ ] Felt: “I know what this building can do next”  

## Do NOT
- Merge perk economy into level costs  
- Touch raid  
- UXML  
- Rewrite `ObsidianQueueEngine`  

## Files (expected)
- `BuildingUpgradePanelMvvm` / VM, BuildMode open path, possibly thin Queue strip component  
- `docs/UI/…` mockups under a `WO-800_*` folder when Claude delivers  
