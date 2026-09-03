# WORK ORDER 1279 - Harvest overflow result modal

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED 2026-08-29 - the framed harvest-overflow result is present in Seeker tester APK 2026.08.29.346849; awaiting owner device test.

**Minted:** 2026-08-29 after collision reconciliation; WO-1278 was already claimed by the earlier post-wave victory modal. CLI banner bumped from 1279 to 1280 in the same edit.

## Problem

Player-chosen Echo harvest overflow was communicated by a tiny transient white notification. The
warning is load-bearing because capped surplus is not banked, but the surface was neither readable
nor inspectable on a phone and could be replaced by another toast.

## Implemented scope

- Replace the toast with one standard Obsidian Compact modal owned by `PanelManager` and a renewable
  `WorldHold`.
- Render the authoritative `BankOverflowStatus` result: resource, granted/requested, uncollected/lost,
  measured current/max capacity, and the relevant container/spend remedy.
- State explicitly that uncollected resources were not added to storage.
- Aggregate repeated signals for the same resource inside the existing harvest warn scope; show one
  modal for a multi-resource collection batch; replace rather than stack any still-open prior result.
- Use the shared labeled Close, phone-safe modal anchors, wrapping and `FontFloorMobile`; never ellipsis.
- Keep battle rewards silent under WO-1207. No economy/cap behavior changes.

## Acceptance / evidence

- `TownBankCapRegression` fails if the presentation is not Obsidian, PanelManager/WorldHold owned,
  truthful about granted/requested/lost, explicit about storage loss, or readable-floor/no-ellipsis.
- Existing `WO1207HarvestTrimWarnRegression` continues to pin one decision per batch, resource/amount
  truth, cooldown suppression, and battle-reward silence.
- Static gate: PASS (2026-08-29).
- Unity compile: `COMPILE_GATE_OK` (fresh `Builds/compile-gate.log`, 2026-08-29).
- Data regression could not start after later shared edits introduced unrelated compile errors in
  `Village/UI/Manage/ManageScreenVM.cs:344,356-358` (`BuildingUpgradeDef` / `BuildingTierDef` mismatch).

## Deliberate gap

No Core-to-Village action seam exists for opening the matching Build/Upgrade storage item. This pass
does not add a dead or reflection-driven CTA; the modal gives the existing useful container/spend
instruction and Close. Add a real action only with an authoritative navigation seam.
