# WO-801 — Queue glance implement: icons + rings + multi-channel (build on live chip)

**Status:** READY TO IMPLEMENT — **BLOCKED on WO-798 owner image-pair sign-off**  
**Minted:** 2026-07-30  
**Program:** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §2  
**Lane:** HUD / queue presentation (single lane — owns QueueStatus chip)  
**Roles:** CLI implement; Claude only if 798 pack needs a follow-up delta  

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
