<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-30
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-30) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-801 — Queue glance implement: icons + rings + multi-channel (build on live chip)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Minted:** 2026-07-30 · **Programmed under 817:** 2026-08-01  
**Master:** `WorkOrders/WORK_ORDER_817_coc_wc3_queue_visual_system.md`  
**Blocked on:** WO-817 Phase 0 visual sign-off (and ideally Phase 2 bars landed)  
**Lane:** HUD / queue presentation (single lane — owns QueueStatus chip)  
**Roles:** CLI implement; Claude mocks under 817  

## Why
WO-798 designs the WC3 feel. This WO is the **code upgrade** of the **already shipped** right-column Builders chip + 5-deep text rows (`631d1e21` / `ObsidianQueueGate.Status`).

## Depends on
- **WO-798** design pack signed (`docs/UI/WO-798_wc3_queue/` + mocks)  
- Live: `ObsidianQueueGate`, `BuildTimerService.PublishStatus`, `HudKitController.BuildQueueStatusChip` / `FormatQueueRows`  
- Optional parallel: **WO-799** engine; cancel **row UI** can ship in a follow-up once chips exist  

## Scope (CLI)
1. Extend `QueueEntry` (additive) as signed design requires, e.g.:
   - `StructureId` or `IconKey`, `Progress01`, optional channel tag  
2. `PublishStatus` fills new fields (Village); HUD still polls `Status` only.  
3. Replace `_queueRowsPlate` text body with **icon + progress ring + pending strip** (Layout A′ in wireframes).  
4. Multi-channel per owner pick from 798:
   - **M1** Builder icons only  
   - **M2** (default lean) + Training/Research mini-rows when busy  
   - **M3** unified Entries with channel  
5. Keep: plate hide when empty; summary button → `RequestToggle`; Version poll.  
6. Extend `ObsidianQueueRegression` for publisher contract if shape changes.  
7. Headless screenshot S0 idle + S2 deep builder queue.  

## Acceptance
- [ ] Glance shows progress geometry + icons, not only `>` / `-` text  
- [ ] Still Core-safe (no HUD→Village ref)  
- [ ] Tofu / colorblind / MinTouchPx oracles green  
- [ ] Owner felt: “WC3 production line on the phone”  

## Do NOT
- Second queue host / bottom dock unless 798 owner chose that alternate  
- Engine / save schema rewrites  
- Full modal restyle (optional phase 2 — new WO if needed)  

## Files
- `ObsidianQueueGate.cs`, `BuildTimerService.cs` (`PublishStatus`), `HudKitController.cs`, `HudAreasHost.cs` (anchors only if needed)  

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `ObsidianQueueGate.cs:72-94; QueueRailView.cs` — icon cards shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
